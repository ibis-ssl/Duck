// Run from the repository root with: dotnet run --file scripts/start-duck-simulation.cs [-- --persistent|--stop-persistent ...]
// This .NET 10 file-based app prepares and starts Duck's base AppHost without a PowerShell dependency.
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

#if !DUCK_LAUNCHER_TESTS
return await Launcher.RunAsync(args);
#endif

/// <summary>Windows の開発用 Duck シミュレーションを起動・停止するスクリプト処理です。</summary>
static class Launcher
{
    private const string StackLockName = "duck-aspire-stack.lock";
    private const string TransferProjectDirectory = "Testing/Duck.Testing.DevelopmentUdpTransfer";

    /// <summary>引数を解釈し、ローカル Duck シミュレーションの起動または停止を実行します。</summary>
    /// <param name="args">起動モードやスタック ID を指定するコマンドライン引数です。</param>
    /// <returns>処理の終了コードです。</returns>
    /// <exception cref="ArgumentException">引数の組み合わせまたは値が無効です。</exception>
    /// <exception cref="InvalidOperationException">必要な実行環境やリポジトリを確認できません。</exception>
    public static async Task<int> RunAsync(string[] args, [CallerFilePath] string sourceFile = "")
    {
        try
        {
            var options = Options.Parse(args);
            var root = FindRepositoryRoot(sourceFile);
            var docker = FindDocker();
            if (options.StopPersistent)
            {
                await StopPersistentAsync(docker, options.StackId);
                return 0;
            }

            EnsureWindows();
            var dotnet = FindExecutable("dotnet") ?? throw new InvalidOperationException(".NET SDK 10.0 is required; dotnet was not found.");
            var sdkList = await CaptureAsync(dotnet, "--list-sdks");
            if (sdkList.ExitCode != 0 || !sdkList.Stdout.Split('\n').Any(line => line.TrimStart().StartsWith("10.0.", StringComparison.Ordinal)))
                throw new InvalidOperationException(".NET SDK 10.0 is required. This launcher does not install software.");

            var stackId = options.Persistent ? options.StackId : $"local-{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}";
            ValidateStackId(stackId);
            var runId = stackId;
            var lifetime = options.Persistent ? "persistent" : "session";
            var mode = options.HostRuntime ? "base-host-runtime" : "base-helper-free-compose";
            var artifactDirectory = Path.Combine(root, "artifacts", "duck-simulation", stackId);
            Directory.CreateDirectory(artifactDirectory);
            var launchId = $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
            var fixtureManifest = Path.Combine(artifactDirectory, $"crane-fixture-{launchId}.json");
            var diagnosticsPath = Path.Combine(artifactDirectory, $"crane-probe-{launchId}.jsonl");
            var safeTracePath = Path.Combine(artifactDirectory, $"identity-handoff-{launchId}.jsonl");
            var stackLock = Path.Combine(Path.GetTempPath(), StackLockName);
            string[] startupLines =
            [
                $"mode={lifetime}", $"stack_id={stackId}", $"run_id={runId}",
                $"started_at_utc={DateTime.UtcNow:O}", $"testing_mode={mode}", "game_controller_ui=manual"
            ];
            await File.WriteAllLinesAsync(Path.Combine(artifactDirectory, $"startup-mode-{launchId}.txt"), startupLines, new UTF8Encoding(false));

            var appHostProject = Path.Combine(root, "Testing", "Duck.Testing.AppHost", "Duck.Testing.AppHost.csproj");
            var appHostExe = Path.Combine(root, "Testing", "Duck.Testing.AppHost", "bin", "Debug", "net10.0", "Duck.Testing.AppHost.exe");
            var environment = new Dictionary<string, string?>
            {
                ["Testing__Mode"] = "base", ["Testing__MatchReadinessOnly"] = "false",
                ["Testing__RuntimeHostOnWindows"] = options.HostRuntime ? "true" : null,
                ["Testing__Compose__DevelopmentUdpTransfer"] = options.HostRuntime ? "true" : null,
                ["Testing__Compose__PullMissingImages"] = "true",
                ["Testing__Compose__StandardBaseEnabled"] = options.HostRuntime ? null : "true",
                ["Testing__Crane__Planner"] = "visibility_graph",
                ["Testing__ContainerMode"] = "true",
                ["RefereeDriver__Enabled"] = "false",
                ["Testing__IdentityControl__DesktopBridgeEnabled"] = "true",
                ["Testing__DebugHost__VisionObserverEnabled"] = "false",
                ["Testing__ContainerLifetime__Mode"] = lifetime,
                ["Testing__StackOwnership__StackId"] = stackId,
                ["Testing__StackOwnership__LockPath"] = stackLock,
                ["Testing__Diagnostics__RunId"] = runId,
                ["Testing__Crane__FixtureManifestPath"] = fixtureManifest,
                ["Testing__Crane__DiagnosticsPath"] = diagnosticsPath,
                ["Testing__IdentityControl__SafeTracePath"] = safeTracePath,
                ["DUCK_ASPIRE_STACK_ID"] = stackId, ["DUCK_ASPIRE_FIXTURE_MANIFEST"] = fixtureManifest,
                ["Testing__IdentityControl__DockerContext"] = null,
                ["Testing__IdentityControl__ExpectedEngineId"] = null,
                ["Testing__IdentityControl__DockerExecutable"] = docker
            };

            Directory.SetCurrentDirectory(root);
            var publishProject = options.HostRuntime
                ? $"{TransferProjectDirectory}/Duck.Testing.DevelopmentUdpTransfer.csproj"
                : "Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj";
            await RunCheckedAsync(dotnet, ["publish", publishProject, "--configuration", "Release", "-m:1", "/nr:false"],
                root, environment, $"Publish {publishProject}");

            var appHostBuildArguments = new List<string> { "build", appHostProject, "--configuration", "Debug", "-m:1", "/nr:false" };
            if (options.HostRuntime)
            {
                appHostBuildArguments.Add("-p:DuckHostRuntimeProfile=true");
            }
            await RunCheckedAsync(dotnet, appHostBuildArguments.ToArray(), root, environment, "Build AppHost");
            var required = new List<string> { "Testing/Duck.Testing.AppHost/bin/Debug/net10.0/Duck.Testing.AppHost.exe" };
            if (options.HostRuntime)
            {
                required.Add("Tracker/Tracker.RuntimeHost/bin/Debug/net10.0/Tracker.RuntimeHost.dll");
                required.Add($"{TransferProjectDirectory}/bin/Release/net10.0/publish/Dockerfile");
                required.Add($"{TransferProjectDirectory}/bin/Release/net10.0/publish/Duck.Testing.DevelopmentUdpTransfer.dll");
            }
            else
            {
                required.Add("Testing/Duck.Testing.AppHost/bin/Debug/net10.0/crane-launch-overlay");
                required.Add("Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish/Tracker.RuntimeHost.dll");
            }
            foreach (var path in required)
                if (!File.Exists(Path.Combine(root, path))) throw new InvalidOperationException($"Startup preparation did not produce required asset '{path}'.");

            Console.WriteLine($"Starting Duck {mode} simulation stack '{stackId}' ({lifetime} lifetime).");
            Console.WriteLine("Docker context and Engine ID are resolved by AppHost; match settings and browser actions remain manual.");
            var logPath = Path.Combine(artifactDirectory, $"apphost-{launchId}.stdout-stderr.log");
            environment["DUCK_LAUNCHER_REDIRECTED_OUTPUT"] = "1";
            var exit = await RunLoggedAsync(appHostExe, [], root, environment, logPath,
                () => StopPersistentAsync(docker, stackId));
            if (exit != 0)
            {
                Console.Error.WriteLine($"AppHost exited with code {exit}. See '{artifactDirectory}'.");
                return exit;
            }
            Console.WriteLine(options.Persistent
                ? $"Persistent stack '{stackId}' remains available. Re-run with --persistent --stack-id {stackId}; cleanup with --stop-persistent --stack-id {stackId}."
                : $"AppHost ended in Session mode; Aspire owns teardown for stack '{stackId}'.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task StopPersistentAsync(string docker, string stackId)
    {
        var lockPath = Path.Combine(Path.GetTempPath(), StackLockName);
        await StopPersistentAsync(stackId, lockPath, arguments => CaptureAsync(docker, arguments));
    }

    /// <summary>排他ロックを保持し、選択したスタックの所有表示をすべて確認してから片付けます。</summary>
    /// <param name="stackId">片付ける Duck スタックの識別子です。</param>
    /// <param name="lockPath">スタックの同時操作を防ぐ排他ロックのファイルパスです。</param>
    /// <param name="runDocker">Docker に引数を渡し、終了値と標準出力・標準エラーを返す処理です。</param>
    /// <returns>所有確認と片付けの完了を表す非同期処理です。</returns>
    internal static async Task StopPersistentAsync(string stackId, string lockPath,
        Func<string[], Task<(int ExitCode, string Stdout, string Stderr)>> runDocker)
    {
        ValidateStackId(stackId);
        var projectHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{stackId}\0{stackId}")))
            .ToLowerInvariant()[..20];
        var composeProject = $"duck-{projectHash}";
        var composeFile = Path.Combine(Path.GetTempPath(), $"{composeProject}.compose.yaml");
        var composeManifestExists = File.Exists(composeFile);
        FileStream lease;
        try { lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new InvalidOperationException($"Could not acquire Duck's stack ownership lock '{lockPath}'; an AppHost or cleanup may be active. No containers were changed.", ex); }
        await using (lease)
        {
            if (composeManifestExists)
                await VerifyPersistentComposeOwnershipAsync(composeProject, stackId, runDocker);
            var listArguments = new List<string>
            {
                "ps", "-aq", "--no-trunc",
                "--filter", "label=duck.aspire.owner=duck-apphost",
                "--filter", $"label=duck.aspire.stack={stackId}",
                "--filter", $"label=duck.aspire.run={stackId}",
            };
            if (composeManifestExists)
            {
                listArguments.AddRange(["--filter", "label=duck.aspire.resource=duck"]);
            }
            var listed = await runDocker(listArguments.ToArray());
            if (listed.ExitCode != 0) throw new InvalidOperationException("Could not list containers for the selected persistent Duck stack.");
            var ids = listed.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var id in ids)
            {
                var inspect = await runDocker(["inspect", "--format", "{{json .Config.Labels}}", id]);
                if (inspect.ExitCode != 0) throw new InvalidOperationException($"Could not verify container {id}; it was left untouched.");
                using var labelsDoc = JsonDocument.Parse(inspect.Stdout);
                var labels = labelsDoc.RootElement;
                if (GetLabel(labels, "duck.aspire.owner") != "duck-apphost" || GetLabel(labels, "duck.aspire.stack") != stackId || GetLabel(labels, "duck.aspire.run") != stackId)
                    throw new InvalidOperationException($"Container {id} did not match exact owner/stack/run labels; it was left untouched.");
            }
            foreach (var id in ids)
                await RunDockerCheckedAsync(runDocker, ["rm", "-f", id], $"Remove owned container {id}");
            if (composeManifestExists)
            {
                await RunDockerCheckedAsync(runDocker,
                    ["compose", "--project-name", composeProject, "--file", composeFile, "down", "--remove-orphans", "--timeout", "15", "--rmi", "local"],
                    $"Stop persistent Compose project {composeProject}");
                File.Delete(composeFile);
            }
            var remaining = await runDocker(["ps", "-aq", "--no-trunc",
                "--filter", "label=duck.aspire.owner=duck-apphost",
                "--filter", $"label=duck.aspire.stack={stackId}",
                "--filter", $"label=duck.aspire.run={stackId}"]);
            if (remaining.ExitCode != 0 || !string.IsNullOrWhiteSpace(remaining.Stdout)) throw new InvalidOperationException("Selected persistent Duck stack still has containers after cleanup.");
            Console.WriteLine($"Removed only containers with stack/run ID '{stackId}'. Docker images were left untouched.");
        }
    }

    private static async Task VerifyPersistentComposeOwnershipAsync(
        string projectName,
        string stackId,
        Func<string[], Task<(int ExitCode, string Stdout, string Stderr)>> runDocker)
    {
        var containers = await runDocker(["ps", "-aq", "--no-trunc", "--filter", $"label=com.docker.compose.project={projectName}"]);
        if (containers.ExitCode != 0) throw new InvalidOperationException("Could not inspect the selected persistent Compose project; no containers were changed.");
        foreach (var id in containers.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var inspected = await runDocker(["inspect", "--format", "{{json .Config.Labels}}", id]);
            if (inspected.ExitCode != 0) throw new InvalidOperationException($"Could not verify Compose container {id}; it was left untouched.");
            using var document = JsonDocument.Parse(inspected.Stdout);
            var labels = document.RootElement;
            if (GetLabel(labels, "com.docker.compose.project") != projectName ||
                GetLabel(labels, "duck.aspire.owner") != "duck-apphost" ||
                GetLabel(labels, "duck.aspire.stack") != stackId ||
                GetLabel(labels, "duck.aspire.run") != stackId ||
                !(GetLabel(labels, "duck.aspire.resource")?.StartsWith("compose-", StringComparison.Ordinal) ?? false))
            {
                throw new InvalidOperationException($"Compose container {id} did not match the selected Duck stack labels; no containers were changed.");
            }
        }

        var networks = await runDocker(["network", "ls", "-q", "--filter", $"label=com.docker.compose.project={projectName}"]);
        if (networks.ExitCode != 0) throw new InvalidOperationException("Could not inspect the selected persistent Compose networks; no containers were changed.");
        foreach (var id in networks.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var inspected = await runDocker(["network", "inspect", "--format", "{{json .Labels}}", id]);
            if (inspected.ExitCode != 0) throw new InvalidOperationException($"Could not verify Compose network {id}; it was left untouched.");
            using var document = JsonDocument.Parse(inspected.Stdout);
            if (GetLabel(document.RootElement, "com.docker.compose.project") != projectName)
                throw new InvalidOperationException($"Compose network {id} did not match the selected project; no containers were changed.");
        }
    }

    private static async Task RunDockerCheckedAsync(
        Func<string[], Task<(int ExitCode, string Stdout, string Stderr)>> runDocker,
        string[] arguments,
        string step)
    {
        Console.WriteLine($"==> {step}");
        var result = await runDocker(arguments);
        Console.Write(result.Stdout);
        Console.Error.Write(result.Stderr);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{step} failed with exit code {result.ExitCode}.");
    }

    private static string? GetLabel(JsonElement labels, string key) => labels.TryGetProperty(key, out var value) ? value.GetString() : null;

    private static async Task RunCheckedAsync(string exe, string[] args, string cwd, IReadOnlyDictionary<string, string?>? env, string step)
    {
        Console.WriteLine($"==> {step}");
        var result = await CaptureAsync(exe, args, cwd, env);
        Console.Write(result.Stdout); Console.Error.Write(result.Stderr);
        if (result.ExitCode != 0) throw new InvalidOperationException($"{step} failed with exit code {result.ExitCode}.");
    }

    /// <summary>ログを先に確保し、AppHost の終了と両出力の記録完了を待ちます。</summary>
    /// <remarks>CreateNew で既存記録の上書きを避け、ログを開けない場合は AppHost を起動しません。</remarks>
    internal static async Task<int> RunLoggedAsync(string exe, string[] args, string cwd,
        IReadOnlyDictionary<string, string?> env, string logPath, Func<Task>? cleanupOwnedResources = null)
    {
        await using var file = new FileStream(logPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await using var log = new StreamWriter(file, new UTF8Encoding(false));
        return await RunAppHostAsync(exe, args, cwd, env, log, cleanupOwnedResources);
    }

    /// <summary>共有コンソールの停止要求を受けても、子プロセスと出力を回収するまで待ちます。</summary>
    /// <remarks>出力処理に失敗した場合は AppHost とその子孫を終了し、session 資源を回収してから失敗を返します。</remarks>
    internal static async Task<int> RunAppHostAsync(string exe, string[] args, string cwd,
        IReadOnlyDictionary<string, string?> env, TextWriter log, Func<Task>? cleanupOwnedResources = null)
    {
        ConsoleCancelEventHandler keepLauncherAlive = (_, signal) => signal.Cancel = true;
        Console.CancelKeyPress += keepLauncherAlive;
        try
        {
            using var process = Start(exe, args, cwd, env, redirect: true);
            using var stopPumps = new CancellationTokenSource();
            var exited = process.WaitForExitAsync();
            var stdout = PumpAsync(process.StandardOutput, Console.Out, log, stopPumps.Token);
            var stderr = PumpAsync(process.StandardError, Console.Error, log, stopPumps.Token);
            try
            {
                var pending = new List<Task> { exited, stdout, stderr };
                while (pending.Count != 0)
                {
                    var completed = await Task.WhenAny(pending);
                    await completed;
                    pending.Remove(completed);
                }
                var summary = $"AppHost exited with code {process.ExitCode}; stdout and stderr drained.";
                await log.WriteLineAsync(summary);
                await log.FlushAsync();
                Console.WriteLine(summary);
                if (process.ExitCode != 0 && cleanupOwnedResources is not null)
                {
                    try { await cleanupOwnedResources(); }
                    catch (Exception cleanupException)
                    {
                        Console.Error.WriteLine($"Session stack cleanup also failed: {cleanupException.Message}");
                    }
                }
                return process.ExitCode;
            }
            catch
            {
                stopPumps.Cancel();
                if (!process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                }
                try { await Task.WhenAll(exited, stdout, stderr); }
                catch { /* Keep the first output or process failure. */ }
                if (cleanupOwnedResources is not null)
                {
                    try { await cleanupOwnedResources(); }
                    catch (Exception cleanupException)
                    {
                        Console.Error.WriteLine($"Session stack cleanup also failed: {cleanupException.Message}");
                    }
                }
                throw;
            }
        }
        finally
        {
            Console.CancelKeyPress -= keepLauncherAlive;
        }
    }

    private static async Task PumpAsync(StreamReader reader, TextWriter console, TextWriter log,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lock (log)
            {
                log.WriteLine(line);
                log.Flush();
            }
            await console.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }

    private static Task<(int ExitCode, string Stdout, string Stderr)> CaptureAsync(string exe, params string[] args) => CaptureAsync(exe, args, Environment.CurrentDirectory, null);
    private static async Task<(int ExitCode, string Stdout, string Stderr)> CaptureAsync(string exe, string[] args, string cwd, IReadOnlyDictionary<string, string?>? env)
    {
        using var process = Start(exe, args, cwd, env, redirect: true);
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    private static Process Start(string exe, string[] args, string cwd, IReadOnlyDictionary<string, string?>? env, bool redirect)
    {
        var info = new ProcessStartInfo(exe) { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = redirect, RedirectStandardError = redirect };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        if (env is not null) foreach (var pair in env) { if (pair.Value is null) info.Environment.Remove(pair.Key); else info.Environment[pair.Key] = pair.Value; }
        return Process.Start(info) ?? throw new InvalidOperationException($"Could not start '{exe}'.");
    }

    private static string FindDocker()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var desktop = Path.Combine(local, "Programs", "DockerDesktop", "resources", "bin", "docker.exe");
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var systemDocker = Path.Combine(programFiles, "Docker", "Docker", "resources", "bin", "docker.exe");
        return File.Exists(desktop) ? desktop
            : File.Exists(systemDocker) ? systemDocker
            : FindExecutable("docker") ?? throw new InvalidOperationException("Docker CLI was not found. Start the already-installed Docker Desktop and ensure its CLI is available.");
    }

    private static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
            if (OperatingSystem.IsWindows() && File.Exists(candidate + ".exe")) return candidate + ".exe";
        }
        return null;
    }

    /// <summary>起動ファイルが属する作業木を選択します。</summary>
    /// <param name="sourceFile">実行中の file-based 起動ファイルです。</param>
    /// <returns>起動ファイルの親から見つかった Duck リポジトリのルートです。</returns>
    internal static string FindRepositoryRoot(string sourceFile)
    {
        if (!Path.IsPathFullyQualified(sourceFile))
            throw new InvalidOperationException("The launcher's source path is unavailable.");
        var current = new FileInfo(sourceFile).Directory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Duck.slnx")) && Directory.Exists(Path.Combine(current.FullName, "Testing", "Duck.Testing.AppHost"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Could not find the Duck repository containing the launcher's source file.");
    }

    private static void EnsureWindows() { if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("This launcher currently targets Windows PowerShell replacement workflow only."); }
    private static void ValidateStackId(string value) { if (!System.Text.RegularExpressions.Regex.IsMatch(value, "\\A[A-Za-z0-9][A-Za-z0-9_-]{0,63}\\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) throw new InvalidOperationException("PersistentStackId must be a 1-64 character ASCII name containing letters, digits, hyphen, or underscore."); }

    /// <summary>シミュレーションの起動モードとスタック識別情報です。</summary>
    /// <param name="Persistent">停止後も Docker スタックを保持する場合は <see langword="true"/> です。</param>
    /// <param name="StopPersistent">保持中の Docker スタックを停止する場合は <see langword="true"/> です。</param>
    /// <param name="HostRuntime">RuntimeHost を Windows ホスト上で実行する場合は <see langword="true"/> です。</param>
    /// <param name="StackId">スタックを識別する ID です。</param>
    internal sealed record Options(bool Persistent, bool StopPersistent, bool HostRuntime, string StackId)
    {
        /// <summary>コマンドライン引数を検証し、起動オプションに変換します。</summary>
        /// <param name="args">起動モードやスタック ID を指定する引数です。</param>
        /// <returns>検証済みの起動オプションです。</returns>
        /// <exception cref="ArgumentException">未知の引数、値の不足、または併用できない引数が指定されました。</exception>
        public static Options Parse(string[] args)
        {
            var persistent = false;
            var stop = false;
            var explicitHostRuntime = false;
            var helperFreeCompose = false;
            string? id = null;
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--persistent": persistent = true; break;
                    case "--stop-persistent": stop = true; break;
                    case "--host-runtime": explicitHostRuntime = true; break;
                    case "--helper-free-compose": helperFreeCompose = true; break;
                    case "--stack-id": if (++i >= args.Length) throw new ArgumentException("--stack-id requires a value."); id = args[i]; break;
                    case "--help":
                    case "-h":
                        Console.WriteLine("""
                            Usage: dotnet run --file scripts/start-duck-simulation.cs [-- OPTIONS]
                              No options, --host-runtime     Windows native RuntimeHost with development UDP transfer
                              --helper-free-compose          Run RuntimeHost inside Docker for this session
                              --persistent [--stack-id ID]   Keep the Docker RuntimeHost stack after exit
                              --stop-persistent --stack-id ID
                            """);
                        Environment.Exit(0);
                        break;
                    default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
                }
            }
            if (persistent && stop) throw new ArgumentException("--persistent and --stop-persistent cannot be used together.");
            if (explicitHostRuntime && (helperFreeCompose || persistent || stop))
                throw new ArgumentException("--host-runtime cannot be combined with --helper-free-compose, --persistent, or --stop-persistent.");
            if (stop && id is null) throw new ArgumentException("--stop-persistent requires an explicit --stack-id.");
            if (id is not null) ValidateStackId(id);
            var hostRuntime = !helperFreeCompose && !persistent && !stop;
            return new Options(persistent, stop, hostRuntime, id ?? "duck-local-development");
        }
    }
}
