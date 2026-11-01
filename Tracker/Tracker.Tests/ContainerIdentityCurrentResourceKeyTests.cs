using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tracker.Tests;

public sealed class ContainerIdentityCurrentResourceKeyTests
{
    [Fact]
    public async Task RunningObservationBindsItsKeyAfterAnInitialNonRunningKey()
    {
        var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance, lifetime);
        var registered = new ContainerResource("duck", "duck-runtime");
        var runner = new CapturingDockerRunner();
        var tracePath = Path.Combine(Path.GetTempPath(), $"duck-current-key-trace-{Guid.NewGuid():N}.jsonl");
        using var stop = new CancellationTokenSource();
        using var service = new ContainerIdentityHandoffService(
            notifications, registered, "duck", "run-current-key", "desktop-linux",
            "engine-current-key", runner, NullLogger<ContainerIdentityHandoffService>.Instance, tracePath);

        await service.StartAsync(stop.Token);
        await notifications.PublishUpdateAsync(registered, "duck-starting-key",
            _ => Snapshot("Starting", null));
        await notifications.PublishUpdateAsync(registered, "duck-running-key",
            _ => Snapshot("Running", new string('a', 64)));

        await Task.Delay(TimeSpan.FromMilliseconds(150));
        await service.StopAsync(CancellationToken.None);

        Assert.Single(runner.Identities);
        Assert.Equal("duck-running-key", runner.Identities[0].ResourceId);
        var traceLines = await File.ReadAllLinesAsync(tracePath);
        Assert.Contains(traceLines, line => line.Contains("ResourceNotification", StringComparison.Ordinal));
        Assert.Contains(traceLines, line => line.Contains("CurrentResourceState", StringComparison.Ordinal));
        Assert.Contains(traceLines, line => line.Contains("ResourceProjection", StringComparison.Ordinal));
        Assert.Contains(traceLines, line => line.Contains("ContainerInspect", StringComparison.Ordinal));
        Assert.Contains(traceLines, line => line.Contains("IdentityExec", StringComparison.Ordinal));
        Assert.All(traceLines, line => Assert.DoesNotContain("duck-running-key", line));
        File.Delete(tracePath);
    }

    private static CustomResourceSnapshot Snapshot(string state, string? containerId) => new()
    {
        ResourceType = "container",
        State = new ResourceStateSnapshot(state, "fixture"),
        Properties = containerId is null
            ? []
            : [new ResourcePropertySnapshot("container.id", containerId)],
    };

    private sealed class CapturingDockerRunner : IDockerCommandRunner
    {
        public List<ResourceDiagnosticIdentity> Identities { get; } = [];

        public Task<string> CaptureAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
            Task.FromResult(arguments.Contains("info") ? "engine-current-key" :
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    Id = new string('a', 64),
                    State = new { Status = "running" },
                    Config = new { Labels = new Dictionary<string, string>
                    {
                        [ContainerIdentityExecHandoff.RunIdLabel] = "run-current-key",
                        [ContainerIdentityExecHandoff.RoleLabel] = "duck",
                    } },
                }));

        public Task<ContainerIdentityControlExecResult> RelayAsync(
            IReadOnlyList<string> arguments, ResourceDiagnosticIdentity identity, string roleName,
            string? expectedBootId, CancellationToken cancellationToken)
        {
            Identities.Add(identity);
            return Task.FromResult(new ContainerIdentityControlExecResult(true, "boot-current-key", false));
        }
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();
        public CancellationToken ApplicationStarted => started.Token;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => stopped.Token;
        public void StopApplication() => stopping.Cancel();
        public void Dispose() { started.Dispose(); stopping.Dispose(); stopped.Dispose(); }
    }
}
