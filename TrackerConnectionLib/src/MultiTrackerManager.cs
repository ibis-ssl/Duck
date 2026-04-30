using System.Collections.Concurrent;

namespace TrackerConnectionLib;

public sealed class MultiTrackerManager<TPacket>
    where TPacket : ITrackerPacket
{
    private readonly ConcurrentDictionary<string, TrackerState<TPacket>> _trackers = new();

    public IReadOnlyDictionary<string, TrackerState<TPacket>> Trackers => _trackers;

    public string? ActiveTrackerUuid { get; private set; }

    public event Action<TrackerState<TPacket>>? TrackerUpdated;
    public event Action<TrackerState<TPacket>>? ActiveTrackerUpdated;

    public void ProcessPacket(TPacket packet)
    {
        var now = DateTime.UtcNow;

        var state = _trackers.AddOrUpdate(
            packet.Uuid,
            uuid => new TrackerState<TPacket>
            {
                Uuid = uuid,
                SourceName = packet.SourceName,
                LastUpdateUtc = now,
                LastPacket = packet
            },
            (_, existing) =>
            {
                existing.SourceName = packet.SourceName;
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

    public void SetActiveTracker(string uuid)
    {
        if (!_trackers.ContainsKey(uuid))
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

            if (ActiveTrackerUuid == pair.Key)
            {
                ActiveTrackerUuid = null;
            }
        }
    }
}