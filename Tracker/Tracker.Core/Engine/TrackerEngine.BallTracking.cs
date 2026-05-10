namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// camera ごとの ball observation を既存 track に割り当て、未観測 track は予測または破棄する。
    /// </summary>
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
                            CreateInitialKalmanAxis(settings, observation.XMm, GetObservedBallUncertaintyMm(settings, observation.Confidence)),
                            CreateInitialKalmanAxis(settings, observation.YMm, GetObservedBallUncertaintyMm(settings, observation.Confidence)),
                            CreateInitialKalmanAxis(settings, observation.ZMm, GetObservedBallUncertaintyMm(settings, observation.Confidence)),
                            observation.EventTimestampNs,
                            observation.EventTimestampNs,
                            observation.Confidence,
                            observation.Confidence,
                            1);
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

    /// <summary>
    /// 観測された ball track を Kalman predict 後に measurement update する。
    /// </summary>
    private static BallTrackState CreateObservedBallTrackState(
        TrackerEngineSettings settings,
        BallTrackState previousState,
        BallObservation observation)
    {
        var predictedState = PredictBallTrackState(settings, previousState, observation.EventTimestampNs);
        var deltaSeconds = GetPredictionDeltaSeconds(previousState.LastUpdateTimestampNs, observation.EventTimestampNs);
        var measurementVariance = GetObservedBallUncertaintyMm(settings, observation.Confidence);

        // Kalman update は predicted state を基準にし、observed velocity は previous position から計算する。
        return predictedState with
        {
            XAxis = UpdateKalmanAxis(predictedState.XAxis, previousState.XAxis.Position, observation.XMm, deltaSeconds, measurementVariance),
            YAxis = UpdateKalmanAxis(predictedState.YAxis, previousState.YAxis.Position, observation.YMm, deltaSeconds, measurementVariance),
            ZAxis = UpdateKalmanAxis(predictedState.ZAxis, previousState.ZAxis.Position, observation.ZMm, deltaSeconds, measurementVariance),
            LastVisibleTimestampNs = observation.EventTimestampNs,
            LastUpdateTimestampNs = observation.EventTimestampNs,
            Visibility = observation.Confidence,
            Quality = observation.Confidence,
            ObservationCount = previousState.ObservationCount + 1,
        };
    }

    /// <summary>
    /// 欠測した ball track を frame timestamp まで予測し、visibility と quality を減衰する。
    /// </summary>
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

    /// <summary>
    /// ball track の Kalman axis を target timestamp まで predict する。
    /// </summary>
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
            XAxis = PredictKalmanAxis(settings, previousState.XAxis, deltaSeconds, processNoise),
            YAxis = PredictKalmanAxis(settings, previousState.YAxis, deltaSeconds, processNoise),
            ZAxis = PredictKalmanAxis(settings, previousState.ZAxis, deltaSeconds, processNoise),
            LastUpdateTimestampNs = targetTimestampNs,
        };
    }

    /// <summary>
    /// camera-local ball track を multi-camera cluster にまとめ、merged ball state を作る。
    /// </summary>
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
                    cluster.Select(state => state.CameraId).Distinct().OrderBy(cameraId => cameraId).ToList(),
                    cluster.Max(state => state.ObservationCount),
                    cluster.Any(state => observedBallTrackIds.Contains(state.LocalTrackId))));
        }

        return mergedStates;
    }

    /// <summary>
    /// camera が重ならず距離 gate 内にある ball track を cluster 化する。
    /// </summary>
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

    /// <summary>
    /// ball cluster に candidate track を追加できるか判定する。
    /// </summary>
    private static bool CanAttachBallTrackToCluster(
        TrackerEngineSettings settings,
        IReadOnlyCollection<BallTrackState> cluster,
        BallTrackState candidate)
    {
        return !cluster.Any(existing => existing.CameraId == candidate.CameraId)
            && cluster.Any(
                existing => GetDistanceMm(existing.XMm, existing.YMm, candidate.XMm, candidate.YMm) <= GetBallMergeDistanceMm(settings));
    }

    /// <summary>
    /// merged ball に前 frame からの内部 track id を割り当て、未一致なら新規 id を採番する。
    /// </summary>
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

    /// <summary>
    /// merged ball state を public な tracked ball DTO に変換する。
    /// </summary>
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

    private static bool IsFreshPreviousPrimaryBall(BallOutputCandidate candidate, TrackedBallState? previousPrimaryBall)
    {
        return candidate.MergedBall.HasFreshObservation
            && previousPrimaryBall is not null
            && candidate.TrackedBall.InternalTrackId == previousPrimaryBall.InternalTrackId;
    }

    private static double GetObservedBallUncertaintyMm(float confidence)
    {
        return 1d / Math.Max(0.001d, confidence);
    }

    private static double GetBallMergeWeight(BallTrackState state)
    {
        return 1d / Math.Max(0.001d, state.PositionUncertaintyMm);
    }

    /// <summary>
    /// raw ball detection から作った camera-local observation。
    /// </summary>
    private sealed record BallObservation(
        uint CameraId,
        long EventTimestampNs,
        double XMm,
        double YMm,
        double ZMm,
        float Confidence);

    /// <summary>
    /// 1 camera 内で維持する ball track state。
    /// </summary>
    private sealed record BallTrackState(
        int LocalTrackId,
        uint CameraId,
        KalmanAxisState XAxis,
        KalmanAxisState YAxis,
        KalmanAxisState ZAxis,
        long LastVisibleTimestampNs,
        long LastUpdateTimestampNs,
        float Visibility,
        double Quality,
        int ObservationCount)
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
        /// 現在推定位置の Z 座標。単位は mm。
        /// </summary>
        public double ZMm => ZAxis.Position;

        /// <summary>
        /// X 方向速度。単位は mm/s。
        /// </summary>
        public double VXMmPerS => XAxis.Velocity;

        /// <summary>
        /// Y 方向速度。単位は mm/s。
        /// </summary>
        public double VYMmPerS => YAxis.Velocity;

        /// <summary>
        /// Z 方向速度。単位は mm/s。
        /// </summary>
        public double VZMmPerS => ZAxis.Velocity;

        /// <summary>
        /// X/Y 位置分散から求めた merge weight 用の不確かさ。
        /// </summary>
        public double PositionUncertaintyMm => (XAxis.PositionVariance + YAxis.PositionVariance) / 2d;
    }

    /// <summary>
    /// camera-local ball track を multi-camera merge した中間 state。
    /// </summary>
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
        IReadOnlyList<uint> SourceCameraIds,
        int ObservationCount,
        bool HasFreshObservation);

    /// <summary>
    /// primary / secondary の出力順序決定時に扱う候補。
    /// </summary>
    private sealed record BallOutputCandidate(MergedBallState MergedBall, TrackedBallState TrackedBall);

    /// <summary>
    /// merged ball の内部 id を次 frame へ継続するための最小状態。
    /// </summary>
    private sealed record MergedBallIdentityState(
        int InternalTrackId,
        double XMm,
        double YMm,
        double VXMmPerS,
        double VYMmPerS,
        long LastVisibleTimestampNs);
}
