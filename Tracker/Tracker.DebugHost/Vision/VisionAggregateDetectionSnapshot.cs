namespace Tracker.DebugHost.Vision;

public sealed record VisionAggregateDetectionSnapshot(
    IReadOnlyList<uint> CameraIds,
    IReadOnlyList<SSL_DetectionBall> Balls,
    IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
    IReadOnlyList<SSL_DetectionRobot> RobotsBlue)
{
    public static VisionAggregateDetectionSnapshot Empty { get; } = new(
        Array.Empty<uint>(),
        Array.Empty<SSL_DetectionBall>(),
        Array.Empty<SSL_DetectionRobot>(),
        Array.Empty<SSL_DetectionRobot>());
}
