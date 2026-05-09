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
    private readonly List<BufferedDetection> pendingDetections = [];
    private readonly Dictionary<CameraRobotKey, RobotTrackState> cameraRobotTrackStates = [];
    private TrackerGeometrySnapshot? geometrySnapshot;
    private string activeProfileName = "default";
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
        cameraRobotTrackStates.Clear();
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
        var nextBallTrackId = 1;

        foreach (var detection in orderedDetections)
        {
            foreach (var ball in detection.Balls)
            {
                balls.Add(new TrackedBallState
                {
                    InternalTrackId = nextBallTrackId++,
                    XMm = ball.X,
                    YMm = ball.Y,
                    ZMm = ball.Z,
                    Visibility = ball.Confidence,
                    SourceCameraIds = [detection.CameraId],
                    LastVisibleTimestampNs = detection.EventTimestampNs,
                    Quality = ball.Confidence,
                });
            }
        }

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
