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
    private readonly List<BufferedDetection> pendingDetections = [];
    private readonly Dictionary<int, BallTrackState> cameraBallTrackStates = [];
    private readonly Dictionary<int, MergedBallIdentityState> mergedBallIdentityStates = [];
    private readonly Dictionary<CameraRobotKey, RobotTrackState> cameraRobotTrackStates = [];
    private TrackerGeometrySnapshot? geometrySnapshot;
    private string activeProfileName = "default";
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
            CommitGroup(group, committedFrames, emittedEvents);
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
        mergedBallIdentityStates.Clear();
        cameraRobotTrackStates.Clear();
        nextCameraBallTrackId = 1;
        nextMergedBallTrackId = 1;
    }

    private void CommitGroup(
        BufferedDetectionGroup group,
        List<TrackerFrame> committedFrames,
        List<TrackerEvent> emittedEvents)
    {
        var orderedDetections = group.Detections;
        var frameTimestampNs = group.AnchorTimestampNs;
        var processedAtNs = GetCurrentUnixTimeNanoseconds();

        var balls = new List<TrackedBallState>();
        var robots = new List<TrackedRobotState>();
        var observedBallTrackIds = UpdateCameraBallTrackStates(orderedDetections, frameTimestampNs);
        foreach (var ballEntry in AssignMergedBallIdentity(CollectMergedBallStates(observedBallTrackIds)))
        {
            balls.Add(CreateTrackedBall(ballEntry));
        }

        balls.Sort(TrackedBallComparer.Instance);

        var observedCameraRobotKeys = UpdateCameraRobotTrackStates(orderedDetections, frameTimestampNs);
        foreach (var robotEntry in CollectMergedRobotStates(observedCameraRobotKeys))
        {
            robots.Add(CreateTrackedRobot(robotEntry.Key, robotEntry.Value));
        }

        robots.Sort(TrackedRobotComparer.Instance);

        var committedFrame = new TrackerFrame
        {
            FrameNumber = nextCommittedFrameNumber++,
            DataTimestampNs = frameTimestampNs,
            ProcessedAtNs = processedAtNs,
            GeometrySnapshot = geometrySnapshot,
            Balls = balls,
            PrimaryBallTrackId = balls.Count > 0 ? balls[0].InternalTrackId : null,
            Robots = robots,
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

        lastCommittedGroupCloseTimestampNs = group.CloseTimestampNs;
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
                                DistanceMm = GetDistanceMm(track.XMm, track.YMm, observation.XMm, observation.YMm),
                            })
                        .Where(candidate => candidate.DistanceMm <= BallTrackMatchDistanceMm)
                        .OrderBy(candidate => candidate.DistanceMm)
                        .ThenBy(candidate => candidate.Track.LocalTrackId)
                        .FirstOrDefault();

                    BallTrackState updatedTrackState;
                    if (matchedTrack is null)
                    {
                        updatedTrackState = new BallTrackState(
                            nextCameraBallTrackId++,
                            observation.CameraId,
                            observation.XMm,
                            observation.YMm,
                            observation.ZMm,
                            observation.EventTimestampNs,
                            observation.EventTimestampNs,
                            observation.Confidence,
                            observation.Confidence,
                            GetObservedBallUncertaintyMm(observation.Confidence),
                            0d,
                            0d,
                            0d);
                    }
                    else
                    {
                        updatedTrackState = CreateObservedBallTrackState(matchedTrack.Track, observation);
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

            var predictedState = CreatePredictedBallTrackState(existingEntry.Value, frameTimestampNs);
            if (predictedState.Visibility <= 0.01f)
            {
                cameraBallTrackStates.Remove(existingEntry.Key);
                continue;
            }

            cameraBallTrackStates[existingEntry.Key] = predictedState;
        }

        return observedTrackIds;
    }

    private static BallTrackState CreateObservedBallTrackState(
        BallTrackState previousState,
        BallObservation observation)
    {
        var vxMmPerS = previousState.VXMmPerS;
        var vyMmPerS = previousState.VYMmPerS;
        var vzMmPerS = previousState.VZMmPerS;

        if (observation.EventTimestampNs > previousState.LastUpdateTimestampNs)
        {
            var deltaSeconds = (observation.EventTimestampNs - previousState.LastUpdateTimestampNs) / 1_000_000_000d;
            vxMmPerS = (observation.XMm - previousState.XMm) / deltaSeconds;
            vyMmPerS = (observation.YMm - previousState.YMm) / deltaSeconds;
            vzMmPerS = (observation.ZMm - previousState.ZMm) / deltaSeconds;
        }

        return previousState with
        {
            XMm = observation.XMm,
            YMm = observation.YMm,
            ZMm = observation.ZMm,
            LastVisibleTimestampNs = observation.EventTimestampNs,
            LastUpdateTimestampNs = observation.EventTimestampNs,
            Visibility = observation.Confidence,
            Quality = observation.Confidence,
            PositionUncertaintyMm = GetObservedBallUncertaintyMm(observation.Confidence),
            VXMmPerS = vxMmPerS,
            VYMmPerS = vyMmPerS,
            VZMmPerS = vzMmPerS,
        };
    }

    private static BallTrackState CreatePredictedBallTrackState(
        BallTrackState previousState,
        long frameTimestampNs)
    {
        if (frameTimestampNs <= previousState.LastUpdateTimestampNs)
        {
            return previousState;
        }

        var deltaSeconds = (frameTimestampNs - previousState.LastUpdateTimestampNs) / 1_000_000_000d;
        var decay = Math.Pow(0.5d, deltaSeconds);

        return previousState with
        {
            XMm = previousState.XMm + previousState.VXMmPerS * deltaSeconds,
            YMm = previousState.YMm + previousState.VYMmPerS * deltaSeconds,
            ZMm = previousState.ZMm + previousState.VZMmPerS * deltaSeconds,
            LastUpdateTimestampNs = frameTimestampNs,
            Visibility = (float)(previousState.Visibility * decay),
            Quality = previousState.Quality * decay,
            PositionUncertaintyMm = previousState.PositionUncertaintyMm + (deltaSeconds * 50d),
        };
    }

    private List<MergedBallState> CollectMergedBallStates(HashSet<int> observedBallTrackIds)
    {
        var freshStates = cameraBallTrackStates.Values
            .Where(state => observedBallTrackIds.Contains(state.LocalTrackId))
            .OrderBy(state => state.LocalTrackId)
            .ToList();
        var staleStates = cameraBallTrackStates.Values
            .Where(state => !observedBallTrackIds.Contains(state.LocalTrackId))
            .OrderBy(state => state.LocalTrackId)
            .ToList();
        var clusters = BuildBallClusters(freshStates);
        var freshClusterCount = clusters.Count;

        foreach (var staleState in staleStates)
        {
            var nearbyFreshClusterExists = clusters.Any(
                cluster => clusters.IndexOf(cluster) < freshClusterCount
                    && CanAttachBallTrackToCluster(cluster, staleState));
            if (nearbyFreshClusterExists)
            {
                continue;
            }

            var staleCluster = clusters.FirstOrDefault(
                cluster => CanAttachBallTrackToCluster(cluster, staleState));
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

    private static List<List<BallTrackState>> BuildBallClusters(IEnumerable<BallTrackState> states)
    {
        var clusters = new List<List<BallTrackState>>();

        foreach (var state in states)
        {
            var matchingClusters = clusters
                .Where(candidate => CanAttachBallTrackToCluster(candidate, state))
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
        IReadOnlyCollection<BallTrackState> cluster,
        BallTrackState candidate)
    {
        return !cluster.Any(existing => existing.CameraId == candidate.CameraId)
            && cluster.Any(
                existing => GetDistanceMm(existing.XMm, existing.YMm, candidate.XMm, candidate.YMm) <= BallMergeDistanceMm);
    }

    private List<MergedBallState> AssignMergedBallIdentity(List<MergedBallState> mergedStates)
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
                .Where(candidate => candidate.DistanceMm <= BallMergeDistanceMm)
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
        IReadOnlyList<BufferedDetection> orderedDetections,
        long frameTimestampNs)
    {
        var observations = CollectCameraRobotObservations(orderedDetections);
        var observedKeys = observations.Keys.ToHashSet();

        foreach (var entry in observations)
        {
            cameraRobotTrackStates[entry.Key] = CreateObservedRobotTrackState(entry.Key, entry.Value);
        }

        foreach (var existingEntry in cameraRobotTrackStates.ToList())
        {
            if (observedKeys.Contains(existingEntry.Key))
            {
                continue;
            }

            var predictedState = CreatePredictedRobotTrackState(existingEntry.Value, frameTimestampNs);
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
        CameraRobotKey key,
        RobotObservation observation)
    {
        var previousState = cameraRobotTrackStates.GetValueOrDefault(key);
        var unwrappedOrientation = UnwrapAngleNearReference(
            observation.OrientationRad,
            previousState?.OrientationRad ?? observation.OrientationRad);
        var vxMmPerS = 0d;
        var vyMmPerS = 0d;
        var angularVelocityRadPerS = 0d;

        if (previousState is not null && observation.EventTimestampNs > previousState.LastUpdateTimestampNs)
        {
            var deltaSeconds = (observation.EventTimestampNs - previousState.LastUpdateTimestampNs) / 1_000_000_000d;
            vxMmPerS = (observation.XMm - previousState.XMm) / deltaSeconds;
            vyMmPerS = (observation.YMm - previousState.YMm) / deltaSeconds;
            angularVelocityRadPerS = (unwrappedOrientation - previousState.OrientationRad) / deltaSeconds;
        }

        return new RobotTrackState(
            observation.XMm,
            observation.YMm,
            unwrappedOrientation,
            observation.EventTimestampNs,
            observation.EventTimestampNs,
            observation.Confidence,
            observation.Confidence,
            vxMmPerS,
            vyMmPerS,
            angularVelocityRadPerS);
    }

    private static RobotTrackState CreatePredictedRobotTrackState(
        RobotTrackState previousState,
        long frameTimestampNs)
    {
        if (frameTimestampNs <= previousState.LastUpdateTimestampNs)
        {
            return previousState;
        }

        var deltaSeconds = (frameTimestampNs - previousState.LastUpdateTimestampNs) / 1_000_000_000d;
        var decay = Math.Pow(0.5d, deltaSeconds);

        return previousState with
        {
            XMm = previousState.XMm + previousState.VXMmPerS * deltaSeconds,
            YMm = previousState.YMm + previousState.VYMmPerS * deltaSeconds,
            OrientationRad = previousState.OrientationRad + previousState.AngularVelocityRadPerS * deltaSeconds,
            LastUpdateTimestampNs = frameTimestampNs,
            Visibility = (float)(previousState.Visibility * decay),
            Quality = previousState.Quality * decay,
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
        var totalWeight = states.Sum(state => Math.Max(0.001d, state.Visibility));
        var mergedX = states.Sum(state => state.XMm * Math.Max(0.001d, state.Visibility)) / totalWeight;
        var mergedY = states.Sum(state => state.YMm * Math.Max(0.001d, state.Visibility)) / totalWeight;
        var orientationReference = states[0].OrientationRad;
        var mergedOrientation = states
            .Sum(state => UnwrapAngleNearReference(state.OrientationRad, orientationReference) * Math.Max(0.001d, state.Visibility))
            / totalWeight;
        var mergedVx = states.Sum(state => state.VXMmPerS * Math.Max(0.001d, state.Visibility)) / totalWeight;
        var mergedVy = states.Sum(state => state.VYMmPerS * Math.Max(0.001d, state.Visibility)) / totalWeight;
        var mergedAngularVelocity = states.Sum(state => state.AngularVelocityRadPerS * Math.Max(0.001d, state.Visibility)) / totalWeight;
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

    private sealed record BallTrackState(
        int LocalTrackId,
        uint CameraId,
        double XMm,
        double YMm,
        double ZMm,
        long LastVisibleTimestampNs,
        long LastUpdateTimestampNs,
        float Visibility,
        double Quality,
        double PositionUncertaintyMm,
        double VXMmPerS,
        double VYMmPerS,
        double VZMmPerS);

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
        double XMm,
        double YMm,
        double OrientationRad,
        long LastVisibleTimestampNs,
        long LastUpdateTimestampNs,
        float Visibility,
        double Quality,
        double VXMmPerS,
        double VYMmPerS,
        double AngularVelocityRadPerS);

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
