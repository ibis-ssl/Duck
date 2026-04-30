namespace TrackerConnectionLib;

public sealed class TrackerWrapperPacketDeserializer
    : ITrackerDeserializer<TrackerPacketAdapter>
{
    public bool TryDeserialize(
        ReadOnlySpan<byte> data,
        out TrackerPacketAdapter? packet)
    {
        try
        {
            var raw = TrackerWrapperPacket.Parser.ParseFrom(data);
            packet = new TrackerPacketAdapter(raw);
            return !string.IsNullOrWhiteSpace(packet.Uuid);
        }
        catch
        {
            packet = null;
            return false;
        }
    }
}