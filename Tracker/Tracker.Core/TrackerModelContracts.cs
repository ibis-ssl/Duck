namespace Tracker.Core;

public sealed class TrackerFrame
{
    public uint FrameNumber { get; init; }

    public long DataTimestampNs { get; init; }

    public long ProcessedAtNs { get; init; }

    public object? GeometrySnapshot { get; init; }

    public IReadOnlyList<TrackedBallState> Balls { get; init; } = [];

    public int? PrimaryBallTrackId { get; init; }

    public IReadOnlyList<TrackedRobotState> Robots { get; init; } = [];

    public KickEventState? KickedBall { get; init; }

    public object? LatestContact { get; init; }

    public BallLeftFieldState? BallLeftField { get; init; }

    public TrackerFrameMetadata Metadata { get; init; } = new();
}

public sealed class TrackedBallState
{
    public int InternalTrackId { get; init; }

    public double XMm { get; init; }

    public double YMm { get; init; }

    public double ZMm { get; init; }

    public double VXMmPerS { get; init; }

    public double VYMmPerS { get; init; }

    public double VZMmPerS { get; init; }

    public float Visibility { get; init; }

    public IReadOnlyList<uint> SourceCameraIds { get; init; } = [];

    public bool IsFlying { get; init; }

    public long LastVisibleTimestampNs { get; init; }

    public double Quality { get; init; }
}

public sealed class TrackedRobotState
{
    public TrackerTeam Team { get; init; }

    public uint RobotId { get; init; }

    public double XMm { get; init; }

    public double YMm { get; init; }

    public double OrientationRad { get; init; }

    public double VXMmPerS { get; init; }

    public double VYMmPerS { get; init; }

    public double AngularVelocityRadPerS { get; init; }

    public float Visibility { get; init; }

    public double Quality { get; init; }

    public bool HasBallContact { get; init; }
}

public sealed class KickEventState
{
    public double StartXMm { get; init; }

    public double StartYMm { get; init; }

    public long StartTimestampNs { get; init; }

    public double InitialVelocityXMmPerS { get; init; }

    public double InitialVelocityYMmPerS { get; init; }

    public double InitialVelocityZMmPerS { get; init; }

    public int BallTrackId { get; init; }

    public double LatestSpeedMmPerS { get; init; }

    public long LatestUpdateTimestampNs { get; init; }

    public double? StopXMm { get; init; }

    public double? StopYMm { get; init; }

    public long? StopTimestampNs { get; init; }

    public uint? KickerRobotId { get; init; }

    public string? KickKind { get; init; }

    public bool IsStillMoving { get; init; }
}

public sealed class BallLeftFieldState
{
    public bool IsOutOfField { get; init; }

    public string? BoundaryName { get; init; }

    public double CrossingXMm { get; init; }

    public double CrossingYMm { get; init; }

    public long CrossingTimestampNs { get; init; }
}

public sealed class TrackerFrameMetadata
{
    public string? SourceName { get; init; }

    public string? ProfileName { get; init; }
}

public enum TrackerTeam
{
    Unknown = 0,
    Yellow = 1,
    Blue = 2,
}
