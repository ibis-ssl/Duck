// Run from the repository root with: dotnet run --file scripts/start-duck-simulation.cs [-- --persistent|--stop-persistent ...]
// This .NET 10 file-based app prepares and starts Duck's base AppHost without a PowerShell dependency.
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

#if !DUCK_LAUNCHER_TESTS
return await Launcher.RunAsync(args);
#endif

/// <summary>Windows の開発用 Duck シミュレーションを起動・停止するスクリプト処理です。</summary>
static class Launcher
{
    private const string StackLockName = "duck-aspire-stack.lock";
    private const string TransferProjectDirectory = "Testing/Duck.Testing.DevelopmentUdpTransfer";
    private static readonly ProtoSubmodule[] RequiredProtoSubmodules =
    [
        new("ssl-game-controller", "SslProto/src/external/ssl-game-controller", "9b0765fe7a07a55124f6ee957d4559348462d227"),
        new("ssl-simulation-protocol", "SslProto/src/external/ssl-simulation-protocol", "1e719a8d410cc84847ec69104da44fed13738291"),
    ];

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

            var gitEnvironment = CreateRestrictedGitEnvironment();
            await PrepareAndValidateProtoSubmodulesAsync(root, gitEnvironment);

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
            foreach (var setting in gitEnvironment) environment[setting.Key] = setting.Value;

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
                CreateFailureCleanup(options.Persistent, () => StopPersistentAsync(docker, stackId)));
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

    /// <summary>通常起動より前に Proto submodule を読取り検査し、既存 object だけで安全に配置できる場合に限り準備します。</summary>
    internal static async Task PrepareAndValidateProtoSubmodulesAsync(string root, IReadOnlyDictionary<string, string?>? gitEnvironment = null)
    {
        gitEnvironment ??= CreateRestrictedGitEnvironment();
        await InspectProtoStateAsync(root, gitEnvironment);
        var missing = new List<ProtoSubmodule>();
        foreach (var submodule in RequiredProtoSubmodules)
        {
            var worktree = Path.Combine(root, submodule.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(worktree) || (!File.Exists(Path.Combine(worktree, ".git")) && !Directory.Exists(Path.Combine(worktree, ".git"))))
                missing.Add(submodule);
        }

        if (missing.Count == 0) return;

        var placed = new List<string>();
        foreach (var submodule in missing)
        {
            (int ExitCode, string Stdout, string Stderr) result;
            try
            {
                result = await CaptureAsync("git",
                    ["submodule", "update", "--init", "--no-fetch", "--checkout", "--", submodule.Path],
                    root, gitEnvironment);
            }
            catch (Exception ex)
            {
                var notAttempted = missing.Where(item => !placed.Contains(item.Path, StringComparer.Ordinal)).Select(item => item.Path);
                throw new InvalidOperationException(
                    $"Local-only Proto preparation could not complete. Successfully prepared: {(placed.Count == 0 ? "none" : string.Join(", ", placed))}. Failed path: {submodule.Path}. Residual paths may remain: {string.Join(", ", notAttempted)}. Error: {ex.Message}", ex);
            }
            if (result.ExitCode != 0)
            {
                var remaining = missing.Where(item => !placed.Contains(item.Path, StringComparer.Ordinal))
                    .Select(item => item.Path);
                throw new InvalidOperationException(
                    $"Local-only Proto preparation stopped. Successfully prepared: {(placed.Count == 0 ? "none" : string.Join(", ", placed))}. Failed path: {submodule.Path}. Residual paths may remain: {string.Join(", ", remaining)}. Git exited {result.ExitCode}: {result.Stderr.Trim()} {result.Stdout.Trim()}".Trim());
            }
            placed.Add(submodule.Path);
        }

        try
        {
            await InspectProtoStateAsync(root, gitEnvironment);
            foreach (var submodule in missing)
            {
                var worktree = Path.Combine(root, submodule.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(Path.Combine(worktree, ".git")) && !Directory.Exists(Path.Combine(worktree, ".git")))
                    throw new InvalidOperationException($"Proto preparation completed without initializing '{submodule.Path}'. Residual path: '{worktree}'.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Proto placement completed for {(placed.Count == 0 ? "none" : string.Join(", ", placed))}, but the read-only recheck failed. Residual paths may remain: {string.Join(", ", missing.Select(item => item.Path))}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>すべての Git 子孫で transport と lazy fetch を拒否し、hook を存在しない場所へ隔離します。</summary>
    internal static IReadOnlyDictionary<string, string?> CreateRestrictedGitEnvironment()
    {
        var hooksPath = Path.Combine(Path.GetTempPath(), $"duck-launcher-disabled-hooks-{Guid.NewGuid():N}");
        if (Directory.Exists(hooksPath) || File.Exists(hooksPath))
            throw new InvalidOperationException("Could not prove the isolated Git hooks path is absent.");
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_ALLOW_PROTOCOL"] = "",
            ["GIT_NO_LAZY_FETCH"] = "1",
            ["GIT_OPTIONAL_LOCKS"] = "0",
            ["GIT_CONFIG_COUNT"] = "3",
            ["GIT_CONFIG_KEY_0"] = "core.hooksPath",
            ["GIT_CONFIG_VALUE_0"] = hooksPath,
            ["GIT_CONFIG_KEY_1"] = "core.fsmonitor",
            ["GIT_CONFIG_VALUE_1"] = "false",
            ["GIT_CONFIG_KEY_2"] = "submodule.recurse",
            ["GIT_CONFIG_VALUE_2"] = "false",
        };
    }

    internal static Task VerifyObjectClosureForTestsAsync(string gitDir, string path, string commit, IReadOnlyDictionary<string, string?> environment) =>
        VerifyObjectClosureAsync(gitDir, new ProtoSubmodule("test", path, commit), environment);

    private static async Task InspectProtoStateAsync(string root, IReadOnlyDictionary<string, string?> gitEnvironment)
    {
        var top = await RunGitAsync(root, gitEnvironment, "rev-parse", "--show-toplevel");
        if (!PathsEqual(root, top.Trim())) throw new InvalidOperationException("The launcher's repository root did not match Git's worktree root.");

        var hooksPath = gitEnvironment.TryGetValue("GIT_CONFIG_VALUE_0", out var configuredHooksPath) ? configuredHooksPath : null;
        if (string.IsNullOrWhiteSpace(hooksPath) || Directory.Exists(hooksPath) || File.Exists(hooksPath))
            throw new InvalidOperationException("Could not prove external Git hooks are disabled before Proto inspection.");
        var effectiveHooksPath = await RunGitAsync(root, gitEnvironment, "config", "--path", "core.hooksPath");
        if (!PathsEqual(hooksPath, effectiveHooksPath.Trim()))
            throw new InvalidOperationException("Git did not honor the isolated hooks path; no Proto files were changed.");
        var effectiveFsmonitor = await RunGitAsync(root, gitEnvironment, "config", "--get", "core.fsmonitor");
        if (!string.Equals(effectiveFsmonitor.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Git filesystem-monitor helpers could not be disabled; no Proto files were changed.");

        var modulesChanged = await CaptureAsync("git", ["diff", "--quiet", "HEAD", "--", ".gitmodules"], root, gitEnvironment);
        if (modulesChanged.ExitCode != 0)
            throw new InvalidOperationException(".gitmodules differs from HEAD or could not be checked; no Proto files were changed.");

        await RejectConfiguredFiltersAsync(root, gitEnvironment, "parent repository", bareStore: false);
        var gitmodulesPath = Path.Combine(root, ".gitmodules");
        foreach (var submodule in RequiredProtoSubmodules)
        {
            var head = await RunGitAsync(root, gitEnvironment, "rev-parse", $"HEAD:{submodule.Path}");
            if (!string.Equals(head.Trim(), submodule.Commit, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Parent HEAD gitlink '{submodule.Path}' is {head.Trim()}, expected {submodule.Commit}.");

            var index = await RunGitAsync(root, gitEnvironment, "ls-files", "--stage", "--", submodule.Path);
            var indexEntries = index.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (indexEntries.Length != 1 || !Regex.IsMatch(indexEntries[0], $@"^160000 {Regex.Escape(submodule.Commit)} 0\t{Regex.Escape(submodule.Path)}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw new InvalidOperationException($"Index gitlink '{submodule.Path}' does not contain exactly the expected commit {submodule.Commit}.");

            var mappedPath = await CaptureAsync("git", ["config", "-f", gitmodulesPath, "--get-all", $"submodule.{submodule.Path}.path"], root, gitEnvironment);
            if (mappedPath.ExitCode != 0 || mappedPath.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is not [var configuredPath] || configuredPath != submodule.Path)
                throw new InvalidOperationException($".gitmodules does not map '{submodule.Path}' to the expected submodule path; no Proto files were changed.");
            var mappedUrl = await CaptureAsync("git", ["config", "-f", gitmodulesPath, "--get-all", $"submodule.{submodule.Path}.url"], root, gitEnvironment);
            if (mappedUrl.ExitCode != 0 || mappedUrl.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is not [var url] || string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException($".gitmodules has no unambiguous URL for '{submodule.Path}'; no Proto files were changed.");

            var customUpdate = await CaptureAsync("git", ["config", "--get-all", $"submodule.{submodule.Path}.update"], root, gitEnvironment);
            if (customUpdate.ExitCode is not (0 or 1) || customUpdate.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(value => !string.Equals(value, "checkout", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Submodule '{submodule.Path}' has a non-checkout or uninspectable update command; no Proto files were changed.");

            var gitDirResult = await RunGitAsync(root, gitEnvironment, "rev-parse", "--git-path", $"modules/{submodule.Path}");
            var gitDir = Path.GetFullPath(Path.IsPathFullyQualified(gitDirResult.Trim())
                ? gitDirResult.Trim()
                : Path.Combine(root, gitDirResult.Trim()));
            EnsureNoReparsePointAncestors(gitDir, $"object store for '{submodule.Path}'");
            if (!Directory.Exists(gitDir))
                throw new InvalidOperationException($"Fixed local object store for '{submodule.Path}' is unavailable at '{gitDir}'. No network fetch was attempted.");
            await RejectConfiguredFiltersAsync(gitDir, gitEnvironment, $"object store for '{submodule.Path}'", bareStore: true);
            await VerifyObjectClosureAsync(gitDir, submodule, gitEnvironment);

            var worktree = Path.Combine(root, submodule.Path.Replace('/', Path.DirectorySeparatorChar));
            EnsureNoReparsePoints(root, worktree);
            EnsureNoReparsePoints(root, Path.Combine(worktree, ".git"));
            if (File.Exists(worktree))
                throw new InvalidOperationException($"Existing Proto path '{submodule.Path}' is a file rather than a checkout directory; it was left untouched.");
            if (Directory.Exists(worktree))
            {
                if (!File.Exists(Path.Combine(worktree, ".git")) && !Directory.Exists(Path.Combine(worktree, ".git")))
                {
                    if (Directory.EnumerateFileSystemEntries(worktree).Any())
                        throw new InvalidOperationException($"Uninitialized Proto path '{submodule.Path}' contains user files; it was left untouched.");
                }
                else
                {
                    var actualGitDir = await RunGitAsync(worktree, gitEnvironment, "rev-parse", "--absolute-git-dir");
                    if (!PathsEqual(gitDir, actualGitDir.Trim()))
                        throw new InvalidOperationException($"Existing Proto checkout '{submodule.Path}' does not use its fixed local object store; it was left untouched.");
                    var checkoutHead = await RunGitAsync(worktree, gitEnvironment, "rev-parse", "HEAD");
                    if (!string.Equals(checkoutHead.Trim(), submodule.Commit, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Existing Proto checkout '{submodule.Path}' is at {checkoutHead.Trim()}, expected {submodule.Commit}; it was left untouched.");
                    var status = await RunGitAsync(worktree, gitEnvironment, "status", "--porcelain=v1", "--untracked-files=all", "--ignored=matching");
                    if (!string.IsNullOrWhiteSpace(status))
                        throw new InvalidOperationException($"Existing Proto checkout '{submodule.Path}' has tracked, untracked, or ignored changes; it was left untouched. First status entry: {status.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0]}");
                }
            }

            var restricted = await CaptureAsync("git", ["-c", "protocol.https.allow=always", "ls-remote", "https://127.0.0.1:1/duck-proto-preflight-probe"], root, gitEnvironment);
            if (restricted.ExitCode == 0 || !restricted.Stderr.Contains("not allowed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Git transport denial could not be verified against protocol.https.allow=always; no Proto files were changed.");
        }

        await VerifyProtoInputsAsync(root, gitEnvironment);
    }

    private static async Task RejectConfiguredFiltersAsync(string gitDirectory, IReadOnlyDictionary<string, string?> environment, string description, bool bareStore)
    {
        var args = bareStore
            ? new[] { "--git-dir", gitDirectory, "config", "--get-regexp", @"^filter\." }
            : new[] { "-C", gitDirectory, "config", "--get-regexp", @"^filter\." };
        var filters = await CaptureAsync("git", args, Environment.CurrentDirectory, environment);
        if (filters.ExitCode is not (0 or 1) || !string.IsNullOrWhiteSpace(filters.Stdout))
            throw new InvalidOperationException($"External Git filters are configured or could not be inspected in the {description}; no Proto files were changed. {filters.Stdout.Trim()} {filters.Stderr.Trim()}".Trim());
    }

    private static async Task VerifyObjectClosureAsync(string gitDir, ProtoSubmodule submodule, IReadOnlyDictionary<string, string?> environment)
    {
        try
        {
            var commitType = await RunGitAsync(Environment.CurrentDirectory, environment, "--git-dir", gitDir, "cat-file", "-t", submodule.Commit);
            if (!string.Equals(commitType.Trim(), "commit", StringComparison.Ordinal))
                throw new InvalidOperationException($"Expected commit {submodule.Commit} was absent from the local object store.");
            var tree = (await RunGitAsync(Environment.CurrentDirectory, environment, "--git-dir", gitDir, "rev-parse", $"{submodule.Commit}^{{tree}}")).Trim();
            var listing = await RunGitAsync(Environment.CurrentDirectory, environment, "--git-dir", gitDir, "ls-tree", "-r", "-t", "-z", "--full-tree", submodule.Commit);
            var objects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [tree] = "tree" };
            foreach (var entry in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                var tab = entry.IndexOf('\t');
                if (tab < 0) throw new InvalidOperationException("The local object listing could not be parsed.");
                var metadata = entry[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (metadata.Length != 3) throw new InvalidOperationException("A local object entry could not be parsed.");
                if (metadata[1] is "tree" or "blob") objects[metadata[2]] = metadata[1];
            }

            var batch = await CheckObjectTypesAsync(gitDir, objects.Keys, environment);
            var missing = batch.Where(item => !objects.TryGetValue(item.Key, out var expected) || item.Value != expected || item.Value == "missing")
                .Select(item => item.Key).ToArray();
            if (missing.Length != 0)
                throw new InvalidOperationException($"Missing or incorrectly typed objects: {string.Join(", ", missing.Take(8))}.");
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Local object closure for '{submodule.Path}' is missing, corrupt, or could not be verified; no fetch was attempted. {ex.Message}", ex);
        }
    }

    private static async Task<Dictionary<string, string>> CheckObjectTypesAsync(string gitDir, IEnumerable<string> objectIds, IReadOnlyDictionary<string, string?> environment)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("--git-dir"); info.ArgumentList.Add(gitDir);
        info.ArgumentList.Add("cat-file"); info.ArgumentList.Add("--batch-check=%(objectname) %(objecttype)");
        foreach (var pair in environment)
        {
            if (pair.Value is null) info.Environment.Remove(pair.Key);
            else info.Environment[pair.Key] = pair.Value;
        }
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Git object closure inspection.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        foreach (var oid in objectIds) await process.StandardInput.WriteLineAsync(oid);
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"Git could not verify the local Proto object closure: {error.Trim()}");
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in (await stdout).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2) results[parts[0]] = parts[1];
        }
        return results;
    }

    private static async Task VerifyProtoInputsAsync(string root, IReadOnlyDictionary<string, string?> environment)
    {
        var projectPath = Path.Combine(root, "SslProto", "SslProto.csproj");
        if (!File.Exists(projectPath)) throw new InvalidOperationException("SslProto/SslProto.csproj is missing; Proto inputs cannot be verified.");
        using var reader = XmlReader.Create(projectPath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var project = XDocument.Load(reader);
        var items = project.Descendants().Where(element => element.Name.LocalName == "Protobuf").ToArray();
        if (items.Length != 2) throw new InvalidOperationException("SslProto.csproj must contain exactly the two reviewed Protobuf input groups.");
        var expected = new[]
        {
            (Include: "src/external/ssl-game-controller/proto/**/*.proto", Root: "src/external/ssl-game-controller/proto", Excludes: Array.Empty<string>(), Additional: Array.Empty<string>()),
            (Include: "src/external/ssl-simulation-protocol/proto/**/*.proto", Root: "src/external/ssl-simulation-protocol/proto", Excludes: new[] { "src/external/ssl-simulation-protocol/proto/ssl_gc_common.proto", "src/external/ssl-simulation-protocol/proto/ssl_vision_geometry.proto", "src/external/ssl-simulation-protocol/proto/ssl_vision_detection.proto" }, Additional: new[] { "src/external/ssl-game-controller/proto/state", "src/external/ssl-game-controller/proto/vision" }),
        };
        for (var i = 0; i < expected.Length; i++)
        {
            var item = items[i];
            var rule = expected[i];
            if (item.Attribute("Include")?.Value.Trim() != rule.Include || item.Attribute("ProtoRoot")?.Value.Trim() != rule.Root)
                throw new InvalidOperationException($"SslProto.csproj Protobuf include or ProtoRoot #{i + 1} is ambiguous or unexpected.");
            var actualExcludes = SplitMsBuildList(item.Attribute("Exclude")?.Value);
            if (!actualExcludes.SequenceEqual(rule.Excludes, StringComparer.Ordinal))
                throw new InvalidOperationException($"SslProto.csproj Protobuf exclude set #{i + 1} differs from the reviewed contract: {string.Join(", ", actualExcludes)}");
            var actualAdditional = SplitMsBuildList(item.Attribute("AdditionalImportDirs")?.Value);
            if (!actualAdditional.SequenceEqual(rule.Additional, StringComparer.Ordinal))
                throw new InvalidOperationException($"SslProto.csproj AdditionalImportDirs #{i + 1} differs from the reviewed contract: {string.Join(", ", actualAdditional)}");
        }

        var filesBySubmodule = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var submodule in RequiredProtoSubmodules)
        {
            var gitDir = Path.GetFullPath(Path.Combine(root, (await RunGitAsync(root, environment, "rev-parse", "--git-path", $"modules/{submodule.Path}")).Trim()));
            var listing = await RunGitAsync(root, environment, "--git-dir", gitDir, "ls-tree", "-r", "-z", "--name-only", submodule.Commit);
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(path => path.StartsWith("proto/", StringComparison.Ordinal) && path.EndsWith(".proto", StringComparison.Ordinal)))
            {
                var blob = await RunGitAsync(root, environment, "--git-dir", gitDir, "rev-parse", $"{submodule.Commit}:{path}");
                files[path] = blob.Trim();
            }
            filesBySubmodule.Add(submodule.Name, files);
        }

        var gameFiles = filesBySubmodule["ssl-game-controller"];
        var simulationFiles = filesBySubmodule["ssl-simulation-protocol"];
        var excludedPaths = expected[1].Excludes.Select(path => path["src/external/ssl-simulation-protocol/".Length..]).ToHashSet(StringComparer.Ordinal);
        foreach (var excluded in excludedPaths)
            if (!simulationFiles.ContainsKey(excluded)) throw new InvalidOperationException($"Expected excluded Proto input is missing from the pinned sim-protocol commit: '{excluded}'.");

        var gameInputs = gameFiles.Keys.ToHashSet(StringComparer.Ordinal);
        var simulationInputs = simulationFiles.Keys.Where(path => !excludedPaths.Contains(path)).ToHashSet(StringComparer.Ordinal);
        var protoContents = new List<(string Module, string Path, string Content)>();
        foreach (var file in gameInputs.Select(path => ("ssl-game-controller", path, gameFiles[path]))
            .Concat(simulationInputs.Select(path => ("ssl-simulation-protocol", path, simulationFiles[path]))))
        {
            var spec = RequiredProtoSubmodules.Single(item => item.Name == file.Item1);
            var gitDir = Path.GetFullPath(Path.Combine(root, (await RunGitAsync(root, environment, "rev-parse", "--git-path", $"modules/{spec.Path}")).Trim()));
            var content = await RunGitAsync(root, environment, "--git-dir", gitDir, "cat-file", "blob", file.Item3);
            protoContents.Add((file.Item1, file.Item2, content));
        }

        var gameImportDirs = new[] { "proto" };
        var simulationImportDirs = new[] { "proto", "proto/state", "proto/vision" };
        var googleProtoRoots = FindGoogleProtoRoots(root);
        foreach (var importDir in new[] { "proto/state", "proto/vision" })
            if (!gameFiles.Keys.Any(path => path.StartsWith(importDir + "/", StringComparison.Ordinal)))
                throw new InvalidOperationException($"Required import directory 'ssl-game-controller/{importDir}' is absent from the pinned Proto inputs.");

        foreach (var input in protoContents)
        {
            var importDirs = input.Module == "ssl-game-controller" ? gameImportDirs : simulationImportDirs;
            foreach (Match match in Regex.Matches(input.Content, @"(?m)^\s*import\s+(?:(?:public|weak)\s+)?""([^""]+)""\s*;"))
            {
                var import = match.Groups[1].Value;
                if (Path.IsPathRooted(import) || import.Split('/').Any(part => part == ".."))
                    throw new InvalidOperationException($"Proto import '{import}' in '{input.Module}/{input.Path}' escapes its import roots.");
                var found = importDirs.Any(directory =>
                    input.Module == "ssl-game-controller"
                        ? gameFiles.ContainsKey($"{directory}/{import}")
                        : simulationFiles.ContainsKey($"{directory}/{import}") || gameFiles.ContainsKey($"{directory}/{import}"))
                    || (import.StartsWith("google/protobuf/", StringComparison.Ordinal)
                        && googleProtoRoots.Any(directory => File.Exists(Path.Combine(directory, import.Replace('/', Path.DirectorySeparatorChar)))));
                if (!found) throw new InvalidOperationException($"Required import '{import}' from '{input.Module}/{input.Path}' is missing from the pinned gitlinks.");
            }
        }

        if (gameInputs.Count == 0 || simulationInputs.Count == 0)
            throw new InvalidOperationException("The reviewed Protobuf include/exclude rules resolved to an empty input set.");
    }

    private static string[] FindGoogleProtoRoots(string root)
    {
        var packageRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assetsPath = Path.Combine(root, "SslProto", "obj", "project.assets.json");
        if (File.Exists(assetsPath))
        {
            using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
            if (assets.RootElement.TryGetProperty("libraries", out var libraries) &&
                assets.RootElement.TryGetProperty("packageFolders", out var folders))
            {
                foreach (var library in libraries.EnumerateObject().Where(item => item.Name.StartsWith("Grpc.Tools/", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!library.Value.TryGetProperty("path", out var relativePackagePath)) continue;
                    foreach (var folder in folders.EnumerateObject())
                        packageRoots.Add(Path.Combine(folder.Name, relativePackagePath.GetString() ?? string.Empty, "build", "native", "include"));
                }
            }
        }

        var globalPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(globalPackages)) packageRoots.Add(Path.Combine(globalPackages, "grpc.tools"));
        var userPackages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "grpc.tools");
        packageRoots.Add(userPackages);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var packageRoot in packageRoots)
        {
            if (!Directory.Exists(packageRoot)) continue;
            foreach (var candidate in Directory.Exists(Path.Combine(packageRoot, "google", "protobuf"))
                ? new[] { packageRoot }
                : Directory.EnumerateDirectories(packageRoot).SelectMany(version => new[]
                {
                    Path.Combine(version, "build", "native", "include"),
                    Path.Combine(version, "tools")
                }))
            {
                if (Directory.Exists(Path.Combine(candidate, "google", "protobuf"))) found.Add(candidate);
            }
        }
        return found.ToArray();
    }

    private static string[] SplitMsBuildList(string? value) => (value ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static async Task<string> RunGitAsync(string workingDirectory, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var result = await CaptureAsync("git", arguments, workingDirectory, environment);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git inspection failed ({string.Join(" ", arguments)}): {result.Stderr.Trim()} {result.Stdout.Trim()}".Trim());
        return result.Stdout;
    }

    private static void EnsureNoReparsePoints(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException($"Proto path '{current}' is a symlink or reparse point and was left untouched.");
        }
    }

    private static void EnsureNoReparsePointAncestors(string path, string description)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current is not null)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"The {description} contains a symlink or reparse point at '{current.FullName}'. No Proto files were changed.");
            current = current.Parent;
        }
    }

    private static bool PathsEqual(string left, string right) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed record ProtoSubmodule(string Name, string Path, string Commit);

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
        FileStream lease;
        try { lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new InvalidOperationException($"Could not acquire Duck's stack ownership lock '{lockPath}'; an AppHost or cleanup may be active. No containers were changed.", ex); }
        await using (lease)
        {
            var composeManifestExists = File.Exists(composeFile);
            if (composeManifestExists)
                await VerifyPersistentComposeOwnershipAsync(composeProject, stackId, runDocker);
            var listArguments = new List<string>
            {
                "ps", "-aq", "--no-trunc",
                "--filter", $"label=duck.aspire.stack={stackId}",
                "--filter", $"label=duck.aspire.run={stackId}",
            };
            var listed = await runDocker(listArguments.ToArray());
            if (listed.ExitCode != 0) throw new InvalidOperationException("Could not list containers for the selected persistent Duck stack.");
            var ids = listed.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var inspectedResources = new List<(string Id, string ResourceRole)>();
            foreach (var id in ids)
            {
                var inspect = await runDocker(["inspect", "--format", "{{json .Config.Labels}}", id]);
                if (inspect.ExitCode != 0) throw new InvalidOperationException($"Could not verify container {id}; it was left untouched.");
                using var labelsDoc = JsonDocument.Parse(inspect.Stdout);
                var labels = labelsDoc.RootElement;
                var role = GetLabel(labels, "duck.aspire.resource");
                var composeProjectLabel = GetLabel(labels, "com.docker.compose.project");
                var isComposeResource = role?.StartsWith("compose-", StringComparison.Ordinal) ?? false;
                if (GetLabel(labels, "duck.aspire.owner") != "duck-apphost" ||
                    GetLabel(labels, "duck.aspire.stack") != stackId ||
                    GetLabel(labels, "duck.aspire.run") != stackId ||
                    string.IsNullOrWhiteSpace(role) ||
                    (composeManifestExists
                        ? (composeProjectLabel is not null && composeProjectLabel != composeProject) || isComposeResource != (composeProjectLabel == composeProject)
                        : composeProjectLabel is not null || isComposeResource))
                    throw new InvalidOperationException($"Container {id} did not match owner/stack/run/Compose project/resource-role labels; it was left untouched.");
                inspectedResources.Add((id, role));
            }
            var containerIdsToRemove = composeManifestExists
                ? inspectedResources.Where(resource => !resource.ResourceRole.StartsWith("compose-", StringComparison.Ordinal)).Select(resource => resource.Id)
                : inspectedResources.Select(resource => resource.Id);
            foreach (var id in containerIdsToRemove)
                await RunDockerCheckedAsync(runDocker, ["rm", "-f", id], $"Remove owned container {id}");
            if (composeManifestExists)
            {
                await RunDockerCheckedAsync(runDocker,
                    ["compose", "--project-name", composeProject, "--file", composeFile, "down", "--remove-orphans", "--timeout", "15", "--rmi", "local"],
                    $"Stop persistent Compose project {composeProject}");
            }
            var remaining = await runDocker(["ps", "-aq", "--no-trunc",
                "--filter", $"label=duck.aspire.stack={stackId}",
                "--filter", $"label=duck.aspire.run={stackId}"]);
            if (remaining.ExitCode != 0 || !string.IsNullOrWhiteSpace(remaining.Stdout)) throw new InvalidOperationException("Selected persistent Duck stack still has containers after cleanup.");
            if (composeManifestExists) File.Delete(composeFile);
            Console.WriteLine($"Removed only verified containers and local Compose images for stack/run ID '{stackId}'.");
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

    /// <summary>失敗時の回収を、この起動で新規作成した一時スタックに限定します。</summary>
    /// <param name="persistent">永続スタックを使う場合は <see langword="true"/> です。</param>
    /// <param name="cleanup">一時スタックを回収する処理です。</param>
    /// <returns>一時スタックの場合は回収処理、永続スタックの場合は <see langword="null"/> です。</returns>
    internal static Func<Task>? CreateFailureCleanup(bool persistent, Func<Task> cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        return persistent ? null : cleanup;
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
