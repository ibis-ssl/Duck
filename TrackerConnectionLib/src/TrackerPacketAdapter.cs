namespace TrackerConnectionLib;

public sealed class TrackerPacketAdapter : ITrackerPacket
{
    public TrackerPacketAdapter(TrackerWrapperPacket packet)
    {
        Packet = packet;
    }

    public TrackerWrapperPacket Packet { get; }

    public string Uuid => Packet.Uuid;

    public string? SourceName => Packet.SourceName;
}