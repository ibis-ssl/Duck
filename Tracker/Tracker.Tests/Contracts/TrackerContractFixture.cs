using Tracker.Core;
using Tracker.Server.Tracking;

namespace Tracker.Tests.Contracts;

public sealed class TrackerContractFixture
{
    public const string DefaultSourceName = "test-source";
    public const string DefaultUuid = "test-uuid";
    public const string DefaultProfileName = "default";
    public const long DefaultReorderWindowNs = 100_000_000;
    public const long DefaultMergeWindowNs = 20_000_000;
    public const int DefaultGeometryResetFieldLengthThresholdMm = 500;
    public const int DefaultGeometryResetFieldWidthThresholdMm = 500;

    public ITrackerEngine CreateEngine()
    {
        return new TrackerEngine();
    }

    public TrackerPacketGenerator CreatePacketGenerator(
        string sourceName = DefaultSourceName,
        string uuid = DefaultUuid)
    {
        return new TrackerPacketGenerator(sourceName, uuid);
    }

    public TrackerPublisherOptions CreatePublisherOptions(
        bool publishUdp = true,
        string multicastAddress = "224.5.23.2",
        int port = 10010,
        string sourceName = DefaultSourceName,
        string uuid = DefaultUuid)
    {
        return new TrackerPublisherOptions
        {
            PublishUdp = publishUdp,
            MulticastAddress = multicastAddress,
            Port = port,
            SourceName = sourceName,
            Uuid = uuid,
        };
    }

    public TrackerResolvedOptions CreateResolvedOptions(
        TrackerEngineSettings? engineSettings = null,
        TrackerPublisherOptions? publisherOptions = null)
    {
        return new TrackerResolvedOptions
        {
            Enabled = true,
            EngineSettings = engineSettings ?? CreateSettings(),
            PublisherOptions = publisherOptions ?? CreatePublisherOptions(),
        };
    }

    public TrackerEngineSettings CreateSettings(
        string profileName = DefaultProfileName,
        long reorderWindowNs = DefaultReorderWindowNs,
        long mergeWindowNs = DefaultMergeWindowNs,
        int geometryResetFieldLengthThresholdMm = DefaultGeometryResetFieldLengthThresholdMm,
        int geometryResetFieldWidthThresholdMm = DefaultGeometryResetFieldWidthThresholdMm,
        TrackerRobotTrackerOverrides? robotTracker = null,
        TrackerBallTrackerOverrides? ballTracker = null,
        TrackerKickDetectorOverrides? kickDetector = null)
    {
        return new TrackerEngineSettings
        {
            ProfileName = profileName,
            ReorderWindowNs = reorderWindowNs,
            MergeWindowNs = mergeWindowNs,
            GeometryResetFieldLengthThresholdMm = geometryResetFieldLengthThresholdMm,
            GeometryResetFieldWidthThresholdMm = geometryResetFieldWidthThresholdMm,
            RobotTracker = robotTracker ?? new TrackerRobotTrackerOverrides(),
            BallTracker = ballTracker ?? new TrackerBallTrackerOverrides(),
            KickDetector = kickDetector ?? new TrackerKickDetectorOverrides(),
        };
    }

    public TrackerProfileSwitchRequest CreateProfileSwitchRequest(
        int requestVersion,
        string profileName,
        TrackerEngineSettings? resolvedBaseSettings = null,
        TrackerRuntimeOverrides? runtimeOverrides = null)
    {
        return new TrackerProfileSwitchRequest
        {
            RequestVersion = requestVersion,
            ProfileName = profileName,
            ResolvedBaseSettings = resolvedBaseSettings ?? CreateSettings(profileName: profileName),
            RuntimeOverrides = runtimeOverrides ?? new TrackerRuntimeOverrides(),
        };
    }

    public TrackerFrame CreateFrame(
        uint frameNumber = 42,
        long dataTimestampNs = 1_000_000_000,
        IReadOnlyList<TrackedBallState>? balls = null,
        IReadOnlyList<TrackedRobotState>? robots = null,
        int? primaryBallTrackId = 1,
        KickEventState? kickedBall = null)
    {
        return new TrackerFrame
        {
            FrameNumber = frameNumber,
            DataTimestampNs = dataTimestampNs,
            ProcessedAtNs = dataTimestampNs + 1_000_000,
            Balls = balls ?? [],
            Robots = robots ?? [],
            PrimaryBallTrackId = primaryBallTrackId,
            KickedBall = kickedBall,
        };
    }

    public TrackedBallState CreateTrackedBall(
        int trackId,
        double xMm = 0,
        double yMm = 0,
        double zMm = 0,
        double vxMmPerS = 0,
        double vyMmPerS = 0,
        double vzMmPerS = 0,
        float visibility = 1.0f,
        long lastVisibleTimestampNs = 1_000_000_000,
        bool isFlying = false)
    {
        return new TrackedBallState
        {
            InternalTrackId = trackId,
            XMm = xMm,
            YMm = yMm,
            ZMm = zMm,
            VXMmPerS = vxMmPerS,
            VYMmPerS = vyMmPerS,
            VZMmPerS = vzMmPerS,
            Visibility = visibility,
            LastVisibleTimestampNs = lastVisibleTimestampNs,
            IsFlying = isFlying,
        };
    }

    public KickEventState CreateKick(
        bool isStillMoving,
        double startXMm = 100,
        double startYMm = 200,
        long startTimestampNs = 9_500_000_000,
        double initialVelocityXMmPerS = 3000,
        double initialVelocityYMmPerS = 1500,
        double initialVelocityZMmPerS = 0,
        double? stopXMm = 900,
        double? stopYMm = 1000,
        long? stopTimestampNs = 12_000_000_000,
        uint? kickerRobotId = null)
    {
        return new KickEventState
        {
            StartXMm = startXMm,
            StartYMm = startYMm,
            StartTimestampNs = startTimestampNs,
            InitialVelocityXMmPerS = initialVelocityXMmPerS,
            InitialVelocityYMmPerS = initialVelocityYMmPerS,
            InitialVelocityZMmPerS = initialVelocityZMmPerS,
            StopXMm = stopXMm,
            StopYMm = stopYMm,
            StopTimestampNs = stopTimestampNs,
            IsStillMoving = isStillMoving,
            BallTrackId = 10,
            KickerRobotId = kickerRobotId,
        };
    }
}
