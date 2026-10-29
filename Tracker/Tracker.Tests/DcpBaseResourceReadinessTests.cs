using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tracker.Tests;

public sealed class DcpBaseResourceReadinessTests
{
    private const string FirstContainerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string NextContainerId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task CurrentIdentityCandidateNeverBecomesReadyWithoutOwnershipAndServiceProof()
    {
        var resource = new ContainerResource("simulator", "example/simulator:fixed");
        var registry = new DcpBaseResourceReadinessRegistry([(resource, "simulator", "shared-run")]);
        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator", "simulator", FirstContainerId), "Running");

        var snapshot = Assert.IsType<DcpBaseResourceReadinessSnapshot>(registry.Get(resource));
        Assert.Equal("simulator", snapshot.ResourceId);
        Assert.Equal(FirstContainerId, snapshot.ContainerId);
        Assert.Equal("shared-run", snapshot.RunId);
        Assert.False(snapshot.IsReady);
        Assert.Contains("unverified", snapshot.Reason, StringComparison.OrdinalIgnoreCase);

        var health = await new DcpBaseResourceReadinessHealthCheck(registry, resource)
            .CheckHealthAsync(null!);
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
    }

    [Fact]
    public void NewResourceKeyOrContainerIdInvalidatesThePreviousReadinessGeneration()
    {
        var resource = new ContainerResource("simulator", "example/simulator:fixed");
        var registry = new DcpBaseResourceReadinessRegistry([(resource, "simulator", "shared-run")]);
        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator", "simulator", FirstContainerId), "Running");
        var first = Assert.IsType<DcpBaseResourceReadinessSnapshot>(registry.Get(resource));

        registry.ObserveUnavailable(resource, "simulator-next-key", "Starting");
        var invalidated = Assert.IsType<DcpBaseResourceReadinessSnapshot>(registry.Get(resource));
        Assert.True(invalidated.Generation > first.Generation);
        Assert.Equal("Unknown", invalidated.State);
        Assert.Null(invalidated.ContainerId);
        Assert.False(invalidated.IsReady);
        Assert.Contains("ambiguous", invalidated.Reason, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("simulator-next-key", registry.ObserveUnavailable(resource, "simulator", "Stopped"));
        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator-next-key", "simulator", NextContainerId), "Running");
        var next = Assert.IsType<DcpBaseResourceReadinessSnapshot>(registry.Get(resource));
        Assert.True(next.Generation > invalidated.Generation);
        Assert.Equal(NextContainerId, next.ContainerId);
        Assert.False(next.IsReady);
    }

    [Fact]
    public void RetiredResourceKeyCannotReplaceTheLatestContainerCandidate()
    {
        var resource = new ContainerResource("simulator", "example/simulator:fixed");
        var registry = new DcpBaseResourceReadinessRegistry([(resource, "simulator", "shared-run")]);
        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator-old-key", "simulator", FirstContainerId), "Running");
        registry.ObserveUnavailable(resource, "simulator-old-key", "Stopped");
        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator-new-key", "simulator", NextContainerId), "Running");

        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator-old-key", "simulator", FirstContainerId), "Running");

        var snapshot = Assert.IsType<DcpBaseResourceReadinessSnapshot>(registry.Get(resource));
        Assert.Equal("simulator-new-key", snapshot.ResourceId);
        Assert.Equal(NextContainerId, snapshot.ContainerId);
        Assert.False(snapshot.IsReady);
    }

    [Fact]
    public void AmbiguousReplacementKeyStaysUnreadyUntilOldKeyStops()
    {
        var resource = new ContainerResource("simulator", "example/simulator:fixed");
        var registry = new DcpBaseResourceReadinessRegistry([(resource, "simulator", "shared-run")]);
        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator-old-key", "simulator", FirstContainerId), "Running");

        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator-new-key", "simulator", NextContainerId), "Running");
        var ambiguous = Assert.IsType<DcpBaseResourceReadinessSnapshot>(registry.Get(resource));
        Assert.Empty(ambiguous.ResourceId);
        Assert.Null(ambiguous.ContainerId);
        Assert.False(ambiguous.IsReady);
        Assert.Contains("ambiguous", ambiguous.Reason, StringComparison.OrdinalIgnoreCase);

        var promotedId = registry.ObserveUnavailable(resource, "simulator-old-key", "Stopped");
        Assert.Equal("simulator-new-key", promotedId);
        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator-new-key", "simulator", NextContainerId), "Running");
        registry.ObserveCurrent(new RegisteredContainerRuntimeTarget("simulator-old-key", "simulator", FirstContainerId), "Running");

        var current = Assert.IsType<DcpBaseResourceReadinessSnapshot>(registry.Get(resource));
        Assert.Equal("simulator-new-key", current.ResourceId);
        Assert.Equal(NextContainerId, current.ContainerId);
        Assert.False(current.IsReady);
    }

    [Fact]
    public void EventConsumerPromotesOnlyReplacementKeyAndIgnoresLateRetiredKey()
    {
        var resource = new ContainerResource("simulator", "example/simulator:fixed");
        var registry = new DcpBaseResourceReadinessRegistry([(resource, "simulator", "shared-run")]);
        var consumer = new DcpBaseResourceReadinessEventConsumer(registry);
        var currentByKey = new Dictionary<string, ResourceEvent>(StringComparer.Ordinal);
        ResourceEvent? GetCurrent(string resourceId) => currentByKey.GetValueOrDefault(resourceId);

        var oldRunning = Event(resource, "simulator-old-key", "Running", FirstContainerId);
        currentByKey[oldRunning.ResourceId] = oldRunning;
        consumer.Observe(oldRunning, oldRunning, "simulator", GetCurrent);

        var replacementRunning = Event(resource, "simulator-new-key", "Running", NextContainerId);
        currentByKey[replacementRunning.ResourceId] = replacementRunning;
        consumer.Observe(replacementRunning, replacementRunning, "simulator", GetCurrent);
        Assert.Null(registry.Get(resource)!.ContainerId);

        var oldStopped = Event(resource, "simulator-old-key", "Stopped", FirstContainerId);
        currentByKey[oldStopped.ResourceId] = oldStopped;
        consumer.Observe(oldStopped, oldStopped, "simulator", GetCurrent);

        var lateOldRunning = Event(resource, "simulator-old-key", "Running", FirstContainerId);
        currentByKey[lateOldRunning.ResourceId] = lateOldRunning;
        consumer.Observe(lateOldRunning, lateOldRunning, "simulator", GetCurrent);

        var snapshot = Assert.IsType<DcpBaseResourceReadinessSnapshot>(registry.Get(resource));
        Assert.Equal("simulator-new-key", snapshot.ResourceId);
        Assert.Equal(NextContainerId, snapshot.ContainerId);
        Assert.False(snapshot.IsReady);
    }

    private static ResourceEvent Event(IResource resource, string resourceId, string state, string containerId) =>
        new(resource, resourceId, new CustomResourceSnapshot
        {
            ResourceType = "container",
            State = new ResourceStateSnapshot(state, "fixture"),
            Properties = [new ResourcePropertySnapshot("container.id", containerId)],
        });
}
