namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
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

    private static double GetBallOutputVisibilityThreshold(TrackerEngineSettings settings)
    {
        return Math.Clamp(settings.BallTracker.OutputVisibilityThreshold ?? 0d, 0d, 1d);
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

    private static double GetRobotOutputVisibilityThreshold(TrackerEngineSettings settings)
    {
        return Math.Clamp(settings.RobotTracker.OutputVisibilityThreshold ?? 0d, 0d, 1d);
    }

    private static bool PassesOutputVisibility(float visibility, double threshold)
    {
        return visibility >= threshold;
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
        var measurementNoise = GetBallMeasurementNoise(settings) / Math.Max(0.001d, confidence);
        return measurementNoise * measurementNoise * GetMeasurementNoiseVarianceScale(settings);
    }

    private static double GetObservedRobotUncertaintyMm(TrackerEngineSettings settings, float confidence)
    {
        var measurementNoise = GetRobotMeasurementNoise(settings) / Math.Max(0.001d, confidence);
        return measurementNoise * measurementNoise * GetMeasurementNoiseVarianceScale(settings);
    }

    private static double GetInitialVelocityVariance(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.KalmanInitialVelocityVariance);
    }

    private static double GetKalmanProcessNoiseScale(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.KalmanProcessNoiseScale);
    }

    private static double GetMeasurementNoiseVarianceScale(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.MeasurementNoiseVarianceScale);
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
}
