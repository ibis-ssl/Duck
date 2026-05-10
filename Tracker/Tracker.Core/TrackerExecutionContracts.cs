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
    private const double BallTrackMatchDistanceMm = 120d;
    private const double BallMergeDistanceMm = 120d;
    private const double RobotTrackMovementGateMm = 120d;
    private const double RobotRadiusMm = 90d;
    private const double BallRadiusMm = 21.5d;
    private const double KickStillMovingSpeedThresholdMmPerS = 400d;
    private const int KickStillMovingGraceFrames = 2;
    private const long RecentContactWindowNs = 200_000_000;
    private const double DefaultVisibilityHalfLifeSeconds = 1d;
    private const double DefaultBallProcessNoise = 50d;
    private const double InitialVelocityVariance = 1_000_000d;
    private const double KalmanProcessNoiseScale = 10_000_000d;
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

    private List<TrackerFrame> FlushCommittedFrames(
        TrackerEngineSettings settings,
        List<TrackerEvent> emittedEvents,
        bool forceFlushBufferedGroups)
    {
        if (maxSeenDetectionTimestampNs is null || pendingDetections.Count == 0)
        {
            return [];
        }

        var bufferedGroups = BuildDetectionGroups(
            pendingDetections
                .OrderBy(detection => detection.EventTimestampNs)
                .ThenBy(detection => detection.CameraId)
                .ThenBy(detection => detection.SourceFrameNumber)
                .ToList(),
            settings.MergeWindowNs);

        var flushableGroupCount = forceFlushBufferedGroups ? bufferedGroups.Count : 0;
        if (!forceFlushBufferedGroups)
        {
            var flushCutoffTimestampNs = maxSeenDetectionTimestampNs.Value - settings.ReorderWindowNs;
            while (flushableGroupCount < bufferedGroups.Count
                && bufferedGroups[flushableGroupCount].CloseTimestampNs <= flushCutoffTimestampNs)
            {
                flushableGroupCount++;
            }
        }

        if (flushableGroupCount == 0)
        {
            return [];
        }

        pendingDetections.Clear();
        foreach (var group in bufferedGroups.Skip(flushableGroupCount))
        {
            pendingDetections.AddRange(group.Detections);
        }

        var committedFrames = new List<TrackerFrame>();
        foreach (var group in bufferedGroups.Take(flushableGroupCount))
        {
            CommitGroup(group, settings, committedFrames, emittedEvents);
        }

        return committedFrames;
    }

    private void ClearPendingStateAndAdvanceLateCutoff(long mergeWindowNs)
    {
        if (pendingDetections.Count > 0)
        {
            var latestBufferedGroupCloseTimestampNs = BuildDetectionGroups(
                    pendingDetections
                        .OrderBy(detection => detection.EventTimestampNs)
                        .ThenBy(detection => detection.CameraId)
                        .ThenBy(detection => detection.SourceFrameNumber)
                        .ToList(),
                    mergeWindowNs)
                .Select(group => group.CloseTimestampNs)
                .DefaultIfEmpty()
                .Max();

            lastCommittedGroupCloseTimestampNs = lastCommittedGroupCloseTimestampNs is null
                ? latestBufferedGroupCloseTimestampNs
                : Math.Max(lastCommittedGroupCloseTimestampNs.Value, latestBufferedGroupCloseTimestampNs);
        }

        pendingDetections.Clear();
        maxSeenDetectionTimestampNs = null;
        cameraBallTrackStates.Clear();
        latestBallContactStates.Clear();
        latestBallLeftFieldStates.Clear();
        mergedBallIdentityStates.Clear();
        cameraRobotTrackStates.Clear();
        activeKickState = null;
        lastCommittedPrimaryBall = null;
        kickBelowStillMovingThresholdFrameCount = 0;
        nextCameraBallTrackId = 1;
        nextMergedBallTrackId = 1;
    }

    private void CommitGroup(
        BufferedDetectionGroup group,
        TrackerEngineSettings settings,
        List<TrackerFrame> committedFrames,
        List<TrackerEvent> emittedEvents)
    {
        var orderedDetections = group.Detections;
        var frameTimestampNs = group.AnchorTimestampNs;
        var processedAtNs = GetCurrentUnixTimeNanoseconds();

        var balls = new List<TrackedBallState>();
        var robots = new List<TrackedRobotState>();
        var observedBallTrackIds = UpdateCameraBallTrackStates(settings, orderedDetections, frameTimestampNs);
        foreach (var ballEntry in AssignMergedBallIdentity(settings, CollectMergedBallStates(settings, observedBallTrackIds)))
        {
            balls.Add(CreateTrackedBall(ballEntry));
        }

        balls.Sort(TrackedBallComparer.Instance);

        var observedCameraRobotKeys = UpdateCameraRobotTrackStates(settings, orderedDetections, frameTimestampNs);
        foreach (var robotEntry in CollectMergedRobotStates(observedCameraRobotKeys))
        {
            robots.Add(CreateTrackedRobot(robotEntry.Key, robotEntry.Value));
        }

        robots.Sort(TrackedRobotComparer.Instance);

        var primaryBall = balls.FirstOrDefault();
        var previousPrimaryBall = lastCommittedPrimaryBall;
        var previousContactState = primaryBall is not null
            && latestBallContactStates.TryGetValue(primaryBall.InternalTrackId, out var currentPrimaryContactState)
            ? currentPrimaryContactState
            : null;
        var previousLeftFieldState = primaryBall is not null
            && latestBallLeftFieldStates.TryGetValue(primaryBall.InternalTrackId, out var currentPrimaryLeftFieldState)
            ? currentPrimaryLeftFieldState
            : null;
        var freshRobotKeys = observedCameraRobotKeys
            .Select(key => new RobotKey(key.Team, key.RobotId))
            .ToHashSet();
        var contactState = CreateBallContactState(settings, primaryBall, robots, freshRobotKeys, frameTimestampNs, previousContactState);
        robots = ApplyBallContactFlags(robots, contactState);
        UpdateLatestBallContactState(primaryBall, contactState);
        PruneLatestBallContactStates(balls);
        var ballLeftFieldState = CreateBallLeftFieldState(primaryBall, previousPrimaryBall, geometrySnapshot, previousLeftFieldState);
        UpdateLatestBallLeftFieldState(primaryBall, ballLeftFieldState);
        PruneLatestBallLeftFieldStates(balls);

        var kickUpdate = UpdateKickState(settings, primaryBall, previousPrimaryBall, previousContactState, contactState, frameTimestampNs);
        activeKickState = kickUpdate.KickState;
        lastCommittedPrimaryBall = primaryBall;

        var committedFrame = new TrackerFrame
        {
            FrameNumber = nextCommittedFrameNumber++,
            DataTimestampNs = frameTimestampNs,
            ProcessedAtNs = processedAtNs,
            GeometrySnapshot = geometrySnapshot,
            Balls = balls,
            PrimaryBallTrackId = balls.Count > 0 ? balls[0].InternalTrackId : null,
            Robots = robots,
            KickedBall = activeKickState,
            LatestContact = contactState,
            BallLeftField = ballLeftFieldState,
            Metadata = new TrackerFrameMetadata
            {
                ProfileName = activeProfileName,
            },
        };

        committedFrames.Add(committedFrame);
        emittedEvents.Add(new TrackerEvent
        {
            Kind = TrackerEventKind.WorldFrameCommitted,
            FrameNumber = committedFrame.FrameNumber,
            ProfileName = activeProfileName,
        });
        if (kickUpdate.KickDetected)
        {
            emittedEvents.Add(new TrackerEvent
            {
                Kind = TrackerEventKind.KickDetected,
                FrameNumber = committedFrame.FrameNumber,
                ProfileName = activeProfileName,
            });
        }

        if (DidBallContactChange(previousContactState, contactState))
        {
            emittedEvents.Add(new TrackerEvent
            {
                Kind = TrackerEventKind.ContactChanged,
                FrameNumber = committedFrame.FrameNumber,
                ProfileName = activeProfileName,
            });
        }

        if (DidBallLeaveField(previousLeftFieldState, ballLeftFieldState))
        {
            emittedEvents.Add(new TrackerEvent
            {
                Kind = TrackerEventKind.BallLeftField,
                FrameNumber = committedFrame.FrameNumber,
                ProfileName = activeProfileName,
            });
        }

        lastCommittedGroupCloseTimestampNs = group.CloseTimestampNs;
    }

    private static BallContactState? CreateBallContactState(
        TrackerEngineSettings settings,
        TrackedBallState? primaryBall,
        IReadOnlyList<TrackedRobotState> robots,
        ISet<RobotKey> freshRobotKeys,
        long frameTimestampNs,
        BallContactState? previousContactState)
    {
        var contactMarginMm = GetContactMarginMm(settings);
        var currentContact = primaryBall is null
            ? null
            : robots
                .Where(robot => freshRobotKeys.Contains(new RobotKey(robot.Team, robot.RobotId)))
                .Select(
                    robot => new
                    {
                        Robot = robot,
                        DistanceMm = GetDistanceMm(robot.XMm, robot.YMm, primaryBall.XMm, primaryBall.YMm),
                    })
                .Where(candidate => candidate.DistanceMm <= RobotRadiusMm + BallRadiusMm + contactMarginMm)
                .OrderBy(candidate => candidate.DistanceMm)
                .ThenBy(candidate => candidate.Robot.Team)
                .ThenBy(candidate => candidate.Robot.RobotId)
                .FirstOrDefault();
        if (currentContact is not null)
        {
            return new BallContactState
            {
                IsInContact = true,
                ContactingRobotId = currentContact.Robot.RobotId,
                ContactingTeam = currentContact.Robot.Team,
                LastRobotId = currentContact.Robot.RobotId,
                LastTeam = currentContact.Robot.Team,
                LastContactTimestampNs = frameTimestampNs,
            };
        }

        if (previousContactState is null)
        {
            return null;
        }

        return new BallContactState
        {
            IsInContact = false,
            ContactingRobotId = null,
            ContactingTeam = TrackerTeam.Unknown,
            LastRobotId = previousContactState.LastRobotId,
            LastTeam = previousContactState.LastTeam,
            LastContactTimestampNs = previousContactState.LastContactTimestampNs,
        };
    }

    private static List<TrackedRobotState> ApplyBallContactFlags(
        IReadOnlyList<TrackedRobotState> robots,
        BallContactState? contactState)
    {
        return robots
            .Select(
                robot => new TrackedRobotState
                {
                    Team = robot.Team,
                    RobotId = robot.RobotId,
                    XMm = robot.XMm,
                    YMm = robot.YMm,
                    OrientationRad = robot.OrientationRad,
                    VXMmPerS = robot.VXMmPerS,
                    VYMmPerS = robot.VYMmPerS,
                    AngularVelocityRadPerS = robot.AngularVelocityRadPerS,
                    Visibility = robot.Visibility,
                    Quality = robot.Quality,
                    HasBallContact = contactState is not null
                        && contactState.IsInContact
                        && contactState.ContactingTeam == robot.Team
                        && contactState.ContactingRobotId == robot.RobotId,
                })
            .ToList();
    }

    private void UpdateLatestBallContactState(
        TrackedBallState? primaryBall,
        BallContactState? contactState)
    {
        if (primaryBall is null)
        {
            return;
        }

        if (contactState is null)
        {
            latestBallContactStates.Remove(primaryBall.InternalTrackId);
            return;
        }

        latestBallContactStates[primaryBall.InternalTrackId] = contactState;
    }

    private void PruneLatestBallContactStates(IReadOnlyList<TrackedBallState> balls)
    {
        var activeTrackIds = balls
            .Select(ball => ball.InternalTrackId)
            .ToHashSet();
        foreach (var staleTrackId in latestBallContactStates.Keys.Except(activeTrackIds).ToList())
        {
            latestBallContactStates.Remove(staleTrackId);
        }
    }

    private static BallLeftFieldState? CreateBallLeftFieldState(
        TrackedBallState? primaryBall,
        TrackedBallState? previousPrimaryBall,
        TrackerGeometrySnapshot? currentGeometrySnapshot,
        BallLeftFieldState? previousLeftFieldState)
    {
        if (primaryBall is null || currentGeometrySnapshot is null || !IsBallOutOfField(primaryBall, currentGeometrySnapshot))
        {
            return null;
        }

        if (previousLeftFieldState?.IsOutOfField == true)
        {
            return previousLeftFieldState;
        }

        var crossing = ProjectBallCrossing(primaryBall, previousPrimaryBall, currentGeometrySnapshot);
        return new BallLeftFieldState
        {
            IsOutOfField = true,
            BoundaryName = crossing.BoundaryName,
            CrossingXMm = crossing.CrossingXMm,
            CrossingYMm = crossing.CrossingYMm,
            CrossingTimestampNs = crossing.CrossingTimestampNs,
        };
    }

    private void UpdateLatestBallLeftFieldState(
        TrackedBallState? primaryBall,
        BallLeftFieldState? ballLeftFieldState)
    {
        if (primaryBall is null)
        {
            return;
        }

        if (ballLeftFieldState is null)
        {
            latestBallLeftFieldStates.Remove(primaryBall.InternalTrackId);
            return;
        }

        latestBallLeftFieldStates[primaryBall.InternalTrackId] = ballLeftFieldState;
    }

    private void PruneLatestBallLeftFieldStates(IReadOnlyList<TrackedBallState> balls)
    {
        var activeTrackIds = balls
            .Select(ball => ball.InternalTrackId)
            .ToHashSet();
        foreach (var staleTrackId in latestBallLeftFieldStates.Keys.Except(activeTrackIds).ToList())
        {
            latestBallLeftFieldStates.Remove(staleTrackId);
        }
    }

    private static bool DidBallLeaveField(
        BallLeftFieldState? previousLeftFieldState,
        BallLeftFieldState? currentLeftFieldState)
    {
        return currentLeftFieldState?.IsOutOfField == true
            && previousLeftFieldState?.IsOutOfField != true;
    }

    private (KickEventState? KickState, bool KickDetected) UpdateKickState(
        TrackerEngineSettings settings,
        TrackedBallState? primaryBall,
        TrackedBallState? previousPrimaryBall,
        BallContactState? previousContactState,
        BallContactState? currentContactState,
        long frameTimestampNs)
    {
        var detectedKick = TryCreateKickEventState(settings, primaryBall, previousPrimaryBall, previousContactState, currentContactState, frameTimestampNs);
        if (detectedKick is not null)
        {
            kickBelowStillMovingThresholdFrameCount = 0;
            return (detectedKick, true);
        }

        if (activeKickState is null || primaryBall is null || activeKickState.BallTrackId != primaryBall.InternalTrackId)
        {
            kickBelowStillMovingThresholdFrameCount = 0;
            return (null, false);
        }

        var planarSpeedMmPerS = GetPlanarSpeedMmPerS(primaryBall);
        var isStillMoving = planarSpeedMmPerS >= KickStillMovingSpeedThresholdMmPerS;
        if (!isStillMoving)
        {
            kickBelowStillMovingThresholdFrameCount++;
            if (kickBelowStillMovingThresholdFrameCount < KickStillMovingGraceFrames)
            {
                isStillMoving = true;
            }
        }
        else
        {
            kickBelowStillMovingThresholdFrameCount = 0;
        }

        return (new KickEventState
        {
            StartXMm = activeKickState.StartXMm,
            StartYMm = activeKickState.StartYMm,
            StartTimestampNs = activeKickState.StartTimestampNs,
            InitialVelocityXMmPerS = activeKickState.InitialVelocityXMmPerS,
            InitialVelocityYMmPerS = activeKickState.InitialVelocityYMmPerS,
            InitialVelocityZMmPerS = activeKickState.InitialVelocityZMmPerS,
            BallTrackId = activeKickState.BallTrackId,
            LatestSpeedMmPerS = planarSpeedMmPerS,
            LatestUpdateTimestampNs = frameTimestampNs,
            StopXMm = isStillMoving ? null : primaryBall.XMm,
            StopYMm = isStillMoving ? null : primaryBall.YMm,
            StopTimestampNs = isStillMoving ? null : frameTimestampNs,
            KickerRobotId = activeKickState.KickerRobotId,
            KickKind = activeKickState.KickKind,
            IsStillMoving = isStillMoving,
        }, false);
    }

    private static bool DidBallContactChange(
        BallContactState? previousContactState,
        BallContactState? currentContactState)
    {
        if (previousContactState is null || currentContactState is null)
        {
            return previousContactState is not null || currentContactState is not null;
        }

        return previousContactState.IsInContact != currentContactState.IsInContact
            || previousContactState.ContactingTeam != currentContactState.ContactingTeam
            || previousContactState.ContactingRobotId != currentContactState.ContactingRobotId;
    }

    private static KickEventState? TryCreateKickEventState(
        TrackerEngineSettings settings,
        TrackedBallState? primaryBall,
        TrackedBallState? previousPrimaryBall,
        BallContactState? previousContactState,
        BallContactState? currentContactState,
        long frameTimestampNs)
    {
        if (primaryBall is null)
        {
            return null;
        }

        var planarSpeedMmPerS = GetPlanarSpeedMmPerS(primaryBall);
        var kickDetectionSpeedThresholdMmPerS = GetKickDetectionSpeedThresholdMmPerS(settings);
        if (planarSpeedMmPerS < kickDetectionSpeedThresholdMmPerS)
        {
            return null;
        }

        var previousPlanarSpeedMmPerS = previousPrimaryBall is not null
            && previousPrimaryBall.InternalTrackId == primaryBall.InternalTrackId
            ? GetPlanarSpeedMmPerS(previousPrimaryBall)
            : 0d;
        if (previousPlanarSpeedMmPerS >= kickDetectionSpeedThresholdMmPerS)
        {
            return null;
        }

        var recentContact = SelectRecentContact(previousContactState, currentContactState, frameTimestampNs);
        if (recentContact?.LastRobotId is null)
        {
            return null;
        }

        var startBall = previousPrimaryBall is not null && previousPrimaryBall.InternalTrackId == primaryBall.InternalTrackId
            ? previousPrimaryBall
            : primaryBall;

        return new KickEventState
        {
            StartXMm = startBall.XMm,
            StartYMm = startBall.YMm,
            StartTimestampNs = startBall.LastVisibleTimestampNs,
            InitialVelocityXMmPerS = primaryBall.VXMmPerS,
            InitialVelocityYMmPerS = primaryBall.VYMmPerS,
            InitialVelocityZMmPerS = primaryBall.VZMmPerS,
            BallTrackId = primaryBall.InternalTrackId,
            LatestSpeedMmPerS = planarSpeedMmPerS,
            LatestUpdateTimestampNs = frameTimestampNs,
            KickerRobotId = recentContact.LastRobotId,
            KickKind = IsChipKick(settings, primaryBall) ? "chip" : "flat",
            IsStillMoving = true,
        };
    }

    private static BallContactState? SelectRecentContact(
        BallContactState? previousContactState,
        BallContactState? currentContactState,
        long frameTimestampNs)
    {
        return new BallContactState?[] { currentContactState, previousContactState }
            .Where(state => state?.LastRobotId is not null)
            .Cast<BallContactState>()
            .Where(state => frameTimestampNs - state.LastContactTimestampNs <= RecentContactWindowNs)
            .OrderByDescending(state => state.LastContactTimestampNs)
            .FirstOrDefault();
    }

    private static double GetPlanarSpeedMmPerS(TrackedBallState ball)
    {
        return Math.Sqrt((ball.VXMmPerS * ball.VXMmPerS) + (ball.VYMmPerS * ball.VYMmPerS));
    }

    private static bool IsChipKick(TrackerEngineSettings settings, TrackedBallState ball)
    {
        var chipHeightThresholdMm = GetChipHeightThresholdMm(settings);
        return ball.ZMm >= chipHeightThresholdMm || ball.VZMmPerS >= chipHeightThresholdMm;
    }

    private static bool IsBallOutOfField(TrackedBallState ball, TrackerGeometrySnapshot geometrySnapshot)
    {
        var halfFieldLengthMm = geometrySnapshot.FieldLengthMm / 2d;
        var halfFieldWidthMm = geometrySnapshot.FieldWidthMm / 2d;
        return Math.Abs(ball.YMm) > halfFieldWidthMm || Math.Abs(ball.XMm) > halfFieldLengthMm;
    }

    private static (string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs) ProjectBallCrossing(
        TrackedBallState ball,
        TrackedBallState? previousPrimaryBall,
        TrackerGeometrySnapshot geometrySnapshot)
    {
        var hasPreviousPoint = previousPrimaryBall is not null && previousPrimaryBall.InternalTrackId == ball.InternalTrackId;
        if (hasPreviousPoint && previousPrimaryBall is not null)
        {
            var firstCrossing = TryProjectFirstPerimeterCrossing(ball, previousPrimaryBall, geometrySnapshot);
            if (firstCrossing is not null)
            {
                return firstCrossing.Value;
            }
        }

        var fallbackBoundaryName = ClassifyBoundaryNameFromCurrentPosition(ball, geometrySnapshot);
        return fallbackBoundaryName switch
        {
            "touch-line" => ProjectTouchLineCrossing(ball, previousPrimaryBall, geometrySnapshot, hasPreviousPoint),
            _ => ProjectGoalLineCrossing(ball, previousPrimaryBall, geometrySnapshot, hasPreviousPoint, fallbackBoundaryName),
        };
    }

    private static string ClassifyBoundaryNameFromCurrentPosition(TrackedBallState ball, TrackerGeometrySnapshot geometrySnapshot)
    {
        var halfGoalWidthMm = geometrySnapshot.GoalWidthMm / 2d;
        if (Math.Abs(ball.YMm) > geometrySnapshot.FieldWidthMm / 2d)
        {
            return "touch-line";
        }

        return Math.Abs(ball.YMm) <= halfGoalWidthMm
            ? "goal-interior"
            : "goal-line";
    }

    private static (string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs)? TryProjectFirstPerimeterCrossing(
        TrackedBallState ball,
        TrackedBallState previousPrimaryBall,
        TrackerGeometrySnapshot geometrySnapshot)
    {
        var halfFieldLengthMm = geometrySnapshot.FieldLengthMm / 2d;
        var halfFieldWidthMm = geometrySnapshot.FieldWidthMm / 2d;
        var halfGoalWidthMm = geometrySnapshot.GoalWidthMm / 2d;
        var candidates = new List<(double Ratio, string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs)>();

        if (Math.Abs(ball.XMm) > halfFieldLengthMm && Math.Abs(ball.XMm - previousPrimaryBall.XMm) >= double.Epsilon)
        {
            var boundaryXMm = Math.Sign(ball.XMm) * halfFieldLengthMm;
            var ratio = (boundaryXMm - previousPrimaryBall.XMm) / (ball.XMm - previousPrimaryBall.XMm);
            if (ratio >= 0d && ratio <= 1d)
            {
                var crossingYMm = previousPrimaryBall.YMm + ((ball.YMm - previousPrimaryBall.YMm) * ratio);
                if (Math.Abs(crossingYMm) <= halfFieldWidthMm)
                {
                    candidates.Add((
                        ratio,
                        Math.Abs(crossingYMm) <= halfGoalWidthMm ? "goal-interior" : "goal-line",
                        boundaryXMm,
                        crossingYMm,
                        InterpolateTimestamp(previousPrimaryBall.LastVisibleTimestampNs, ball.LastVisibleTimestampNs, ratio)));
                }
            }
        }

        if (Math.Abs(ball.YMm) > halfFieldWidthMm && Math.Abs(ball.YMm - previousPrimaryBall.YMm) >= double.Epsilon)
        {
            var boundaryYMm = Math.Sign(ball.YMm) * halfFieldWidthMm;
            var ratio = (boundaryYMm - previousPrimaryBall.YMm) / (ball.YMm - previousPrimaryBall.YMm);
            if (ratio >= 0d && ratio <= 1d)
            {
                var crossingXMm = previousPrimaryBall.XMm + ((ball.XMm - previousPrimaryBall.XMm) * ratio);
                if (Math.Abs(crossingXMm) <= halfFieldLengthMm)
                {
                    candidates.Add((
                        ratio,
                        "touch-line",
                        crossingXMm,
                        boundaryYMm,
                        InterpolateTimestamp(previousPrimaryBall.LastVisibleTimestampNs, ball.LastVisibleTimestampNs, ratio)));
                }
            }
        }

        return candidates.Count == 0
            ? null
            : candidates
                .OrderBy(candidate => candidate.Ratio)
                .Select(candidate => (candidate.BoundaryName, candidate.CrossingXMm, candidate.CrossingYMm, candidate.CrossingTimestampNs))
                .First();
    }

    private static (string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs) ProjectTouchLineCrossing(
        TrackedBallState ball,
        TrackedBallState? previousPrimaryBall,
        TrackerGeometrySnapshot geometrySnapshot,
        bool hasPreviousPoint)
    {
        var boundaryYMm = Math.Sign(ball.YMm) * (geometrySnapshot.FieldWidthMm / 2d);
        if (!hasPreviousPoint || previousPrimaryBall is null || Math.Abs(ball.YMm - previousPrimaryBall.YMm) < double.Epsilon)
        {
            return ("touch-line", ball.XMm, boundaryYMm, ball.LastVisibleTimestampNs);
        }

        var ratio = Math.Clamp(
            (boundaryYMm - previousPrimaryBall.YMm) / (ball.YMm - previousPrimaryBall.YMm),
            0d,
            1d);
        return (
            "touch-line",
            previousPrimaryBall.XMm + ((ball.XMm - previousPrimaryBall.XMm) * ratio),
            boundaryYMm,
            InterpolateTimestamp(previousPrimaryBall.LastVisibleTimestampNs, ball.LastVisibleTimestampNs, ratio));
    }

    private static (string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs) ProjectGoalLineCrossing(
        TrackedBallState ball,
        TrackedBallState? previousPrimaryBall,
        TrackerGeometrySnapshot geometrySnapshot,
        bool hasPreviousPoint,
        string boundaryName)
    {
        var boundaryXMm = Math.Sign(ball.XMm) * (geometrySnapshot.FieldLengthMm / 2d);
        if (!hasPreviousPoint || previousPrimaryBall is null || Math.Abs(ball.XMm - previousPrimaryBall.XMm) < double.Epsilon)
        {
            return (boundaryName, boundaryXMm, ball.YMm, ball.LastVisibleTimestampNs);
        }

        var ratio = Math.Clamp(
            (boundaryXMm - previousPrimaryBall.XMm) / (ball.XMm - previousPrimaryBall.XMm),
            0d,
            1d);
        return (
            boundaryName,
            boundaryXMm,
            previousPrimaryBall.YMm + ((ball.YMm - previousPrimaryBall.YMm) * ratio),
            InterpolateTimestamp(previousPrimaryBall.LastVisibleTimestampNs, ball.LastVisibleTimestampNs, ratio));
    }

    private static long InterpolateTimestamp(long startTimestampNs, long endTimestampNs, double ratio)
    {
        return startTimestampNs + (long)Math.Round((endTimestampNs - startTimestampNs) * ratio);
    }

    private static BufferedDetection CreateBufferedDetection(SSL_DetectionFrame detection)
    {
        return new BufferedDetection(
            detection.FrameNumber,
            detection.CameraId,
            ConvertSecondsToNanoseconds(SelectEventTimeSeconds(detection)),
            detection.Balls.ToList(),
            detection.RobotsYellow.ToList(),
            detection.RobotsBlue.ToList());
    }

    private static double SelectEventTimeSeconds(SSL_DetectionFrame detection)
    {
        return detection.TCapture > 0 ? detection.TCapture : detection.TSent;
    }

    private static List<BufferedDetectionGroup> BuildDetectionGroups(
        List<BufferedDetection> orderedDetections,
        long mergeWindowNs)
    {
        var groups = new List<BufferedDetectionGroup>();
        var currentGroup = new List<BufferedDetection>();
        var currentAnchorTimestampNs = 0L;

        foreach (var detection in orderedDetections)
        {
            if (currentGroup.Count == 0)
            {
                currentGroup.Add(detection);
                currentAnchorTimestampNs = detection.EventTimestampNs;
                continue;
            }

            if (detection.EventTimestampNs - currentAnchorTimestampNs <= mergeWindowNs)
            {
                currentGroup.Add(detection);
                continue;
            }

            groups.Add(new BufferedDetectionGroup(
                currentAnchorTimestampNs,
                currentAnchorTimestampNs + mergeWindowNs,
                [.. currentGroup]));
            currentGroup = [detection];
            currentAnchorTimestampNs = detection.EventTimestampNs;
        }

        if (currentGroup.Count > 0)
        {
            groups.Add(new BufferedDetectionGroup(
                currentAnchorTimestampNs,
                currentAnchorTimestampNs + mergeWindowNs,
                [.. currentGroup]));
        }

        return groups;
    }

    private bool ShouldResetForGeometryChange(
        TrackerGeometrySnapshot updatedGeometrySnapshot,
        TrackerEngineSettings settings)
    {
        if (geometrySnapshot is null)
        {
            return false;
        }

        return Math.Abs(updatedGeometrySnapshot.FieldLengthMm - geometrySnapshot.FieldLengthMm)
                >= settings.GeometryResetFieldLengthThresholdMm
            || Math.Abs(updatedGeometrySnapshot.FieldWidthMm - geometrySnapshot.FieldWidthMm)
                >= settings.GeometryResetFieldWidthThresholdMm
            || updatedGeometrySnapshot.GoalWidthMm != geometrySnapshot.GoalWidthMm
            || updatedGeometrySnapshot.GoalDepthMm != geometrySnapshot.GoalDepthMm;
    }

    private static TrackerGeometrySnapshot CreateGeometrySnapshot(SSL_GeometryData geometry)
    {
        var field = geometry.Field;
        return new TrackerGeometrySnapshot
        {
            FieldLengthMm = field?.FieldLength ?? 0,
            FieldWidthMm = field?.FieldWidth ?? 0,
            GoalWidthMm = field?.GoalWidth ?? 0,
            GoalDepthMm = field?.GoalDepth ?? 0,
            BoundaryWidthMm = field?.BoundaryWidth ?? 0,
            BoundaryWidthGoalLineMm = field is not null && field.BoundaryWidthGoalLine > 0
                ? field.BoundaryWidthGoalLine
                : field?.BoundaryWidth ?? 0,
            LineThicknessMm = field is not null && field.LineThickness > 0
                ? field.LineThickness
                : 10,
        };
    }

    private HashSet<int> UpdateCameraBallTrackStates(
        TrackerEngineSettings settings,
        IReadOnlyList<BufferedDetection> orderedDetections,
        long frameTimestampNs)
    {
        var observedTrackIds = new HashSet<int>();

        foreach (var cameraGroup in orderedDetections
                     .GroupBy(detection => detection.CameraId)
                     .OrderBy(group => group.Key))
        {
            var availableTracks = cameraBallTrackStates.Values
                .Where(track => track.CameraId == cameraGroup.Key)
                .ToDictionary(track => track.LocalTrackId);

            foreach (var detection in cameraGroup.OrderBy(detection => detection.EventTimestampNs).ThenBy(detection => detection.SourceFrameNumber))
            {
                var claimedTrackIds = new HashSet<int>();
                var observations = detection.Balls
                    .Select(
                        ball => new BallObservation(
                            detection.CameraId,
                            detection.EventTimestampNs,
                            ball.X,
                            ball.Y,
                            ball.Z,
                            ball.Confidence))
                    .OrderByDescending(observation => observation.Confidence)
                    .ToList();

                foreach (var observation in observations)
                {
                    var matchedTrack = availableTracks.Values
                        .Where(track => !claimedTrackIds.Contains(track.LocalTrackId))
                        .Select(
                            track => new
                            {
                                Track = track,
                                PredictedTrack = PredictBallTrackState(settings, track, observation.EventTimestampNs),
                            })
                        .Select(
                            candidate => new
                            {
                                candidate.Track,
                                DistanceMm = GetDistanceMm(candidate.PredictedTrack.XMm, candidate.PredictedTrack.YMm, observation.XMm, observation.YMm),
                            })
                        .Where(candidate => candidate.DistanceMm <= GetBallTrackMatchDistanceMm(settings))
                        .OrderBy(candidate => candidate.DistanceMm)
                        .ThenBy(candidate => candidate.Track.LocalTrackId)
                        .FirstOrDefault();

                    BallTrackState updatedTrackState;
                    if (matchedTrack is null)
                    {
                        updatedTrackState = new BallTrackState(
                            nextCameraBallTrackId++,
                            observation.CameraId,
                            CreateInitialKalmanAxis(observation.XMm, GetObservedBallUncertaintyMm(settings, observation.Confidence)),
                            CreateInitialKalmanAxis(observation.YMm, GetObservedBallUncertaintyMm(settings, observation.Confidence)),
                            CreateInitialKalmanAxis(observation.ZMm, GetObservedBallUncertaintyMm(settings, observation.Confidence)),
                            observation.EventTimestampNs,
                            observation.EventTimestampNs,
                            observation.Confidence,
                            observation.Confidence);
                    }
                    else
                    {
                        updatedTrackState = CreateObservedBallTrackState(settings, matchedTrack.Track, observation);
                    }

                    claimedTrackIds.Add(updatedTrackState.LocalTrackId);
                    availableTracks[updatedTrackState.LocalTrackId] = updatedTrackState;
                    cameraBallTrackStates[updatedTrackState.LocalTrackId] = updatedTrackState;
                    observedTrackIds.Add(updatedTrackState.LocalTrackId);
                }
            }
        }

        foreach (var existingEntry in cameraBallTrackStates.ToList())
        {
            if (observedTrackIds.Contains(existingEntry.Key))
            {
                continue;
            }

            var predictedState = CreatePredictedBallTrackState(settings, existingEntry.Value, frameTimestampNs);
            if (predictedState is null || predictedState.Visibility <= 0.01f)
            {
                cameraBallTrackStates.Remove(existingEntry.Key);
                continue;
            }

            cameraBallTrackStates[existingEntry.Key] = predictedState;
        }

        return observedTrackIds;
    }

    private static BallTrackState CreateObservedBallTrackState(
        TrackerEngineSettings settings,
        BallTrackState previousState,
        BallObservation observation)
    {
        var predictedState = PredictBallTrackState(settings, previousState, observation.EventTimestampNs);
        var deltaSeconds = GetPredictionDeltaSeconds(previousState.LastUpdateTimestampNs, observation.EventTimestampNs);
        var measurementVariance = GetObservedBallUncertaintyMm(settings, observation.Confidence);

        return predictedState with
        {
            XAxis = UpdateKalmanAxis(predictedState.XAxis, previousState.XAxis.Position, observation.XMm, deltaSeconds, measurementVariance),
            YAxis = UpdateKalmanAxis(predictedState.YAxis, previousState.YAxis.Position, observation.YMm, deltaSeconds, measurementVariance),
            ZAxis = UpdateKalmanAxis(predictedState.ZAxis, previousState.ZAxis.Position, observation.ZMm, deltaSeconds, measurementVariance),
            LastVisibleTimestampNs = observation.EventTimestampNs,
            LastUpdateTimestampNs = observation.EventTimestampNs,
            Visibility = observation.Confidence,
            Quality = observation.Confidence,
        };
    }

    private static BallTrackState? CreatePredictedBallTrackState(
        TrackerEngineSettings settings,
        BallTrackState previousState,
        long frameTimestampNs)
    {
        if (frameTimestampNs <= previousState.LastUpdateTimestampNs)
        {
            return previousState;
        }

        var ballTrackLifetimeNs = GetBallTrackLifetimeNs(settings);
        if (ballTrackLifetimeNs is not null && frameTimestampNs - previousState.LastVisibleTimestampNs > ballTrackLifetimeNs.Value)
        {
            return null;
        }

        var visibilityHalfLifeSeconds = GetBallVisibilityHalfLifeSeconds(settings);
        var predictedState = PredictBallTrackState(settings, previousState, frameTimestampNs);

        return predictedState with
        {
            LastUpdateTimestampNs = frameTimestampNs,
            Visibility = ComputeDecayVisibility(previousState.Visibility, GetPredictionDeltaSeconds(previousState.LastUpdateTimestampNs, frameTimestampNs), visibilityHalfLifeSeconds),
            Quality = ComputeDecayQuality(previousState.Quality, GetPredictionDeltaSeconds(previousState.LastUpdateTimestampNs, frameTimestampNs), visibilityHalfLifeSeconds),
        };
    }

    private static BallTrackState PredictBallTrackState(
        TrackerEngineSettings settings,
        BallTrackState previousState,
        long targetTimestampNs)
    {
        var deltaSeconds = GetPredictionDeltaSeconds(previousState.LastUpdateTimestampNs, targetTimestampNs);
        if (deltaSeconds <= 0d)
        {
            return previousState;
        }

        var processNoise = GetBallProcessNoise(settings);
        return previousState with
        {
            XAxis = PredictKalmanAxis(previousState.XAxis, deltaSeconds, processNoise),
            YAxis = PredictKalmanAxis(previousState.YAxis, deltaSeconds, processNoise),
            ZAxis = PredictKalmanAxis(previousState.ZAxis, deltaSeconds, processNoise),
            LastUpdateTimestampNs = targetTimestampNs,
        };
    }

    private List<MergedBallState> CollectMergedBallStates(
        TrackerEngineSettings settings,
        HashSet<int> observedBallTrackIds)
    {
        var freshStates = cameraBallTrackStates.Values
            .Where(state => observedBallTrackIds.Contains(state.LocalTrackId))
            .OrderBy(state => state.LocalTrackId)
            .ToList();
        var staleStates = cameraBallTrackStates.Values
            .Where(state => !observedBallTrackIds.Contains(state.LocalTrackId))
            .OrderBy(state => state.LocalTrackId)
            .ToList();
        var clusters = BuildBallClusters(settings, freshStates);
        var freshClusterCount = clusters.Count;

        foreach (var staleState in staleStates)
        {
            var nearbyFreshClusterExists = clusters.Any(
                cluster => clusters.IndexOf(cluster) < freshClusterCount
                    && CanAttachBallTrackToCluster(settings, cluster, staleState));
            if (nearbyFreshClusterExists)
            {
                continue;
            }

            var staleCluster = clusters.FirstOrDefault(
                cluster => CanAttachBallTrackToCluster(settings, cluster, staleState));
            if (staleCluster is null)
            {
                clusters.Add([staleState]);
                continue;
            }

            staleCluster.Add(staleState);
        }

        var mergedStates = new List<MergedBallState>();
        foreach (var cluster in clusters)
        {
            var totalWeight = cluster.Sum(state => GetBallMergeWeight(state));
            var mergedX = cluster.Sum(state => state.XMm * GetBallMergeWeight(state)) / totalWeight;
            var mergedY = cluster.Sum(state => state.YMm * GetBallMergeWeight(state)) / totalWeight;
            var mergedZ = cluster.Sum(state => state.ZMm * GetBallMergeWeight(state)) / totalWeight;
            var mergedVx = cluster.Sum(state => state.VXMmPerS * GetBallMergeWeight(state)) / totalWeight;
            var mergedVy = cluster.Sum(state => state.VYMmPerS * GetBallMergeWeight(state)) / totalWeight;
            var mergedVz = cluster.Sum(state => state.VZMmPerS * GetBallMergeWeight(state)) / totalWeight;

            mergedStates.Add(
                new MergedBallState(
                    0,
                    mergedX,
                    mergedY,
                    mergedZ,
                    mergedVx,
                    mergedVy,
                    mergedVz,
                    cluster.Average(state => state.Visibility),
                    cluster.Max(state => state.LastVisibleTimestampNs),
                    cluster.Average(state => state.Quality),
                    cluster.Select(state => state.CameraId).Distinct().OrderBy(cameraId => cameraId).ToList()));
        }

        return mergedStates;
    }

    private static List<List<BallTrackState>> BuildBallClusters(
        TrackerEngineSettings settings,
        IEnumerable<BallTrackState> states)
    {
        var clusters = new List<List<BallTrackState>>();

        foreach (var state in states)
        {
            var matchingClusters = clusters
                .Where(candidate => CanAttachBallTrackToCluster(settings, candidate, state))
                .ToList();
            if (matchingClusters.Count == 0)
            {
                clusters.Add([state]);
                continue;
            }

            var mergedCluster = matchingClusters[0];
            mergedCluster.Add(state);

            foreach (var extraCluster in matchingClusters.Skip(1))
            {
                mergedCluster.AddRange(extraCluster);
                clusters.Remove(extraCluster);
            }
        }

        return clusters;
    }

    private static bool CanAttachBallTrackToCluster(
        TrackerEngineSettings settings,
        IReadOnlyCollection<BallTrackState> cluster,
        BallTrackState candidate)
    {
        return !cluster.Any(existing => existing.CameraId == candidate.CameraId)
            && cluster.Any(
                existing => GetDistanceMm(existing.XMm, existing.YMm, candidate.XMm, candidate.YMm) <= GetBallMergeDistanceMm(settings));
    }

    private List<MergedBallState> AssignMergedBallIdentity(
        TrackerEngineSettings settings,
        List<MergedBallState> mergedStates)
    {
        var assignedStates = new List<MergedBallState>();
        var unmatchedPreviousStates = mergedBallIdentityStates.Values.ToDictionary(state => state.InternalTrackId);

        foreach (var mergedState in mergedStates.OrderBy(state => state.XMm).ThenBy(state => state.YMm))
        {
            var matchedPreviousState = unmatchedPreviousStates.Values
                .Select(
                    previousState => new
                    {
                        State = previousState,
                        DistanceMm = GetDistanceMm(
                            GetPredictedMergedBallXMm(previousState, mergedState.LastVisibleTimestampNs),
                            GetPredictedMergedBallYMm(previousState, mergedState.LastVisibleTimestampNs),
                            mergedState.XMm,
                            mergedState.YMm),
                    })
                .Where(candidate => candidate.DistanceMm <= GetBallMergeDistanceMm(settings))
                .OrderBy(candidate => candidate.DistanceMm)
                .ThenBy(candidate => candidate.State.InternalTrackId)
                .FirstOrDefault();

            var internalTrackId = matchedPreviousState?.State.InternalTrackId ?? nextMergedBallTrackId++;
            if (matchedPreviousState is not null)
            {
                unmatchedPreviousStates.Remove(matchedPreviousState.State.InternalTrackId);
            }

            assignedStates.Add(mergedState with { InternalTrackId = internalTrackId });
        }

        mergedBallIdentityStates.Clear();
        foreach (var assignedState in assignedStates)
        {
            mergedBallIdentityStates[assignedState.InternalTrackId] = new MergedBallIdentityState(
                assignedState.InternalTrackId,
                assignedState.XMm,
                assignedState.YMm,
                assignedState.VXMmPerS,
                assignedState.VYMmPerS,
                assignedState.LastVisibleTimestampNs);
        }

        return assignedStates;
    }

    private static double GetPredictedMergedBallXMm(MergedBallIdentityState state, long targetTimestampNs)
    {
        return state.XMm + (state.VXMmPerS * GetPredictionDeltaSeconds(state.LastVisibleTimestampNs, targetTimestampNs));
    }

    private static double GetPredictedMergedBallYMm(MergedBallIdentityState state, long targetTimestampNs)
    {
        return state.YMm + (state.VYMmPerS * GetPredictionDeltaSeconds(state.LastVisibleTimestampNs, targetTimestampNs));
    }

    private static double GetPredictionDeltaSeconds(long sourceTimestampNs, long targetTimestampNs)
    {
        if (targetTimestampNs <= sourceTimestampNs)
        {
            return 0d;
        }

        return (targetTimestampNs - sourceTimestampNs) / 1_000_000_000d;
    }

    private static TrackedBallState CreateTrackedBall(MergedBallState state)
    {
        return new TrackedBallState
        {
            InternalTrackId = state.InternalTrackId,
            XMm = state.XMm,
            YMm = state.YMm,
            ZMm = state.ZMm,
            VXMmPerS = state.VXMmPerS,
            VYMmPerS = state.VYMmPerS,
            VZMmPerS = state.VZMmPerS,
            Visibility = state.Visibility,
            SourceCameraIds = state.SourceCameraIds,
            LastVisibleTimestampNs = state.LastVisibleTimestampNs,
            Quality = state.Quality,
        };
    }

    private static double GetObservedBallUncertaintyMm(float confidence)
    {
        return 1d / Math.Max(0.001d, confidence);
    }

    private static double GetBallMergeWeight(BallTrackState state)
    {
        return 1d / Math.Max(0.001d, state.PositionUncertaintyMm);
    }

    private HashSet<CameraRobotKey> UpdateCameraRobotTrackStates(
        TrackerEngineSettings settings,
        IReadOnlyList<BufferedDetection> orderedDetections,
        long frameTimestampNs)
    {
        var observations = CollectCameraRobotObservations(orderedDetections);
        var observedKeys = observations.Keys.ToHashSet();

        foreach (var entry in observations)
        {
            cameraRobotTrackStates[entry.Key] = CreateObservedRobotTrackState(settings, entry.Key, entry.Value);
        }

        foreach (var existingEntry in cameraRobotTrackStates.ToList())
        {
            if (observedKeys.Contains(existingEntry.Key))
            {
                continue;
            }

            var predictedState = CreatePredictedRobotTrackState(settings, existingEntry.Value, frameTimestampNs);
            if (predictedState.Visibility <= 0.01f)
            {
                cameraRobotTrackStates.Remove(existingEntry.Key);
                continue;
            }

            cameraRobotTrackStates[existingEntry.Key] = predictedState;
        }

        return observedKeys;
    }

    private static Dictionary<CameraRobotKey, RobotObservation> CollectCameraRobotObservations(
        IReadOnlyList<BufferedDetection> orderedDetections)
    {
        var observations = new Dictionary<CameraRobotKey, RobotObservation>();

        foreach (var detection in orderedDetections)
        {
            foreach (var robot in detection.RobotsYellow)
            {
                AddRobotObservation(observations, TrackerTeam.Yellow, robot, detection.CameraId, detection.EventTimestampNs);
            }

            foreach (var robot in detection.RobotsBlue)
            {
                AddRobotObservation(observations, TrackerTeam.Blue, robot, detection.CameraId, detection.EventTimestampNs);
            }
        }

        return observations;
    }

    private static void AddRobotObservation(
        Dictionary<CameraRobotKey, RobotObservation> observations,
        TrackerTeam team,
        SSL_DetectionRobot robot,
        uint cameraId,
        long eventTimestampNs)
    {
        observations[new CameraRobotKey(cameraId, team, robot.RobotId)] =
            new RobotObservation(cameraId, robot.X, robot.Y, robot.Orientation, robot.Confidence)
            {
                EventTimestampNs = eventTimestampNs,
            };
    }

    private RobotTrackState CreateObservedRobotTrackState(
        TrackerEngineSettings settings,
        CameraRobotKey key,
        RobotObservation observation)
    {
        var previousState = cameraRobotTrackStates.GetValueOrDefault(key);
        var unwrappedOrientation = UnwrapAngleNearReference(
            observation.OrientationRad,
            previousState?.OrientationRad ?? observation.OrientationRad);

        if (previousState is null)
        {
            var measurementVariance = GetObservedRobotUncertaintyMm(settings, observation.Confidence);
            return new RobotTrackState(
                CreateInitialKalmanAxis(observation.XMm, measurementVariance),
                CreateInitialKalmanAxis(observation.YMm, measurementVariance),
                CreateInitialKalmanAxis(unwrappedOrientation, measurementVariance),
                observation.EventTimestampNs,
                observation.EventTimestampNs,
                observation.Confidence,
                observation.Confidence / GetRobotMeasurementNoise(settings));
        }

        var predictedState = PredictRobotTrackState(settings, previousState, observation.EventTimestampNs);
        var deltaSeconds = GetPredictionDeltaSeconds(previousState.LastUpdateTimestampNs, observation.EventTimestampNs);
        var distanceMm = GetDistanceMm(predictedState.XMm, predictedState.YMm, observation.XMm, observation.YMm);
        if (distanceMm > GetRobotMovementGateMm(settings))
        {
            var measurementVariance = GetObservedRobotUncertaintyMm(settings, observation.Confidence);
            return new RobotTrackState(
                CreateInitialKalmanAxis(observation.XMm, measurementVariance),
                CreateInitialKalmanAxis(observation.YMm, measurementVariance),
                CreateInitialKalmanAxis(unwrappedOrientation, measurementVariance),
                observation.EventTimestampNs,
                observation.EventTimestampNs,
                observation.Confidence,
                observation.Confidence / GetRobotMeasurementNoise(settings));
        }

        var observedMeasurementVariance = GetObservedRobotUncertaintyMm(settings, observation.Confidence);
        return predictedState with
        {
            XAxis = UpdateKalmanAxis(predictedState.XAxis, previousState.XAxis.Position, observation.XMm, deltaSeconds, observedMeasurementVariance),
            YAxis = UpdateKalmanAxis(predictedState.YAxis, previousState.YAxis.Position, observation.YMm, deltaSeconds, observedMeasurementVariance),
            OrientationAxis = UpdateKalmanAxis(
                predictedState.OrientationAxis,
                previousState.OrientationAxis.Position,
                unwrappedOrientation,
                deltaSeconds,
                observedMeasurementVariance),
            LastVisibleTimestampNs = observation.EventTimestampNs,
            LastUpdateTimestampNs = observation.EventTimestampNs,
            Visibility = observation.Confidence,
            Quality = observation.Confidence / GetRobotMeasurementNoise(settings),
        };
    }

    private static RobotTrackState CreatePredictedRobotTrackState(
        TrackerEngineSettings settings,
        RobotTrackState previousState,
        long frameTimestampNs)
    {
        if (frameTimestampNs <= previousState.LastUpdateTimestampNs)
        {
            return previousState;
        }

        var visibilityHalfLifeSeconds = GetRobotVisibilityHalfLifeSeconds(settings);
        var predictedState = PredictRobotTrackState(settings, previousState, frameTimestampNs);

        return predictedState with
        {
            LastUpdateTimestampNs = frameTimestampNs,
            Visibility = ComputeDecayVisibility(previousState.Visibility, GetPredictionDeltaSeconds(previousState.LastUpdateTimestampNs, frameTimestampNs), visibilityHalfLifeSeconds),
            Quality = ComputeDecayQuality(previousState.Quality, GetPredictionDeltaSeconds(previousState.LastUpdateTimestampNs, frameTimestampNs), visibilityHalfLifeSeconds),
        };
    }

    private static RobotTrackState PredictRobotTrackState(
        TrackerEngineSettings settings,
        RobotTrackState previousState,
        long targetTimestampNs)
    {
        var deltaSeconds = GetPredictionDeltaSeconds(previousState.LastUpdateTimestampNs, targetTimestampNs);
        if (deltaSeconds <= 0d)
        {
            return previousState;
        }

        var processNoise = GetRobotProcessNoise(settings);
        return previousState with
        {
            XAxis = PredictKalmanAxis(previousState.XAxis, deltaSeconds, processNoise),
            YAxis = PredictKalmanAxis(previousState.YAxis, deltaSeconds, processNoise),
            OrientationAxis = PredictKalmanAxis(previousState.OrientationAxis, deltaSeconds, processNoise),
            LastUpdateTimestampNs = targetTimestampNs,
        };
    }

    private Dictionary<RobotKey, List<RobotTrackState>> CollectMergedRobotStates(
        HashSet<CameraRobotKey> observedCameraRobotKeys)
    {
        var mergedStates = new Dictionary<RobotKey, List<RobotTrackState>>();

        foreach (var entry in cameraRobotTrackStates)
        {
            var robotKey = new RobotKey(entry.Key.Team, entry.Key.RobotId);
            if (!mergedStates.TryGetValue(robotKey, out var bucket))
            {
                bucket = [];
                mergedStates[robotKey] = bucket;
            }

            bucket.Add(entry.Value);
        }

        foreach (var robotKey in mergedStates.Keys.ToList())
        {
            var freshStates = observedCameraRobotKeys
                .Where(cameraKey => cameraKey.Team == robotKey.Team && cameraKey.RobotId == robotKey.RobotId)
                .Select(cameraKey => cameraRobotTrackStates[cameraKey])
                .ToList();
            if (freshStates.Count > 0)
            {
                mergedStates[robotKey] = freshStates;
            }
        }

        return mergedStates;
    }

    private static TrackedRobotState CreateTrackedRobot(
        RobotKey key,
        IReadOnlyList<RobotTrackState> states)
    {
        var totalWeight = states.Sum(GetRobotMergeWeight);
        var mergedX = states.Sum(state => state.XMm * GetRobotMergeWeight(state)) / totalWeight;
        var mergedY = states.Sum(state => state.YMm * GetRobotMergeWeight(state)) / totalWeight;
        var orientationReference = states[0].OrientationRad;
        var mergedOrientation = states
            .Sum(state => UnwrapAngleNearReference(state.OrientationRad, orientationReference) * GetRobotMergeWeight(state))
            / totalWeight;
        var mergedVx = states.Sum(state => state.VXMmPerS * GetRobotMergeWeight(state)) / totalWeight;
        var mergedVy = states.Sum(state => state.VYMmPerS * GetRobotMergeWeight(state)) / totalWeight;
        var mergedAngularVelocity = states.Sum(state => state.AngularVelocityRadPerS * GetRobotMergeWeight(state)) / totalWeight;
        var visibility = states.Average(state => state.Visibility);
        var quality = states.Average(state => state.Quality);

        return new TrackedRobotState
        {
            Team = key.Team,
            RobotId = key.RobotId,
            XMm = mergedX,
            YMm = mergedY,
            OrientationRad = NormalizeAngle(mergedOrientation),
            VXMmPerS = mergedVx,
            VYMmPerS = mergedVy,
            AngularVelocityRadPerS = mergedAngularVelocity,
            Visibility = visibility,
            Quality = quality,
        };
    }

    private static double UnwrapAngleNearReference(double angle, double reference)
    {
        var adjusted = angle;
        while (adjusted - reference > Math.PI)
        {
            adjusted -= Math.PI * 2;
        }

        while (adjusted - reference < -Math.PI)
        {
            adjusted += Math.PI * 2;
        }

        return adjusted;
    }

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle;
        while (normalized <= -Math.PI)
        {
            normalized += Math.PI * 2;
        }

        while (normalized > Math.PI)
        {
            normalized -= Math.PI * 2;
        }

        return normalized;
    }

    private static double GetDistanceMm(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
    }

    private static double GetBallTrackMatchDistanceMm(TrackerEngineSettings settings)
    {
        var gatedDistanceMm = settings.BallTracker.Gate is null
            ? BallTrackMatchDistanceMm
            : BallTrackMatchDistanceMm * settings.BallTracker.Gate.Value;
        return settings.BallTracker.OutlierLimitMm is null
            ? gatedDistanceMm
            : Math.Min(settings.BallTracker.OutlierLimitMm.Value, gatedDistanceMm);
    }

    private static double GetBallMergeDistanceMm(TrackerEngineSettings settings)
    {
        var gatedDistanceMm = settings.BallTracker.Gate is null
            ? BallMergeDistanceMm
            : BallMergeDistanceMm * settings.BallTracker.Gate.Value;
        return settings.BallTracker.OutlierLimitMm is null
            ? gatedDistanceMm
            : Math.Min(settings.BallTracker.OutlierLimitMm.Value, gatedDistanceMm);
    }

    private static long? GetBallTrackLifetimeNs(TrackerEngineSettings settings)
    {
        return settings.BallTracker.TrackLifetimeNs;
    }

    private static double GetBallMeasurementNoise(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.BallTracker.MeasurementNoise ?? 1d);
    }

    private static double GetBallProcessNoise(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.BallTracker.ProcessNoise ?? DefaultBallProcessNoise);
    }

    private static double GetBallVisibilityHalfLifeSeconds(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.BallTracker.VisibilityHalfLifeSeconds ?? DefaultVisibilityHalfLifeSeconds);
    }

    private static double GetRobotMovementGateMm(TrackerEngineSettings settings)
    {
        var gatedDistanceMm = settings.RobotTracker.Gate is null
            ? RobotTrackMovementGateMm
            : RobotTrackMovementGateMm * settings.RobotTracker.Gate.Value;
        return settings.RobotTracker.OutlierLimitMm is null
            ? gatedDistanceMm
            : Math.Min(settings.RobotTracker.OutlierLimitMm.Value, gatedDistanceMm);
    }

    private static double GetRobotMeasurementNoise(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.RobotTracker.MeasurementNoise ?? 1d);
    }

    private static double GetRobotProcessNoise(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.RobotTracker.ProcessNoise ?? DefaultBallProcessNoise);
    }

    private static double GetRobotVisibilityHalfLifeSeconds(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.RobotTracker.VisibilityHalfLifeSeconds ?? DefaultVisibilityHalfLifeSeconds);
    }

    private static float ComputeDecayVisibility(float visibility, double deltaSeconds, double halfLifeSeconds)
    {
        var decay = Math.Pow(0.5d, deltaSeconds / halfLifeSeconds);
        return (float)(visibility * decay);
    }

    private static double ComputeDecayQuality(double quality, double deltaSeconds, double halfLifeSeconds)
    {
        var decay = Math.Pow(0.5d, deltaSeconds / halfLifeSeconds);
        return quality * decay;
    }

    private static double GetObservedBallUncertaintyMm(TrackerEngineSettings settings, float confidence)
    {
        return GetBallMeasurementNoise(settings) / Math.Max(0.001d, confidence);
    }

    private static double GetObservedRobotUncertaintyMm(TrackerEngineSettings settings, float confidence)
    {
        return GetRobotMeasurementNoise(settings) / Math.Max(0.001d, confidence);
    }

    private static KalmanAxisState CreateInitialKalmanAxis(double position, double measurementVariance)
    {
        return new KalmanAxisState(
            position,
            0d,
            measurementVariance,
            InitialVelocityVariance);
    }

    private static KalmanAxisState PredictKalmanAxis(
        KalmanAxisState state,
        double deltaSeconds,
        double processNoise)
    {
        var processVariance = processNoise * KalmanProcessNoiseScale;
        return state with
        {
            Position = state.Position + state.Velocity * deltaSeconds,
            PositionVariance = state.PositionVariance
                + (deltaSeconds * deltaSeconds * state.VelocityVariance)
                + (processVariance * deltaSeconds * deltaSeconds),
            VelocityVariance = state.VelocityVariance + processVariance,
        };
    }

    private static KalmanAxisState UpdateKalmanAxis(
        KalmanAxisState predictedState,
        double previousPosition,
        double measurement,
        double deltaSeconds,
        double measurementVariance)
    {
        var innovationVariance = predictedState.PositionVariance + measurementVariance;
        if (innovationVariance <= 0d)
        {
            return predictedState;
        }

        var gain = predictedState.PositionVariance / innovationVariance;
        var observedVelocity = deltaSeconds > 0d
            ? (measurement - previousPosition) / deltaSeconds
            : predictedState.Velocity;
        if (gain >= 0.9999d)
        {
            return predictedState with
            {
                Position = measurement,
                Velocity = observedVelocity,
                PositionVariance = Math.Max(0.001d, (1d - gain) * predictedState.PositionVariance),
                VelocityVariance = Math.Max(0.001d, (1d - gain) * predictedState.VelocityVariance),
            };
        }

        return predictedState with
        {
            Position = predictedState.Position + gain * (measurement - predictedState.Position),
            Velocity = predictedState.Velocity + gain * (observedVelocity - predictedState.Velocity),
            PositionVariance = Math.Max(0.001d, (1d - gain) * predictedState.PositionVariance),
            VelocityVariance = Math.Max(0.001d, (1d - gain) * predictedState.VelocityVariance),
        };
    }

    private static double GetRobotMergeWeight(RobotTrackState state)
    {
        return 1d / Math.Max(0.001d, state.PositionUncertaintyMm);
    }

    private static double GetContactMarginMm(TrackerEngineSettings settings)
    {
        return settings.KickDetector.ContactMarginMm ?? TrackerEngineSettings.DefaultContactMarginMm;
    }

    private static double GetKickDetectionSpeedThresholdMmPerS(TrackerEngineSettings settings)
    {
        return settings.KickDetector.KickSpeedThresholdMmPerS ?? TrackerEngineSettings.DefaultKickDetectionSpeedThresholdMmPerS;
    }

    private static double GetChipHeightThresholdMm(TrackerEngineSettings settings)
    {
        return settings.KickDetector.ChipHeightThresholdMm ?? TrackerEngineSettings.DefaultChipHeightThresholdMm;
    }

    private static long ConvertSecondsToNanoseconds(double seconds)
    {
        return (long)Math.Round(seconds * 1_000_000_000d, MidpointRounding.AwayFromZero);
    }

    private static long GetCurrentUnixTimeNanoseconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
    }

    private sealed record BufferedDetection(
        uint SourceFrameNumber,
        uint CameraId,
        long EventTimestampNs,
        IReadOnlyList<SSL_DetectionBall> Balls,
        IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
        IReadOnlyList<SSL_DetectionRobot> RobotsBlue);

    private sealed record BallObservation(
        uint CameraId,
        long EventTimestampNs,
        double XMm,
        double YMm,
        double ZMm,
        float Confidence);

    private readonly record struct KalmanAxisState(
        double Position,
        double Velocity,
        double PositionVariance,
        double VelocityVariance);

    private sealed record BallTrackState(
        int LocalTrackId,
        uint CameraId,
        KalmanAxisState XAxis,
        KalmanAxisState YAxis,
        KalmanAxisState ZAxis,
        long LastVisibleTimestampNs,
        long LastUpdateTimestampNs,
        float Visibility,
        double Quality)
    {
        public double XMm => XAxis.Position;

        public double YMm => YAxis.Position;

        public double ZMm => ZAxis.Position;

        public double VXMmPerS => XAxis.Velocity;

        public double VYMmPerS => YAxis.Velocity;

        public double VZMmPerS => ZAxis.Velocity;

        public double PositionUncertaintyMm => (XAxis.PositionVariance + YAxis.PositionVariance) / 2d;
    }

    private sealed record MergedBallState(
        int InternalTrackId,
        double XMm,
        double YMm,
        double ZMm,
        double VXMmPerS,
        double VYMmPerS,
        double VZMmPerS,
        float Visibility,
        long LastVisibleTimestampNs,
        double Quality,
        IReadOnlyList<uint> SourceCameraIds);

    private sealed record MergedBallIdentityState(
        int InternalTrackId,
        double XMm,
        double YMm,
        double VXMmPerS,
        double VYMmPerS,
        long LastVisibleTimestampNs);

    private sealed record RobotKey(TrackerTeam Team, uint RobotId);

    private sealed record CameraRobotKey(uint CameraId, TrackerTeam Team, uint RobotId);

    private sealed record RobotObservation(
        uint CameraId,
        double XMm,
        double YMm,
        double OrientationRad,
        float Confidence)
    {
        public long EventTimestampNs { get; init; }
    }

    private sealed record RobotTrackState(
        KalmanAxisState XAxis,
        KalmanAxisState YAxis,
        KalmanAxisState OrientationAxis,
        long LastVisibleTimestampNs,
        long LastUpdateTimestampNs,
        float Visibility,
        double Quality)
    {
        public double XMm => XAxis.Position;

        public double YMm => YAxis.Position;

        public double OrientationRad => OrientationAxis.Position;

        public double VXMmPerS => XAxis.Velocity;

        public double VYMmPerS => YAxis.Velocity;

        public double AngularVelocityRadPerS => OrientationAxis.Velocity;

        public double PositionUncertaintyMm => (XAxis.PositionVariance + YAxis.PositionVariance) / 2d;
    }

    private sealed record BufferedDetectionGroup(
        long AnchorTimestampNs,
        long CloseTimestampNs,
        IReadOnlyList<BufferedDetection> Detections);

    private sealed class TrackedBallComparer : IComparer<TrackedBallState>
    {
        public static TrackedBallComparer Instance { get; } = new();

        public int Compare(TrackedBallState? x, TrackedBallState? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var visibilityComparison = y.Visibility.CompareTo(x.Visibility);
            if (visibilityComparison != 0)
            {
                return visibilityComparison;
            }

            var timestampComparison = y.LastVisibleTimestampNs.CompareTo(x.LastVisibleTimestampNs);
            if (timestampComparison != 0)
            {
                return timestampComparison;
            }

            return x.InternalTrackId.CompareTo(y.InternalTrackId);
        }
    }

    private sealed class TrackedRobotComparer : IComparer<TrackedRobotState>
    {
        public static TrackedRobotComparer Instance { get; } = new();

        public int Compare(TrackedRobotState? x, TrackedRobotState? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var teamComparison = x.Team.CompareTo(y.Team);
            if (teamComparison != 0)
            {
                return teamComparison;
            }

            return x.RobotId.CompareTo(y.RobotId);
        }
    }
}

public sealed class TrackerEngineSettings
{
    public const double DefaultKickDetectionSpeedThresholdMmPerS = 800d;
    public const double DefaultChipHeightThresholdMm = 120d;
    public const double DefaultContactMarginMm = 25d;

    public string ProfileName { get; init; } = "default";

    public long ReorderWindowNs { get; init; }

    public long MergeWindowNs { get; init; }

    public int GeometryResetFieldLengthThresholdMm { get; init; }

    public int GeometryResetFieldWidthThresholdMm { get; init; }

    public TrackerRobotTrackerOverrides RobotTracker { get; init; } = new();

    public TrackerBallTrackerOverrides BallTracker { get; init; } = new();

    public TrackerKickDetectorOverrides KickDetector { get; init; } = new()
    {
        KickSpeedThresholdMmPerS = DefaultKickDetectionSpeedThresholdMmPerS,
        ChipHeightThresholdMm = DefaultChipHeightThresholdMm,
        ContactMarginMm = DefaultContactMarginMm,
    };
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

    public double? VisibilityHalfLifeSeconds { get; init; }

    public double? Gate { get; init; }

    public double? OutlierLimitMm { get; init; }
}

public sealed class TrackerBallTrackerOverrides
{
    public double? ProcessNoise { get; init; }

    public double? MeasurementNoise { get; init; }

    public double? VisibilityHalfLifeSeconds { get; init; }

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
