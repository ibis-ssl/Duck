using Tracker.Server.Components.Vision;
using Tracker.Server.Tracking;

namespace Tracker.Tests;

public class TrackerProfileControlViewStateTests
{
    [Fact]
    public void FromOptions_UsesTrackedSnapshotActiveProfileAndMarksItSelected()
    {
        var options = new TrackerOptions
        {
            ActiveProfileName = "default",
            Profiles = new Dictionary<string, TrackerProfileOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new(),
                ["fast"] = new(),
            },
        };
        var snapshot = new TrackedSnapshot(
            LatestFrame: null,
            ReceivedAt: null,
            ActiveProfileName: "fast",
            PublishSuccessCount: 0,
            PublishFailureCount: 0);

        var viewState = TrackerProfileControlViewState.FromOptions(options, snapshot);

        Assert.Equal("fast", viewState.ActiveProfileName);
        Assert.Equal(["default", "fast"], viewState.Profiles.Select(profile => profile.Name));
        Assert.False(viewState.Profiles[0].IsActive);
        Assert.True(viewState.Profiles[1].IsActive);
    }

    [Fact]
    public void FromOptions_WithoutProfiles_ReturnsConfiguredActiveProfileOnly()
    {
        var options = new TrackerOptions
        {
            ActiveProfileName = "default",
            Profiles = new Dictionary<string, TrackerProfileOptions>(StringComparer.OrdinalIgnoreCase),
        };
        var snapshot = new TrackedSnapshot(
            LatestFrame: null,
            ReceivedAt: null,
            ActiveProfileName: "default",
            PublishSuccessCount: 0,
            PublishFailureCount: 0);

        var viewState = TrackerProfileControlViewState.FromOptions(options, snapshot);

        var profile = Assert.Single(viewState.Profiles);
        Assert.Equal("default", profile.Name);
        Assert.True(profile.IsActive);
    }
}
