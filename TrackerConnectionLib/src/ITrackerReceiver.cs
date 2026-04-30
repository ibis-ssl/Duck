namespace TrackerConnectionLib;
public interface ITrackerReceiver<TPacket>
    where TPacket : ITrackerPacket
{
    event Action<TPacket>? PacketReceived;

    void Start();
    void Stop();
}