namespace Tracker.DebugHost.Vision;

public sealed record VisionPacketSnapshot(
    SSL_WrapperPacket? LatestPacket,
    SSL_DetectionFrame? Detection,
    IReadOnlyList<VisionCameraSnapshot> Cameras,
    VisionAggregateDetectionSnapshot AggregateDetection,
    SSL_GeometryData? Geometry,
    long PacketCount,
    long ErrorCount,
    string? RemoteEndpoint,
    DateTimeOffset? ReceivedAt,
    string? LastError)
{
    public static VisionPacketSnapshot Empty { get; } = new(
        null,
        null,
        Array.Empty<VisionCameraSnapshot>(),
        VisionAggregateDetectionSnapshot.Empty,
        null,
        0,
        0,
        null,
        null,
        null);
}
