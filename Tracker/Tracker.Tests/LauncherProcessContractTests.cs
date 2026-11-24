using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Tracker.Tests;

/// <summary>起動元の作業木選択、ログ確保、AppHost の終了値とログ回収を検証します。</summary>
public sealed class LauncherProcessContractTests
{
    /// <summary>両方の固定版が準備済みなら、ローカル検査が index と既存 checkout を変えないことを確認します。</summary>
    [Fact]
    public async Task PreparedProtoPreflightIsReadOnly()
    {
        var root = FindDuckRepositoryRoot();
        var headBefore = await GitAsync(root, ["rev-parse", "HEAD"]);
        var indexBefore = await GitAsync(root, "ls-files", "--stage", "--", "SslProto/src/external/ssl-game-controller", "SslProto/src/external/ssl-simulation-protocol");
        var submoduleBefore = await GitAsync(root, "submodule", "status", "--", "SslProto/src/external/ssl-game-controller", "SslProto/src/external/ssl-simulation-protocol");

        await Launcher.PrepareAndValidateProtoSubmodulesAsync(root);

        Assert.Equal(headBefore, await GitAsync(root, ["rev-parse", "HEAD"]));
        Assert.Equal(indexBefore, await GitAsync(root, "ls-files", "--stage", "--", "SslProto/src/external/ssl-game-controller", "SslProto/src/external/ssl-simulation-protocol"));
        Assert.Equal(submoduleBefore, await GitAsync(root, "submodule", "status", "--", "SslProto/src/external/ssl-game-controller", "SslProto/src/external/ssl-simulation-protocol"));
    }

    /// <summary>通常起動の Git 制約が個別 file.allow=always より優先し、外部照会を必要としないことを確認します。</summary>
    [Fact]
    public async Task RestrictedGitEnvironmentDeniesFileTransportDespiteProtocolOverride()
    {
        using var directory = new TemporaryDirectory();
        var remote = Path.Combine(directory.Path, "bare.git");
        await GitAsync(directory.Path, "init", "--quiet", "--bare", remote);
        var environment = Launcher.CreateRestrictedGitEnvironment();
        Assert.Equal(string.Empty, environment["GIT_ALLOW_PROTOCOL"]);
        Assert.Equal("1", environment["GIT_NO_LAZY_FETCH"]);

        var result = await RunGitProcessAsync(directory.Path,
            ["-c", "protocol.file.allow=always", "ls-remote", new Uri(remote).AbsoluteUri],
            environment);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("transport 'file' not allowed", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>partial clone の promisor blob 欠落を取得せず検出し、欠落 object を明示することを確認します。</summary>
    [Fact]
    public async Task ObjectClosureRejectsMissingPromisorBlobWithoutFetching()
    {
        using var directory = new TemporaryDirectory();
        var repo = Path.Combine(directory.Path, "repo");
        Directory.CreateDirectory(repo);
        await GitAsync(repo, "init", "--quiet");
        await GitAsync(repo, "config", "user.name", "Duck test");
        await GitAsync(repo, "config", "user.email", "duck-test@example.invalid");
        Directory.CreateDirectory(Path.Combine(repo, "proto"));
        var blobPath = Path.Combine(repo, "proto", "missing.proto");
        await File.WriteAllTextAsync(blobPath, "syntax = \"proto3\";\n");
        var blob = (await GitAsync(repo, "hash-object", "-w", "proto/missing.proto")).Trim();
        await GitAsync(repo, "add", "proto/missing.proto");
        await GitAsync(repo, "commit", "--quiet", "-m", "fixture");
        var commit = (await GitAsync(repo, "rev-parse", "HEAD")).Trim();
        var gitDir = (await GitAsync(repo, "rev-parse", "--absolute-git-dir")).Trim();
        await GitAsync(repo, "config", "extensions.partialClone", "origin");
        await GitAsync(repo, "config", "remote.origin.promisor", "true");
        await GitAsync(repo, "config", "remote.origin.url", "https://127.0.0.1:1/duck-proto-preflight-missing-object");
        var looseBlob = Path.Combine(gitDir, "objects", blob[..2], blob[2..]);
        Assert.True(File.Exists(looseBlob));
        File.Delete(looseBlob);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.VerifyObjectClosureForTestsAsync(gitDir, "fixture", commit, Launcher.CreateRestrictedGitEnvironment()));

        Assert.Contains(blob, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(looseBlob));
    }

    [Fact]
    public async Task ObjectClosureRejectsCorruptBlobBeforeAnySubmodulePlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var gitDir = Path.Combine(fixture.Root, ".git", "modules", "SslProto", "src", "external", "ssl-simulation-protocol");
        var pinned = "1e719a8d410cc84847ec69104da44fed13738291";
        var blob = (await GitAsync(fixture.Root, "--git-dir", gitDir, "rev-parse", $"{pinned}:proto/ssl_gc_common.proto")).Trim();
        var originalPayload = await GitAsync(fixture.Root, "--git-dir", gitDir, "cat-file", "blob", blob);
        var payload = Encoding.UTF8.GetBytes(originalPayload);
        var wrongPayload = payload.ToArray();
        wrongPayload[0] ^= 1;
        var objectFile = Path.Combine(gitDir, "objects", blob[..2], blob[2..]);
        Directory.CreateDirectory(Path.GetDirectoryName(objectFile)!);
        if (File.Exists(objectFile) && !OperatingSystem.IsWindows()) File.SetUnixFileMode(objectFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var header = Encoding.ASCII.GetBytes($"blob {payload.Length}\0");
        using (var output = File.Create(objectFile))
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
        {
            await zlib.WriteAsync(header);
            await zlib.WriteAsync(wrongPayload);
        }

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains(blob, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hash check", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
    }

    [Fact]
    public async Task MissingMaterializedProtoInputIsReportedByReadOnlyCheck()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        await GitAsync(fixture.Root, "submodule", "update", "--init", "--no-fetch", "--checkout", "--", "SslProto/src/external/ssl-simulation-protocol");
        var path = Path.Combine(fixture.Worktree("ssl-simulation-protocol"), "proto", "ssl_simulation_robot_control.proto");
        File.Delete(path);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("ssl_simulation_robot_control.proto", failure.Message, StringComparison.Ordinal);
        Assert.True(failure.Message.Contains("missing required input", StringComparison.Ordinal), failure.Message);
        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
    }

    [Fact]
    public async Task MaterializedProtoContentMustMatchPinnedBlobBeforeStatusCheck()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        await GitAsync(fixture.Root, "submodule", "update", "--init", "--no-fetch", "--checkout", "--", "SslProto/src/external/ssl-simulation-protocol");
        var path = Path.Combine(fixture.Worktree("ssl-simulation-protocol"), "proto", "ssl_simulation_robot_control.proto");
        await File.AppendAllTextAsync(path, "// local fixture mutation\n");
        var changedContent = await File.ReadAllTextAsync(path);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("does not match its pinned blob", failure.Message, StringComparison.Ordinal);
        Assert.Equal(changedContent, await File.ReadAllTextAsync(path));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
    }

    [Fact]
    public async Task DirtyPreparedCheckoutStopsBeforeProtoInputRead()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        await GitAsync(fixture.Root, "submodule", "update", "--init", "--no-fetch", "--checkout", "--", "SslProto/src/external/ssl-simulation-protocol");
        var worktree = fixture.Worktree("ssl-simulation-protocol");
        var missingProto = Path.Combine(worktree, "proto", "ssl_simulation_robot_control.proto");
        File.Delete(missingProto);
        var untracked = Path.Combine(worktree, "untracked-preflight-fixture.txt");
        await File.WriteAllTextAsync(untracked, "preserve this fixture\n");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("has tracked, untracked, or ignored changes", failure.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(untracked));
        Assert.False(File.Exists(missingProto));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
    }

    [Fact]
    public async Task MissingSavedIndexIsAllowedOnlyInsideAnInspectableGitDirectory()
    {
        using var directory = new TemporaryDirectory();
        var existingGitDirectory = Path.Combine(directory.Path, "modules", "existing.git");
        Directory.CreateDirectory(existingGitDirectory);
        await Launcher.VerifyModuleIndexForTestsAsync(existingGitDirectory, "fixture", "0000000000000000000000000000000000000000",
            Launcher.CreateRestrictedGitEnvironment(), required: false);

        var unavailableGitDirectory = Path.Combine(directory.Path, "modules", "vanished.git");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.VerifyModuleIndexForTestsAsync(unavailableGitDirectory, "fixture", "0000000000000000000000000000000000000000",
                Launcher.CreateRestrictedGitEnvironment(), required: false));

        Assert.Contains("Saved Git directory", failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(unavailableGitDirectory));
        Assert.False(File.Exists(Path.Combine(unavailableGitDirectory, "index")));
    }

    [Fact]
    public async Task MalformedSavedIndexPathStopsBeforeEitherPlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var indexPath = Path.Combine(fixture.Root, ".git", "modules", "SslProto", "src", "external", "ssl-simulation-protocol", "index");
        Directory.CreateDirectory(indexPath);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("reparse point or not a regular file", failure.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(indexPath));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
    }

    /// <summary>2つ目の事前検査が失敗した場合は、1つ目の未初期化 checkout も作成しないことを確認します。</summary>
    [Fact]
    public async Task AllSubmodulePrechecksFinishBeforeTheFirstPlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        fixture.RemoveObjectStore("ssl-simulation-protocol");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("ssl-simulation-protocol", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
    }

    /// <summary>固定 object store からだけ初期化し、既存の hooksPath にある hook を起動しないことを確認します。</summary>
    [Fact]
    public async Task LocalPreparationDoesNotRunCustomPostCheckoutHook()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var hooks = Path.Combine(fixture.Root, "custom-hooks");
        Directory.CreateDirectory(hooks);
        var marker = Path.Combine(fixture.Root, "hook-ran.txt");
        var hook = Path.Combine(hooks, "post-checkout");
        var markerLiteral = marker.Replace("'", "'\\''", StringComparison.Ordinal);
        await File.WriteAllTextAsync(hook, $"#!/bin/sh\nprintf 'ran\\n' >> '{markerLiteral}'\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await GitAsync(fixture.Root, "config", "core.hooksPath", hooks);
        await GitAsync(fixture.Root, "config", "protocol.https.allow", "always");

        await Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root);

        Assert.True(File.Exists(Path.Combine(fixture.Worktree("ssl-game-controller"), ".git")));
        Assert.True(File.Exists(Path.Combine(fixture.Worktree("ssl-simulation-protocol"), ".git")));
        Assert.True(Directory.EnumerateFiles(Path.Combine(fixture.Worktree("ssl-game-controller"), "proto"), "*.proto", SearchOption.AllDirectories).Any());
        Assert.True(Directory.EnumerateFiles(Path.Combine(fixture.Worktree("ssl-simulation-protocol"), "proto"), "*.proto", SearchOption.AllDirectories).Any());
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task WorktreeFilterAttributeStopsBeforeStatusOrPlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        await GitAsync(fixture.Root, "submodule", "update", "--init", "--no-fetch", "--checkout", "--", "SslProto/src/external/ssl-simulation-protocol");
        var filter = await CreateGlobalLfsFilterEnvironmentAsync(fixture.Root, string.Empty);
        await File.WriteAllTextAsync(Path.Combine(fixture.Worktree("ssl-simulation-protocol"), ".gitattributes"), "proto/*.proto filter=lfs\n");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root, filter.Environment));

        Assert.Contains("Checkout filter 'lfs'", failure.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(filter.MarkerPath));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.True(File.Exists(Path.Combine(fixture.Worktree("ssl-simulation-protocol"), ".gitattributes")));
    }

    [Fact]
    public async Task SavedSubmoduleIndexMismatchStopsBeforeOtherPlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        await GitAsync(fixture.Root, "submodule", "update", "--init", "--no-fetch", "--checkout", "--", "SslProto/src/external/ssl-game-controller");
        var worktree = fixture.Worktree("ssl-game-controller");
        await GitAsync(worktree, "update-index", "--add", "--cacheinfo", "100644,9b0765fe7a07a55124f6ee957d4559348462d227,unexpected.proto");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("Saved index", failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
        Assert.True(File.Exists(Path.Combine(worktree, ".git")));
    }

    [Fact]
    public async Task AlternateParentIndexStopsBeforeAnyPlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var environment = new Dictionary<string, string?>(Launcher.CreateRestrictedGitEnvironment(), StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_INDEX_FILE"] = Path.Combine(fixture.Root, "alternate-index"),
        };

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root, environment));

        Assert.Contains("alternate parent index", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "alternate-index")));
    }

    [Fact]
    public async Task OrdinaryProtoInputsDoNotRequireNuGetAssetsOrCacheInspection()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var assets = Path.Combine(fixture.Root, "SslProto", "obj", "project.assets.json");
        if (File.Exists(assets)) File.Delete(assets);
        var environment = new Dictionary<string, string?>(Launcher.CreateRestrictedGitEnvironment(), StringComparer.OrdinalIgnoreCase)
        {
            ["NUGET_PACKAGES"] = Path.Combine(fixture.Root, "no-nuget-cache"),
        };

        await Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root, environment);

        Assert.False(File.Exists(assets));
        Assert.False(Directory.Exists(environment["NUGET_PACKAGES"]));
        Assert.True(Directory.EnumerateFiles(Path.Combine(fixture.Worktree("ssl-game-controller"), "proto"), "*.proto", SearchOption.AllDirectories).Any());
    }

    /// <summary>有効な smudge filter は変更前に拒否し、helper と checkout のどちらも実行しないことを確認します。</summary>
    [Fact]
    public async Task ExternalFilterConfigurationStopsBeforeAnyPlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var filter = await CreateGlobalLfsFilterEnvironmentAsync(fixture.Root, "proto/ssl_gc_common.proto filter=lfs\n");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root, filter.Environment));

        Assert.Contains("ssl-simulation-protocol/proto/ssl_gc_common.proto", failure.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(filter.MarkerPath));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
    }

    [Fact]
    public async Task UnusedGlobalLfsFilterDefinitionsAllowLocalPreparationWithoutLaunchingHelpers()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var filter = await CreateGlobalLfsFilterEnvironmentAsync(fixture.Root, string.Empty);

        await Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root, filter.Environment);

        Assert.True(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.True(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
        Assert.False(File.Exists(filter.MarkerPath));
    }

    [Fact]
    public async Task UnreadableAttributeSourceStopsBeforeEitherSubmoduleIsPlaced()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = await SubmoduleFixture.CreateAsync();
        var filter = await CreateGlobalLfsFilterEnvironmentAsync(fixture.Root, "proto/ssl_gc_common.proto filter=lfs\n");
        File.SetUnixFileMode(filter.AttributesPath, UnixFileMode.UserWrite);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root, filter.Environment));

        Assert.Contains("could not be inspected safely", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(filter.MarkerPath));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
    }

    [Fact]
    public async Task CommittedFilterAttributeIsReadFromTheFixedTreeWithoutRunningItsHelper()
    {
        using var directory = new TemporaryDirectory();
        var repo = Path.Combine(directory.Path, "attribute-repo");
        Directory.CreateDirectory(repo);
        await GitAsync(repo, "init", "--quiet");
        await GitAsync(repo, "config", "user.name", "Duck test");
        await GitAsync(repo, "config", "user.email", "duck-test@example.invalid");
        Directory.CreateDirectory(Path.Combine(repo, "proto"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".gitattributes"), "*.proto filter=unset\n");
        await File.WriteAllTextAsync(Path.Combine(repo, "proto", "active.proto"), "syntax = \"proto3\";\n");
        await GitAsync(repo, "add", ".gitattributes", "proto/active.proto");
        await GitAsync(repo, "commit", "--quiet", "-m", "filter attributes");
        var gitDir = (await GitAsync(repo, "rev-parse", "--absolute-git-dir")).Trim();
        var commit = (await GitAsync(repo, "rev-parse", "HEAD")).Trim();
        var filter = await CreateGlobalLfsFilterEnvironmentAsync(repo, string.Empty);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.VerifyNoApplicableFiltersForTestsAsync(gitDir, commit, filter.Environment));

        Assert.Contains("cannot be distinguished", failure.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(filter.MarkerPath));
    }

    /// <summary>Git に登録されたカスタム submodule update helper を実行せず、配置前に停止することを確認します。</summary>
    [Fact]
    public async Task CustomSubmoduleUpdateHelperStopsBeforeAnyPlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var marker = Path.Combine(fixture.Root, "update-helper-ran.txt");
        await GitAsync(fixture.Root, "config", $"submodule.SslProto/src/external/ssl-game-controller.update", $"!touch {marker}");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("non-checkout", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(marker));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
    }

    /// <summary>index gitlink の不一致を検出した場合に submodule 配置を開始しないことを確認します。</summary>
    [Fact]
    public async Task IndexGitlinkMismatchStopsBeforeAnyPlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var otherCommit = (await GitAsync(fixture.Root, "rev-parse", "HEAD")).Trim();
        await GitAsync(fixture.Root, "update-index", "--cacheinfo", $"160000,{otherCommit},SslProto/src/external/ssl-game-controller");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("Index gitlink", failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
    }

    /// <summary>既存 submodule の untracked ファイルを検出し、保持したまま起動前に停止することを確認します。</summary>
    [Fact]
    public async Task ExistingSubmoduleChangesStopWithoutOverwritingFiles()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        await GitAsync(fixture.Root, "submodule", "update", "--init", "--no-fetch", "--checkout", "--", "SslProto/src/external/ssl-game-controller");
        var sentinel = Path.Combine(fixture.Worktree("ssl-game-controller"), "user-file.txt");
        await File.WriteAllTextAsync(sentinel, "keep me");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("tracked, untracked, or ignored changes", failure.Message, StringComparison.Ordinal);
        Assert.Equal("keep me", await File.ReadAllTextAsync(sentinel));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
    }

    /// <summary>MSBuild Proto glob が設計と異なる場合に、submodule 配置前に停止することを確認します。</summary>
    [Fact]
    public async Task UnexpectedProtoGlobStopsBeforeAnyPlacement()
    {
        using var fixture = await SubmoduleFixture.CreateAsync();
        var project = Path.Combine(fixture.Root, "SslProto", "SslProto.csproj");
        var contents = await File.ReadAllTextAsync(project);
        await File.WriteAllTextAsync(project, contents.Replace("ssl-game-controller/proto/**/*.proto", "ssl-game-controller/proto/**/*.txt", StringComparison.Ordinal));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.PrepareAndValidateProtoSubmodulesAsync(fixture.Root));

        Assert.Contains("include or ProtoRoot", failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Worktree("ssl-game-controller")));
        Assert.False(Directory.Exists(fixture.Worktree("ssl-simulation-protocol")));
    }

    /// <summary>起動時の既定 ID と明示 ID のどちらも、停止時に同じ対象を選べることを確認します。</summary>
    [Theory]
    [InlineData("duck-local-development")]
    [InlineData("test-stack-130")]
    public void PersistentStopAcceptsEitherExplicitId(string id)
    {
        var start = Launcher.Options.Parse(id == "duck-local-development"
            ? ["--persistent"] : ["--persistent", "--stack-id", id]);
        var stop = Launcher.Options.Parse(["--stop-persistent", "--stack-id", id]);
        Assert.True(start.Persistent);
        Assert.True(stop.StopPersistent);
        Assert.Equal(id, start.StackId);
        Assert.Equal(start.StackId, stop.StackId);
    }

    /// <summary>停止用の排他ロックを取得できない場合に Docker を呼び出さないことを確認します。</summary>
    [Fact]
    public async Task StopDoesNotCallDockerWhileTheLockIsHeld()
    {
        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "stack.lock");
        using var lease = new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var called = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Launcher.StopPersistentAsync("test-stack", lockPath, _ =>
        {
            called = true;
            return Task.FromResult((0, "", ""));
        }));

        Assert.False(called);
    }

    /// <summary>取得した全コンテナの所有者・stack・run が一致するまで削除しないことを確認します。</summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("stack")]
    [InlineData("run")]
    public async Task StopVerifiesAllLabelsBeforeDeleting(string mismatched)
    {
        using var directory = new TemporaryDirectory();
        var commands = new List<string>();
        var lockPath = Path.Combine(directory.Path, "stack.lock");

        Task<(int ExitCode, string Stdout, string Stderr)> Docker(string[] arguments)
        {
            commands.Add(arguments[0]);
            Assert.ThrowsAny<IOException>(() => File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            if (arguments[0] == "ps") return Task.FromResult((0, "owned\nforeign\n", ""));
            var labels = new Dictionary<string, string>
            {
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = "test-stack",
                ["duck.aspire.run"] = "test-stack",
                ["duck.aspire.resource"] = "duck",
            };
            if (arguments[^1] == "foreign") labels[$"duck.aspire.{mismatched}"] = "different";
            return Task.FromResult((0, System.Text.Json.JsonSerializer.Serialize(labels), ""));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.StopPersistentAsync("test-stack", lockPath, Docker));

        Assert.Equal(new[] { "ps", "inspect", "inspect" }, commands);
    }

    /// <summary>別の作業木がカレントでも、起動ファイルが配置された作業木を選ぶことを確認します。</summary>
    [Fact]
    public void RootComesFromTheLauncherFile()
    {
        using var directory = new TemporaryDirectory();
        var other = directory.CreateRepo("other");
        var desired = directory.CreateRepo("desired");

        Assert.NotEqual(other, Launcher.FindRepositoryRoot(Path.Combine(desired, "scripts", "start.cs")));
        Assert.Equal(desired, Launcher.FindRepositoryRoot(Path.Combine(desired, "scripts", "start.cs")));
        Assert.Throws<InvalidOperationException>(() => Launcher.FindRepositoryRoot("relative.cs"));
    }

    /// <summary>停止 ID の省略と末尾 LF を拒否し、別のスタックを誤って選ぶのを防ぐことを確認します。</summary>
    [Fact]
    public void StopRequiresAnExplicitIdWithoutTrailingNewline()
    {
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--stop-persistent"]));
        Assert.Throws<InvalidOperationException>(() => Launcher.Options.Parse(
            ["--stop-persistent", "--stack-id", "test-stack\n"]));
    }

    /// <summary>Docker の stack/run filter を通過する所有者混在を検出し、削除前に停止することを確認します。</summary>
    [Fact]
    public async Task StopRejectsMixedOwnersBeforeRemovingAnyContainer()
    {
        using var directory = new TemporaryDirectory();
        var commands = new List<string>();
        var lockPath = Path.Combine(directory.Path, "stack.lock");
        var stackId = $"test-stack-{Guid.NewGuid():N}";

        var resources = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["owned"] = new(StringComparer.Ordinal)
            {
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "duck",
            },
            ["foreign"] = new(StringComparer.Ordinal)
            {
                ["duck.aspire.owner"] = "another-owner",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "debug-host",
            },
        };

        Task<(int ExitCode, string Stdout, string Stderr)> Docker(string[] arguments)
        {
            commands.Add(arguments[0]);
            Assert.ThrowsAny<IOException>(() => File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            if (arguments[0] == "ps")
            {
                var filters = arguments.Select((value, index) => (value, index))
                    .Where(item => item.value == "--filter" && item.index + 1 < arguments.Length)
                    .Select(item => arguments[item.index + 1])
                    .Where(value => value.StartsWith("label=", StringComparison.Ordinal))
                    .Select(value => value["label=".Length..].Split('=', 2))
                    .Select(parts => (Key: parts[0], Value: parts[1]))
                    .ToArray();
                var matches = resources.Where(pair => filters.All(filter =>
                    pair.Value.TryGetValue(filter.Key, out var value) && value == filter.Value))
                    .Select(pair => pair.Key);
                return Task.FromResult((0, string.Join('\n', matches), ""));
            }
            if (arguments[0] == "inspect")
                return Task.FromResult((0, System.Text.Json.JsonSerializer.Serialize(resources[arguments[^1]]), ""));
            return Task.FromResult((0, "", ""));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.StopPersistentAsync(stackId, lockPath, Docker));

        Assert.Equal(new[] { "ps", "inspect", "inspect" }, commands);
    }

    /// <summary>マニフェストがある場合も、別 Compose project と別 role の同一 stack/run 異所有者を変更前に拒否します。</summary>
    [Fact]
    public async Task StopRejectsForeignOwnerAcrossComposeProjectsBeforeAnyMutation()
    {
        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "stack.lock");
        var stackId = $"test-stack-{Guid.NewGuid():N}";
        var projectHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{stackId}\0{stackId}"))).ToLowerInvariant()[..20];
        var project = $"duck-{projectHash}";
        var manifestPath = Path.Combine(Path.GetTempPath(), $"{project}.compose.yaml");
        var commands = new List<string>();
        var resources = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["compose-owned"] = new(StringComparer.Ordinal)
            {
                ["com.docker.compose.project"] = project,
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "compose-cm4-sim",
            },
            ["duck-owned"] = new(StringComparer.Ordinal)
            {
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "duck",
            },
            ["foreign-project"] = new(StringComparer.Ordinal)
            {
                ["com.docker.compose.project"] = "unrelated-compose-project",
                ["duck.aspire.owner"] = "another-owner",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "compose-other-role",
            },
        };
        await File.WriteAllTextAsync(manifestPath, "persistent manifest");
        try
        {
            Task<(int ExitCode, string Stdout, string Stderr)> Docker(string[] arguments)
            {
                commands.Add(arguments[0] == "network" ? "network ls" : arguments[0]);
                Assert.ThrowsAny<IOException>(() => File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                if (arguments[0] == "ps" || (arguments.Length > 1 && arguments[0] == "network" && arguments[1] == "ls"))
                {
                    var filters = arguments.Select((value, index) => (value, index))
                        .Where(item => item.value == "--filter" && item.index + 1 < arguments.Length)
                        .Select(item => arguments[item.index + 1])
                        .Where(value => value.StartsWith("label=", StringComparison.Ordinal))
                        .Select(value => value["label=".Length..].Split('=', 2))
                        .Select(parts => (Key: parts[0], Value: parts[1]))
                        .ToArray();
                    if (arguments[0] == "network")
                        return Task.FromResult((0, "", ""));
                    var matches = resources.Where(pair => filters.All(filter =>
                        pair.Value.TryGetValue(filter.Key, out var value) && value == filter.Value))
                        .Select(pair => pair.Key);
                    return Task.FromResult((0, string.Join('\n', matches), ""));
                }
                if (arguments[0] == "inspect")
                    return Task.FromResult((0, System.Text.Json.JsonSerializer.Serialize(resources[arguments[^1]]), ""));
                return Task.FromResult((0, "", ""));
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Launcher.StopPersistentAsync(stackId, lockPath, Docker));

            Assert.DoesNotContain("rm", commands);
            Assert.DoesNotContain("compose", commands);
            Assert.Equal(new[] { "ps", "inspect", "network ls", "ps", "inspect", "inspect", "inspect" }, commands);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>停止時の Compose down 失敗では再試行用マニフェストを保持し、成功した再試行後にだけ消すことを確認します。</summary>
    [Fact]
    public async Task StopKeepsManifestAfterDownFailureAndDeletesItAfterRetry()
    {
        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "stack.lock");
        var stackId = $"test-stack-{Guid.NewGuid():N}";
        var projectHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{stackId}\0{stackId}"))).ToLowerInvariant()[..20];
        var project = $"duck-{projectHash}";
        var manifestPath = Path.Combine(Path.GetTempPath(), $"{project}.compose.yaml");
        var resources = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["compose-owned"] = new(StringComparer.Ordinal)
            {
                ["com.docker.compose.project"] = project,
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "compose-cm4-sim",
            },
            ["duck-owned"] = new(StringComparer.Ordinal)
            {
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "duck",
            },
        };
        var failFirstDown = true;
        await File.WriteAllTextAsync(manifestPath, "persistent compose manifest");
        try
        {
            Task<(int ExitCode, string Stdout, string Stderr)> Docker(string[] arguments)
            {
                if (arguments[0] == "ps" || (arguments.Length > 1 && arguments[0] == "network" && arguments[1] == "ls"))
                {
                    var filters = arguments.Select((value, index) => (value, index))
                        .Where(item => item.value == "--filter" && item.index + 1 < arguments.Length)
                        .Select(item => arguments[item.index + 1])
                        .Where(value => value.StartsWith("label=", StringComparison.Ordinal))
                        .Select(value => value["label=".Length..].Split('=', 2))
                        .Select(parts => (Key: parts[0], Value: parts[1]))
                        .ToArray();
                    if (arguments[0] == "network")
                        return Task.FromResult((0, "", ""));
                    var matches = resources.Where(pair => filters.All(filter =>
                        pair.Value.TryGetValue(filter.Key, out var value) && value == filter.Value))
                        .Select(pair => pair.Key);
                    return Task.FromResult((0, string.Join('\n', matches), ""));
                }
                if (arguments[0] == "inspect")
                    return Task.FromResult((0, System.Text.Json.JsonSerializer.Serialize(resources[arguments[^1]]), ""));
                if (arguments[0] == "rm")
                {
                    resources.Remove(arguments[^1]);
                    return Task.FromResult((0, "", ""));
                }
                if (arguments[0] == "compose" && arguments.Contains("down", StringComparer.Ordinal))
                {
                    if (failFirstDown)
                    {
                        failFirstDown = false;
                        return Task.FromResult((17, "", "simulated Compose down failure"));
                    }
                    resources.Remove("compose-owned");
                    return Task.FromResult((0, "", ""));
                }
                return Task.FromResult((0, "", ""));
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Launcher.StopPersistentAsync(stackId, lockPath, Docker));
            Assert.True(File.Exists(manifestPath));

            await Launcher.StopPersistentAsync(stackId, lockPath, Docker);

            Assert.False(File.Exists(manifestPath));
            Assert.Empty(resources);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>新規一時起動の失敗は専用回収を行い、既存常駐スタックの失敗では全体回収を呼ばないことを確認します。</summary>
    [Fact]
    public async Task FailureCleanupIsLimitedToNewTemporaryLaunches()
    {
        using var directory = new TemporaryDirectory();
        var (exe, args) = Child("exit 23", "exit 23");
        var cleanupCalls = 0;
        Task Cleanup()
        {
            cleanupCalls++;
            return Task.CompletedTask;
        }

        var temporaryCleanup = Launcher.CreateFailureCleanup(persistent: false, Cleanup);
        Assert.NotNull(temporaryCleanup);
        Assert.Equal(23, await Launcher.RunLoggedAsync(
            exe, args, directory.Path, new Dictionary<string, string?>(),
            Path.Combine(directory.Path, "temporary.log"), temporaryCleanup));
        Assert.Equal(1, cleanupCalls);

        var persistentCleanup = Launcher.CreateFailureCleanup(persistent: true, Cleanup);
        Assert.Null(persistentCleanup);
        Assert.Equal(23, await Launcher.RunLoggedAsync(
            exe, args, directory.Path, new Dictionary<string, string?>(),
            Path.Combine(directory.Path, "persistent.log"), persistentCleanup));
        Assert.Equal(1, cleanupCalls);
    }

    /// <summary>ログを新規作成できない場合は、AppHost を開始せず既存記録も保持することを確認します。</summary>
    [Fact]
    public async Task AppHostStartsOnlyAfterLogIsOpened()
    {
        using var directory = new TemporaryDirectory();
        var marker = Path.Combine(directory.Path, "started");
        var (exe, args) = Child($"Set-Content -LiteralPath '{marker}' started", $"echo started > '{marker}'");
        var occupied = Path.Combine(directory.Path, "occupied.log");
        File.WriteAllText(occupied, "existing");

        await Assert.ThrowsAsync<IOException>(() => Launcher.RunLoggedAsync(
            exe, args, directory.Path, new Dictionary<string, string?>(), occupied));

        Assert.False(File.Exists(marker));
        Assert.Equal("existing", File.ReadAllText(occupied));
    }

    /// <summary>AppHost の終了値を保ち、終了前に標準出力と標準エラーをログへ回収することを確認します。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public async Task AppHostReturnsTheChildExitAfterDrainingBothPipes(int exitCode)
    {
        using var directory = new TemporaryDirectory();
        var (exe, args) = Child(
            $"Write-Output final-out; [Console]::Error.WriteLine('final-err'); exit {exitCode}",
            $"echo final-out; echo final-err >&2; exit {exitCode}");
        var logPath = Path.Combine(directory.Path, "output.log");
        var cleanupCalled = false;

        Assert.Equal(exitCode, await Launcher.RunLoggedAsync(
            exe, args, directory.Path, new Dictionary<string, string?>(), logPath, () =>
            {
                cleanupCalled = true;
                return Task.CompletedTask;
            }));
        Assert.Equal(exitCode != 0, cleanupCalled);

        var lines = File.ReadAllLines(logPath);
        Assert.Contains("final-out", lines[..^1]);
        Assert.Contains("final-err", lines[..^1]);
        Assert.Equal($"AppHost exited with code {exitCode}; stdout and stderr drained.", lines[^1]);
    }

    /// <summary>ログ書き込み失敗時に AppHost と子孫を停止し、全プロセスを回収することを確認します。</summary>
    [Fact]
    public async Task LogWriteFailureTerminatesTheChildAndDescendant()
    {
        using var directory = new TemporaryDirectory();
        var writer = new FailingWriter();
        var cleanupCalled = false;
        var (exe, args) = Child(
            """$child = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList @('-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 18') -WindowStyle Hidden -PassThru; Write-Output $child.Id; Write-Output $PID; Start-Sleep -Seconds 18""",
            """sleep 18 & child=$!; echo $child; echo $$; wait""");
        try
        {
            var action = Launcher.RunAppHostAsync(exe, args, directory.Path,
                new Dictionary<string, string?>(), writer, () =>
                {
                    cleanupCalled = true;
                    return Task.CompletedTask;
                });
            await Assert.ThrowsAsync<IOException>(() => action.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(cleanupCalled);
            Assert.Equal(2, writer.ProcessIds.Count);
            foreach (var processId in writer.ProcessIds)
                await AssertProcessExitedAsync(processId);
        }
        finally
        {
            foreach (var processId in writer.ProcessIds)
            {
                try
                {
                    using var child = Process.GetProcessById(processId);
                    if (!child.HasExited) child.Kill(entireProcessTree: true);
                }
                catch (ArgumentException) { }
            }
        }
    }

    /// <summary>対象プロセスが終了するまで短時間確認します。</summary>
    private static async Task AssertProcessExitedAsync(int processId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited) return;
            }
            catch (ArgumentException) { return; }
            await Task.Delay(50);
        }
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(processId));
    }

    private static (string FileName, string[] Arguments) Child(string powershell, string shell) =>
        OperatingSystem.IsWindows()
            ? (Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                ["-NoProfile", "-NonInteractive", "-Command", powershell])
            : ("/bin/sh", ["-c", shell]);

    private static string FindDuckRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Duck.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Duck repository root was not found from the test output directory.");
    }

    private static async Task<string> GitAsync(string workingDirectory, params string[] arguments)
    {
        var result = await RunGitProcessAsync(workingDirectory, arguments, null);
        Assert.True(result.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {result.Stderr}");
        return result.Stdout;
    }

    private static async Task<SyntheticFilterEnvironment> CreateGlobalLfsFilterEnvironmentAsync(string root, string attributes)
    {
        var config = Path.Combine(root, "synthetic-global-gitconfig");
        var attributesPath = Path.Combine(root, "synthetic-global-attributes");
        var marker = Path.Combine(root, "filter-helper-started.txt");
        var helper = Path.Combine(root, OperatingSystem.IsWindows() ? "filter-helper.cmd" : "filter-helper.sh");
        var helperContents = OperatingSystem.IsWindows()
            ? "@echo off\r\necho started>\"%DUCK_FILTER_HELPER_MARKER%\"\r\n"
            : "#!/bin/sh\nprintf 'started\\n' > \"$DUCK_FILTER_HELPER_MARKER\"\n";
        await File.WriteAllTextAsync(attributesPath, attributes);
        await File.WriteAllTextAsync(helper, helperContents);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(helper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var command = OperatingSystem.IsWindows() ? $"cmd.exe /d /c call \"{helper}\"" : $"sh \"{helper}\"";
        await GitAsync(root, "config", "--file", config, "core.attributesFile", attributesPath);
        await GitAsync(root, "config", "--file", config, "filter.lfs.clean", command);
        await GitAsync(root, "config", "--file", config, "filter.lfs.smudge", command);
        await GitAsync(root, "config", "--file", config, "filter.lfs.process", command);
        await GitAsync(root, "config", "--file", config, "filter.lfs.required", "true");
        await GitAsync(root, "config", "--file", config, "filter.unset.smudge", command);
        await GitAsync(root, "config", "--file", config, "filter.unset.process", command);
        var environment = new Dictionary<string, string?>(Launcher.CreateRestrictedGitEnvironment(), StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_CONFIG_GLOBAL"] = config,
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["DUCK_FILTER_HELPER_MARKER"] = marker,
        };
        return new SyntheticFilterEnvironment(environment, attributesPath, marker);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunGitProcessAsync(
        string workingDirectory,
        string[] arguments,
        IReadOnlyDictionary<string, string?>? environment)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                if (pair.Value is null) info.Environment.Remove(pair.Key);
                else info.Environment[pair.Key] = pair.Value;
            }
        }
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Git for the launcher test.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    private sealed record SyntheticFilterEnvironment(
        IReadOnlyDictionary<string, string?> Environment,
        string AttributesPath,
        string MarkerPath);

    /// <summary>テスト用の独立した最小リポジトリを作ります。</summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>このテストだけが使用する一時領域です。</summary>
        public string Path { get; } = Directory.CreateTempSubdirectory("duck-launcher-test-").FullName;

        /// <summary>ランチャーが識別できる最小構成の作業木を作ります。</summary>
        public string CreateRepo(string name)
        {
            var root = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(System.IO.Path.Combine(root, "Testing", "Duck.Testing.AppHost"));
            Directory.CreateDirectory(System.IO.Path.Combine(root, "scripts"));
            File.Create(System.IO.Path.Combine(root, "Duck.slnx")).Dispose();
            return root;
        }

        /// <summary>テストで作成した一時領域を削除します。</summary>
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>実際の固定 Proto object を使う使い捨て親作業木を作ります。</summary>
    private sealed class SubmoduleFixture : IDisposable
    {
        private readonly TemporaryDirectory directory = new();
        private readonly string sourceRoot = FindDuckRepositoryRoot();

        public string Root { get; }

        private SubmoduleFixture()
        {
            Root = Path.Combine(directory.Path, "parent");
            Directory.CreateDirectory(Root);
        }

        public static async Task<SubmoduleFixture> CreateAsync()
        {
            var fixture = new SubmoduleFixture();
            var root = fixture.Root;
            await GitAsync(root, "init", "--quiet");
            await GitAsync(root, "config", "user.name", "Duck test");
            await GitAsync(root, "config", "user.email", "duck-test@example.invalid");
            foreach (var relative in new[] { ".gitmodules", "SslProto/SslProto.csproj" })
            {
                var destination = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(fixture.sourceRoot, relative.Replace('/', Path.DirectorySeparatorChar)), destination);
                await GitAsync(root, "add", relative);
            }

            var sourceAssets = Path.Combine(fixture.sourceRoot, "SslProto", "obj", "project.assets.json");
            if (File.Exists(sourceAssets))
            {
                var destination = Path.Combine(root, "SslProto", "obj", "project.assets.json");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(sourceAssets, destination);
            }

            await GitAsync(root, "update-index", "--add", "--cacheinfo", "160000,9b0765fe7a07a55124f6ee957d4559348462d227,SslProto/src/external/ssl-game-controller");
            await GitAsync(root, "update-index", "--add", "--cacheinfo", "160000,1e719a8d410cc84847ec69104da44fed13738291,SslProto/src/external/ssl-simulation-protocol");
            await GitAsync(root, "commit", "--quiet", "-m", "fixture");

            foreach (var name in new[] { "ssl-game-controller", "ssl-simulation-protocol" })
            {
                var path = name == "ssl-game-controller"
                    ? "SslProto/src/external/ssl-game-controller"
                    : "SslProto/src/external/ssl-simulation-protocol";
                var store = Path.Combine(root, ".git", "modules", path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(store)!);
                var commit = name == "ssl-game-controller"
                    ? "9b0765fe7a07a55124f6ee957d4559348462d227"
                    : "1e719a8d410cc84847ec69104da44fed13738291";
                await GitAsync(root, "init", "--quiet", "--bare", store);
                await GitAsync(root, "--git-dir", store, "fetch", Path.Combine(fixture.sourceRoot, path.Replace('/', Path.DirectorySeparatorChar)), commit);
                await GitAsync(root, "--git-dir", store, "config", "core.bare", "false");
            }
            return fixture;
        }

        public string Worktree(string name) => Path.Combine(Root, name == "ssl-game-controller"
            ? "SslProto/src/external/ssl-game-controller"
            : "SslProto/src/external/ssl-simulation-protocol");

        public void RemoveObjectStore(string name)
        {
            var gitDir = Path.Combine(Root, ".git", "modules", (name == "ssl-game-controller"
                ? "SslProto/src/external/ssl-game-controller"
                : "SslProto/src/external/ssl-simulation-protocol").Replace('/', Path.DirectorySeparatorChar));
            var objects = Path.Combine(gitDir, "objects");
            Assert.True(Directory.Exists(objects));
            Directory.Delete(objects, recursive: true);
            Directory.CreateDirectory(objects);
        }

        public void Dispose() => directory.Dispose();
    }

    /// <summary>二つのプロセス ID を記録してからログ書き込み失敗を再現します。</summary>
    private sealed class FailingWriter : StringWriter
    {
        /// <summary>障害前に受け取った AppHost と子孫の識別子です。</summary>
        public List<int> ProcessIds { get; } = [];

        /// <summary>二つ目の識別子を書き込む際に I/O 障害を発生させます。</summary>
        public override void WriteLine(string? value)
        {
            ProcessIds.Add(int.Parse(value!));
            if (ProcessIds.Count == 2) throw new IOException("Simulated logging failure.");
        }
    }
}
