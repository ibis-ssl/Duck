namespace TrackerConnectionLib;
public interface ITrackerDeserializer<TPacket>
    where TPacket : ITrackerPacket
{
    bool TryDeserialize(ReadOnlySpan<byte> data, out TPacket? packet);
}