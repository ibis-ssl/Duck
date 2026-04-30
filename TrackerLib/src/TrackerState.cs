namespace TrackerLib;
public sealed class TrackerState<TPacket>
where TPacket : ITrackerPacket
{
    public required string Uuid { get; init; }
    public string? SourceName { get; set; }
    public DateTime LastUpdateUtc { get; set; }
    public TPacket? LastPacket { get; set; }
}