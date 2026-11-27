using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class FixedInputPreparationTests
{
    [Fact]
    public void PrepareMissingRequiresExplicitMatch()
    {
        Assert.False(Launcher.Options.Parse(["--play-match"]).PrepareMissing);
        Assert.True(Launcher.Options.Parse(["--play-match", "--prepare-missing"]).PrepareMissing);
        Assert.Equal(45, Launcher.Options.Parse(["--play-match", "--prepare-missing"]).HalfSeconds);
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--prepare-missing"]));
    }

    [Fact]
    public async Task MissingStoreIsVerifiedBeforePlacementAndExistingStoreIsNotReplaced()
    {
        var temporary = Directory.CreateTempSubdirectory("duck-fixed-input-");
        try
        {
            var destination = Path.Combine(temporary.FullName, "module");
            var calls = 0;
            async Task Fetch(string path, string url, string commit)
            {
                calls++;
                Assert.Equal("https://example.invalid/fixture", url);
                Assert.Equal("fixed-commit", commit);
                Directory.CreateDirectory(path);
                await File.WriteAllTextAsync(Path.Combine(path, "marker"), "original");
            }
            async Task Verify(string path)
            {
                Assert.False(Directory.Exists(destination));
                Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(path, "marker")));
            }
            await Launcher.PrepareMissingProtoStoreAsync(destination, "https://example.invalid/fixture", "fixed-commit", Fetch, Verify);
            Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(destination, "marker")));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Launcher.PrepareMissingProtoStoreAsync(destination, "https://example.invalid/fixture", "fixed-commit", Fetch, Verify));
            Assert.Equal(1, calls);
            Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(destination, "marker")));
        }
        finally { temporary.Delete(recursive: true); }
    }

    [Fact]
    public async Task FailedOrUnverifiedStoreIsNotPlaced()
    {
        var temporary = Directory.CreateTempSubdirectory("duck-fixed-input-");
        try
        {
            var destination = Path.Combine(temporary.FullName, "module");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Launcher.PrepareMissingProtoStoreAsync(destination, "https://example.invalid/fixture", "fixed-commit",
                    (_, _, _) => throw new IOException("synthetic fetch failure"), _ => Task.CompletedTask));
            Assert.False(Directory.Exists(destination));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Launcher.PrepareMissingProtoStoreAsync(destination, "https://example.invalid/fixture", "fixed-commit",
                    (stage, _, _) => { Directory.CreateDirectory(stage); return Task.CompletedTask; },
                    _ => throw new InvalidOperationException("synthetic missing object")));
            Assert.False(Directory.Exists(destination));
            Assert.Empty(Directory.EnumerateFileSystemEntries(temporary.FullName));
        }
        finally { temporary.Delete(recursive: true); }
    }

    [Fact]
    public async Task CachedTagsAndDigestsAreReusedAndOnlyMissingPinnedReferenceIsPulled()
    {
        var image = WindowsDevelopmentMatchInputs.FixedImages[0];
        var present = new HashSet<string>(StringComparer.Ordinal) { image.Reference, image.PinnedReference };
        var pulls = new List<string>();
        var verified = new List<string>();
        Task<bool> Exists(string reference) => Task.FromResult(present.Contains(reference));
        Task Verify(string reference) { verified.Add(reference); return Task.CompletedTask; }
        Task Pull(string reference) { pulls.Add(reference); present.Add(reference); return Task.CompletedTask; }

        Assert.Equal(image.PinnedReference, await WindowsDevelopmentMatchInputs.PrepareImageAsync(image, Exists, Verify, Pull));
        Assert.Empty(pulls);
        present.Clear();
        present.Add(image.PinnedReference);
        Assert.Equal(image.PinnedReference, await WindowsDevelopmentMatchInputs.PrepareImageAsync(image, Exists, Verify, Pull));
        Assert.Empty(pulls);
        present.Clear();
        Assert.Equal(image.PinnedReference, await WindowsDevelopmentMatchInputs.PrepareImageAsync(image, Exists, Verify, Pull));
        Assert.Equal(image.PinnedReference, Assert.Single(pulls));
        Assert.Equal(new[] { image.Reference, image.PinnedReference, image.PinnedReference, image.PinnedReference }, verified);
    }

    [Fact]
    public async Task PresentMismatchAndPullFailureStopWithoutFallback()
    {
        var image = WindowsDevelopmentMatchInputs.FixedImages[0];
        var pulled = false;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            WindowsDevelopmentMatchInputs.PrepareImageAsync(image, _ => Task.FromResult(true),
                _ => throw new InvalidDataException("wrong digest"), _ => { pulled = true; return Task.CompletedTask; }));
        Assert.False(pulled);
        var verified = false;
        await Assert.ThrowsAsync<IOException>(() =>
            WindowsDevelopmentMatchInputs.PrepareImageAsync(image, _ => Task.FromResult(false),
                _ => { verified = true; return Task.CompletedTask; }, _ => throw new IOException("pull failure")));
        Assert.False(verified);
    }

    [Fact]
    public void FixedEvidenceRequiresRepositoryPlatformAndSelectedManifest()
    {
        var image = WindowsDevelopmentMatchInputs.FixedImages[0];
        var actual = new WindowsMatchImage(image.Service, image.Reference, "default-id", "linux/amd64",
            [image.PinnedReference], image.PlatformDigest, "platform-id");
        WindowsDevelopmentMatchInputs.VerifyFixedImage(image, actual);
        Assert.Throws<InvalidDataException>(() => WindowsDevelopmentMatchInputs.VerifyFixedImage(image, actual with { Platform = "linux/arm64" }));
        Assert.Throws<InvalidDataException>(() => WindowsDevelopmentMatchInputs.VerifyFixedImage(image, actual with { PlatformDigest = null }));
        Assert.Throws<InvalidDataException>(() => WindowsDevelopmentMatchInputs.VerifyFixedImage(image, actual with { PlatformDigest = "sha256:other" }));
        Assert.Throws<InvalidDataException>(() => WindowsDevelopmentMatchInputs.VerifyFixedImage(image, actual with { RepoDigests = ["other/repo@" + image.PinnedReference.Split('@')[1]] }));
    }

    [Fact]
    public void RegistryPreparationRejectsInsecureOrMirroredSources()
    {
        WindowsDevelopmentMatchInputs.VerifyOfficialRegistryConfiguration("""{"Mirrors":null,"IndexConfigs":{},"InsecureRegistryCIDRs":null}""");
        WindowsDevelopmentMatchInputs.VerifyOfficialRegistryConfiguration("""
            {"Mirrors":[],"IndexConfigs":{"docker.io":{"Secure":true}},"InsecureRegistryCIDRs":["127.0.0.0/8","::1/128"]}
            """);
        Assert.Throws<InvalidOperationException>(() => WindowsDevelopmentMatchInputs.VerifyOfficialRegistryConfiguration("""
            {"Mirrors":["https://example.invalid"],"IndexConfigs":{},"InsecureRegistryCIDRs":[]}
            """));
        Assert.Throws<InvalidOperationException>(() => WindowsDevelopmentMatchInputs.VerifyOfficialRegistryConfiguration("""
            {"Mirrors":[],"IndexConfigs":{"ghcr.io":{"Secure":false}},"InsecureRegistryCIDRs":[]}
            """));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task PreflightUsesOnlyPinnedMissingReferenceAndStopsOnMismatchOrFailure(bool mismatch, bool pullFailure, bool wrongDefaultPlatform = false)
    {
        var temporary = Directory.CreateTempSubdirectory("duck-fixed-preflight-");
        try
        {
            var stack = new ComposeSimulationStack("fixed-preflight", Path.Combine(temporary.FullName, "compose.yml"),
                "docker", WindowsDevelopmentMatchInputs.DockerContext);
            var builder = Aspire.Hosting.DistributedApplication.CreateBuilder([]);
            foreach (var fixedImage in WindowsDevelopmentMatchInputs.FixedImages.Where(image => image.Service != "transfer-base"))
                stack.AddService(builder, fixedImage.Service, fixedImage.Service, fixedImage.Reference,
                    isOwner: fixedImage.Service == "cm4-sim");
            var transfer = WindowsDevelopmentMatchInputs.FixedImages.Single(image => image.Service == "transfer-base");
            await File.WriteAllTextAsync(Path.Combine(temporary.FullName, "Dockerfile"), $"FROM {transfer.Reference}\n");
            stack.AddService(builder, "development-udp-transfer", "development-udp-transfer", null, buildContext: temporary.FullName);

            var runner = new FixedImageRunner(mismatch, pullFailure, wrongDefaultPlatform);
            if (mismatch || pullFailure || wrongDefaultPlatform)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => WindowsDevelopmentMatchInputs.ReadImagesAsync(stack,
                    "engine-id", runner, CancellationToken.None, prepareMissing: true));
                Assert.Equal(pullFailure ? 1 : 0, runner.Pulls.Count);
                return;
            }

            var result = await WindowsDevelopmentMatchInputs.ReadImagesAsync(stack, "engine-id", runner,
                CancellationToken.None, prepareMissing: true);
            var absent = WindowsDevelopmentMatchInputs.FixedImages.Single(image => image.Service == "simulator");
            Assert.Equal(absent.PinnedReference, Assert.Single(runner.Pulls));
            Assert.Equal(absent.PinnedReference, stack.Services.Single(service => service.ServiceName == "simulator").Image);
            Assert.Equal(5, result.Images.Count);
            Assert.All(stack.Services, service => Assert.Equal("linux/amd64", service.Platform));
            Assert.All(result.Images, image => Assert.Contains("@sha256:", image.Reference));
            Assert.Contains("platform:", stack.RenderYaml());
            Assert.False(stack.PullMissingImages);
        }
        finally { temporary.Delete(recursive: true); }
    }

    private sealed class FixedImageRunner(bool mismatch, bool pullFailure, bool wrongDefaultPlatform) : IComposeCommandRunner
    {
        public List<string> Pulls { get; } = [];
        public Task<ComposeCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken token)
        {
            ComposeCommandResult Result(int exit, string output = "", string error = "") => new(exit, output, error);
            var reference = arguments[^1];
            if (arguments.Contains("info"))
                return Task.FromResult(reference.Contains("RegistryConfig")
                    ? Result(0, """{"Mirrors":[],"IndexConfigs":{},"InsecureRegistryCIDRs":["127.0.0.0/8"]}""")
                    : Result(0, "engine-id|linux|amd64|29.9.1|Docker Desktop"));
            if (arguments.Contains("pull"))
            {
                Pulls.Add(reference);
                return Task.FromResult(pullFailure ? Result(1, error: "synthetic failure") : Result(0));
            }
            var image = WindowsDevelopmentMatchInputs.FixedImages.Single(item => item.Reference == reference || item.PinnedReference == reference);
            if (image.Service == "simulator" && !Pulls.Contains(reference))
                return Task.FromResult(Result(1, error: $"Error response from daemon: No such image: {reference}"));
            var digest = mismatch && image.Service == "crane" ? "sha256:other" : image.PlatformDigest;
            return Task.FromResult(Result(0, System.Text.Json.JsonSerializer.Serialize(new
            {
                Id = "image-id",
                RepoDigests = new[] { image.PinnedReference },
                Os = "linux",
                Architecture = wrongDefaultPlatform && image.Service == "crane" && !arguments.Contains("--platform") ? "arm64" : "amd64",
                Descriptor = new { digest }
            })));
        }
        public IAsyncDisposable StartLogs(string executable, IReadOnlyList<string> arguments, Action<string> writeLine) =>
            throw new NotSupportedException();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtoPreflightRejectsModifiedSourceOrExistingFileBeforeFetch(bool wrongSource)
    {
        var temporary = Directory.CreateTempSubdirectory("duck-fixed-proto-guard-");
        try
        {
            var root = temporary.FullName;
            async Task Git(params string[] arguments)
            {
                var info = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false };
                foreach (var argument in arguments) info.ArgumentList.Add(argument);
                using var process = System.Diagnostics.Process.Start(info)!;
                await process.WaitForExitAsync();
                Assert.Equal(0, process.ExitCode);
            }
            await Git("init", "--quiet");
            const string gc = "SslProto/src/external/ssl-game-controller";
            const string sim = "SslProto/src/external/ssl-simulation-protocol";
            var source = wrongSource ? "https://example.invalid/wrong" : "https://github.com/RoboCup-SSL/ssl-game-controller";
            await File.WriteAllTextAsync(Path.Combine(root, ".gitmodules"),
                $"[submodule \"{gc}\"]\n\tpath = {gc}\n\turl = {source}\n[submodule \"{sim}\"]\n\tpath = {sim}\n\turl = https://github.com/RoboCup-SSL/ssl-simulation-protocol\n");
            await Git("add", ".gitmodules");
            await Git("update-index", "--add", "--cacheinfo", $"160000,9b0765fe7a07a55124f6ee957d4559348462d227,{gc}");
            await Git("update-index", "--add", "--cacheinfo", $"160000,1e719a8d410cc84847ec69104da44fed13738291,{sim}");
            await Git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "--quiet", "-m", "fixture");
            var existing = Path.Combine(root, gc, "user-file");
            Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
            await File.WriteAllTextAsync(existing, "preserve");
            var fetches = 0;
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Launcher.PrepareAndValidateProtoSubmodulesAsync(root, prepareMissing: true,
                    fetchMissing: (_, _, _) => { fetches++; throw new IOException("unexpected fetch"); }));
            Assert.Contains(wrongSource ? "Unexpected official Proto URL" : "Existing Proto data", error.Message);
            Assert.Equal(0, fetches);
            Assert.Equal("preserve", await File.ReadAllTextAsync(existing));
        }
        finally { temporary.Delete(recursive: true); }
    }
}
