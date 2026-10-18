using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

public sealed class TrackerComparisonSourceResolverTests
{
    [Theory]
    [InlineData("ibis", "Duck")]
    [InlineData("TIGERs", "TIGERs")]
    [InlineData("ER-FORCE", "ER-Force")]
    [InlineData("unknown", null)]
    public void Resolve_MapsLogicalRoleFromSourceName(string sourceName, string? expectedRole)
    {
        var result = TrackerComparisonSourceResolver.Resolve(
            sourceName,
            sourceName,
            "source-uuid",
            "192.0.2.1:11010",
            "external");

        Assert.Equal(expectedRole, result.LogicalRole);
    }

    [Fact]
    public void Resolve_PrefersUuidAndUsesEndpointWhenUuidIsMissing()
    {
        var sameUuidDifferentEndpoint = TrackerComparisonSourceResolver.Resolve(
            "TIGERs", "TIGERs", "tracker-id", "192.0.2.1:11010", "external");
        var sameUuidAnotherEndpoint = TrackerComparisonSourceResolver.Resolve(
            "TIGERs", "TIGERs", "tracker-id", "192.0.2.2:11010", "external");
        var noUuidEndpointOne = TrackerComparisonSourceResolver.Resolve(
            "TIGERs", "TIGERs", string.Empty, "192.0.2.1:11010", "external");
        var noUuidEndpointTwo = TrackerComparisonSourceResolver.Resolve(
            "TIGERs", "TIGERs", string.Empty, "192.0.2.2:11010", "external");

        Assert.Equal(sameUuidDifferentEndpoint.IdentityKey, sameUuidAnotherEndpoint.IdentityKey);
        Assert.NotEqual(noUuidEndpointOne.IdentityKey, noUuidEndpointTwo.IdentityKey);
        Assert.Equal(
            noUuidEndpointOne.IdentityKey,
            TrackerComparisonSourceResolver.Resolve(
                "TIGERs", "TIGERs", null, "192.0.2.1:11010", "external").IdentityKey);
    }

    [Fact]
    public void Resolve_UsesSameContractForLiveAndSavedSnapshotMetadata()
    {
        var live = TrackerComparisonSourceResolver.Resolve(
            "TIGERs", "TIGERs", string.Empty, "192.0.2.77:11010", "external");
        var saved = TrackerComparisonSourceResolver.Resolve(
            "TIGERs", "TIGERs", string.Empty, "192.0.2.77:11010", "external");

        Assert.Equal(live, saved);
    }
}
