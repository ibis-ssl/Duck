namespace TrackerLib;
public interface ITrackerPacket
{
    string Uuid { get; }
    string? SourceName { get; }
}