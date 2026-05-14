using TrackerConnectionLib;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// TrackerConnectionLib の live tracker packet 更新を CaptureOn session の snapshot sidecar へ記録する。
/// </summary>
public sealed class TrackerConnectionLibSnapshotRecorder : IDisposable
{
    private readonly MultiTrackerManager<TrackerPacketAdapter> manager;
    private readonly TrackerPacketSnapshotLogWriter writer;

    /// <summary>
    /// live tracker manager と snapshot writer を接続し、以後の tracker 更新を保存対象にする。
    /// </summary>
    public TrackerConnectionLibSnapshotRecorder(
        MultiTrackerManager<TrackerPacketAdapter> manager,
        TrackerPacketSnapshotLogWriter writer)
    {
        this.manager = manager;
        this.writer = writer;
        this.manager.TrackerUpdated += CaptureTrackerUpdate;
    }

    /// <summary>
    /// manager からの購読を解除する。
    /// </summary>
    public void Dispose()
    {
        manager.TrackerUpdated -= CaptureTrackerUpdate;
    }

    private void CaptureTrackerUpdate(TrackerState<TrackerPacketAdapter> state)
    {
        if (state.LastPacket is null)
        {
            return;
        }

        writer.CapturePacket(
            state.LastPacket.Packet,
            state.ReceivedAt ?? DateTimeOffset.UtcNow,
            state.RemoteEndpoint?.ToString(),
            state.SourceRole,
            state.SourceLabel);
    }
}
