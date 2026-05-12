namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// camera ごとの robot observation を track state に反映し、未観測 track は予測または破棄する。
    /// </summary>
    private HashSet<CameraRobotKey> UpdateCameraRobotTrackStates(
        TrackerEngineSettings settings,
        IReadOnlyList<BufferedDetection> orderedDetections,
        long frameTimestampNs)
    {
        var observations = CollectCameraRobotObservations(settings, orderedDetections);
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

    /// <summary>
    /// raw detection から camera robot observation を収集し、同一 robot id の遠方外れ値を除く。
    /// </summary>
    private Dictionary<CameraRobotKey, RobotObservation> CollectCameraRobotObservations(
        TrackerEngineSettings settings,
        IReadOnlyList<BufferedDetection> orderedDetections)
    {
        var observations = new Dictionary<CameraRobotKey, RobotObservation>();

        foreach (var detection in orderedDetections)
        {
            var detectionObservations = new Dictionary<CameraRobotKey, RobotObservation>();
            AddRobotObservations(
                detectionObservations,
                TrackerTeam.Yellow,
                detection.RobotsYellow,
                detection.CameraId,
                detection.EventTimestampNs);
            AddRobotObservations(
                detectionObservations,
                TrackerTeam.Blue,
                detection.RobotsBlue,
                detection.CameraId,
                detection.EventTimestampNs);

            foreach (var observation in detectionObservations)
            {
                AddRobotObservationCandidate(settings, observations, observation.Key, observation.Value);
            }
        }

        return DropLikelyRobotIdentitySwitches(
            settings,
            DropFarRobotOutliersWhenSameRobotHasNearObservation(settings, observations));
    }

    /// <summary>
    /// merge window 内で同じ camera/team/id の候補が複数ある場合、既存 track に近い候補を優先する。
    /// </summary>
    private void AddRobotObservationCandidate(
        TrackerEngineSettings settings,
        Dictionary<CameraRobotKey, RobotObservation> observations,
        CameraRobotKey key,
        RobotObservation candidate)
    {
        if (!observations.TryGetValue(key, out var current))
        {
            observations[key] = candidate;
            return;
        }

        var movementGateMm = GetRobotMovementGateMm(settings);
        var currentIsNearTrack = IsNearExistingRobotTrack(settings, key, current, movementGateMm);
        var candidateIsNearTrack = IsNearExistingRobotTrack(settings, key, candidate, movementGateMm);
        if (currentIsNearTrack && !candidateIsNearTrack)
        {
            return;
        }

        if (candidateIsNearTrack && !currentIsNearTrack)
        {
            observations[key] = candidate;
            return;
        }

        if (candidate.Confidence > current.Confidence
            || (candidate.Confidence == current.Confidence && candidate.EventTimestampNs >= current.EventTimestampNs))
        {
            observations[key] = candidate;
        }
    }

    /// <summary>
    /// 同一 robot id の複数 camera 観測で、既存 track 近傍の観測がある場合に遠方 outlier を落とす。
    /// </summary>
    private Dictionary<CameraRobotKey, RobotObservation> DropFarRobotOutliersWhenSameRobotHasNearObservation(
        TrackerEngineSettings settings,
        Dictionary<CameraRobotKey, RobotObservation> observations)
    {
        var filtered = new Dictionary<CameraRobotKey, RobotObservation>();
        var movementGateMm = GetRobotMovementGateMm(settings);

        foreach (var robotGroup in observations.GroupBy(observation => new RobotKey(observation.Key.Team, observation.Key.RobotId)))
        {
            var groupedObservations = robotGroup.ToArray();
            if (groupedObservations.Length == 1)
            {
                filtered[groupedObservations[0].Key] = groupedObservations[0].Value;
                continue;
            }

            var nearExistingTrackObservations = groupedObservations
                .Where(observation => IsNearExistingRobotTrack(settings, observation.Key, observation.Value, movementGateMm))
                .ToArray();
            if (nearExistingTrackObservations.Length == 0)
            {
                foreach (var observation in groupedObservations)
                {
                    filtered[observation.Key] = observation.Value;
                }

                continue;
            }

            foreach (var observation in groupedObservations)
            {
                if (nearExistingTrackObservations.Any(
                        anchor => anchor.Key.Equals(observation.Key)
                            || GetDistanceMm(anchor.Value.XMm, anchor.Value.YMm, observation.Value.XMm, observation.Value.YMm) <= movementGateMm))
                {
                    filtered[observation.Key] = observation.Value;
                }
            }
        }

        return filtered;
    }

    /// <summary>
    /// 既存別 ID track 近傍に現れた sudden id switch 候補を落とす。
    /// </summary>
    private Dictionary<CameraRobotKey, RobotObservation> DropLikelyRobotIdentitySwitches(
        TrackerEngineSettings settings,
        Dictionary<CameraRobotKey, RobotObservation> observations)
    {
        var filtered = new Dictionary<CameraRobotKey, RobotObservation>();
        var identitySwitchDistanceMm = GetRobotIdentitySwitchDistanceMm(settings);
        if (identitySwitchDistanceMm <= 0d)
        {
            return observations;
        }

        var movementGateMm = GetRobotMovementGateMm(settings);
        foreach (var observation in observations)
        {
            if (IsNearExistingRobotTrack(settings, observation.Key, observation.Value, movementGateMm))
            {
                filtered[observation.Key] = observation.Value;
                continue;
            }

            if (IsNearDifferentExistingRobotTrack(settings, observation.Key, observation.Value, identitySwitchDistanceMm))
            {
                continue;
            }

            filtered[observation.Key] = observation.Value;
        }

        return filtered;
    }

    /// <summary>
    /// observation が同一 camera/team/id の既存 track の予測位置に近いか判定する。
    /// </summary>
    private bool IsNearExistingRobotTrack(
        TrackerEngineSettings settings,
        CameraRobotKey key,
        RobotObservation observation,
        double movementGateMm)
    {
        if (!cameraRobotTrackStates.TryGetValue(key, out var previousState))
        {
            return false;
        }

        var predictedState = PredictRobotTrackState(settings, previousState, observation.EventTimestampNs);
        return GetDistanceMm(predictedState.XMm, predictedState.YMm, observation.XMm, observation.YMm) <= movementGateMm;
    }

    /// <summary>
    /// observation が同一 camera/team の別 ID 既存 track 近傍にあるか判定する。
    /// </summary>
    private bool IsNearDifferentExistingRobotTrack(
        TrackerEngineSettings settings,
        CameraRobotKey key,
        RobotObservation observation,
        double identitySwitchDistanceMm)
    {
        return cameraRobotTrackStates.Any(
            entry =>
            {
                if (entry.Key.CameraId != key.CameraId
                    || entry.Key.Team != key.Team
                    || entry.Key.RobotId == key.RobotId)
                {
                    return false;
                }

                var predictedState = PredictRobotTrackState(settings, entry.Value, observation.EventTimestampNs);
                return GetDistanceMm(predictedState.XMm, predictedState.YMm, observation.XMm, observation.YMm) <= identitySwitchDistanceMm;
            });
    }

    private static void AddRobotObservations(
        Dictionary<CameraRobotKey, RobotObservation> observations,
        TrackerTeam team,
        IEnumerable<SSL_DetectionRobot> robots,
        uint cameraId,
        long eventTimestampNs)
    {
        foreach (var robot in robots.OrderByDescending(candidate => candidate.Confidence).ThenBy(candidate => candidate.RobotId))
        {
            if (HasCloseRobotObservationWithDifferentId(observations, team, robot, cameraId))
            {
                continue;
            }

            AddRobotObservation(observations, team, robot, cameraId, eventTimestampNs);
        }
    }

    private static bool HasCloseRobotObservationWithDifferentId(
        IReadOnlyDictionary<CameraRobotKey, RobotObservation> observations,
        TrackerTeam team,
        SSL_DetectionRobot robot,
        uint cameraId)
    {
        return observations.Any(
            observation => observation.Key.CameraId == cameraId
                && observation.Key.Team == team
                && observation.Key.RobotId != robot.RobotId
                && GetDistanceMm(observation.Value.XMm, observation.Value.YMm, robot.X, robot.Y) < RobotCloseDuplicateDistanceMm);
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

    /// <summary>
    /// robot observation を既存 track に反映し、gate 外なら新しい Kalman state として再初期化する。
    /// </summary>
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
            var orientationMeasurementVariance = GetObservedRobotOrientationUncertaintyRad(settings, observation.Confidence);
            return new RobotTrackState(
                CreateInitialKalmanAxis(settings, observation.XMm, measurementVariance),
                CreateInitialKalmanAxis(settings, observation.YMm, measurementVariance),
                CreateInitialKalmanAxis(unwrappedOrientation, orientationMeasurementVariance, GetRobotInitialAngularVelocityVariance(settings)),
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
            var orientationMeasurementVariance = GetObservedRobotOrientationUncertaintyRad(settings, observation.Confidence);
            return new RobotTrackState(
                CreateInitialKalmanAxis(settings, observation.XMm, measurementVariance),
                CreateInitialKalmanAxis(settings, observation.YMm, measurementVariance),
                CreateInitialKalmanAxis(unwrappedOrientation, orientationMeasurementVariance, GetRobotInitialAngularVelocityVariance(settings)),
                observation.EventTimestampNs,
                observation.EventTimestampNs,
                observation.Confidence,
                observation.Confidence / GetRobotMeasurementNoise(settings));
        }

        var observedMeasurementVariance = GetObservedRobotUncertaintyMm(settings, observation.Confidence);
        var observedOrientationMeasurementVariance = GetObservedRobotOrientationUncertaintyRad(settings, observation.Confidence);
        var angularVelocityLimitRadPerS = GetRobotAngularVelocityLimitRadPerS(settings);
        // Kalman update は predicted state を基準にし、observed velocity は previous position から計算する。
        return predictedState with
        {
            XAxis = UpdateKalmanAxis(predictedState.XAxis, previousState.XAxis.Position, observation.XMm, deltaSeconds, observedMeasurementVariance),
            YAxis = UpdateKalmanAxis(predictedState.YAxis, previousState.YAxis.Position, observation.YMm, deltaSeconds, observedMeasurementVariance),
            OrientationAxis = UpdateKalmanAxis(
                predictedState.OrientationAxis,
                previousState.OrientationAxis.Position,
                unwrappedOrientation,
                deltaSeconds,
                observedOrientationMeasurementVariance,
                angularVelocityLimitRadPerS),
            LastVisibleTimestampNs = observation.EventTimestampNs,
            LastUpdateTimestampNs = observation.EventTimestampNs,
            Visibility = observation.Confidence,
            Quality = observation.Confidence / GetRobotMeasurementNoise(settings),
        };
    }

    /// <summary>
    /// 欠測した robot track を frame timestamp まで予測し、visibility と quality を減衰する。
    /// </summary>
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

    /// <summary>
    /// robot track の Kalman axis を target timestamp まで predict する。
    /// </summary>
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
        var angularVelocityLimitRadPerS = GetRobotAngularVelocityLimitRadPerS(settings);
        return previousState with
        {
            XAxis = PredictKalmanAxis(settings, previousState.XAxis, deltaSeconds, processNoise),
            YAxis = PredictKalmanAxis(settings, previousState.YAxis, deltaSeconds, processNoise),
            OrientationAxis = PredictKalmanAxis(
                ClampKalmanAxisVelocity(previousState.OrientationAxis, angularVelocityLimitRadPerS),
                deltaSeconds,
                GetRobotOrientationProcessVariance(settings)),
            LastUpdateTimestampNs = targetTimestampNs,
        };
    }

    /// <summary>
    /// camera-local robot track を team と robot id 単位の merge bucket にまとめる。
    /// </summary>
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

    /// <summary>
    /// 同一 robot id の camera-local track 群を public な tracked robot DTO に変換する。
    /// </summary>
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

    /// <summary>
    /// team と robot id で robot identity を表す key。
    /// </summary>
    private sealed record RobotKey(TrackerTeam Team, uint RobotId);

    /// <summary>
    /// camera id、team、robot id で camera-local robot track を表す key。
    /// </summary>
    private sealed record CameraRobotKey(uint CameraId, TrackerTeam Team, uint RobotId);

    /// <summary>
    /// raw robot detection から作った camera-local observation。
    /// </summary>
    private sealed record RobotObservation(
        uint CameraId,
        double XMm,
        double YMm,
        double OrientationRad,
        float Confidence)
    {
        /// <summary>
        /// observation の event timestamp。単位は ns。
        /// </summary>
        public long EventTimestampNs { get; init; }
    }

    /// <summary>
    /// 1 camera 内で維持する robot track state。
    /// </summary>
    private sealed record RobotTrackState(
        KalmanAxisState XAxis,
        KalmanAxisState YAxis,
        KalmanAxisState OrientationAxis,
        long LastVisibleTimestampNs,
        long LastUpdateTimestampNs,
        float Visibility,
        double Quality)
    {
        /// <summary>
        /// 現在推定位置の X 座標。単位は mm。
        /// </summary>
        public double XMm => XAxis.Position;

        /// <summary>
        /// 現在推定位置の Y 座標。単位は mm。
        /// </summary>
        public double YMm => YAxis.Position;

        /// <summary>
        /// 現在推定 orientation。単位は rad。
        /// </summary>
        public double OrientationRad => OrientationAxis.Position;

        /// <summary>
        /// X 方向速度。単位は mm/s。
        /// </summary>
        public double VXMmPerS => XAxis.Velocity;

        /// <summary>
        /// Y 方向速度。単位は mm/s。
        /// </summary>
        public double VYMmPerS => YAxis.Velocity;

        /// <summary>
        /// 角速度。単位は rad/s。
        /// </summary>
        public double AngularVelocityRadPerS => OrientationAxis.Velocity;

        /// <summary>
        /// X/Y 位置分散から求めた merge weight 用の不確かさ。
        /// </summary>
        public double PositionUncertaintyMm => (XAxis.PositionVariance + YAxis.PositionVariance) / 2d;
    }
}
