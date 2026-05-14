using Tracker.DebugHost.Components.Vision;
using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

public class TrackerProfileControlViewStateTests
{
    /// <summary>
    /// 何を確認しているか: snapshot の active profile が view state の選択状態として優先されること。
    /// </summary>
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

    /// <summary>
    /// 何を確認しているか: profile 一覧が空の場合でも設定済み active profile だけを表示できること。
    /// </summary>
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

    /// <summary>
    /// 何を確認しているか: TrackedSnapshotStore が configured initial active profile を初期 snapshot に保持すること。
    /// </summary>
    [Fact]
    public void TrackedSnapshotStore_UsesConfiguredInitialActiveProfile()
    {
        var store = new TrackedSnapshotStore("sim");

        var snapshot = store.GetSnapshot();

        Assert.Equal("sim", snapshot.ActiveProfileName);
        Assert.Null(snapshot.LatestFrame);
        Assert.Null(snapshot.ReceivedAt);
    }
}
