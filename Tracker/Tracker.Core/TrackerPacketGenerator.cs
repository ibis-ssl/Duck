namespace Tracker.Core;

public sealed class TrackerPacketGenerator
{
    public TrackerPacketGenerator(string sourceName, string uuid)
    {
        SourceName = sourceName;
        Uuid = uuid;
    }

    public string SourceName { get; }

    public string Uuid { get; }

    public TrackerWrapperPacket Generate(TrackerFrame frame)
    {
        throw new NotImplementedException();
    }
}
