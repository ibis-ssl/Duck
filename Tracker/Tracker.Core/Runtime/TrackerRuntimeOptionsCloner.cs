namespace Tracker.Core;

/// <summary>
/// TrackerCoordinator が保持する mutable option を外部参照から切り離す clone helper。
/// </summary>
internal static class TrackerRuntimeOptionsCloner
{
    /// <summary>
    /// 解決済み tracker runtime options を coordinator 内部保持用に複製する。
    /// </summary>
    public static TrackerRuntimeResolvedOptions CloneResolvedOptions(TrackerRuntimeResolvedOptions options)
    {
        return new TrackerRuntimeResolvedOptions
        {
            Enabled = options.Enabled,
            EngineSettings = CloneSettings(options.EngineSettings),
            PublisherOptions = ClonePublisherOptions(options.PublisherOptions),
        };
    }

    /// <summary>
    /// engine settings を profile switch 適用時に独立した値として複製する。
    /// </summary>
    public static TrackerEngineSettings CloneSettings(TrackerEngineSettings settings)
    {
        return new TrackerEngineSettings
        {
            ProfileName = settings.ProfileName,
            ReorderWindowNs = settings.ReorderWindowNs,
            MergeWindowNs = settings.MergeWindowNs,
            GeometryResetFieldLengthThresholdMm = settings.GeometryResetFieldLengthThresholdMm,
            GeometryResetFieldWidthThresholdMm = settings.GeometryResetFieldWidthThresholdMm,
            KalmanInitialVelocityVariance = settings.KalmanInitialVelocityVariance,
            KalmanProcessNoiseScale = settings.KalmanProcessNoiseScale,
            MeasurementNoiseVarianceScale = settings.MeasurementNoiseVarianceScale,
            RobotTracker = CloneRobotTracker(settings.RobotTracker),
            BallTracker = CloneBallTracker(settings.BallTracker),
            KickDetector = CloneKickDetector(settings.KickDetector),
        };
    }

    /// <summary>
    /// publisher options を publisher 適用時の独立した値として複製する。
    /// </summary>
    public static TrackerPublisherOptions ClonePublisherOptions(TrackerPublisherOptions options)
    {
        return new TrackerPublisherOptions
        {
            PublishUdp = options.PublishUdp,
            MulticastAddress = options.MulticastAddress,
            Port = options.Port,
            SourceName = options.SourceName,
            Uuid = options.Uuid,
        };
    }

    /// <summary>
    /// runtime overrides を pending / in-flight request 用に複製する。
    /// </summary>
    public static TrackerRuntimeOverrides CloneRuntimeOverrides(TrackerRuntimeOverrides overrides)
    {
        return new TrackerRuntimeOverrides
        {
            Publish = new TrackerPublishOverrides
            {
                MulticastAddress = overrides.Publish.MulticastAddress,
                Port = overrides.Publish.Port,
                SourceName = overrides.Publish.SourceName,
                Uuid = overrides.Publish.Uuid,
            },
            RobotTracker = CloneRobotTracker(overrides.RobotTracker),
            BallTracker = CloneBallTracker(overrides.BallTracker),
            KickDetector = CloneKickDetector(overrides.KickDetector),
        };
    }

    private static TrackerRobotTrackerOverrides CloneRobotTracker(TrackerRobotTrackerOverrides tracker)
    {
        return new TrackerRobotTrackerOverrides
        {
            ProcessNoise = tracker.ProcessNoise,
            MeasurementNoise = tracker.MeasurementNoise,
            VisibilityHalfLifeSeconds = tracker.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = tracker.OutputVisibilityThreshold,
            Gate = tracker.Gate,
            OutlierLimitMm = tracker.OutlierLimitMm,
            IdentitySwitchDistanceMm = tracker.IdentitySwitchDistanceMm,
            OrientationMeasurementNoiseRad = tracker.OrientationMeasurementNoiseRad,
            OrientationProcessNoise = tracker.OrientationProcessNoise,
            InitialAngularVelocityVariance = tracker.InitialAngularVelocityVariance,
            AngularVelocityLimitRadPerS = tracker.AngularVelocityLimitRadPerS,
        };
    }

    private static TrackerBallTrackerOverrides CloneBallTracker(TrackerBallTrackerOverrides tracker)
    {
        return new TrackerBallTrackerOverrides
        {
            ProcessNoise = tracker.ProcessNoise,
            MeasurementNoise = tracker.MeasurementNoise,
            VisibilityHalfLifeSeconds = tracker.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = tracker.OutputVisibilityThreshold,
            Gate = tracker.Gate,
            OutlierLimitMm = tracker.OutlierLimitMm,
            TrackLifetimeNs = tracker.TrackLifetimeNs,
        };
    }

    private static TrackerKickDetectorOverrides CloneKickDetector(TrackerKickDetectorOverrides detector)
    {
        return new TrackerKickDetectorOverrides
        {
            KickSpeedThresholdMmPerS = detector.KickSpeedThresholdMmPerS,
            ChipHeightThresholdMm = detector.ChipHeightThresholdMm,
            ContactMarginMm = detector.ContactMarginMm,
        };
    }
}
