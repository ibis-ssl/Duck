namespace Tracker.DebugHost.Vision;

public sealed record VisionCameraSnapshot(
    uint CameraId,
    SSL_WrapperPacket? LatestPacket,
    SSL_DetectionFrame Detection,
    string? RemoteEndpoint,
    DateTimeOffset? ReceivedAt);
