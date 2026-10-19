using Google.Protobuf;
using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

public sealed class TrackerComparisonReadinessTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_RequiresThreeDistinctFreshDecodableRoleSources()
    {
        var result = TrackerComparisonReadiness.Evaluate(
            [Source("ibis", "duck-id"), Source("TIGERs", "tigers-id"), Source("ER-FORCE", "erforce-id")],
            Now,
            TimeSpan.FromSeconds(5));

        Assert.True(result.IsReady);
        Assert.Empty(result.MissingRoles);
        Assert.Empty(result.ConflictingRoles);
        Assert.Equal(new[] { "Duck", "ER-Force", "TIGERs" }, result.Sources.Select(source => source.LogicalRole).Order(StringComparer.Ordinal));
        Assert.Contains(result.Sources, source => source.LogicalRole == "TIGERs" && source.SourceUuid == "tigers-id");
    }

    [Theory]
    [InlineData("Duck")]
    [InlineData("TIGERs")]
    [InlineData("ER-Force")]
    public void Evaluate_MissingOrStaleRoleIsNotReady(string missingRole)
    {
        var sources = new List<ExternalTrackerReadSideSnapshot>
        {
            Source("ibis", "duck-id"), Source("TIGERs", "tigers-id"), Source("ER-FORCE", "erforce-id"),
        };
        sources.RemoveAt(missingRole switch { "Duck" => 0, "TIGERs" => 1, _ => 2 });
        sources.Add(Source("ibis", "old-duck", Now.AddSeconds(-6)));

        var result = TrackerComparisonReadiness.Evaluate(sources, Now, TimeSpan.FromSeconds(5));

        Assert.False(result.IsReady);
        Assert.Contains(missingRole, result.MissingRoles);
    }

    [Fact]
    public void Evaluate_RejectsOneIdentityClaimedByMultipleLogicalRoles()
    {
        var result = TrackerComparisonReadiness.Evaluate(
            [Source("ibis", "shared"), Source("TIGERs", "shared"), Source("ER-FORCE", "erforce-id")],
            Now,
            TimeSpan.FromSeconds(5));

        Assert.False(result.IsReady);
        Assert.Contains("Duck", result.ConflictingRoles);
        Assert.Contains("TIGERs", result.ConflictingRoles);
    }

    [Fact]
    public void Evaluate_GroupsSameUuidAcrossEndpointsButRejectsOneRoleWithMultipleIdentities()
    {
        var result = TrackerComparisonReadiness.Evaluate(
            [Source("ibis", "duck-id"), Source("TIGERs", "tigers-id", endpoint: "192.0.2.2:11010"),
             Source("TIGERs", "tigers-id", endpoint: "192.0.2.3:11010"), Source("ER-FORCE", "erforce-id")],
            Now,
            TimeSpan.FromSeconds(5));

        Assert.True(result.IsReady);
        Assert.Empty(result.ConflictingRoles);

        result = TrackerComparisonReadiness.Evaluate(
            [Source("ibis", "duck-id"), Source("TIGERs", "tigers-a"), Source("TIGERs", "tigers-b"), Source("ER-FORCE", "erforce-id")],
            Now,
            TimeSpan.FromSeconds(5));

        Assert.False(result.IsReady);
        Assert.Contains("TIGERs", result.ConflictingRoles);
    }

    [Fact]
    public void Evaluate_RejectsOneRoleClaimedByMultipleUuidlessEndpoints()
    {
        var result = TrackerComparisonReadiness.Evaluate(
            [Source("ibis", "duck-id"),
             Source("TIGERs", string.Empty, endpoint: "192.0.2.2:11010"),
             Source("TIGERs", string.Empty, endpoint: "192.0.2.3:11010"),
             Source("ER-FORCE", "erforce-id")],
            Now,
            TimeSpan.FromSeconds(5));

        Assert.False(result.IsReady);
        Assert.Contains("TIGERs", result.ConflictingRoles);
    }

    private static ExternalTrackerReadSideSnapshot Source(
        string sourceName,
        string uuid,
        DateTimeOffset? receivedAt = null,
        string endpoint = "192.0.2.1:11010") =>
        new(
            new TrackerWrapperPacket { SourceName = sourceName, Uuid = uuid },
            receivedAt ?? Now,
            "external",
            sourceName,
            sourceName,
            uuid,
            endpoint);
}
