// Run from the repository root with: dotnet run --file scripts/start-duck-simulation.cs [-- --persistent|--stop-persistent ...]
// This .NET 10 file-based app prepares and starts Duck's base AppHost without a PowerShell dependency.
using System.Diagnostics;
using System.Text;
using System.Text.Json;

return await Launcher.RunAsync(args);

static class Launcher
{
    private const string StackLockName = "duck-aspire-stack.lock";
    private static readonly string[] NativeSources =
    [
        "transport_core.c", "transport_control_frontend.c", "transport_path_policy.c",
        "transport_server_posix.c", "control_main_posix.c"
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            if (options.HostRuntime && options.Persistent)
                throw new InvalidOperationException("--host-runtime does not support --persistent; Compose project cleanup is currently tied to AppHost shutdown.");
            var root = FindRepositoryRoot();
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
            var mode = options.HostRuntime ? "base-host-runtime" : "base";
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
                ["Testing__Crane__Planner"] = options.HostRuntime ? "visibility_graph" : null,
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
            if (!options.HostRuntime)
                await EnsureCm4TransportHelperAsync(root, appHostProject);
            var publishProjects = options.HostRuntime
                ? new[] { "Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj", "Testing/Duck.Testing.RefereeDriver.Host/Duck.Testing.RefereeDriver.Host.csproj" }
                : new[] { "Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj", "Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj", "Testing/Duck.Testing.RefereeDriver.Host/Duck.Testing.RefereeDriver.Host.csproj" };
            foreach (var project in publishProjects)
                await RunCheckedAsync(dotnet, ["publish", project, "--configuration", "Release", "-m:1", "/nr:false"], root, environment, $"Publish {project}");

            var appHostBuildArguments = new List<string> { "build", appHostProject, "--configuration", "Debug", "-m:1", "/nr:false" };
            if (options.HostRuntime)
                appHostBuildArguments.Add("-p:DuckHostRuntimeProfile=true");
            await RunCheckedAsync(dotnet, appHostBuildArguments.ToArray(), root, environment, "Build AppHost");
            var required = new List<string>
            {
                "Tracker/Tracker.DebugHost/bin/Release/net10.0/publish/Tracker.DebugHost.dll",
                "Testing/Duck.Testing.RefereeDriver.Host/bin/Release/net10.0/publish/Duck.Testing.RefereeDriver.Host.dll",
                "Testing/Duck.Testing.AppHost/bin/Debug/net10.0/Duck.Testing.AppHost.exe",
            };
            if (!options.HostRuntime)
            {
                required.Add("Testing/Duck.Testing.AppHost/bin/Debug/net10.0/cm4-transport-control");
                required.Add("Testing/Duck.Testing.AppHost/bin/Debug/net10.0/crane-launch-overlay");
            }
            if (options.HostRuntime) required.Add("Tracker/Tracker.RuntimeHost/bin/Debug/net10.0/Tracker.RuntimeHost.dll");
            else required.Add("Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish/Tracker.RuntimeHost.dll");
            foreach (var path in required)
                if (!File.Exists(Path.Combine(root, path))) throw new InvalidOperationException($"Startup preparation did not produce required asset '{path}'.");

            Console.WriteLine($"Starting Duck {mode} simulation stack '{stackId}' ({lifetime} lifetime).");
            Console.WriteLine("Docker context and Engine ID are resolved by AppHost; match settings and browser actions remain manual.");
            var logPath = Path.Combine(artifactDirectory, $"apphost-{launchId}.stdout-stderr.log");
            var exit = await RunLoggedAsync(appHostExe, [], root, environment, logPath);
            if (exit != 0) throw new InvalidOperationException($"AppHost exited with code {exit}. See '{artifactDirectory}'.");
            Console.WriteLine(options.Persistent
                ? $"Persistent stack '{stackId}' remains available. Re-run with --persistent --stack-id {stackId}{(options.HostRuntime ? " --host-runtime" : "")}; cleanup with --stop-persistent --stack-id {stackId}."
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
        ValidateStackId(stackId);
        var lockPath = Path.Combine(Path.GetTempPath(), StackLockName);
        FileStream lease;
        try { lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new InvalidOperationException($"Could not acquire Duck's stack ownership lock '{lockPath}'; an AppHost or cleanup may be active. No containers were changed.", ex); }
        await using (lease)
        {
            var listed = await CaptureAsync(docker, "ps", "-aq", "--no-trunc", "--filter", $"label=duck.aspire.stack={stackId}", "--filter", $"label=duck.aspire.run={stackId}");
            if (listed.ExitCode != 0) throw new InvalidOperationException("Could not list containers for the selected persistent Duck stack.");
            var ids = listed.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var id in ids)
            {
                var inspect = await CaptureAsync(docker, "inspect", "--format", "{{json .Config.Labels}}", id);
                if (inspect.ExitCode != 0) throw new InvalidOperationException($"Could not verify container {id}; it was left untouched.");
                using var labelsDoc = JsonDocument.Parse(inspect.Stdout);
                var labels = labelsDoc.RootElement;
                if (GetLabel(labels, "duck.aspire.owner") != "duck-apphost" || GetLabel(labels, "duck.aspire.stack") != stackId || GetLabel(labels, "duck.aspire.run") != stackId)
                    throw new InvalidOperationException($"Container {id} did not match exact owner/stack/run labels; it was left untouched.");
            }
            foreach (var id in ids) await RunCheckedAsync(docker, ["rm", "-f", id], Environment.CurrentDirectory, null, $"Remove owned container {id}");
            var remaining = await CaptureAsync(docker, "ps", "-aq", "--no-trunc", "--filter", $"label=duck.aspire.stack={stackId}", "--filter", $"label=duck.aspire.run={stackId}");
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

    private static async Task<int> RunLoggedAsync(string exe, string[] args, string cwd, IReadOnlyDictionary<string, string?> env, string logPath)
    {
        using var process = Start(exe, args, cwd, env, redirect: true);
        await using var log = new StreamWriter(logPath, false, new UTF8Encoding(false));
        var stdout = PumpAsync(process.StandardOutput, Console.Out, log);
        var stderr = PumpAsync(process.StandardError, Console.Error, log);
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        return process.ExitCode;
    }

    private static async Task PumpAsync(StreamReader reader, TextWriter console, TextWriter log)
    {
            while (await reader.ReadLineAsync() is { } line)
        {
            await console.WriteLineAsync(line);
                lock (log)
                {
                    log.WriteLine(line);
                    log.Flush();
                }
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

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Duck.slnx")) && Directory.Exists(Path.Combine(current.FullName, "Testing", "Duck.Testing.AppHost"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Run this file-based app from inside the Duck repository.");
    }

    private static void EnsureWindows() { if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("This launcher currently targets Windows PowerShell replacement workflow only."); }
    private static void ValidateStackId(string value) { if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) throw new InvalidOperationException("PersistentStackId must be a 1-64 character ASCII name containing letters, digits, hyphen, or underscore."); }

    private sealed record Options(bool Persistent, bool StopPersistent, bool HostRuntime, string StackId)
    {
        public static Options Parse(string[] args)
        {
            var persistent = false; var stop = false; var hostRuntime = false; var id = "duck-local-development";
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--persistent": persistent = true; break;
                    case "--stop-persistent": stop = true; break;
                    case "--host-runtime": hostRuntime = true; break;
                    case "--stack-id": if (++i >= args.Length) throw new ArgumentException("--stack-id requires a value."); id = args[i]; break;
                    case "--help": case "-h": Console.WriteLine("Usage: dotnet run --file scripts/start-duck-simulation.cs [--host-runtime] [--persistent] [--stack-id ID] | [--stop-persistent --stack-id ID]"); Environment.Exit(0); break;
                    default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
                }
            }
            if (persistent && stop) throw new ArgumentException("--persistent and --stop-persistent cannot be used together.");
            if (stop && id == "duck-local-development") throw new ArgumentException("--stop-persistent requires an explicit --stack-id.");
            return new Options(persistent, stop, hostRuntime, id);
        }
    }
}
