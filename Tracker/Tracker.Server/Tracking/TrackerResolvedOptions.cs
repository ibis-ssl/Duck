using Tracker.Core;

namespace Tracker.Server.Tracking;

public sealed class TrackerResolvedOptions
{
    public bool Enabled { get; init; }

    public TrackerEngineSettings EngineSettings { get; init; } = new();

    public TrackerPublisherOptions PublisherOptions { get; init; } = new();
}
