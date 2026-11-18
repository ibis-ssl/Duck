// Run from the repository root with: dotnet run --file scripts/start-duck-simulation.cs [-- --persistent|--stop-persistent ...]
// This .NET 10 file-based app prepares and starts Duck's base AppHost without a PowerShell dependency.
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

#if !DUCK_LAUNCHER_TESTS
return await Launcher.RunAsync(args);
#endif

/// <summary>既存の標準構成に必要な発行と起動を行い、所有する実行資源の停止を扱います。</summary>
static class Launcher
{
    private const string StackLockName = "duck-aspire-stack.lock";
    private static readonly string[] NativeSources =
    [
        "transport_core.c", "transport_control_frontend.c", "transport_path_policy.c",
        "transport_server_posix.c", "control_main_posix.c"
    ];

    /// <summary>起動ファイルが属する作業木で、起動または明示停止を実行します。</summary>
    /// <param name="args">利用者が指定した起動・停止の引数です。</param>
    /// <param name="sourceFile">呼び出し元の起動ファイルのパスです。</param>
    /// <returns>成功時は 0、AppHost の異常終了時はその終了値、準備や記録の失敗時は 1 です。</returns>
    public static async Task<int> RunAsync(string[] args, [CallerFilePath] string sourceFile = "")
    {
        try
        {
            var options = Options.Parse(args);
            var root = FindRepositoryRoot(sourceFile);
            var docker = FindDocker();
            var stackLock = Path.Combine(Path.GetTempPath(), StackLockName);
            if (options.StopPersistent)
            {
                await StopPersistentAsync(options.StackId, stackLock, command => CaptureAsync(docker, command));
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
            const string mode = "base";
            var artifactDirectory = Path.Combine(root, "artifacts", "duck-simulation", stackId);
            Directory.CreateDirectory(artifactDirectory);
            var launchId = $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
            var fixtureManifest = Path.Combine(artifactDirectory, $"crane-fixture-{launchId}.json");
            var diagnosticsPath = Path.Combine(artifactDirectory, $"crane-probe-{launchId}.jsonl");
            var safeTracePath = Path.Combine(artifactDirectory, $"identity-handoff-{launchId}.jsonl");
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
                ["Testing__ContainerMode"] = "true", ["RefereeDriver__Enabled"] = "true",
                ["Testing__IdentityControl__DesktopBridgeEnabled"] = "true",
                ["Testing__DebugHost__VisionObserverEnabled"] = "true",
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
                ["Testing__IdentityControl__DockerExecutable"] = null
            };

            Directory.SetCurrentDirectory(root);
            await EnsureCm4TransportHelperAsync(root, appHostProject);
            var publishProjects = new[]
            {
                "Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj",
                "Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj",
                "Testing/Duck.Testing.RefereeDriver.Host/Duck.Testing.RefereeDriver.Host.csproj"
            };
            foreach (var project in publishProjects)
                await RunCheckedAsync(dotnet, ["publish", project, "--configuration", "Release", "-m:1", "/nr:false"], root, environment, $"Publish {project}");

            await RunCheckedAsync(dotnet, ["build", appHostProject, "--configuration", "Debug", "-m:1", "/nr:false"], root, environment, "Build AppHost");
            var required = new List<string>
            {
                "Tracker/Tracker.DebugHost/bin/Release/net10.0/publish/Tracker.DebugHost.dll",
                "Testing/Duck.Testing.RefereeDriver.Host/bin/Release/net10.0/publish/Duck.Testing.RefereeDriver.Host.dll",
                "Testing/Duck.Testing.AppHost/bin/Debug/net10.0/Duck.Testing.AppHost.exe",
                "Testing/Duck.Testing.AppHost/bin/Debug/net10.0/cm4-transport-control",
                "Testing/Duck.Testing.AppHost/bin/Debug/net10.0/crane-launch-overlay"
            };
            required.Add("Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish/Tracker.RuntimeHost.dll");
            foreach (var path in required)
                if (!File.Exists(Path.Combine(root, path))) throw new InvalidOperationException($"Startup preparation did not produce required asset '{path}'.");

            Console.WriteLine($"Starting Duck {mode} simulation stack '{stackId}' ({lifetime} lifetime).");
            Console.WriteLine("Docker context and Engine ID are resolved by AppHost; match settings and browser actions remain manual.");
            var logPath = Path.Combine(artifactDirectory, $"apphost-{launchId}.stdout-stderr.log");
            var exit = await RunLoggedAsync(appHostExe, [], root, environment, logPath);
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

    /// <summary>排他ロックを保持し、所有ラベルが全件一致した場合だけ永続資源を削除します。</summary>
    internal static async Task StopPersistentAsync(string stackId, string lockPath,
        Func<string[], Task<(int ExitCode, string Stdout, string Stderr)>> runDocker)
    {
        ValidateStackId(stackId);
        FileStream lease;
        try { lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new InvalidOperationException($"Could not acquire Duck's stack ownership lock '{lockPath}'; an AppHost or cleanup may be active. No containers were changed.", ex); }
        await using (lease)
        {
            var listed = await runDocker(["ps", "-aq", "--no-trunc", "--filter", $"label=duck.aspire.stack={stackId}", "--filter", $"label=duck.aspire.run={stackId}"]);
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
            {
                var removed = await runDocker(["rm", "-f", id]);
                Console.Write(removed.Stdout);
                Console.Error.Write(removed.Stderr);
                if (removed.ExitCode != 0)
                    throw new InvalidOperationException($"Removing owned container {id} failed with exit code {removed.ExitCode}.");
            }
            var remaining = await runDocker(["ps", "-aq", "--no-trunc", "--filter", $"label=duck.aspire.stack={stackId}", "--filter", $"label=duck.aspire.run={stackId}"]);
            if (remaining.ExitCode != 0 || !string.IsNullOrWhiteSpace(remaining.Stdout)) throw new InvalidOperationException("Selected persistent Duck stack still has containers after cleanup.");
            Console.WriteLine($"Removed only containers with stack/run ID '{stackId}'. Docker images were left untouched.");
        }
    }

    private static string? GetLabel(JsonElement labels, string key) => labels.TryGetProperty(key, out var value) ? value.GetString() : null;

    private static async Task EnsureCm4TransportHelperAsync(string root, string appHostProject)
    {
        var native = Path.Combine(root, "native", "cm4-transport");
        var intermediate = Path.Combine(Path.GetDirectoryName(appHostProject)!, "obj", "Debug", "net10.0", "cm4-transport-control");
        var output = Path.Combine(Path.GetDirectoryName(appHostProject)!, "bin", "Debug", "net10.0", "cm4-transport-control");
        var newestSource = Directory.GetFiles(native, "*.c").Where(f => NativeSources.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .Concat(Directory.GetFiles(native, "*.h")).Max(File.GetLastWriteTimeUtc);
        Directory.CreateDirectory(Path.GetDirectoryName(intermediate)!);
        if (IsCurrentElf(intermediate, newestSource)) return;
        if (IsCurrentElf(output, newestSource)) { File.Copy(output, intermediate, true); return; }

        var wsl = FindExecutable("wsl.exe") ?? throw new InvalidOperationException("CM4 helper is missing or stale. Existing WSL with a C compiler is required; no software will be installed.");
        var repoLinux = await CaptureAsync(wsl, "--exec", "wslpath", "-a", root);
        var outputLinux = await CaptureAsync(wsl, "--exec", "wslpath", "-a", intermediate);
        if (repoLinux.ExitCode != 0 || outputLinux.ExitCode != 0) throw new InvalidOperationException("Could not map repository/helper path into the existing WSL distribution.");
        const string command = "set -eu; repository=\"$1\"; output=\"$2\"; cd \"$repository\"; command -v cc >/dev/null 2>&1 || { echo 'No C compiler is available in existing WSL; no installation attempted.' >&2; exit 127; }; cc -std=c11 -O2 -Wall -Wextra -Werror -pedantic -static -I native/cm4-transport native/cm4-transport/transport_core.c native/cm4-transport/transport_control_frontend.c native/cm4-transport/transport_path_policy.c native/cm4-transport/transport_server_posix.c native/cm4-transport/control_main_posix.c -o \"$output\"";
        await RunCheckedAsync(wsl, ["--exec", "sh", "-c", command, "duck-cm4-helper", repoLinux.Stdout.Trim(), outputLinux.Stdout.Trim()], root, null, "Build Linux CM4 helper with existing WSL toolchain");
        if (!IsElf64X64(intermediate)) throw new InvalidOperationException("CM4 helper build did not produce a Linux x86-64 executable.");
    }

    private static bool IsCurrentElf(string path, DateTime newestSource) => IsElf64X64(path) && File.GetLastWriteTimeUtc(path) >= newestSource;
    private static bool IsElf64X64(string path)
    {
        if (!File.Exists(path)) return false;
        var b = File.ReadAllBytes(path);
        return b.Length >= 20 && b[0] == 0x7f && b[1] == (byte)'E' && b[2] == (byte)'L' && b[3] == (byte)'F' && b[4] == 2 && b[5] == 1 && BitConverter.ToUInt16(b, 18) == 62;
    }

    private static async Task RunCheckedAsync(string exe, string[] args, string cwd, IReadOnlyDictionary<string, string?>? env, string step)
    {
        Console.WriteLine($"==> {step}");
        var result = await CaptureAsync(exe, args, cwd, env);
        Console.Write(result.Stdout); Console.Error.Write(result.Stderr);
        if (result.ExitCode != 0) throw new InvalidOperationException($"{step} failed with exit code {result.ExitCode}.");
    }

    /// <summary>ログファイルを先に確保し、AppHost の終了と両出力の記録完了を待ちます。</summary>
    internal static async Task<int> RunLoggedAsync(string exe, string[] args, string cwd,
        IReadOnlyDictionary<string, string?> env, string logPath)
    {
        await using var file = new FileStream(logPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await using var log = new StreamWriter(file, new UTF8Encoding(false));
        return await RunAppHostAsync(exe, args, cwd, env, log);
    }

    /// <summary>共有コンソールの停止要求を受けても、子プロセスと出力を回収するまで待ちます。</summary>
    internal static async Task<int> RunAppHostAsync(string exe, string[] args, string cwd,
        IReadOnlyDictionary<string, string?> env, TextWriter log)
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
                // 出力側の障害を子の自然終了まで待たない。子が書き続けるとパイプが詰まる。
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
                catch { /* 呼び出し元には最初の処理障害を返す。 */ }
                throw;
            }
        }
        finally
        {
            Console.CancelKeyPress -= keepLauncherAlive;
        }
    }

    /// <summary>パイプを最後まで読み、出力を同じログに順次書き込みます。</summary>
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
        return File.Exists(desktop) ? desktop : FindExecutable("docker") ?? throw new InvalidOperationException("Docker CLI was not found. Start the already-installed Docker Desktop and ensure its CLI is available.");
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

    /// <summary>起動ファイルから親をたどり、そのファイルが属する作業木を選びます。</summary>
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
        throw new InvalidOperationException($"The launcher file '{sourceFile}' is not inside a Duck worktree.");
    }

    private static void EnsureWindows() { if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("This launcher currently targets Windows PowerShell replacement workflow only."); }
    private static void ValidateStackId(string value) { if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9_-]{0,63}\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) throw new InvalidOperationException("PersistentStackId must be a 1-64 character ASCII name containing letters, digits, hyphen, or underscore."); }

    /// <summary>起動の寿命と停止先を確定した引数設定です。</summary>
    internal sealed record Options(bool Persistent, bool StopPersistent, string StackId)
    {
        internal const string DefaultStackId = "duck-local-development";
        public static Options Parse(string[] args)
        {
            var persistent = false;
            var stop = false;
            string? explicitId = null;
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--persistent": persistent = true; break;
                    case "--stop-persistent": stop = true; break;
                    case "--stack-id":
                        if (++i >= args.Length) throw new ArgumentException("--stack-id requires a value.");
                        explicitId = args[i];
                        ValidateStackId(explicitId);
                        break;
                    case "--help": case "-h": Console.WriteLine("Usage: dotnet run --file scripts/start-duck-simulation.cs [--persistent] [--stack-id ID] | [--stop-persistent --stack-id ID]"); Environment.Exit(0); break;
                    default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
                }
            }
            if (persistent && stop) throw new ArgumentException("--persistent and --stop-persistent cannot be used together.");
            if (stop && explicitId is null) throw new ArgumentException("--stop-persistent requires an explicit --stack-id.");
            return new Options(persistent, stop, explicitId ?? DefaultStackId);
        }
    }
}
