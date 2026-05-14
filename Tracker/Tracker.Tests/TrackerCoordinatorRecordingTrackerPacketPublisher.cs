using Tracker.Core;
using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TrackerCoordinator test が publish された tracker packet を記録するための shared support double を提供する。
/// </summary>
internal sealed class TrackerCoordinatorRecordingTrackerPacketPublisher : ITrackerPacketPublisher
{
    public TrackerPublisherOptions CurrentOptions { get; private set; } = new();

    public List<TrackerWrapperPacket> Packets { get; } = [];

    public List<int> PublishedPorts { get; } = [];

    public void ApplyConfiguration(TrackerPublisherOptions options)
    {
        CurrentOptions = new TrackerPublisherOptions
        {
            PublishUdp = options.PublishUdp,
            MulticastAddress = options.MulticastAddress,
            Port = options.Port,
            SourceName = options.SourceName,
            Uuid = options.Uuid,
        };
    }

    public void Publish(TrackerWrapperPacket packet)
    {
        Packets.Add(packet.Clone());
        PublishedPorts.Add(CurrentOptions.Port);
    }
}
