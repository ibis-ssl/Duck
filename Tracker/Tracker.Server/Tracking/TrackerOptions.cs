using Tracker.Core;

namespace Tracker.Server.Tracking;

public sealed class TrackerOptions
{
    public bool Enabled { get; init; } = true;

    public bool PublishUdp { get; init; } = true;

    public string SourceName { get; init; } = "ibisduck-tracker";

    public string Uuid { get; init; } = "ibisduck-tracker";

    public string ActiveProfileName { get; init; } = "default";

    public TrackerDiagnosticsOptions Diagnostics { get; init; } = new();

    public TrackerRuntimeOverrides RuntimeOverrides { get; init; } = new();

    public Dictionary<string, TrackerProfileOptions> Profiles { get; init; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new(),
        };
}

public sealed class TrackerDiagnosticsOptions
{
    public string? FilePath { get; init; }
}

public sealed class TrackerProfileOptions
{
    public TrackerPublishProfileOptions Publish { get; init; } = new();

    public TrackerEngineProfileOptions Engine { get; init; } = new();

    public TrackerRobotTrackerOverrides RobotTracker { get; init; } = new();

    public TrackerBallTrackerOverrides BallTracker { get; init; } = new();

    public TrackerKickDetectorOverrides KickDetector { get; init; } = new();
}

public sealed class TrackerPublishProfileOptions
{
    public string MulticastAddress { get; init; } = "224.5.23.2";

    public int Port { get; init; } = 10010;
}

public sealed class TrackerEngineProfileOptions
{
    public long ReorderWindowNs { get; init; } = 100_000_000;

    public long MergeWindowNs { get; init; } = 20_000_000;

    public int GeometryResetFieldLengthThresholdMm { get; init; } = 500;

    public int GeometryResetFieldWidthThresholdMm { get; init; } = 500;
}
