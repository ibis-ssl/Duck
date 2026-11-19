using Tracker.Core;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// TrackerCoordinator が保持する mutable option を外部参照から切り離す clone helper。
/// </summary>
internal static class TrackerOptionsCloner
{
    /// <summary>
    /// 解決済み tracker options を coordinator 内部保持用に複製する。
    /// </summary>
    /// <param name="options">複製元の解決済み設定。</param>
    /// <returns>値を複製した解決済み設定。</returns>
    public static TrackerResolvedOptions CloneResolvedOptions(TrackerResolvedOptions options)
    {
        return new TrackerResolvedOptions
        {
            Enabled = options.Enabled,
            EngineSettings = CloneSettings(options.EngineSettings),
            PublisherOptions = ClonePublisherOptions(options.PublisherOptions),
            Diagnostics = CloneDiagnosticsOptions(options.Diagnostics),
        };
    }

    /// <summary>
    /// engine settings を profile switch 適用時に独立した値として複製する。
    /// </summary>
    /// <param name="settings">複製元の engine 設定。</param>
    /// <returns>値を複製した engine 設定。</returns>
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
    /// <param name="options">複製元の publisher 設定。</param>
    /// <returns>値を複製した publisher 設定。</returns>
    public static TrackerPublisherOptions ClonePublisherOptions(TrackerPublisherOptions options)
    {
        return new TrackerPublisherOptions
        {
            PublishUdp = options.PublishUdp,
            MulticastAddress = options.MulticastAddress,
            Port = options.Port,
            InterfaceName = options.InterfaceName,
            SourceName = options.SourceName,
            Uuid = options.Uuid,
        };
    }

    /// <summary>
    /// diagnostics options を coordinator 内部保持用に複製する。
    /// </summary>
    /// <param name="options">複製元の診断設定。</param>
    /// <returns>値を複製した診断設定。</returns>
    public static TrackerDiagnosticsOptions CloneDiagnosticsOptions(TrackerDiagnosticsOptions options)
    {
        return new TrackerDiagnosticsOptions
        {
            FilePath = options.FilePath,
        };
    }

    /// <summary>
    /// runtime overrides を pending / in-flight request 用に複製する。
    /// </summary>
    /// <param name="overrides">複製元の runtime override。</param>
    /// <returns>値を複製した runtime override。</returns>
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
