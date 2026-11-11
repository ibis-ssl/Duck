using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class DockerIdentitySettingsResolverTests
{
    [Fact]
    public async Task ResolvesActiveContextAndEngineWhenNeitherIsConfigured()
    {
        var calls = new List<IReadOnlyList<string>>();
        var results = new Queue<DockerStdioCommandResult>([
            new(0, "desktop-linux\n", string.Empty),
            new(0, "engine-123\n", string.Empty),
        ]);

        var resolved = await DockerIdentitySettingsResolver.ResolveAsync(
            null,
            null,
            (arguments, _, _) =>
            {
                calls.Add(arguments);
                return Task.FromResult(results.Dequeue());
            },
            CancellationToken.None);

        Assert.Equal(new DockerIdentitySettings("desktop-linux", "engine-123"), resolved);
        Assert.Equal(2, calls.Count);
        Assert.Equal(new[] { "context", "show" }, calls[0]);
        Assert.Equal(new[] { "--context", "desktop-linux", "info", "--format", "{{.ID}}" }, calls[1]);
    }

    [Fact]
    public async Task PreservesExplicitContextAndExpectedEngineWhenTheyMatch()
    {
        IReadOnlyList<string>? capturedArguments = null;

        var resolved = await DockerIdentitySettingsResolver.ResolveAsync(
            "named-context",
            "engine-123",
            (arguments, _, _) =>
            {
                capturedArguments = arguments;
                return Task.FromResult(new DockerStdioCommandResult(0, "engine-123\n", string.Empty));
            },
            CancellationToken.None);

        Assert.Equal(new DockerIdentitySettings("named-context", "engine-123"), resolved);
        Assert.Equal(new[] { "--context", "named-context", "info", "--format", "{{.ID}}" }, capturedArguments);
    }

    [Fact]
    public async Task RejectsConfiguredEngineThatDoesNotMatchSelectedContext()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DockerIdentitySettingsResolver.ResolveAsync(
                "named-context",
                "stale-engine",
                (_, _, _) => Task.FromResult(new DockerStdioCommandResult(0, "current-engine\n", string.Empty)),
                CancellationToken.None));

        Assert.Contains("does not match", exception.Message);
        Assert.DoesNotContain("stale-engine", exception.Message);
        Assert.DoesNotContain("current-engine", exception.Message);
    }

    [Fact]
    public async Task ReportsContextLookupFailureWithoutReturningPartialIdentity()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DockerIdentitySettingsResolver.ResolveAsync(
                null,
                null,
                (_, _, _) => Task.FromResult(new DockerStdioCommandResult(1, string.Empty, "daemon unavailable")),
                CancellationToken.None));

        Assert.Contains("active Docker context", exception.Message);
        Assert.Contains("exited with code 1", exception.Message);
    }

    [Fact]
    public async Task ReportsEmptyEngineIdentityAsActionableFailure()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DockerIdentitySettingsResolver.ResolveAsync(
                "desktop-linux",
                null,
                (_, _, _) => Task.FromResult(new DockerStdioCommandResult(0, " \r\n", string.Empty)),
                CancellationToken.None));

        Assert.Contains("no single Docker Engine ID", exception.Message);
        Assert.Contains("docker --context desktop-linux info", exception.Message);
    }
}
