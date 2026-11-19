using Tracker.Core;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// profile switch の重複要求抑制に使う tracker option の値比較を提供する。
/// </summary>
internal static class TrackerResolvedOptionsComparer
{
    /// <summary>
    /// 解決済み tracker option が engine / publisher 設定として等価か判定する。
    /// </summary>
    public static bool AreResolvedOptionsEquivalent(
        TrackerResolvedOptions left,
        TrackerResolvedOptions right)
    {
        return left.Enabled == right.Enabled
            && left.EngineSettings.ProfileName == right.EngineSettings.ProfileName
            && left.EngineSettings.ReorderWindowNs == right.EngineSettings.ReorderWindowNs
            && left.EngineSettings.MergeWindowNs == right.EngineSettings.MergeWindowNs
            && left.EngineSettings.GeometryResetFieldLengthThresholdMm == right.EngineSettings.GeometryResetFieldLengthThresholdMm
            && left.EngineSettings.GeometryResetFieldWidthThresholdMm == right.EngineSettings.GeometryResetFieldWidthThresholdMm
            && left.EngineSettings.KalmanInitialVelocityVariance == right.EngineSettings.KalmanInitialVelocityVariance
            && left.EngineSettings.KalmanProcessNoiseScale == right.EngineSettings.KalmanProcessNoiseScale
            && left.EngineSettings.MeasurementNoiseVarianceScale == right.EngineSettings.MeasurementNoiseVarianceScale
            && AreRobotTrackerOverridesEquivalent(left.EngineSettings.RobotTracker, right.EngineSettings.RobotTracker)
            && AreBallTrackerOverridesEquivalent(left.EngineSettings.BallTracker, right.EngineSettings.BallTracker)
            && AreKickDetectorOverridesEquivalent(left.EngineSettings.KickDetector, right.EngineSettings.KickDetector)
            && left.PublisherOptions.PublishUdp == right.PublisherOptions.PublishUdp
            && left.PublisherOptions.MulticastAddress == right.PublisherOptions.MulticastAddress
            && left.PublisherOptions.Port == right.PublisherOptions.Port
            && left.PublisherOptions.SourceName == right.PublisherOptions.SourceName
            && left.PublisherOptions.Uuid == right.PublisherOptions.Uuid;
    }

    /// <summary>
    /// runtime overrides が profile switch 要求として等価か判定する。
    /// </summary>
    public static bool AreRuntimeOverridesEquivalent(
        TrackerRuntimeOverrides left,
        TrackerRuntimeOverrides right)
    {
        return left.Publish.MulticastAddress == right.Publish.MulticastAddress
            && left.Publish.Port == right.Publish.Port
            && left.Publish.SourceName == right.Publish.SourceName
            && left.Publish.Uuid == right.Publish.Uuid
            && AreRobotTrackerOverridesEquivalent(left.RobotTracker, right.RobotTracker)
            && AreBallTrackerOverridesEquivalent(left.BallTracker, right.BallTracker)
            && AreKickDetectorOverridesEquivalent(left.KickDetector, right.KickDetector);
    }

    private static bool AreRobotTrackerOverridesEquivalent(
        TrackerRobotTrackerOverrides left,
        TrackerRobotTrackerOverrides right)
    {
        return left.ProcessNoise == right.ProcessNoise
            && left.MeasurementNoise == right.MeasurementNoise
            && left.VisibilityHalfLifeSeconds == right.VisibilityHalfLifeSeconds
            && left.OutputVisibilityThreshold == right.OutputVisibilityThreshold
            && left.Gate == right.Gate
            && left.OutlierLimitMm == right.OutlierLimitMm
            && left.IdentitySwitchDistanceMm == right.IdentitySwitchDistanceMm
            && left.OrientationMeasurementNoiseRad == right.OrientationMeasurementNoiseRad
            && left.OrientationProcessNoise == right.OrientationProcessNoise
            && left.InitialAngularVelocityVariance == right.InitialAngularVelocityVariance
            && left.AngularVelocityLimitRadPerS == right.AngularVelocityLimitRadPerS;
    }

    private static bool AreBallTrackerOverridesEquivalent(
        TrackerBallTrackerOverrides left,
        TrackerBallTrackerOverrides right)
    {
        return left.ProcessNoise == right.ProcessNoise
            && left.MeasurementNoise == right.MeasurementNoise
            && left.VisibilityHalfLifeSeconds == right.VisibilityHalfLifeSeconds
            && left.OutputVisibilityThreshold == right.OutputVisibilityThreshold
            && left.Gate == right.Gate
            && left.OutlierLimitMm == right.OutlierLimitMm
            && left.TrackLifetimeNs == right.TrackLifetimeNs;
    }

    private static bool AreKickDetectorOverridesEquivalent(
        TrackerKickDetectorOverrides left,
        TrackerKickDetectorOverrides right)
    {
        return left.KickSpeedThresholdMmPerS == right.KickSpeedThresholdMmPerS
            && left.ChipHeightThresholdMm == right.ChipHeightThresholdMm
            && left.ContactMarginMm == right.ContactMarginMm;
    }
}
