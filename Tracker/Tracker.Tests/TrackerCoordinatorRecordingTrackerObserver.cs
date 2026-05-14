using Tracker.Core;
using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TrackerCoordinator test が observer 通知順序と snapshot 更新を記録するための shared support double を提供する。
/// </summary>
internal sealed class TrackerCoordinatorRecordingTrackerObserver : ITrackerObserver
{
    private readonly TrackedSnapshotStore snapshotStore;

    public TrackerCoordinatorRecordingTrackerObserver(TrackedSnapshotStore snapshotStore)
    {
        this.snapshotStore = snapshotStore;
    }

    public List<string> Events { get; } = [];

    public bool LatestFrameWasClearedAtGeometryReset { get; private set; }

    public bool LatestFrameWasClearedAtProfileSwitch { get; private set; }

    public void OnProfileSwitched(string profileName)
    {
        LatestFrameWasClearedAtProfileSwitch = snapshotStore.GetSnapshot().LatestFrame is null;
        Events.Add($"profile:{profileName}");
    }

    public void OnGeometryReset()
    {
        LatestFrameWasClearedAtGeometryReset = snapshotStore.GetSnapshot().LatestFrame is null;
        Events.Add("geometry-reset");
    }

    public void OnWorldFrameCommitted(TrackerFrame frame)
    {
        Events.Add($"world-frame:{frame.FrameNumber}");
    }

    public void OnKickDetected(KickEventState kick, TrackerFrame frame)
    {
        Events.Add($"kick:{frame.FrameNumber}");
    }

    public void OnContactChanged(TrackerFrame frame)
    {
        Events.Add($"contact:{frame.FrameNumber}");
    }

    public void OnBallLeftField(BallLeftFieldState state, TrackerFrame frame)
    {
        Events.Add($"left-field:{frame.FrameNumber}");
    }
}
