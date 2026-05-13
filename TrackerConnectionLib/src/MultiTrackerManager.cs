using System.Collections.Concurrent;
using System.Net;

namespace TrackerConnectionLib;

public sealed class MultiTrackerManager<TPacket>
    where TPacket : ITrackerPacket
{
    private readonly ConcurrentDictionary<string, TrackerState<TPacket>> _trackers = new();
    private readonly string? _selfUuid;
    private readonly string? _selfSourceName;

    public MultiTrackerManager()
    {
    }

    public MultiTrackerManager(string selfUuid, string selfSourceName)
    {
        _selfUuid = selfUuid;
        _selfSourceName = selfSourceName;
    }

    public IReadOnlyDictionary<string, TrackerState<TPacket>> Trackers => _trackers;

    public string? ActiveTrackerUuid { get; private set; }

    public event Action<TrackerState<TPacket>>? TrackerUpdated;
    public event Action<TrackerState<TPacket>>? ActiveTrackerUpdated;

    public void ProcessPacket(TPacket packet)
    {
        ProcessPacket(packet, remoteEndpoint: null, DateTimeOffset.UtcNow);
    }

    public void ProcessPacket(TPacket packet, EndPoint? remoteEndpoint, DateTimeOffset receivedAt)
    {
        var now = DateTime.UtcNow;
        var sourceRole = GetSourceRole(packet);
        var sourceLabel = GetSourceLabel(packet, remoteEndpoint, sourceRole);

        var trackerKey = CreateTrackerKey(packet, remoteEndpoint);

        var state = _trackers.AddOrUpdate(
            trackerKey,
            _ => new TrackerState<TPacket>
            {
                Uuid = packet.Uuid,
                SourceName = packet.SourceName,
                RemoteEndpoint = remoteEndpoint,
                ReceivedAt = receivedAt,
                SourceRole = sourceRole,
                SourceLabel = sourceLabel,
                LastUpdateUtc = now,
                LastPacket = packet
            },
            (_, existing) =>
            {
                existing.SourceName = packet.SourceName;
                existing.RemoteEndpoint = remoteEndpoint;
                existing.ReceivedAt = receivedAt;
                existing.SourceRole = sourceRole;
                existing.SourceLabel = sourceLabel;
                existing.LastUpdateUtc = now;
                existing.LastPacket = packet;
                return existing;
            });

        ActiveTrackerUuid ??= packet.Uuid;

        TrackerUpdated?.Invoke(state);

        if (ActiveTrackerUuid == packet.Uuid)
        {
            ActiveTrackerUpdated?.Invoke(state);
        }
    }

    private string GetSourceRole(TPacket packet)
    {
        if (string.Equals(packet.Uuid, _selfUuid, StringComparison.Ordinal)
            && string.Equals(packet.SourceName, _selfSourceName, StringComparison.Ordinal))
        {
            return "own";
        }

        if (string.IsNullOrWhiteSpace(packet.Uuid)
            && string.IsNullOrWhiteSpace(packet.SourceName))
        {
            return "unknown";
        }

        return "external";
    }

    private static string GetSourceLabel(TPacket packet, EndPoint? remoteEndpoint, string sourceRole)
    {
        if (sourceRole == "unknown")
        {
            return "unknown";
        }

        if (!string.IsNullOrWhiteSpace(packet.SourceName))
        {
            return packet.SourceName;
        }

        if (!string.IsNullOrWhiteSpace(packet.Uuid))
        {
            return packet.Uuid;
        }

        return remoteEndpoint?.ToString() ?? sourceRole;
    }

    private static string CreateTrackerKey(TPacket packet, EndPoint? remoteEndpoint)
    {
        var sourceName = packet.SourceName ?? string.Empty;
        var endpoint = remoteEndpoint?.ToString() ?? string.Empty;
        return $"{packet.Uuid}\u001f{sourceName}\u001f{endpoint}";
    }

    public void SetActiveTracker(string uuid)
    {
        if (!_trackers.Values.Any(state => string.Equals(state.Uuid, uuid, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Tracker not found: {uuid}");
        }

        ActiveTrackerUuid = uuid;
    }

    public void RemoveTimedOutTrackers(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;

        foreach (var pair in _trackers)
        {
            if (now - pair.Value.LastUpdateUtc <= timeout)
            {
                continue;
            }

            _trackers.TryRemove(pair.Key, out _);
        }

        if (ActiveTrackerUuid is not null
            && !_trackers.Values.Any(state => string.Equals(state.Uuid, ActiveTrackerUuid, StringComparison.Ordinal)))
        {
            ActiveTrackerUuid = null;
        }
    }
}
