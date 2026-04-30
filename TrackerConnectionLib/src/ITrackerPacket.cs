namespace TrackerConnectionLib;
public interface ITrackerPacket
{
    string Uuid { get; }
    string? SourceName { get; }
}