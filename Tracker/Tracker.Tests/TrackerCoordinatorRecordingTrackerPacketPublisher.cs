using Tracker.Core;
using Tracker.Server.Tracking;

namespace Tracker.Tests;

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
