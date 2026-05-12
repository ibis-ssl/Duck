namespace TrackerConnectionLib;

public sealed class TrackerState<TPacket>
    where TPacket : ITrackerPacket
{
    public required string Uuid { get; init; }
    public string? SourceName { get; set; }
    public System.Net.EndPoint? RemoteEndpoint { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public string SourceRole { get; set; } = "unknown";
    public string SourceLabel { get; set; } = "unknown";
    public DateTime LastUpdateUtc { get; set; }
    public TPacket? LastPacket { get; set; }
}
