namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// reorder window と merge window から publish 可能な detection group を確定し、world frame に変換する。
    /// </summary>
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
            // max seen event time から ReorderWindow を引いた時刻までの group だけを flush する。
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

    /// <summary>
    /// profile switch や geometry reset 時に pending state を clear し、late packet cutoff を前へ進める。
    /// </summary>
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

    /// <summary>
    /// 1 つの merge group から tracked world frame と関連 event を組み立てる。
    /// </summary>
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
        var previousPrimaryBall = lastCommittedPrimaryBall;
        var observedBallTrackIds = UpdateCameraBallTrackStates(settings, orderedDetections, frameTimestampNs);
        var ballCandidates = AssignMergedBallIdentity(settings, CollectMergedBallStates(settings, observedBallTrackIds))
            .Where(ballEntry => PassesOutputVisibility(ballEntry.Visibility, GetBallOutputVisibilityThreshold(settings)))
            .Select(ballEntry => new BallOutputCandidate(ballEntry, CreateTrackedBall(ballEntry)))
            .ToList();
        // 前回 primary ball の継続を secondary sort より先に優先する。
        ballCandidates.Sort(
            (left, right) =>
            {
                var leftPrimaryComparison = IsFreshPreviousPrimaryBall(left, previousPrimaryBall)
                    .CompareTo(IsFreshPreviousPrimaryBall(right, previousPrimaryBall));
                return leftPrimaryComparison != 0
                    ? -leftPrimaryComparison
                    : TrackedBallComparer.Instance.Compare(left.TrackedBall, right.TrackedBall);
            });

        for (var index = 0; index < ballCandidates.Count; index++)
        {
            var ballCandidate = ballCandidates[index];
            if (index > 0
                && (!ballCandidate.MergedBall.HasFreshObservation
                    || ballCandidate.MergedBall.ObservationCount < BallGrownUpObservationCount))
            {
                continue;
            }

            balls.Add(ballCandidate.TrackedBall);
        }

        var observedCameraRobotKeys = UpdateCameraRobotTrackStates(settings, orderedDetections, frameTimestampNs);
        foreach (var robotEntry in CollectMergedRobotStates(observedCameraRobotKeys))
        {
            var trackedRobot = CreateTrackedRobot(robotEntry.Key, robotEntry.Value);
            if (!PassesOutputVisibility(trackedRobot.Visibility, GetRobotOutputVisibilityThreshold(settings)))
            {
                continue;
            }

            robots.Add(trackedRobot);
        }

        robots.Sort(TrackedRobotComparer.Instance);

        var primaryBall = balls.FirstOrDefault();
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
            SourceDetections = CreateSourceDetectionFrames(orderedDetections),
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
}
