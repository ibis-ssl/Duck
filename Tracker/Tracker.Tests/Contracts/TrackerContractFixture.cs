using Tracker.Core;

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

    public TrackerEngineSettings CreateSettings(
        string profileName = DefaultProfileName,
        long reorderWindowNs = DefaultReorderWindowNs,
        long mergeWindowNs = DefaultMergeWindowNs,
        int geometryResetFieldLengthThresholdMm = DefaultGeometryResetFieldLengthThresholdMm,
        int geometryResetFieldWidthThresholdMm = DefaultGeometryResetFieldWidthThresholdMm)
    {
        return new TrackerEngineSettings
        {
            ProfileName = profileName,
            ReorderWindowNs = reorderWindowNs,
            MergeWindowNs = mergeWindowNs,
            GeometryResetFieldLengthThresholdMm = geometryResetFieldLengthThresholdMm,
            GeometryResetFieldWidthThresholdMm = geometryResetFieldWidthThresholdMm,
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

    public KickEventState CreateKick(bool isStillMoving)
    {
        return new KickEventState
        {
            StartXMm = 100,
            StartYMm = 200,
            StartTimestampNs = 9_500_000_000,
            InitialVelocityXMmPerS = 3000,
            InitialVelocityYMmPerS = 1500,
            InitialVelocityZMmPerS = 0,
            StopXMm = 900,
            StopYMm = 1000,
            StopTimestampNs = 12_000_000_000,
            IsStillMoving = isStillMoving,
            BallTrackId = 10,
        };
    }
}
