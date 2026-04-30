namespace Tracker.Server.Vision;

public sealed record VisionPacketSnapshot(
    SSL_WrapperPacket? LatestPacket,
    SSL_DetectionFrame? Detection,
    SSL_GeometryData? Geometry,
    long PacketCount,
    long ErrorCount,
    string? RemoteEndpoint,
    DateTimeOffset? ReceivedAt,
    string? LastError);
