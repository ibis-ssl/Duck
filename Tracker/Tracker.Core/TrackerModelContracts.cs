namespace Tracker.Core;

public sealed class TrackerFrame
{
    public uint FrameNumber { get; init; }

    public long DataTimestampNs { get; init; }

    public long ProcessedAtNs { get; init; }

    public TrackerGeometrySnapshot? GeometrySnapshot { get; init; }

    public IReadOnlyList<TrackedBallState> Balls { get; init; } = [];

    public int? PrimaryBallTrackId { get; init; }

    public IReadOnlyList<TrackedRobotState> Robots { get; init; } = [];

    public KickEventState? KickedBall { get; init; }

    public BallContactState? LatestContact { get; init; }

    public BallLeftFieldState? BallLeftField { get; init; }

    public TrackerFrameMetadata Metadata { get; init; } = new();

    public IReadOnlyList<TrackerSourceDetectionFrame> SourceDetections { get; init; } = [];
}

public sealed class TrackerGeometrySnapshot
{
    public int FieldLengthMm { get; init; }

    public int FieldWidthMm { get; init; }

    public int GoalWidthMm { get; init; }

    public int GoalDepthMm { get; init; }

    public int BoundaryWidthMm { get; init; }

    public int BoundaryWidthGoalLineMm { get; init; }

    public int PenaltyAreaDepthMm { get; init; }

    public int PenaltyAreaWidthMm { get; init; }

    public int CenterCircleRadiusMm { get; init; }

    public int LineThicknessMm { get; init; }

    public IReadOnlyList<TrackerGeometryLineSegment> FieldLines { get; init; } = [];

    public IReadOnlyList<TrackerGeometryCircularArc> FieldArcs { get; init; } = [];
}

public sealed class TrackerGeometryLineSegment
{
    public string Name { get; init; } = string.Empty;

    public double P1XMm { get; init; }

    public double P1YMm { get; init; }

    public double P2XMm { get; init; }

    public double P2YMm { get; init; }

    public double ThicknessMm { get; init; }

    public SSL_FieldShapeType Type { get; init; } = SSL_FieldShapeType.Undefined;
}

public sealed class TrackerGeometryCircularArc
{
    public string Name { get; init; } = string.Empty;

    public double CenterXMm { get; init; }

    public double CenterYMm { get; init; }

    public double RadiusMm { get; init; }

    public double A1Rad { get; init; }

    public double A2Rad { get; init; }

    public double ThicknessMm { get; init; }

    public SSL_FieldShapeType Type { get; init; } = SSL_FieldShapeType.Undefined;
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

public sealed class BallContactState
{
    public bool IsInContact { get; init; }

    public uint? ContactingRobotId { get; init; }

    public TrackerTeam ContactingTeam { get; init; }

    public uint? LastRobotId { get; init; }

    public TrackerTeam LastTeam { get; init; }

    public long LastContactTimestampNs { get; init; }
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

public sealed class TrackerSourceDetectionFrame
{
    public uint SourceFrameNumber { get; init; }

    public uint CameraId { get; init; }

    public long EventTimestampNs { get; init; }

    public IReadOnlyList<SSL_DetectionBall> Balls { get; init; } = [];

    public IReadOnlyList<SSL_DetectionRobot> RobotsYellow { get; init; } = [];

    public IReadOnlyList<SSL_DetectionRobot> RobotsBlue { get; init; } = [];
}

public enum TrackerTeam
{
    Unknown = 0,
    Yellow = 1,
    Blue = 2,
}
