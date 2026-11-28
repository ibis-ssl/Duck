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
            await PrepareAndValidateProtoSubmodulesAsync(root, gitEnvironment, options.PrepareMissing,
                confirmMissing: options.PrepareMissing ? null : ConfirmProtoPreparationAsync);
            var sourceCommit = options.PlayMatch
                ? (await RunGitAsync(root, gitEnvironment, "rev-parse", "HEAD")).Trim()
                : null;
            if (options.PlayMatch &&
                !string.IsNullOrWhiteSpace(await RunGitAsync(root, gitEnvironment, "status", "--porcelain", "--untracked-files=normal")))
                throw new InvalidOperationException("--play-match requires a clean checkout so the recorded commit identifies the source.");

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
                $"started_at_utc={DateTime.UtcNow:O}", $"testing_mode={mode}",
                $"game_controller_ui={(options.PlayMatch ? "automated" : "manual")}",
            ];
            await File.WriteAllLinesAsync(Path.Combine(artifactDirectory, $"startup-mode-{launchId}.txt"), startupLines, new UTF8Encoding(false));
            if (options.PlayMatch)
                await File.AppendAllLinesAsync(Path.Combine(artifactDirectory, $"startup-mode-{launchId}.txt"),
                    [$"source_commit={sourceCommit}", $"match_half_seconds={options.HalfSeconds}", "match_maximum_duration_seconds=300",
                        .. RequiredProtoSubmodules.Select(submodule => $"proto_{submodule.Name}={submodule.Commit}")]);

            var appHostProject = Path.Combine(root, "Testing", "Duck.Testing.AppHost", "Duck.Testing.AppHost.csproj");
            var appHostExe = Path.Combine(root, "Testing", "Duck.Testing.AppHost", "bin", "Debug", "net10.0", "Duck.Testing.AppHost.exe");
            var environment = new Dictionary<string, string?>
            {
                ["Testing__Mode"] = "base", ["Testing__MatchReadinessOnly"] = "false",
                ["Testing__RuntimeHostOnWindows"] = options.HostRuntime ? "true" : null,
                ["Testing__Compose__DevelopmentUdpTransfer"] = options.HostRuntime ? "true" : null,
                ["Testing__Compose__PullMissingImages"] = options.PlayMatch ? "false" : "true",
                ["Testing__WindowsMatch__ResultDirectory"] = options.PlayMatch ? artifactDirectory : null,
                ["Testing__WindowsMatch__PrepareMissing"] = options.PrepareMissing ? "true" : "false",
                ["Testing__WindowsMatch__SourceCommit"] = sourceCommit,
                ["Testing__WindowsMatch__HalfSeconds"] = options.PlayMatch ? options.HalfSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
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
            Console.WriteLine(options.PlayMatch
                ? "AppHost will check the local fixed images and run the match through POST_GAME."
                : "Docker context and Engine ID are resolved by AppHost; match settings and browser actions remain manual.");
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
    internal static async Task PrepareAndValidateProtoSubmodulesAsync(string root, IReadOnlyDictionary<string, string?>? gitEnvironment = null, bool prepareMissing = false, Func<string, string, string, Task>? fetchMissing = null,
        Func<IReadOnlyList<ProtoPreparationTarget>, Task<bool>>? confirmMissing = null,
        Func<string, FileAttributes>? inspectAttributes = null, Func<string, byte[]>? readProtoFile = null)
    {
        gitEnvironment ??= CreateRestrictedGitEnvironment();
        if (Environment.GetEnvironmentVariable("GIT_INDEX_FILE") is not null ||
            gitEnvironment.TryGetValue("GIT_INDEX_FILE", out var alternateIndex) && alternateIndex is not null)
            throw new InvalidOperationException("GIT_INDEX_FILE redirects Git to an alternate parent index; no Proto files were changed.");
        if (gitEnvironment.GetValueOrDefault("GIT_ALLOW_PROTOCOL") != string.Empty || gitEnvironment.GetValueOrDefault("GIT_NO_LAZY_FETCH") != "1")
            throw new InvalidOperationException("The restricted Git environment is incomplete; no Proto files were changed.");
        if (prepareMissing || confirmMissing is not null)
        {
            var stores = new List<(ProtoSubmodule Module, string GitDir)>();
            await InspectProtoStateAsync(root, gitEnvironment, stores, validateStaticInputsWhenStoresMissing: confirmMissing is not null,
                inspectAttributes: inspectAttributes, readProtoFile: readProtoFile);
            if (!prepareMissing && stores.Count > 0)
            {
                var plan = stores.Select(item => new ProtoPreparationTarget(item.Module.Name, item.Module.Commit,
                    OfficialProtoUrl(item.Module.Name), item.GitDir,
                    Path.GetFullPath(Path.Combine(root, item.Module.Path.Replace('/', Path.DirectorySeparatorChar))))).ToArray();
                if (!await (confirmMissing ?? ConfirmProtoPreparationAsync)(plan))
                    throw new InvalidOperationException("Fixed Proto preparation was not explicitly approved; Duck was not started.");

                var rechecked = new List<(ProtoSubmodule Module, string GitDir)>();
                await InspectProtoStateAsync(root, gitEnvironment, rechecked, validateStaticInputsWhenStoresMissing: true,
                    inspectAttributes: inspectAttributes, readProtoFile: readProtoFile);
                if (!SamePreparationPlan(plan, rechecked.Select(item => new ProtoPreparationTarget(item.Module.Name, item.Module.Commit,
                        OfficialProtoUrl(item.Module.Name), item.GitDir,
                        Path.GetFullPath(Path.Combine(root, item.Module.Path.Replace('/', Path.DirectorySeparatorChar)))))
                    .ToArray()))
                    throw new InvalidOperationException("Fixed Proto preparation plan changed after approval; no external fetch was attempted and Duck was not started.");
                stores = rechecked;
            }
            var staged = new List<(string Destination, DirectoryInfo Area, string Store)>();
            var placedStores = new List<string>();
            try
            {
                foreach (var (module, gitDir) in stores)
                {
                    var area = CreateProtoStagingArea(gitDir);
                    var store = Path.Combine(area.FullName, "store");
                    staged.Add((gitDir, area, store));
                    await (fetchMissing ?? FetchProtoStoreAsync)(store, OfficialProtoUrl(module.Name), module.Commit);
                }
                // Inspect the full combined set before moving any staged store into a real destination.
                await InspectProtoStateAsync(root, gitEnvironment, stagedStores:
                    staged.ToDictionary(item => item.Destination, item => item.Store, StringComparer.OrdinalIgnoreCase));
                foreach (var item in staged)
                {
                    EnsureNoReparsePointAncestors(item.Destination, "Proto object store");
                    Directory.CreateDirectory(Path.GetDirectoryName(item.Destination)!);
                    Directory.Move(item.Store, item.Destination);
                    placedStores.Add(item.Destination);
                }
            }
            catch (Exception ex)
            {
                // Do not remove or replace any existing object store. Retain already placed stores for retry.
                throw new InvalidOperationException($"Fixed Proto preparation stopped. Stores placed by this run: {string.Join(", ", placedStores)}. {ex.Message}", ex);
            }
            finally
            {
                foreach (var item in staged) item.Area.Delete(recursive: true);
            }
        }
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

    private static bool SamePreparationPlan(IReadOnlyList<ProtoPreparationTarget> approved, IReadOnlyList<ProtoPreparationTarget> current) =>
        approved.Count == current.Count && approved.Zip(current).All(pair => pair.First == pair.Second);

    private static Task<bool> ConfirmProtoPreparationAsync(IReadOnlyList<ProtoPreparationTarget> plan)
    {
        return ConfirmProtoPreparationAsync(plan, () => Console.IsInputRedirected, Console.ReadLine, Console.WriteLine, Console.Error.WriteLine);
    }

    internal static Task<bool> ConfirmProtoPreparationAsync(IReadOnlyList<ProtoPreparationTarget> plan,
        Func<bool> isInputRedirected, Func<string?> readLine, Action<string> writeLine, Action<string> writeError)
    {
        writeLine("The following fixed Proto inputs are absent. Only these exact official sources and destinations will be prepared:");
        foreach (var target in plan)
        {
            writeLine($"  SHA: {target.Commit}");
            writeLine($"  Official URL: {target.Url}");
            writeLine($"  Object store: {target.ObjectStorePath}");
            writeLine($"  Working copy: {target.WorkingCopyPath}");
        }
        if (isInputRedirected())
        {
            writeError("Input is redirected or non-interactive. No external fetch was attempted; rerun in an interactive terminal and enter FETCH to approve.");
            return Task.FromResult(false);
        }
        Console.Write("Enter FETCH to approve this exact plan (anything else stops): ");
        return Task.FromResult(string.Equals(readLine(), "FETCH", StringComparison.Ordinal));
    }

    internal sealed record ProtoPreparationTarget(string Name, string Commit, string Url, string ObjectStorePath, string WorkingCopyPath);

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

    /// <summary>ローカル Proto object 閉包の検査をテスト用の Git ディレクトリで実行します。</summary>
    /// <param name="gitDir">検査対象の Git ディレクトリです。</param>
    /// <param name="path">診断に使う対象パスです。</param>
    /// <param name="commit">検査する固定 commit です。</param>
    /// <param name="environment">外部通信を制限する Git 環境です。</param>
    internal static Task VerifyObjectClosureForTestsAsync(string gitDir, string path, string commit, IReadOnlyDictionary<string, string?> environment) =>
        VerifyObjectClosureAsync(gitDir, new ProtoSubmodule("test", path, commit), environment);

    private static async Task InspectProtoStateAsync(string root, IReadOnlyDictionary<string, string?> gitEnvironment,
        List<(ProtoSubmodule Module, string GitDir)>? missingStores = null,
        IReadOnlyDictionary<string, string>? stagedStores = null,
        bool validateStaticInputsWhenStoresMissing = false,
        Func<string, FileAttributes>? inspectAttributes = null, Func<string, byte[]>? readProtoFile = null)
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

        EnsureNoReparsePoints(root, Path.Combine(root, ".gitmodules"), inspectAttributes);
        var committedModules = await RunGitAsync(root, gitEnvironment, "cat-file", "blob", "HEAD:.gitmodules");
        if (!File.Exists(Path.Combine(root, ".gitmodules")) ||
            !string.Equals(await File.ReadAllTextAsync(Path.Combine(root, ".gitmodules")), committedModules, StringComparison.Ordinal))
            throw new InvalidOperationException(".gitmodules differs from HEAD or could not be checked; no Proto files were changed.");
        var stagedModules = await RunGitAsync(root, gitEnvironment, "ls-files", "--stage", "--", ".gitmodules");
        var headModulesEntry = (await RunGitAsync(root, gitEnvironment, "ls-tree", "HEAD", "--", ".gitmodules")).Trim();
        var headModulesMetadata = headModulesEntry.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var expectedIndexModules = headModulesMetadata.Length == 4 && headModulesMetadata[1] == "blob"
            ? $"{headModulesMetadata[0]} {headModulesMetadata[2]} 0\t.gitmodules"
            : string.Empty;
        if (expectedIndexModules.Length == 0 || !string.Equals(stagedModules.Trim(), expectedIndexModules, StringComparison.Ordinal))
            throw new InvalidOperationException("Index entry for .gitmodules differs from HEAD or could not be checked; no Proto files were changed.");

        var verifiedProtoContents = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
        var worktreesToCheck = new List<(ProtoSubmodule Module, string Path)>();
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

            if ((missingStores is not null || stagedStores is not null) && url != OfficialProtoUrl(submodule.Name))
                throw new InvalidOperationException($"Unexpected official Proto URL for {submodule.Path}; no fetch attempted.");

            var customUpdate = await CaptureAsync("git", ["config", "--get-all", $"submodule.{submodule.Path}.update"], root, gitEnvironment);
            if (customUpdate.ExitCode is not (0 or 1) || customUpdate.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(value => !string.Equals(value, "checkout", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Submodule '{submodule.Path}' has a non-checkout or uninspectable update command; no Proto files were changed.");

            var gitDirResult = await RunGitAsync(root, gitEnvironment, "rev-parse", "--git-path", $"modules/{submodule.Path}");
            var gitDir = Path.GetFullPath(Path.IsPathFullyQualified(gitDirResult.Trim())
                ? gitDirResult.Trim()
                : Path.Combine(root, gitDirResult.Trim()));
            EnsureNoReparsePointAncestors(gitDir, $"object store for '{submodule.Path}'", inspectAttributes);
            var storeAttributes = GetInspectedPathAttributes(gitDir, $"Proto object store '{submodule.Path}'", inspectAttributes);
            if (storeAttributes is null)
            {
                if (missingStores is null && stagedStores is null)
                    throw LocalProtoAssetFailure(submodule, $"fixed local object store is unavailable at '{gitDir}'; no network fetch was attempted");
                var absentWorktree = Path.Combine(root, submodule.Path.Replace('/', Path.DirectorySeparatorChar));
                var missingStoreWorktreeAttributes = GetInspectedPathAttributes(absentWorktree, $"Proto working copy '{submodule.Path}'", inspectAttributes);
                if (missingStoreWorktreeAttributes is not null && (missingStoreWorktreeAttributes.Value & FileAttributes.Directory) == 0)
                    throw new InvalidOperationException($"Existing Proto path '{submodule.Path}' is a file rather than a checkout directory; it was left untouched.");
                var worktreeGitMarker = missingStoreWorktreeAttributes is null ? null : GetInspectedPathAttributes(
                    Path.Combine(absentWorktree, ".git"), $"Proto working copy metadata '{submodule.Path}/.git'", inspectAttributes);
                if (worktreeGitMarker is not null || missingStoreWorktreeAttributes is not null && Directory.EnumerateFileSystemEntries(absentWorktree).Any())
                    throw new InvalidOperationException($"Existing Proto data for {submodule.Path} was left untouched; no fetch attempted.");
                if (stagedStores is not null && stagedStores.TryGetValue(gitDir, out var stagedStore))
                {
                    EnsureNoReparsePointAncestors(stagedStore, "staged Proto object store", inspectAttributes);
                    gitDir = stagedStore;
                }
                else
                {
                    if (missingStores is null) throw new InvalidOperationException($"Missing staged Proto store for {submodule.Path}.");
                    missingStores.Add((submodule, gitDir));
                    continue;
                }
            }
            else
            {
                if ((storeAttributes.Value & FileAttributes.Directory) == 0)
                    throw new InvalidOperationException($"Proto object store '{submodule.Path}' is not a directory; no Proto files were changed.");
                if (stagedStores is not null && stagedStores.ContainsKey(gitDir))
                    throw new InvalidOperationException($"Proto store appeared during preparation: {submodule.Path}; no store was replaced.");
            }
            await VerifyModuleIndexAsync(gitDir, submodule, gitEnvironment, required: false);
            verifiedProtoContents[submodule.Name] = await VerifyObjectClosureAsync(gitDir, submodule, gitEnvironment);
            await VerifyNoApplicableFiltersAsync(gitDir, submodule, gitEnvironment);

            var worktree = Path.Combine(root, submodule.Path.Replace('/', Path.DirectorySeparatorChar));
            var worktreeAttributes = GetInspectedPathAttributes(worktree, $"Proto working copy '{submodule.Path}'", inspectAttributes);
            if (worktreeAttributes is not null && (worktreeAttributes.Value & FileAttributes.Directory) == 0)
                throw new InvalidOperationException($"Existing Proto path '{submodule.Path}' is a file rather than a checkout directory; it was left untouched.");
            if (worktreeAttributes is not null)
            {
                var worktreeGitMarker = GetInspectedPathAttributes(Path.Combine(worktree, ".git"), $"Proto working copy metadata '{submodule.Path}/.git'", inspectAttributes);
                if (worktreeGitMarker is null)
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
                    await VerifyModuleIndexAsync(gitDir, submodule, gitEnvironment, required: true);
                    await VerifyNoApplicableFiltersAsync(gitDir, submodule, gitEnvironment, worktree);
                    worktreesToCheck.Add((submodule, worktree));
                }
            }
        }

        if (missingStores is null || missingStores.Count == 0)
            await VerifyProtoInputsAsync(root, verifiedProtoContents);
        else if (validateStaticInputsWhenStoresMissing)
        {
            var expected = VerifyProtoProjectConfiguration(root);
            VerifyAvailableMaterializedProtoInputs(root, expected, verifiedProtoContents, inspectAttributes, readProtoFile);
        }

        // Recheck after reading the pinned inputs so concurrent worktree changes cannot pass the preflight.
        foreach (var (module, worktree) in worktreesToCheck)
            await EnsurePreparedProtoCheckoutCleanAsync(module, worktree, gitEnvironment);
    }

    /// <summary>不足する object store があっても、読める既存 checkout の固定 Proto バイト列を照合します。</summary>
    private static void VerifyAvailableMaterializedProtoInputs(string root,
        (string Include, string Root, string[] Excludes, string[] AdditionalImportDirs)[] expected,
        IReadOnlyDictionary<string, Dictionary<string, byte[]>> contentsBySubmodule,
        Func<string, FileAttributes>? inspectAttributes, Func<string, byte[]>? readProtoFile)
    {
        foreach (var module in RequiredProtoSubmodules)
        {
            if (!contentsBySubmodule.TryGetValue(module.Name, out var pinnedFiles) || pinnedFiles.Count == 0) continue;
            var worktree = Path.Combine(root, module.Path.Replace('/', Path.DirectorySeparatorChar));
            var attributes = GetInspectedPathAttributes(worktree, $"Proto working copy '{module.Path}'", inspectAttributes);
            if (attributes is null) continue;
            if ((attributes.Value & FileAttributes.Directory) == 0)
                throw new InvalidOperationException($"Proto working copy '{module.Path}' is not a directory; no Proto files were changed.");
            var gitMarker = GetInspectedPathAttributes(Path.Combine(worktree, ".git"), $"Proto working copy metadata '{module.Path}/.git'", inspectAttributes);
            if (gitMarker is null) continue;
            var inputs = module.Name == "ssl-game-controller"
                ? pinnedFiles.Keys
                : pinnedFiles.Keys.Where(path => !expected[1].Excludes.Any(exclude =>
                    string.Equals(path, exclude["src/external/ssl-simulation-protocol/".Length..], StringComparison.Ordinal)));
            VerifyMaterializedProtoFiles(root, module, inputs, pinnedFiles, inspectAttributes, readProtoFile);
        }
    }

    private static string OfficialProtoUrl(string name) => name switch
    {
        "ssl-game-controller" => "https://github.com/RoboCup-SSL/ssl-game-controller",
        "ssl-simulation-protocol" => "https://github.com/RoboCup-SSL/ssl-simulation-protocol",
        _ => throw new InvalidOperationException("Unknown fixed Proto source.")
    };

    // Stage the acquisition alongside the missing store. Never replace an existing store.
    internal static async Task PrepareMissingProtoStoreAsync(string destination, string url, string commit,
        Func<string, string, string, Task> fetch, Func<string, Task> verify)
    {
        var staging = CreateProtoStagingArea(destination);
        try
        {
            var store = Path.Combine(staging.FullName, "store");
            await fetch(store, url, commit);
            await verify(store);
            EnsureNoReparsePointAncestors(destination, "Proto object store");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(store, destination);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not prepare fixed Proto {url} at {commit}: {ex.Message}", ex);
        }
        finally { staging.Delete(recursive: true); }
    }

    private static DirectoryInfo CreateProtoStagingArea(string destination)
    {
        EnsureNoReparsePointAncestors(destination, "Proto object store");
        if (GetInspectedPathAttributes(destination, "Proto object store") is not null)
            throw new InvalidOperationException("Existing Proto object store was left untouched.");
        // Store staging outside the target tree, so a failed check leaves no new target directories.
        var existingParent = new DirectoryInfo(Path.GetDirectoryName(destination)!);
        while (!existingParent.Exists) existingParent = existingParent.Parent!;
        return Directory.CreateDirectory(Path.Combine(existingParent.FullName, $"duck-proto-{Guid.NewGuid():N}"));
    }

    private static async Task FetchProtoStoreAsync(string store, string url, string commit)
    {
        // Disallow inherited Git repository/config overrides and URL rewrites.
        var environment = Environment.GetEnvironmentVariables().Keys.Cast<string>()
            .Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(key => key, _ => (string?)null, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in CreateRestrictedGitEnvironment()) environment[pair.Key] = pair.Value;
        environment["GIT_ALLOW_PROTOCOL"] = "https";
        environment["GIT_CONFIG_NOSYSTEM"] = "1";
        environment["GIT_CONFIG_GLOBAL"] = Path.Combine(Path.GetDirectoryName(store)!, "absent.gitconfig");
        environment["GIT_TERMINAL_PROMPT"] = "0";
        var cwd = Path.GetDirectoryName(store)!;
        async Task Git(params string[] args)
        {
            var result = await CaptureAsync("git", args, cwd, environment);
            if (result.ExitCode != 0) throw new InvalidOperationException($"Git {args[0]} failed ({result.ExitCode}): {result.Stderr.Trim()}");
        }
        await Git("init", "--bare", "--template=", store);
        await Git("--git-dir", store, "-c", "http.sslVerify=true", "-c", "http.followRedirects=false",
            "-c", "credential.helper=", "fetch", "--no-tags", "--no-recurse-submodules", "--depth=1", url, commit);
        await Git("--git-dir", store, "update-ref", "HEAD", commit);
        await Git("--git-dir", store, "config", "remote.origin.url", url);
        await Git("--git-dir", store, "config", "core.bare", "false");
    }

    private static async Task EnsurePreparedProtoCheckoutCleanAsync(ProtoSubmodule module, string worktree,
        IReadOnlyDictionary<string, string?> gitEnvironment)
    {
        var indexEntries = await RunGitAsync(worktree, gitEnvironment, "ls-files", "-v", "-z");
        foreach (var entry in indexEntries.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.Length < 3 || entry[1] != ' ')
                throw LocalProtoAssetFailure(module, $"the saved index returned an unreadable entry '{entry}'");
            var tag = entry[0];
            if (char.IsLower(tag) || tag == 'S')
                throw LocalProtoAssetFailure(module, $"saved index flags hide worktree changes for '{entry[2..]}' (tag '{tag}')");
        }
        var status = await RunGitAsync(worktree, gitEnvironment, "status", "--porcelain=v1", "--untracked-files=all", "--ignored=matching");
        if (!string.IsNullOrWhiteSpace(status))
            throw new InvalidOperationException($"Existing Proto checkout '{module.Path}' has tracked, untracked, or ignored changes; it was left untouched. First status entry: {status.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0]}");
    }

    private static InvalidOperationException LocalProtoAssetFailure(ProtoSubmodule module, string actualState, Exception? inner = null) =>
        new($"Local Proto asset check failed for '{module.Path}': expected SHA {module.Commit}; actual local state: {actualState}. No submodule files were changed by this check. Safe next step: follow the manual Issue #158 preparation instructions in Testing/README.md, resolve the reported local state, and rerun the check.", inner);

    /// <summary>固定 Proto tree に適用される外部 filter がないことをテスト用に検査します。</summary>
    /// <param name="gitDirectory">検査対象の Git ディレクトリです。</param>
    /// <param name="commit">検査する固定 commit です。</param>
    /// <param name="environment">外部通信を制限する Git 環境です。</param>
    internal static Task VerifyNoApplicableFiltersForTestsAsync(string gitDirectory, string commit, IReadOnlyDictionary<string, string?> environment) =>
        VerifyNoApplicableFiltersAsync(gitDirectory, new ProtoSubmodule("test", "test", commit), environment);

    /// <summary>保存済み index と固定 Proto tree の一致をテスト用に検査します。</summary>
    /// <param name="gitDirectory">検査対象の Git ディレクトリです。</param>
    /// <param name="path">診断に使う対象パスです。</param>
    /// <param name="commit">照合する固定 commit です。</param>
    /// <param name="environment">外部通信を制限する Git 環境です。</param>
    /// <param name="required">既存 checkout 用の index が必須かを示します。</param>
    internal static Task VerifyModuleIndexForTestsAsync(string gitDirectory, string path, string commit,
        IReadOnlyDictionary<string, string?> environment, bool required) =>
        VerifyModuleIndexAsync(gitDirectory, new ProtoSubmodule("test", path, commit), environment, required);

    private static async Task VerifyNoApplicableFiltersAsync(string gitDirectory, ProtoSubmodule submodule, IReadOnlyDictionary<string, string?> environment, string? worktree = null)
    {
        var listing = await RunGitStrictAsync(Environment.CurrentDirectory, environment,
            "--git-dir", gitDirectory, "ls-tree", "-r", "-z", "--full-tree", submodule.Commit);
        var paths = new List<string>();
        foreach (var entry in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = entry.IndexOf('\t');
            if (separator <= 0) throw new InvalidOperationException($"Git returned an unreadable tree entry while checking filters for '{submodule.Path}'.");
            var metadata = entry[..separator].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (metadata.Length != 3) throw new InvalidOperationException($"Git returned an unreadable tree entry while checking filters for '{submodule.Path}'.");
            if (metadata[1] == "blob") paths.Add(entry[(separator + 1)..]);
        }
        if (paths.Count == 0) throw new InvalidOperationException($"Pinned Proto submodule '{submodule.Path}' has no checkout blobs to inspect for filters.");

        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false, true),
            StandardOutputEncoding = new UTF8Encoding(false, true),
            StandardErrorEncoding = new UTF8Encoding(false, true),
        };
        var attributeArguments = worktree is null
            ? new[] { "--git-dir", gitDirectory, "check-attr", "--all", "--source", submodule.Commit, "-z", "--stdin" }
            : new[] { "-C", worktree, "check-attr", "--all", "-z", "--stdin" };
        foreach (var argument in attributeArguments)
            info.ArgumentList.Add(argument);
        foreach (var setting in environment)
        {
            if (setting.Value is null) info.Environment.Remove(setting.Key);
            else info.Environment[setting.Key] = setting.Value;
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Git attribute inspection.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            var input = Encoding.UTF8.GetBytes(string.Join('\0', paths) + '\0');
            await process.StandardInput.BaseStream.WriteAsync(input);
            process.StandardInput.Close();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0 || !string.IsNullOrEmpty(stderr))
                throw new InvalidOperationException($"Effective filter attributes for '{submodule.Path}' could not be inspected safely. Git exited {process.ExitCode}: {stderr.Trim()}".Trim());

            var fields = stdout.Length == 0 ? Array.Empty<string>() : stdout.Split('\0', StringSplitOptions.None);
            if (fields.Length != 0 && (fields[^1].Length != 0 || (fields.Length - 1) % 3 != 0))
                throw new InvalidOperationException($"Git returned an incomplete filter-attribute result for '{submodule.Path}'; no Proto files were changed.");
            var knownPaths = paths.ToHashSet(StringComparer.Ordinal);
            for (var i = 0; i < fields.Length / 3; i++)
            {
                var outputPath = fields[i * 3];
                var attribute = fields[i * 3 + 1];
                var value = fields[i * 3 + 2];
                if (!knownPaths.Contains(outputPath) || string.IsNullOrEmpty(attribute))
                    throw new InvalidOperationException($"Git returned an ambiguous filter-attribute result for '{submodule.Path}'; no Proto files were changed.");
                if (attribute != "filter") continue;
                if (value == "unset" && !await HasUnsetFilterHelperAsync(gitDirectory, environment)) continue;
                if (value != "unset")
                    throw new InvalidOperationException($"Checkout filter '{value}' applies to '{submodule.Path}/{outputPath}'. External Git filters are not started; no Proto files were changed.");
                throw new InvalidOperationException($"Checkout filter attribute for '{submodule.Path}/{outputPath}' cannot be distinguished from an explicit unset filter value with an effective helper. No Proto files were changed.");
            }
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
            try { await process.WaitForExitAsync(); } catch { }
            throw;
        }
    }

    private static async Task VerifyModuleIndexAsync(string gitDirectory, ProtoSubmodule submodule, IReadOnlyDictionary<string, string?> environment, bool required)
    {
        FileAttributes gitDirectoryAttributes;
        try
        {
            gitDirectoryAttributes = File.GetAttributes(gitDirectory);
        }
        catch (Exception ex)
        {
            throw LocalProtoAssetFailure(submodule, $"saved Git directory '{gitDirectory}' could not be inspected", ex);
        }
        if ((gitDirectoryAttributes & FileAttributes.Directory) == 0 || (gitDirectoryAttributes & FileAttributes.ReparsePoint) != 0)
            throw LocalProtoAssetFailure(submodule, $"saved Git directory '{gitDirectory}' is not a regular directory");

        var indexPath = Path.Combine(gitDirectory, "index");
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(indexPath);
        }
        catch (FileNotFoundException) when (!required)
        {
            return;
        }
        catch (FileNotFoundException ex)
        {
            throw LocalProtoAssetFailure(submodule, "existing checkout has no saved index; it was left untouched", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw LocalProtoAssetFailure(submodule, "saved index path is unavailable; it was left untouched", ex);
        }
        catch (Exception ex)
        {
            throw LocalProtoAssetFailure(submodule, $"saved index could not be read safely; it was left untouched ({ex.Message})", ex);
        }
        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) != 0)
            throw LocalProtoAssetFailure(submodule, $"saved index path '{indexPath}' is a reparse point or not a regular file; it was left untouched");
        string index;
        string tree;
        try
        {
            index = await RunGitAsync(Environment.CurrentDirectory, environment, "--git-dir", gitDirectory, "ls-files", "--stage", "-z");
            tree = await RunGitAsync(Environment.CurrentDirectory, environment, "--git-dir", gitDirectory, "ls-tree", "-r", "-z", "--full-tree", submodule.Commit);
        }
        catch (Exception ex)
        {
            throw LocalProtoAssetFailure(submodule, $"saved index or pinned tree query failed: {ex.Message}", ex);
        }
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in tree.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = entry.IndexOf('\t');
            if (tab < 0) throw LocalProtoAssetFailure(submodule, "pinned tree query returned an unreadable entry");
            var metadata = entry[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (metadata.Length != 3 || metadata[1] is not ("blob" or "commit"))
                throw LocalProtoAssetFailure(submodule, "pinned tree query returned an unsupported entry");
            expected[entry[(tab + 1)..]] = $"{metadata[0]} {metadata[2]}";
        }
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in index.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = entry.IndexOf('\t');
            if (tab < 0) throw LocalProtoAssetFailure(submodule, "saved index query returned an unmerged or unreadable entry; it was left untouched");
            var metadata = entry[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (metadata.Length != 3 || metadata[2] != "0" || !actual.TryAdd(entry[(tab + 1)..], $"{metadata[0]} {metadata[1]}"))
                throw LocalProtoAssetFailure(submodule, "saved index query returned conflicts or duplicate entries; it was left untouched");
        }
        if (actual.Count != expected.Count || expected.Any(pair => !actual.TryGetValue(pair.Key, out var value) || value != pair.Value))
            throw LocalProtoAssetFailure(submodule, $"saved index differs from pinned tree {submodule.Commit}; it was left untouched");
    }

    private static async Task<bool> HasUnsetFilterHelperAsync(string gitDirectory, IReadOnlyDictionary<string, string?> environment)
    {
        foreach (var operation in new[] { "clean", "smudge", "process" })
        {
            var result = await CaptureAsync("git", ["--git-dir", gitDirectory, "config", "--get-all", $"filter.unset.{operation}"], Environment.CurrentDirectory, environment);
            if (result.ExitCode == 0) return true;
            if (result.ExitCode != 1 || !string.IsNullOrEmpty(result.Stderr))
                throw new InvalidOperationException($"The effective filter.unset.{operation} helper could not be inspected safely. No Proto files were changed. {result.Stderr.Trim()}".Trim());
        }
        return false;
    }

    private static async Task<Dictionary<string, byte[]>> VerifyObjectClosureAsync(string gitDir, ProtoSubmodule submodule, IReadOnlyDictionary<string, string?> environment)
    {
        try
        {
            var tree = (await RunGitAsync(Environment.CurrentDirectory, environment, "--git-dir", gitDir, "rev-parse", $"{submodule.Commit}^{{tree}}")).Trim();
            var listing = await RunGitAsync(Environment.CurrentDirectory, environment, "--git-dir", gitDir, "ls-tree", "-r", "-t", "-z", "--full-tree", submodule.Commit);
            var objects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [submodule.Commit] = "commit", [tree] = "tree" };
            var protoObjects = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                var tab = entry.IndexOf('\t');
                if (tab < 0) throw new InvalidOperationException("The local object listing could not be parsed.");
                var metadata = entry[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (metadata.Length != 3) throw new InvalidOperationException("A local object entry could not be parsed.");
                if (metadata[1] is "tree" or "blob")
                {
                    if (objects.TryGetValue(metadata[2], out var previousType) && previousType != metadata[1])
                        throw new InvalidOperationException($"Object {metadata[2]} has conflicting types in the pinned tree.");
                    objects[metadata[2]] = metadata[1];
                }
                var path = entry[(tab + 1)..];
                if (metadata[1] == "blob" && path.StartsWith("proto/", StringComparison.Ordinal) && path.EndsWith(".proto", StringComparison.Ordinal))
                    protoObjects[path] = metadata[2];
            }

            if (protoObjects.Count == 0) throw new InvalidOperationException("The pinned tree has no Proto files to verify.");
            var verifiedObjects = await VerifyObjectBatchAsync(gitDir, objects, protoObjects.Values.ToHashSet(StringComparer.OrdinalIgnoreCase), environment);
            return protoObjects.ToDictionary(pair => pair.Key, pair => verifiedObjects[pair.Value], StringComparer.Ordinal);
        }
        catch (InvalidOperationException ex)
        {
            throw LocalProtoAssetFailure(submodule,
                $"object closure is missing, corrupt, or could not be verified; no fetch was attempted. {ex.Message}", ex);
        }
    }

    private static async Task<Dictionary<string, byte[]>> VerifyObjectBatchAsync(string gitDir, IReadOnlyDictionary<string, string> objects,
        IReadOnlySet<string> retain, IReadOnlyDictionary<string, string?> environment)
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
        info.ArgumentList.Add("cat-file"); info.ArgumentList.Add("--batch");
        foreach (var pair in environment)
        {
            if (pair.Value is null) info.Environment.Remove(pair.Key);
            else info.Environment[pair.Key] = pair.Value;
        }
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Git object closure inspection.");
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            using var stdout = new BufferedStream(process.StandardOutput.BaseStream, 64 * 1024);
            var verifiedProtoObjects = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var readTask = ReadAndVerifyObjectBatchAsync(stdout, objects, retain, verifiedProtoObjects);
            var requested = Encoding.ASCII.GetBytes(string.Join('\n', objects.Keys) + "\n");
            async Task WriteRequestsAsync()
            {
                await process.StandardInput.BaseStream.WriteAsync(requested);
                process.StandardInput.Close();
            }
            var writeTask = WriteRequestsAsync();
            if (await Task.WhenAny(readTask, writeTask) == readTask)
                await readTask;
            await writeTask;
            await readTask;
            await process.WaitForExitAsync();
            var error = await stderr;
            if (process.ExitCode != 0 || !string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Git could not read the complete local Proto object batch: {error.Trim()}");
            return verifiedProtoObjects;
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
            try { await process.WaitForExitAsync(); } catch { }
            throw;
        }
    }

    private static async Task ReadAndVerifyObjectBatchAsync(Stream output, IReadOnlyDictionary<string, string> objects,
        IReadOnlySet<string> retain, Dictionary<string, byte[]> retainedObjects)
    {
        foreach (var (requestedOid, expectedType) in objects)
        {
            var line = await ReadAsciiLineAsync(output, 256)
                ?? throw new InvalidOperationException($"Git object batch ended before object {requestedOid} was returned.");
            var headerFields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (headerFields.Length != 3 || !string.Equals(headerFields[0], requestedOid, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(headerFields[1], expectedType, StringComparison.Ordinal) ||
                !long.TryParse(headerFields[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var size) || size < 0)
                throw new InvalidOperationException($"Git returned an invalid or missing object header for {requestedOid}: '{line}'.");

            var algorithm = requestedOid.Length switch
            {
                40 => HashAlgorithmName.SHA1,
                64 => HashAlgorithmName.SHA256,
                _ => throw new InvalidOperationException($"Object ID '{requestedOid}' has an unsupported format.")
            };
            using var hash = IncrementalHash.CreateHash(algorithm);
            hash.AppendData(Encoding.ASCII.GetBytes($"{expectedType} {size}\0"));
            MemoryStream? capture = null;
            if (retain.Contains(requestedOid))
            {
                if (size > int.MaxValue) throw new InvalidOperationException($"Proto object {requestedOid} is too large to verify as an input file.");
                capture = new MemoryStream(checked((int)size));
            }

            var buffer = new byte[81920];
            long remaining = size;
            while (remaining > 0)
            {
                var count = (int)Math.Min(buffer.Length, remaining);
                var read = await output.ReadAsync(buffer.AsMemory(0, count));
                if (read == 0) throw new InvalidOperationException($"Git truncated object {requestedOid} in its batch output.");
                hash.AppendData(buffer.AsSpan(0, read));
                if (capture is not null) await capture.WriteAsync(buffer.AsMemory(0, read));
                remaining -= read;
            }
            if (await ReadBatchByteAsync(output) != '\n')
                throw new InvalidOperationException($"Git returned an invalid terminator for object {requestedOid}.");
            var actualOid = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (!string.Equals(actualOid, requestedOid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Object {requestedOid} failed its content hash check (actual {actualOid}).");
            if (capture is not null)
            {
                retainedObjects[requestedOid] = capture.ToArray();
                capture.Dispose();
            }
        }
        if (await ReadBatchByteAsync(output) != -1)
            throw new InvalidOperationException("Git returned unexpected trailing data after the local object batch.");
    }

    private static async Task<string?> ReadAsciiLineAsync(Stream stream, int maximumLength)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var value = await ReadBatchByteAsync(stream);
            if (value == -1) return bytes.Count == 0 ? null : throw new InvalidOperationException("Git returned a truncated object header.");
            if (value == '\n') return Encoding.ASCII.GetString(bytes.ToArray());
            if (bytes.Count >= maximumLength) throw new InvalidOperationException("Git returned an oversized object header.");
            bytes.Add((byte)value);
        }
    }

    private static async Task<int> ReadBatchByteAsync(Stream stream)
    {
        var value = new byte[1];
        var read = await stream.ReadAsync(value.AsMemory());
        return read == 0 ? -1 : value[0];
    }

    private static (string Include, string Root, string[] Excludes, string[] AdditionalImportDirs)[] VerifyProtoProjectConfiguration(string root)
    {
        var projectPath = Path.Combine(root, "SslProto", "SslProto.csproj");
        if (!File.Exists(projectPath)) throw new InvalidOperationException("SslProto/SslProto.csproj is missing; Proto inputs cannot be verified.");
        var project = LoadBuildXml(projectPath);
        VerifyReviewedProtoDeclarations(projectPath, project, isProject: true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var propertiesPath = ResolveDirectoryBuildPropsPath(root, projectDirectory);
        var properties = propertiesPath is not null && File.Exists(propertiesPath) ? LoadBuildXml(propertiesPath) : null;
        if (properties is not null && propertiesPath is not null)
            VerifyReviewedProtoDeclarations(propertiesPath, properties!, isProject: false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var buildSettings = ReadProtoBuildSettings(projectPath, project, propertiesPath, properties);
        if (buildSettings.ContainsKey("Protobuf_StandardImportsPath"))
            throw new InvalidOperationException("Protobuf_StandardImportsPath is explicitly changed by the project, loaded Directory.Build.props, or process environment; the standard import set is indeterminate and no Proto files were changed.");
        if (!buildSettings.TryGetValue("ImportDirectoryBuildTargets", out var importTargets) || !string.Equals(importTargets, "false", StringComparison.OrdinalIgnoreCase))
        {
            var targetsPath = ResolveDirectoryBuildTargetsPath(root, projectDirectory, buildSettings);
            if (targetsPath is not null && File.Exists(targetsPath))
            {
                VerifyReviewedProtoDeclarations(targetsPath, LoadBuildXml(targetsPath), isProject: false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                if (ReadProtoBuildSettings(targetsPath, LoadBuildXml(targetsPath), null, null, includeSelectors: false)
                    .ContainsKey("Protobuf_StandardImportsPath"))
                    throw new InvalidOperationException($"Loaded build file '{targetsPath}' changes Protobuf_StandardImportsPath; the standard import set is indeterminate and no Proto files were changed.");
            }
        }
        var items = project.Descendants().Where(IsProtobufItem).ToArray();
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
            var protoCompile = item.Attribute("ProtoCompile")?.Value ?? item.Elements().FirstOrDefault(element => element.Name.LocalName == "ProtoCompile")?.Value;
            if (protoCompile is not null && !string.Equals(protoCompile.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"SslProto.csproj Protobuf item #{i + 1} sets ProtoCompile to '{protoCompile.Trim()}'; the effective compile input is not the reviewed default, so no Proto files were changed.");
            if (item.Attribute("Include")?.Value.Trim() != rule.Include || item.Attribute("ProtoRoot")?.Value.Trim() != rule.Root)
                throw new InvalidOperationException($"SslProto.csproj Protobuf include or ProtoRoot #{i + 1} is ambiguous or unexpected.");
            var actualExcludes = SplitMsBuildList(item.Attribute("Exclude")?.Value);
            if (!actualExcludes.SequenceEqual(rule.Excludes, StringComparer.Ordinal))
                throw new InvalidOperationException($"SslProto.csproj Protobuf exclude set #{i + 1} differs from the reviewed contract: {string.Join(", ", actualExcludes)}");
            var actualAdditional = SplitMsBuildList(item.Attribute("AdditionalImportDirs")?.Value);
            if (!actualAdditional.SequenceEqual(rule.Additional, StringComparer.Ordinal))
                throw new InvalidOperationException($"SslProto.csproj AdditionalImportDirs #{i + 1} differs from the reviewed contract: {string.Join(", ", actualAdditional)}");
        }

        return expected;
    }

    private static Task VerifyProtoInputsAsync(string root,
        IReadOnlyDictionary<string, Dictionary<string, byte[]>> contentsBySubmodule)
    {
        var expected = VerifyProtoProjectConfiguration(root);
        var filesBySubmodule = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
        foreach (var submodule in RequiredProtoSubmodules)
        {
            if (!contentsBySubmodule.TryGetValue(submodule.Name, out var files) || files.Count == 0)
                throw LocalProtoAssetFailure(submodule, "verified Proto objects are unavailable");
            filesBySubmodule.Add(submodule.Name, files);
        }

        var gameFiles = filesBySubmodule["ssl-game-controller"];
        var simulationFiles = filesBySubmodule["ssl-simulation-protocol"];
        var excludedPaths = expected[1].Excludes.Select(path => path["src/external/ssl-simulation-protocol/".Length..]).ToHashSet(StringComparer.Ordinal);
        foreach (var excluded in excludedPaths)
            if (!simulationFiles.ContainsKey(excluded)) throw LocalProtoAssetFailure(RequiredProtoSubmodules[1], $"pinned tree is missing expected Proto input '{excluded}'");

        var gameInputs = gameFiles.Keys.ToHashSet(StringComparer.Ordinal);
        var simulationInputs = simulationFiles.Keys.Where(path => !excludedPaths.Contains(path)).ToHashSet(StringComparer.Ordinal);
        var protoContents = new List<(string Module, string Path, string Content)>();
        foreach (var file in gameInputs.Select(path => ("ssl-game-controller", path, gameFiles[path]))
            .Concat(simulationInputs.Select(path => ("ssl-simulation-protocol", path, simulationFiles[path]))))
        {
            string content;
            try { content = new UTF8Encoding(false, true).GetString(file.Item3); }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidOperationException($"Pinned Proto input '{file.Item1}/{file.Item2}' is not valid UTF-8.", ex);
            }
            protoContents.Add((file.Item1, file.Item2, content));
        }

        var gameImportDirs = new[] { "proto" };
        var simulationImportDirs = new[] { "proto", "proto/state", "proto/vision" };
        foreach (var importDir in new[] { "proto/state", "proto/vision" })
            if (!gameFiles.Keys.Any(path => path.StartsWith(importDir + "/", StringComparison.Ordinal)))
                throw LocalProtoAssetFailure(RequiredProtoSubmodules[0], $"pinned tree has no Proto input in required import directory 'ssl-game-controller/{importDir}'");

        foreach (var spec in RequiredProtoSubmodules)
        {
            var worktree = Path.Combine(root, spec.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(Path.Combine(worktree, ".git")) && !Directory.Exists(Path.Combine(worktree, ".git"))) continue;
            var included = spec.Name == "ssl-game-controller" ? gameInputs : simulationInputs;
            var pinnedFiles = spec.Name == "ssl-game-controller" ? gameFiles : simulationFiles;
            VerifyMaterializedProtoFiles(root, spec, included, pinnedFiles);
            if (spec.Name == "ssl-game-controller")
                foreach (var importDir in new[] { "proto/state", "proto/vision" })
                    if (!Directory.Exists(Path.Combine(worktree, importDir.Replace('/', Path.DirectorySeparatorChar))))
                        throw LocalProtoAssetFailure(spec, $"prepared checkout is missing required import directory '{importDir}'");
        }

        foreach (var input in protoContents)
        {
            var importDirs = input.Module == "ssl-game-controller" ? gameImportDirs : simulationImportDirs;
            foreach (Match match in Regex.Matches(input.Content, @"(?m)^\s*import\s+(?:(?:public|weak)\s+)?""([^""]+)""\s*;"))
            {
                var import = match.Groups[1].Value;
                if (Path.IsPathRooted(import) || import.Split('/').Any(part => part == ".."))
                    throw new InvalidOperationException($"Proto import '{import}' in '{input.Module}/{input.Path}' escapes its import roots.");
                if (import.StartsWith("google/protobuf/", StringComparison.Ordinal)) continue;
                var found = importDirs.Any(directory =>
                    input.Module == "ssl-game-controller"
                        ? gameFiles.ContainsKey($"{directory}/{import}")
                        : simulationFiles.ContainsKey($"{directory}/{import}") || gameFiles.ContainsKey($"{directory}/{import}"));
                if (!found)
                {
                    var module = RequiredProtoSubmodules.Single(item => item.Name == input.Module);
                    throw LocalProtoAssetFailure(module, $"pinned tree has no required import '{import}' referenced by '{input.Path}'");
                }
            }
        }

        if (gameInputs.Count == 0 || simulationInputs.Count == 0)
            throw new InvalidOperationException("The reviewed Protobuf include/exclude rules resolved to an empty input set.");
        return Task.CompletedTask;
    }

    /// <summary>実効 Proto 入力に関係する宣言だけを静的に確認し、未確定なら配置前に停止します。</summary>
    private static void VerifyReviewedProtoDeclarations(string path, XDocument document, bool isProject, HashSet<string> visited)
    {
        var fullPath = Path.GetFullPath(path);
        if (!visited.Add(fullPath)) return;
        var root = document.Root;
        if (root is null) throw new InvalidOperationException($"Build file '{path}' has no XML root; Proto inputs are indeterminate and no Proto files were changed.");
        if (isProject && root.Attribute("Condition") is not null)
            throw new InvalidOperationException($"SslProto.csproj has a project-level Condition; Proto inputs are indeterminate and no Proto files were changed.");

        foreach (var item in document.Descendants().Where(IsProtobufItem))
        {
            if (item.Attribute("Update") is not null || item.Attribute("Remove") is not null)
                throw new InvalidOperationException($"Build file '{path}' updates or removes Protobuf items; the effective Proto inputs are indeterminate and no Proto files were changed.");
            if (!isProject)
                throw new InvalidOperationException($"Loaded build file '{path}' declares Protobuf items; the effective Proto inputs are not the reviewed project declarations and no Proto files were changed.");
            for (var ancestor = item; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor.Attribute("Condition") is not null || ancestor.Name.LocalName is "Choose" or "When" or "Target")
                    throw new InvalidOperationException("SslProto.csproj gates a Protobuf declaration with a condition or target; effective Proto inputs are indeterminate and no Proto files were changed.");
            }
            var protoCompile = item.Attribute("ProtoCompile")?.Value ?? item.Elements().FirstOrDefault(element => element.Name.LocalName == "ProtoCompile")?.Value;
            if (protoCompile is not null && !string.Equals(protoCompile.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Build file '{path}' changes ProtoCompile metadata to '{protoCompile.Trim()}'; effective Proto inputs are indeterminate and no Proto files were changed.");
        }

        foreach (var declaration in document.Descendants().Where(element => element.Name.LocalName is "EnableDefaultProtobufItems" or "ProtoCompile"))
        {
            if (declaration.Ancestors().Any(IsProtobufItem)) continue;
            var value = declaration.Value.Trim();
            if (declaration.Name.LocalName == "ProtoCompile" && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) continue;
            throw new InvalidOperationException($"Build file '{path}' declares {declaration.Name.LocalName}, which can alter effective Proto inputs; no Proto files were changed.");
        }

        foreach (var output in document.Descendants().Where(element => element.Name.LocalName == "Output" &&
                     string.Equals(element.Attribute("ItemName")?.Value, "Protobuf", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Build file '{path}' creates Protobuf items from a target; effective Proto inputs are indeterminate and no Proto files were changed.");

        foreach (var import in document.Descendants().Where(element => element.Name.LocalName == "Import"))
        {
            var importedPath = ResolveStaticBuildImport(fullPath, import);
            if (!visited.Contains(importedPath))
                VerifyReviewedProtoDeclarations(importedPath, LoadBuildXml(importedPath), isProject: false, visited);
        }
    }

    private static bool IsProtobufItem(XElement element) =>
        string.Equals(element.Name.LocalName, "Protobuf", StringComparison.OrdinalIgnoreCase);

    /// <summary>評価を要しない単一の import パスだけを受け入れます。</summary>
    private static string ResolveStaticBuildImport(string importingPath, XElement import)
    {
        var expression = import.Attribute("Project")?.Value.Trim();
        if (string.IsNullOrEmpty(expression) || expression.IndexOfAny(['$', '@', '*', '?', '%', ';']) >= 0 ||
            import.Attribute("Sdk") is not null)
            throw new InvalidOperationException($"Build file '{importingPath}' has an import whose target cannot be resolved statically; effective Proto inputs are indeterminate and no Proto files were changed.");
        var importedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(importingPath)!,
            expression.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(importedPath))
            throw new InvalidOperationException($"Build file '{importingPath}' imports unavailable file '{expression}'; effective Proto inputs are indeterminate and no Proto files were changed.");
        return importedPath;
    }

    /// <summary>この preflight が扱う MSBuild 設定だけを読み、未知の条件式は書込み前に拒否します。</summary>
    private static Dictionary<string, string> ReadProtoBuildSettings(string projectPath, XDocument project,
        string? propertiesPath, XDocument? properties, bool includeSelectors = true)
    {
        var names = includeSelectors
            ? new HashSet<string>(["DirectoryBuildTargetsPath", "ImportDirectoryBuildTargets", "Protobuf_StandardImportsPath"], StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(["Protobuf_StandardImportsPath"], StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var environmentValue = GetEnvironmentProperty(name);
            if (environmentValue is not null && !(name == "ImportDirectoryBuildTargets" && environmentValue.Length == 0))
                AddBuildSetting(values, name, environmentValue);
        }
        var importedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (propertiesPath is not null && properties is not null)
            ReadBuildSettingsFromDocument(propertiesPath, properties, names, values, importedPaths);
        ReadBuildSettingsFromDocument(projectPath, project, names, values, importedPaths);
        return values;
    }

    private static void ReadBuildSettingsFromDocument(string path, XDocument document, HashSet<string> names,
        Dictionary<string, string> values, HashSet<string> importedPaths, string? conditionalImportFrom = null)
    {
        var fullPath = Path.GetFullPath(path);
        // MSBuild は同一パスの後続 import を評価しません。
        if (!importedPaths.Add(fullPath)) return;

        void Visit(XElement element)
        {
            if (element.Name.LocalName == "Import")
            {
                var importedPath = ResolveStaticBuildImport(fullPath, element);
                if (importedPaths.Contains(importedPath)) return;
                var imported = LoadBuildXml(importedPath);
                if (conditionalImportFrom is null && HasBuildCondition(element))
                {
                    // 読み込みを仮定しても、後続の確定した import を省略しないようにします。
                    var possibleImports = new HashSet<string>(importedPaths, importedPaths.Comparer);
                    ReadBuildSettingsFromDocument(importedPath, imported, names, values, possibleImports, fullPath);
                }
                else
                    ReadBuildSettingsFromDocument(importedPath, imported, names, values, importedPaths, conditionalImportFrom);
                return;
            }

            if (element.Name.LocalName == "PropertyGroup")
            {
                foreach (var property in element.Elements().Where(child => names.Contains(child.Name.LocalName)))
                {
                    if ((string.Equals(property.Name.LocalName, "ImportDirectoryBuildTargets", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(property.Name.LocalName, "DirectoryBuildTargetsPath", StringComparison.OrdinalIgnoreCase)) &&
                        element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "Target"))
                        continue;
                    // 同じ値でも、先に読み込むと後続の import が無視され、最終値が変わることがあります。
                    if (conditionalImportFrom is not null)
                        throw new InvalidOperationException($"Build file '{conditionalImportFrom}' conditionally imports Proto-related settings: '{path}' sets '{property.Name.LocalName}'. The effective values are indeterminate and no Proto files were changed.");
                    if (HasBuildCondition(property))
                        throw new InvalidOperationException($"Build file '{path}' conditionally sets '{property.Name.LocalName}'; its effective Proto configuration is indeterminate and no Proto files were changed.");
                    var value = property.Value.Trim();
                    AddBuildSetting(values, property.Name.LocalName, value);
                }
            }

            foreach (var child in element.Elements()) Visit(child);
        }

        if (document.Root is null)
            throw new InvalidOperationException($"Build file '{path}' has no XML root; Proto inputs are indeterminate and no Proto files were changed.");
        foreach (var child in document.Root.Elements()) Visit(child);
    }

    private static void AddBuildSetting(Dictionary<string, string> values, string name, string value)
    {
        // MSBuild evaluates properties in order; an unconditional later assignment, including an empty value, replaces the earlier value.
        values[name] = value;
    }

    private static bool HasBuildCondition(XElement element) =>
        element.AncestorsAndSelf().Any(candidate => candidate.Attribute("Condition") is not null ||
            candidate.Name.LocalName is "Choose" or "When" or "Otherwise");

    private static string? GetEnvironmentProperty(string name)
    {
        var matches = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key is string key && string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Value?.ToString() ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"Process environment has conflicting case variants for '{name}'; Proto inputs are indeterminate and no Proto files were changed.");
        return matches.FirstOrDefault();
    }

    private static string? ResolveDirectoryBuildPropsPath(string root, string projectDirectory)
    {
        var importProps = GetEnvironmentProperty("ImportDirectoryBuildProps");
        if (string.Equals(importProps, "false", StringComparison.OrdinalIgnoreCase)) return null;
        if (importProps is not null && !string.Equals(importProps, "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ImportDirectoryBuildProps has a non-boolean environment value; effective Proto inputs are indeterminate and no Proto files were changed.");
        var overridePath = GetEnvironmentProperty("DirectoryBuildPropsPath");
        if (string.IsNullOrEmpty(overridePath)) return FindNearestBuildFile(projectDirectory, "Directory.Build.props");
        if (overridePath.Contains('$') || overridePath.Contains('@') || overridePath.Contains('*') || overridePath.Contains('?') || !Path.IsPathFullyQualified(overridePath))
            throw new InvalidOperationException("DirectoryBuildPropsPath is not a fixed absolute path; loaded Proto-related settings are indeterminate and no Proto files were changed.");
        var fixedPath = EnsureBuildFilePath(overridePath);
        EnsurePathWithinRoot(root, fixedPath);
        return fixedPath;
    }

    private static string? ResolveDirectoryBuildTargetsPath(string root, string projectDirectory, IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue("ImportDirectoryBuildTargets", out var importTargets) &&
            !string.IsNullOrEmpty(importTargets) &&
            !string.Equals(importTargets, "true", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(importTargets, "false", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ImportDirectoryBuildTargets is not a literal true or false value; effective Proto inputs are indeterminate and no Proto files were changed.");
        if (settings.TryGetValue("DirectoryBuildTargetsPath", out var overridePath) && !string.IsNullOrEmpty(overridePath))
        {
            if (overridePath.Contains('$') || overridePath.Contains('@') || overridePath.Contains('*') || overridePath.Contains('?') || !Path.IsPathFullyQualified(overridePath))
                throw new InvalidOperationException("DirectoryBuildTargetsPath is not a fixed absolute path; effective Proto inputs are indeterminate and no Proto files were changed.");
            var fixedPath = EnsureBuildFilePath(overridePath);
            EnsurePathWithinRoot(root, fixedPath);
            return fixedPath;
        }
        return FindNearestBuildFile(projectDirectory, "Directory.Build.targets");
    }

    private static string EnsureBuildFilePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        try
        {
            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.Directory) != 0 || (attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Build file '{fullPath}' is not a regular file; Proto inputs are indeterminate and no Proto files were changed.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        return fullPath;
    }

    private static void EnsurePathWithinRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException($"Configured build file '{path}' is outside the repository and cannot be checked safely; no Proto files were changed.");
    }

    private static XDocument LoadBuildXml(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    /// <summary>MSBuild が読む最も近いディレクトリ設定だけを探します。</summary>
    private static string? FindNearestBuildFile(string startDirectory, string name)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, name);
            try
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) != 0 || (attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException($"Loaded build file candidate '{path}' is not a regular file; Proto inputs are indeterminate and no Proto files were changed.");
                return path;
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (InvalidOperationException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Loaded build file candidate '{path}' could not be inspected; Proto inputs are indeterminate and no Proto files were changed.", ex);
            }
        }
        return null;
    }

    private static void VerifyMaterializedProtoFiles(string root, ProtoSubmodule spec, IEnumerable<string> inputs,
        IReadOnlyDictionary<string, byte[]>? pinnedFiles = null,
        Func<string, FileAttributes>? inspectAttributes = null, Func<string, byte[]>? readProtoFile = null)
    {
        var worktree = Path.Combine(root, spec.Path.Replace('/', Path.DirectorySeparatorChar));
        foreach (var proto in inputs)
        {
            var path = Path.Combine(worktree, proto.Replace('/', Path.DirectorySeparatorChar));
            EnsureNoReparsePoints(root, path, inspectAttributes);
            if (!File.Exists(path))
                throw LocalProtoAssetFailure(spec, $"prepared checkout is missing required input '{proto}'");
            if (pinnedFiles is null) continue;
            if (!pinnedFiles.TryGetValue(proto, out var pinned))
                throw LocalProtoAssetFailure(spec, $"pinned Proto input '{spec.Path}/{proto}' is unavailable");
            byte[] actual;
            try { actual = (readProtoFile ?? File.ReadAllBytes)(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw LocalProtoAssetFailure(spec, $"prepared Proto input '{proto}' could not be read: {ex.Message}", ex);
            }
            if (!ProtoBytesMatchCheckout(actual, pinned))
                throw LocalProtoAssetFailure(spec, $"prepared input '{spec.Path}/{proto}' differs from its pinned blob; it was left untouched");
        }
    }

    private static bool ProtoBytesMatchCheckout(byte[] actual, byte[] pinned)
    {
        if (actual.AsSpan().SequenceEqual(pinned)) return true;
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            static string NormalizeLines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);
            return string.Equals(NormalizeLines(utf8.GetString(actual)), NormalizeLines(utf8.GetString(pinned)), StringComparison.Ordinal);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
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

    private static async Task<string> RunGitStrictAsync(string workingDirectory, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false, true),
            StandardErrorEncoding = new UTF8Encoding(false, true),
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var setting in environment)
        {
            if (setting.Value is null) info.Environment.Remove(setting.Key);
            else info.Environment[setting.Key] = setting.Value;
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Git attribute-tree inspection.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0 || stderr.Length != 0)
                throw new InvalidOperationException($"Git attribute-tree inspection failed ({string.Join(" ", arguments)}): {stderr.Trim()} {stdout.Trim()}".Trim());
            return stdout;
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
            try { await process.WaitForExitAsync(); } catch { }
            throw;
        }
    }

    private static void EnsureNoReparsePoints(string root, string path, Func<string, FileAttributes>? inspectAttributes = null)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException($"Proto path '{path}' is outside the inspected root '{root}'.");
        _ = GetInspectedPathAttributes(fullPath, "Proto path", inspectAttributes);
    }

    private static void EnsureNoReparsePointAncestors(string path, string description,
        Func<string, FileAttributes>? inspectAttributes = null)
    {
        _ = GetInspectedPathAttributes(path, description, inspectAttributes);
    }

    /// <summary>パスとすべての祖先を検査し、検査できた不在と不正・読取不能な状態を区別します。</summary>
    private static FileAttributes? GetInspectedPathAttributes(string path, string description,
        Func<string, FileAttributes>? inspectAttributes = null)
    {
        var fullPath = Path.GetFullPath(path);
        var chain = new Stack<string>();
        string? current = fullPath;
        while (current is not null)
        {
            chain.Push(current);
            var parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) break;
            current = parent;
        }
        FileAttributes? result = null;
        while (chain.Count > 0)
        {
            var entry = chain.Pop();
            FileAttributes attributes;
            try { attributes = (inspectAttributes ?? File.GetAttributes)(entry); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"The {description} could not be inspected at '{entry}'. No Proto files were changed.", ex);
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"The {description} contains a symlink or reparse point at '{entry}'. No Proto files were changed.");
            if (chain.Count > 0 && (attributes & FileAttributes.Directory) == 0)
                throw new InvalidOperationException($"The {description} has a non-directory ancestor at '{entry}'. No Proto files were changed.");
            result = attributes;
        }
        return result;
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
    internal sealed record Options(bool Persistent, bool StopPersistent, bool HostRuntime, string StackId, bool PlayMatch = false, int HalfSeconds = 45, bool PrepareMissing = false)
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
            var playMatch = false;
            var prepareMissing = false;
            int? halfSeconds = null;
            string? id = null;
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--persistent": persistent = true; break;
                    case "--stop-persistent": stop = true; break;
                    case "--host-runtime": explicitHostRuntime = true; break;
                    case "--helper-free-compose": helperFreeCompose = true; break;
                    case "--play-match": playMatch = true; break;
                    case "--prepare-missing": prepareMissing = true; break;
                    case "--half-seconds":
                        if (halfSeconds is not null || ++i >= args.Length ||
                            !int.TryParse(args[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds) || seconds is < 1 or > 148)
                            throw new ArgumentException("--half-seconds requires one integer from 1 to 148.");
                        halfSeconds = seconds;
                        break;
                    case "--stack-id": if (++i >= args.Length) throw new ArgumentException("--stack-id requires a value."); id = args[i]; break;
                    case "--help":
                    case "-h":
                        Console.WriteLine("""
                            Usage: dotnet run --file scripts/start-duck-simulation.cs [-- OPTIONS]
                              No options, --host-runtime     Windows native RuntimeHost with development UDP transfer
                              --helper-free-compose          Run RuntimeHost inside Docker for this session
                              --play-match                   Play and verify one Windows RuntimeHost match
                              --prepare-missing              Prepare missing pinned match inputs (--play-match only)
                              --half-seconds N               Normal half seconds (1-148, default 45; --play-match only)
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
            if (prepareMissing && !playMatch) throw new ArgumentException("--prepare-missing requires --play-match.");
            if (halfSeconds is not null && !playMatch) throw new ArgumentException("--half-seconds requires --play-match.");
            if (playMatch && !hostRuntime)
                throw new ArgumentException("--play-match requires the Windows native RuntimeHost session.");
            return new Options(persistent, stop, hostRuntime, id ?? "duck-local-development", playMatch, halfSeconds ?? 45, prepareMissing);
        }
    }
}
