using System.Runtime.InteropServices;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Tracker.Tests;

public sealed class MatchLinuxBindPermissionsTests
{
    [Theory]
    [InlineData("game-controller", "/tmp/state-store.json.stream", false, true)]
    [InlineData("game-controller", "/tmp/match-engine.json", false, false)]
    [InlineData("match-controller", "/match-results", true, true)]
    [InlineData("ssl-log-recorder", "/data", true, true)]
    public async Task MatchBindGrantsOnlyRequiredGroupAccess(
        string resourceName, string target, bool directory, bool writable)
    {
        if (!OperatingSystem.IsLinux())
            throw Xunit.Sdk.SkipException.ForSkip("Linux host-bind modes require a native Linux verifier.");

        using var appHost = await CreateMatchModelAsync();
        var resource = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == resourceName));
        var mount = Assert.Single(resource.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Target == target);
        if (writable)
            Assert.False(mount.IsReadOnly);
        Assert.NotNull(mount.Source);
        Assert.Equal(directory, Directory.Exists(mount.Source));
        var mode = File.GetUnixFileMode(mount.Source!);
        var required = UnixFileMode.GroupRead;
        if (writable)
            required |= UnixFileMode.GroupWrite;
        else
            Assert.Equal((UnixFileMode)0, mode & UnixFileMode.GroupWrite);
        if (directory)
            required |= UnixFileMode.GroupExecute;
        Assert.True((mode & required) == required,
            $"{resourceName}:{target} needs group mode {required}; mode was {mode}.");
        Assert.Equal((UnixFileMode)0, mode & UnixFileMode.OtherWrite);
        if (!directory)
            Assert.Equal((UnixFileMode)0, mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute));
    }

    [Theory]
    [InlineData("game-controller")]
    [InlineData("match-controller")]
    [InlineData("ssl-log-recorder")]
    public async Task MatchBindConsumerReceivesHostAccessGroup(string resourceName)
    {
        if (!OperatingSystem.IsLinux())
            throw Xunit.Sdk.SkipException.ForSkip("Linux container identity requires a native Linux verifier.");

        using var appHost = await CreateMatchModelAsync();
        var resource = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == resourceName));
        var arguments = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
            await annotation.Callback(new ContainerRuntimeArgsCallbackContext(arguments, CancellationToken.None));
        var argv = arguments.Select(argument => Assert.IsType<string>(argument)).ToArray();
        var userIndex = Array.IndexOf(argv, "--user");
        // Preserve each image's configured user. Access can come from a shared
        // supplementary group; no UID override is required by this regression.
        var identity = userIndex >= 0 && userIndex + 1 < argv.Length
            ? argv[userIndex + 1].Split(':') : Array.Empty<string>();
        var hostGroup = GetEffectiveGroupId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var primaryGroupMatches = identity.Length == 2 && identity[1] == hostGroup;
        var supplementaryGroupMatches = argv.Where((argument, index) =>
            argument == "--group-add" && index + 1 < argv.Length && argv[index + 1] == hostGroup).Any();
        Assert.True(primaryGroupMatches || supplementaryGroupMatches,
            $"{resourceName} must receive the host GID for the specific group-writable bind paths.");
    }

    [Fact]
    public async Task MatchPermissionPreparationLeavesRepositoryFixturesAndSharedTempModesUnchanged()
    {
        if (!OperatingSystem.IsLinux())
            throw Xunit.Sdk.SkipException.ForSkip("Linux host-bind modes require a native Linux verifier.");

        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "match");
        var fixtures = new[] { "state-store-initial.json.stream", "engine-config-initial.json" }
            .Select(name => Path.Combine(fixtureRoot, name)).ToArray();
        var contents = fixtures.Select(File.ReadAllBytes).ToArray();
        var modes = fixtures.Select(File.GetUnixFileMode).ToArray();
        var tempMode = File.GetUnixFileMode(Path.GetTempPath());
        using var appHost = await CreateMatchModelAsync();
        for (var index = 0; index < fixtures.Length; index++)
        {
            Assert.Equal(contents[index], File.ReadAllBytes(fixtures[index]));
            Assert.Equal(modes[index], File.GetUnixFileMode(fixtures[index]));
        }
        Assert.Equal(tempMode, File.GetUnixFileMode(Path.GetTempPath()));
    }

    private static Task<IDistributedApplicationTestingBuilder> CreateMatchModelAsync()
    {
        var key = "issue51-permission-test-" + Guid.NewGuid().ToString("N");
        // CreateAsync composes the model only; never Build/Start this fixture or launch Docker.
        return DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
        [
            "--Testing:Mode=match",
            "--Testing:Diagnostics:RunId=" + key,
            "--Testing:StackOwnership:StackId=" + key,
            "--Testing:StackOwnership:LockPath=" + Path.Combine(Path.GetTempPath(), key + ".lock"),
            "--Testing:Crane:FixtureManifestPath=" + Path.Combine(Path.GetTempPath(), key + ".json"),
        ]);
    }

    [DllImport("libc", EntryPoint = "getegid")]
    private static extern uint GetEffectiveGroupId();
}
