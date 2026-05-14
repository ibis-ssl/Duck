using TrackerConnectionLib;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// live 3rd party tracker state を UI read-side 用の immutable snapshot として保持する。
/// </summary>
public sealed class ExternalTrackerSnapshotStore : IDisposable
{
    private readonly object gate = new();
    private readonly MultiTrackerManager<TrackerPacketAdapter> manager;
    private readonly Dictionary<string, ExternalTrackerReadSideSnapshot> snapshots = [];

    /// <summary>
    /// live tracker manager の更新を read-side snapshot store に接続する。
    /// </summary>
    public ExternalTrackerSnapshotStore(MultiTrackerManager<TrackerPacketAdapter> manager)
    {
        this.manager = manager;
        foreach (var state in manager.Trackers.Values)
        {
            CaptureTrackerUpdate(state);
        }

        this.manager.TrackerUpdated += CaptureTrackerUpdate;
    }

    /// <summary>
    /// 現在の external tracker snapshot を clone 済み DTO として返す。
    /// </summary>
    public IReadOnlyList<ExternalTrackerReadSideSnapshot> GetSnapshot()
    {
        lock (gate)
        {
            return snapshots.Values
                .OrderBy(snapshot => snapshot.SourceLabel, StringComparer.Ordinal)
                .ThenBy(snapshot => snapshot.RemoteEndpoint, StringComparer.Ordinal)
                .Select(snapshot => snapshot with
                {
                    Packet = snapshot.Packet.Clone(),
                })
                .ToArray();
        }
    }

    /// <inheritdoc />
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

        var packet = state.LastPacket.Packet.Clone();
        var sourceUuid = packet.Uuid ?? state.Uuid ?? string.Empty;
        var sourceName = packet.SourceName ?? state.SourceName ?? string.Empty;
        var remoteEndpoint = state.RemoteEndpoint?.ToString() ?? string.Empty;
        var snapshot = new ExternalTrackerReadSideSnapshot(
            packet,
            state.ReceivedAt,
            state.SourceRole,
            state.SourceLabel,
            sourceName,
            sourceUuid,
            remoteEndpoint);
        var key = CreateStoreKey(sourceUuid, sourceName, remoteEndpoint);

        lock (gate)
        {
            snapshots[key] = snapshot;
        }
    }

    private static string CreateStoreKey(string sourceUuid, string sourceName, string remoteEndpoint)
    {
        return string.Join('\u001f', sourceUuid, sourceName, remoteEndpoint);
    }
}

/// <summary>
/// live display が参照する 3rd party tracker の read-side snapshot。
/// </summary>
public sealed record ExternalTrackerReadSideSnapshot(
    TrackerWrapperPacket Packet,
    DateTimeOffset? ReceivedAt,
    string SourceRole,
    string SourceLabel,
    string SourceName,
    string SourceUuid,
    string RemoteEndpoint);
