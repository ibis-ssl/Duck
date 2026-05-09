namespace Tracker.Core;

public interface ITrackerEngine
{
    TrackerUpdateResult Update(
        SSL_WrapperPacket? packet,
        TrackerEngineSettings settings,
        TrackerProfileSwitchRequest? profileSwitchRequest = null);
}

public sealed class TrackerEngine : ITrackerEngine
{
    public TrackerUpdateResult Update(
        SSL_WrapperPacket? packet,
        TrackerEngineSettings settings,
        TrackerProfileSwitchRequest? profileSwitchRequest = null)
    {
        throw new NotImplementedException();
    }
}

public sealed class TrackerEngineSettings
{
    public string ProfileName { get; init; } = "default";

    public long ReorderWindowNs { get; init; }

    public long MergeWindowNs { get; init; }

    public int GeometryResetFieldLengthThresholdMm { get; init; }

    public int GeometryResetFieldWidthThresholdMm { get; init; }
}

public sealed class TrackerRuntimeOverrides
{
    public TrackerPublishOverrides Publish { get; init; } = new();

    public TrackerRobotTrackerOverrides RobotTracker { get; init; } = new();

    public TrackerBallTrackerOverrides BallTracker { get; init; } = new();

    public TrackerKickDetectorOverrides KickDetector { get; init; } = new();
}

public sealed class TrackerPublishOverrides
{
    public string? MulticastAddress { get; init; }

    public int? Port { get; init; }

    public string? SourceName { get; init; }

    public string? Uuid { get; init; }
}

public sealed class TrackerRobotTrackerOverrides
{
    public double? ProcessNoise { get; init; }

    public double? MeasurementNoise { get; init; }

    public double? Gate { get; init; }

    public double? OutlierLimitMm { get; init; }
}

public sealed class TrackerBallTrackerOverrides
{
    public double? ProcessNoise { get; init; }

    public double? MeasurementNoise { get; init; }

    public double? Gate { get; init; }

    public double? OutlierLimitMm { get; init; }

    public long? TrackLifetimeNs { get; init; }
}

public sealed class TrackerKickDetectorOverrides
{
    public double? KickSpeedThresholdMmPerS { get; init; }

    public double? ChipHeightThresholdMm { get; init; }

    public double? ContactMarginMm { get; init; }
}

public sealed class TrackerProfileSwitchRequest
{
    public int RequestVersion { get; init; }

    public string ProfileName { get; init; } = "default";

    public TrackerEngineSettings ResolvedBaseSettings { get; init; } = new();

    public TrackerRuntimeOverrides RuntimeOverrides { get; init; } = new();
}

public sealed class TrackerUpdateResult
{
    public IReadOnlyList<TrackerFrame> CommittedFrames { get; init; } = [];

    public IReadOnlyList<TrackerEvent> EmittedEvents { get; init; } = [];

    public TrackerEngineDiagnostics Diagnostics { get; init; } = new();
}

public sealed class TrackerEngineDiagnostics
{
    public int LatePacketDropCount { get; init; }
}

public sealed class TrackerEvent
{
    public TrackerEventKind Kind { get; init; }

    public uint? FrameNumber { get; init; }

    public string? ProfileName { get; init; }
}

public enum TrackerEventKind
{
    ProfileSwitched = 1,
    GeometryReset = 2,
    WorldFrameCommitted = 3,
    KickDetected = 4,
    ContactChanged = 5,
    BallLeftField = 6,
}

public interface ITrackerObserver
{
    void OnProfileSwitched(string profileName);

    void OnGeometryReset();

    void OnWorldFrameCommitted(TrackerFrame frame);

    void OnKickDetected(KickEventState kick, TrackerFrame frame);

    void OnContactChanged(TrackerFrame frame);

    void OnBallLeftField(BallLeftFieldState state, TrackerFrame frame);
}
