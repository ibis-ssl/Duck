using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using System.Text.Json;

namespace Tracker.Tests;

public sealed class MatchParticipantStatusProjectionTests
{
    [Fact]
    public void DockerInspectEvidenceRequiresCurrentFullIdLabelsAndNetwork()
    {
        var target = new MatchParticipantCurrentTarget("resource-instance", new string('a', 64), "Running");

        Assert.True(VerifiedContainerLogEvidence.TryParse(
            InspectJson(target.ContainerId, "2026-11-04T11:59:00.000000000Z"),
            target,
            "run-1",
            "stack-1",
            "tigers-blue",
            out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(DateTimeOffset.Parse("2026-11-04T11:59:00Z"), parsed.CreatedAtUtc);

        Assert.False(VerifiedContainerLogEvidence.TryParse(
            InspectJson(new string('b', 64), "2026-11-04T11:59:00.000000000Z"),
            target,
            "run-1",
            "stack-1",
            "tigers-blue",
            out _));
    }

    [Fact]
    public void DockerGenerationTimestampAcceptsNewStartupBacklogAndRejectsOldTimestamp()
    {
        var resource = new ContainerResource("tigers-blue", "tigers-image");
        var created = DateTimeOffset.Parse("2026-11-04T12:00:00Z");
        var tracker = new MatchParticipantLogEvidenceTracker(
            [resource], "run-1", new TestTimeProvider(created.AddSeconds(5)));
        var target = new MatchParticipantCurrentTarget("resource-instance", new string('c', 64), "Running");
        tracker.SetCurrentTarget(resource, target, generationBoundaryUtc: created);

        tracker.ObserveLogLine(resource, "2026-11-04T12:00:00.100000000Z INFO Received geometry from vision", 1);
        tracker.ObserveLogLine(resource, "2026-11-04T12:00:00.200000000Z INFO Publishing vision tracking packets to 224.5.23.2:11010", 2);
        Assert.True(tracker.GetEvidence(resource).IsReady);

        tracker.SetCurrentTarget(resource, target with { ContainerId = new string('d', 64) },
            generationBoundaryUtc: created.AddSeconds(3));
        tracker.ObserveLogLine(resource, "2026-11-04T12:00:01.100000000Z INFO Received geometry from vision", 1);
        tracker.ObserveLogLine(resource, "2026-11-04T12:00:01.200000000Z INFO Publishing vision tracking packets to 224.5.23.2:11010", 2);
        Assert.False(tracker.GetEvidence(resource).IsReady);
    }

    [Fact]
    public void DockerGenerationRevalidationRejectsChangedCreatedOrNetworkIdentity()
    {
        var target = new MatchParticipantCurrentTarget("resource-instance", new string('e', 64), "Running");
        Assert.True(VerifiedContainerLogEvidence.TryParse(
            InspectJson(target.ContainerId, "2026-11-04T11:59:00Z"), target, "run-1", "stack-1", "tigers-blue", out var before));
        Assert.True(VerifiedContainerLogEvidence.TryParse(
            InspectJson(target.ContainerId, "2026-11-04T11:59:01Z", networkId: new string('f', 64)), target,
            "run-1", "stack-1", "tigers-blue", out var after));

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.False(before.IsSameGeneration(after));
    }

    [Fact]
    public void DockerRestartIsANewGenerationEvenWhenIdCreatedAndNetworkAreUnchanged()
    {
        var target = new MatchParticipantCurrentTarget("resource-instance", new string('e', 64), "Running");
        Assert.True(VerifiedContainerLogEvidence.TryParse(
            InspectJson(target.ContainerId, "2026-11-04T11:50:00Z", startedAt: "2026-11-04T11:59:00Z"),
            target, "run-1", "stack-1", "tigers-blue", out var before));
        Assert.True(VerifiedContainerLogEvidence.TryParse(
            InspectJson(target.ContainerId, "2026-11-04T11:50:00Z", startedAt: "2026-11-04T12:01:00Z", restartCount: 1),
            target, "run-1", "stack-1", "tigers-blue", out var after));

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.False(before.IsSameGeneration(after));
        Assert.Equal(DateTimeOffset.Parse("2026-11-04T12:01:00Z"), after.StartedAtUtc);
    }

    [Fact]
    public void ServiceReadinessRequiresCurrentIdentityAndAllMarkers()
    {
        var resource = new ContainerResource("tigers-blue", "tigers-image");
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-11-04T12:00:00Z"));
        var tracker = new MatchParticipantLogEvidenceTracker([resource], "run-1", clock);
        var target = new MatchParticipantCurrentTarget("opaque-instance", new string('a', 64), "Running");

        tracker.ObserveLogLine(resource, "2026-11-04 12:00:00,500 INFO Received geometry from vision", 1);
        tracker.SetCurrentTarget(resource, target, minimumLineNumber: 1);
        Assert.False(tracker.GetEvidence(resource).IsReady);

        tracker.ObserveLogLine(resource, "2026-11-04 12:00:01,000 INFO Received geometry from vision", 2);
        tracker.ObserveLogLine(resource, "2026-11-04 12:00:01,100 INFO Publishing vision tracking packets to 224.5.23.2:11010", 3);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(tracker.GetEvidence(resource).IsReady);

        tracker.SetCurrentTarget(resource, target with { ContainerId = new string('b', 64) }, minimumLineNumber: 3);
        tracker.ObserveLogLine(resource, "2026-11-04 12:00:02,000 INFO Received geometry from vision", 4);
        Assert.False(tracker.GetEvidence(resource).IsReady);
    }

    [Fact]
    public void ReplacementGenerationRejectsPriorTimestampedLogs()
    {
        var resource = new ContainerResource("tigers-blue", "tigers-image");
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-11-04T12:00:00Z"));
        var tracker = new MatchParticipantLogEvidenceTracker([resource], "run-1", clock);
        var first = new MatchParticipantCurrentTarget("instance-1", new string('1', 64), "Running");
        tracker.SetCurrentTarget(resource, first);
        tracker.ObserveLogLine(resource, "2026-11-04 12:00:00,100 INFO Received geometry from vision", 1);
        tracker.ObserveLogLine(resource, "2026-11-04 12:00:00,200 INFO Publishing vision tracking packets to 224.5.23.2:11010", 2);
        Assert.True(tracker.GetEvidence(resource).IsReady);

        clock.Advance(TimeSpan.FromSeconds(2));
        var replacement = first with { ResourceId = "instance-2", ContainerId = new string('2', 64) };
        tracker.SetCurrentTarget(resource, replacement, minimumLineNumber: 2, generationBoundaryUtc: clock.GetUtcNow());
        tracker.ObserveLogLine(resource, "2026-11-04 12:00:00,300 INFO Received geometry from vision", 3);
        Assert.False(tracker.GetEvidence(resource).IsReady);

        tracker.ObserveLogLine(resource, "2026-11-04 12:00:03,000 INFO Received geometry from vision", 4);
        tracker.ObserveLogLine(resource, "2026-11-04 12:00:03,100 INFO Publishing vision tracking packets to 224.5.23.2:11010", 5);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(tracker.GetEvidence(resource).IsReady);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    public void ExternalServiceReadinessRejectsMissingOrPartialContainerIdentity(string? containerId)
    {
        var status = new MatchParticipantResourceStatus(
            "autoref-tigers", "Running", "Healthy", "opaque-instance", containerId, "run-1",
            true, DateTimeOffset.UtcNow, null, null);

        Assert.False(status.IsReady);
    }

    [Fact]
    public void AutoRefRequiresEveryObservedPositiveMarker()
    {
        var resource = new ContainerResource("autoref-tigers", "autoref-image");
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-11-04T12:00:00Z"));
        var tracker = new MatchParticipantLogEvidenceTracker([resource], "run-1", clock);
        tracker.SetCurrentTarget(resource, new MatchParticipantCurrentTarget("id", new string('c', 64), "Running"));

        tracker.ObserveLogLine(resource, "2026-11-04 12:00:00,100 INFO Received geometry from vision", 1);
        tracker.ObserveLogLine(resource, "2026-11-04 12:00:00,200 INFO Changed AutoRef mode to ACTIVE", 2);
        Assert.False(tracker.GetEvidence(resource).IsReady);

        tracker.ObserveLogLine(resource, "2026-11-04 12:00:00,300 INFO Successfully registered AutoRef", 3);
        Assert.True(tracker.GetEvidence(resource).IsReady);

        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.False(tracker.GetEvidence(resource).IsReady);
    }

    [Fact]
    public void GenericParticipantRequiresExplicitHealthyStatus()
    {
        var status = new MatchParticipantResourceStatus(
            "crane", "Running", null, "id", new string('d', 64), "run-1",
            false, null, null, null);

        Assert.False(status.IsReady);
        Assert.False((status with { HealthStatus = "Unhealthy" }).IsReady);
        Assert.True((status with { HealthStatus = "Healthy" }).IsReady);
    }

    [Fact]
    public void RecorderRequiresFreshGrowthForCurrentContainer()
    {
        var tracker = new RecorderOutputGrowthTracker();
        var now = DateTimeOffset.Parse("2026-11-04T12:00:00Z");
        var first = new MatchParticipantCurrentTarget("id-1", new string('e', 64), "Running");
        var recent = now.AddSeconds(-1);

        Assert.False(tracker.Observe(first, "run-1", 100, recent, now));
        Assert.True(tracker.Observe(first, "run-1", 101, recent, now));
        Assert.False(tracker.Observe(first, "run-1", 101, recent, now));
        Assert.False(tracker.Observe(first, "run-1", 102, now.AddSeconds(-16), now));

        var replacement = first with { ContainerId = new string('f', 64) };
        Assert.False(tracker.Observe(replacement, "run-1", 500, recent, now));
        Assert.True(tracker.Observe(replacement, "run-1", 501, recent, now));
        Assert.False(tracker.Observe(replacement, "run-2", 700, recent, now));
        Assert.True(tracker.Observe(replacement, "run-2", 701, recent, now));
        Assert.False(tracker.Observe(null, "run-1", 600, recent, now));
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        internal void Advance(TimeSpan amount) => current += amount;
    }

    private static string InspectJson(
        string id,
        string created,
        string? networkId = null,
        string startedAt = "2026-11-04T11:59:00Z",
        int restartCount = 0) => JsonSerializer.Serialize(new
    {
        Id = id,
        Created = created,
        State = new { Status = "running", StartedAt = startedAt },
        RestartCount = restartCount,
        Config = new
        {
            Labels = new Dictionary<string, string>
            {
                ["duck.aspire.stack"] = "stack-1",
                ["duck.aspire.resource"] = "tigers-blue",
                ["duck.aspire.run"] = "run-1",
            },
        },
        HostConfig = new { NetworkMode = "duck-network" },
        NetworkSettings = new
        {
            Networks = new Dictionary<string, object>
            {
                ["duck-network"] = new { NetworkID = networkId ?? new string('1', 64), IPAddress = "172.20.0.3" },
            },
        },
    });
}
