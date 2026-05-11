namespace Tracker.Core;

/// <summary>
/// event-time buffer、camera-local tracking、world frame commit を統括する Core tracker engine。
/// </summary>
public sealed partial class TrackerEngine : ITrackerEngine
{
    private const double BallTrackMatchDistanceMm = 120d;
    private const double BallMergeDistanceMm = 120d;
    private const double RobotTrackMovementGateMm = 120d;
    private const double RobotRadiusMm = 90d;
    private const double RobotCloseDuplicateDistanceMm = RobotRadiusMm * 1.5d;
    private const double BallRadiusMm = 21.5d;
    private const int BallGrownUpObservationCount = 3;
    private const double KickStillMovingSpeedThresholdMmPerS = 400d;
    private const int KickStillMovingGraceFrames = 2;
    private const long RecentContactWindowNs = 200_000_000;
    private const double DefaultVisibilityHalfLifeSeconds = 1d;
    private const double DefaultBallProcessNoise = 50d;
    private readonly List<BufferedDetection> pendingDetections = [];
    private readonly Dictionary<int, BallTrackState> cameraBallTrackStates = [];
    private readonly Dictionary<int, BallContactState> latestBallContactStates = [];
    private readonly Dictionary<int, BallLeftFieldState> latestBallLeftFieldStates = [];
    private readonly Dictionary<int, MergedBallIdentityState> mergedBallIdentityStates = [];
    private readonly Dictionary<CameraRobotKey, RobotTrackState> cameraRobotTrackStates = [];
    private TrackerGeometrySnapshot? geometrySnapshot;
    private KickEventState? activeKickState;
    private TrackedBallState? lastCommittedPrimaryBall;
    private string activeProfileName = "default";
    private int kickBelowStillMovingThresholdFrameCount;
    private int nextCameraBallTrackId = 1;
    private int nextMergedBallTrackId = 1;
    private uint nextCommittedFrameNumber = 1;
    private long? maxSeenDetectionTimestampNs;
    private long? lastCommittedGroupCloseTimestampNs;

    /// <summary>
    /// 入力 packet と任意の profile switch request を処理し、確定済み frame と event を返す。
    /// </summary>
    public TrackerUpdateResult Update(
        SSL_WrapperPacket? packet,
        TrackerEngineSettings settings,
        TrackerProfileSwitchRequest? profileSwitchRequest = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var effectiveSettings = settings;
        var emittedEvents = new List<TrackerEvent>();
        var latePacketDropCount = 0;
        var forceFlushBufferedGroups = false;

        if (profileSwitchRequest is not null)
        {
            effectiveSettings = profileSwitchRequest.ResolvedBaseSettings;
            activeProfileName = profileSwitchRequest.ProfileName;
            // profile switch event は state clear 後、同じ result 内の WorldFrameCommitted より前に出す。
            ClearPendingStateAndAdvanceLateCutoff(settings.MergeWindowNs);
            emittedEvents.Add(new TrackerEvent
            {
                Kind = TrackerEventKind.ProfileSwitched,
                ProfileName = activeProfileName,
            });
        }
        else
        {
            activeProfileName = settings.ProfileName;
        }

        if (packet?.Geometry is not null)
        {
            var updatedGeometrySnapshot = CreateGeometrySnapshot(packet.Geometry);
            if (ShouldResetForGeometryChange(updatedGeometrySnapshot, effectiveSettings))
            {
                // geometry 大変更では pending detection と tracking state を捨て、frame number は維持する。
                ClearPendingStateAndAdvanceLateCutoff(effectiveSettings.MergeWindowNs);
                emittedEvents.Add(new TrackerEvent
                {
                    Kind = TrackerEventKind.GeometryReset,
                    ProfileName = activeProfileName,
                });
                forceFlushBufferedGroups = true;
            }

            geometrySnapshot = updatedGeometrySnapshot;
        }

        if (packet?.Detection is not null)
        {
            var bufferedDetection = CreateBufferedDetection(packet.Detection);

            if (lastCommittedGroupCloseTimestampNs is not null
                && bufferedDetection.EventTimestampNs <= lastCommittedGroupCloseTimestampNs.Value)
            {
                latePacketDropCount++;
            }
            else
            {
                pendingDetections.Add(bufferedDetection);
                maxSeenDetectionTimestampNs = maxSeenDetectionTimestampNs is null
                    ? bufferedDetection.EventTimestampNs
                    : Math.Max(maxSeenDetectionTimestampNs.Value, bufferedDetection.EventTimestampNs);
            }
        }

        var committedFrames = FlushCommittedFrames(effectiveSettings, emittedEvents, forceFlushBufferedGroups);

        return new TrackerUpdateResult
        {
            CommittedFrames = committedFrames,
            EmittedEvents = emittedEvents,
            Diagnostics = new TrackerEngineDiagnostics
            {
                LatePacketDropCount = latePacketDropCount,
            },
        };
    }
}
