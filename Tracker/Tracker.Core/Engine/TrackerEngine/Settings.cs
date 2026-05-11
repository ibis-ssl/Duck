namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// 2 点間の平面距離を mm 単位で求める。
    /// </summary>
    private static double GetDistanceMm(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
    }

    /// <summary>
    /// ball camera-local track matching に使う gate 距離を profile / runtime override から解決する。
    /// </summary>
    private static double GetBallTrackMatchDistanceMm(TrackerEngineSettings settings)
    {
        var gatedDistanceMm = settings.BallTracker.Gate is null
            ? BallTrackMatchDistanceMm
            : BallTrackMatchDistanceMm * settings.BallTracker.Gate.Value;
        return settings.BallTracker.OutlierLimitMm is null
            ? gatedDistanceMm
            : Math.Min(settings.BallTracker.OutlierLimitMm.Value, gatedDistanceMm);
    }

    /// <summary>
    /// multi-camera ball merge に使う gate 距離を profile / runtime override から解決する。
    /// </summary>
    private static double GetBallMergeDistanceMm(TrackerEngineSettings settings)
    {
        var gatedDistanceMm = settings.BallTracker.Gate is null
            ? BallMergeDistanceMm
            : BallMergeDistanceMm * settings.BallTracker.Gate.Value;
        return settings.BallTracker.OutlierLimitMm is null
            ? gatedDistanceMm
            : Math.Min(settings.BallTracker.OutlierLimitMm.Value, gatedDistanceMm);
    }

    /// <summary>
    /// 欠測した ball track を保持できる寿命を ns 単位で返す。未指定時は無期限として null を返す。
    /// </summary>
    private static long? GetBallTrackLifetimeNs(TrackerEngineSettings settings)
    {
        return settings.BallTracker.TrackLifetimeNs;
    }

    /// <summary>
    /// ball Kalman update に使う measurement noise を下限付きで解決する。
    /// </summary>
    private static double GetBallMeasurementNoise(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.BallTracker.MeasurementNoise ?? 1d);
    }

    /// <summary>
    /// ball Kalman predict に使う process noise を下限付きで解決する。
    /// </summary>
    private static double GetBallProcessNoise(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.BallTracker.ProcessNoise ?? DefaultBallProcessNoise);
    }

    /// <summary>
    /// ball visibility decay に使う half-life 秒数を下限付きで解決する。
    /// </summary>
    private static double GetBallVisibilityHalfLifeSeconds(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.BallTracker.VisibilityHalfLifeSeconds ?? DefaultVisibilityHalfLifeSeconds);
    }

    /// <summary>
    /// ball を出力対象に残す visibility threshold を 0 から 1 の範囲で解決する。
    /// </summary>
    private static double GetBallOutputVisibilityThreshold(TrackerEngineSettings settings)
    {
        return Math.Clamp(settings.BallTracker.OutputVisibilityThreshold ?? 0d, 0d, 1d);
    }

    /// <summary>
    /// robot camera-local track matching に使う gate 距離を profile / runtime override から解決する。
    /// </summary>
    private static double GetRobotMovementGateMm(TrackerEngineSettings settings)
    {
        var gatedDistanceMm = settings.RobotTracker.Gate is null
            ? RobotTrackMovementGateMm
            : RobotTrackMovementGateMm * settings.RobotTracker.Gate.Value;
        return settings.RobotTracker.OutlierLimitMm is null
            ? gatedDistanceMm
            : Math.Min(settings.RobotTracker.OutlierLimitMm.Value, gatedDistanceMm);
    }

    /// <summary>
    /// robot Kalman update に使う measurement noise を下限付きで解決する。
    /// </summary>
    private static double GetRobotMeasurementNoise(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.RobotTracker.MeasurementNoise ?? 1d);
    }

    /// <summary>
    /// robot Kalman predict に使う process noise を下限付きで解決する。
    /// </summary>
    private static double GetRobotProcessNoise(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.RobotTracker.ProcessNoise ?? DefaultBallProcessNoise);
    }

    /// <summary>
    /// robot visibility decay に使う half-life 秒数を下限付きで解決する。
    /// </summary>
    private static double GetRobotVisibilityHalfLifeSeconds(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.RobotTracker.VisibilityHalfLifeSeconds ?? DefaultVisibilityHalfLifeSeconds);
    }

    /// <summary>
    /// robot を出力対象に残す visibility threshold を 0 から 1 の範囲で解決する。
    /// </summary>
    private static double GetRobotOutputVisibilityThreshold(TrackerEngineSettings settings)
    {
        return Math.Clamp(settings.RobotTracker.OutputVisibilityThreshold ?? 0d, 0d, 1d);
    }

    /// <summary>
    /// track visibility が出力 threshold を満たすか判定する。
    /// </summary>
    private static bool PassesOutputVisibility(float visibility, double threshold)
    {
        return visibility >= threshold;
    }

    /// <summary>
    /// 欠測時間と half-life から visibility を指数減衰させる。
    /// </summary>
    private static float ComputeDecayVisibility(float visibility, double deltaSeconds, double halfLifeSeconds)
    {
        var decay = Math.Pow(0.5d, deltaSeconds / halfLifeSeconds);
        return (float)(visibility * decay);
    }

    /// <summary>
    /// 欠測時間と half-life から quality を指数減衰させる。
    /// </summary>
    private static double ComputeDecayQuality(double quality, double deltaSeconds, double halfLifeSeconds)
    {
        var decay = Math.Pow(0.5d, deltaSeconds / halfLifeSeconds);
        return quality * decay;
    }

    /// <summary>
    /// raw ball confidence と tracker 設定から観測位置の不確かさを算出する。
    /// </summary>
    private static double GetObservedBallUncertaintyMm(TrackerEngineSettings settings, float confidence)
    {
        var measurementNoise = GetBallMeasurementNoise(settings) / Math.Max(0.001d, confidence);
        return measurementNoise * measurementNoise * GetMeasurementNoiseVarianceScale(settings);
    }

    /// <summary>
    /// raw robot confidence と tracker 設定から観測位置の不確かさを算出する。
    /// </summary>
    private static double GetObservedRobotUncertaintyMm(TrackerEngineSettings settings, float confidence)
    {
        var measurementNoise = GetRobotMeasurementNoise(settings) / Math.Max(0.001d, confidence);
        return measurementNoise * measurementNoise * GetMeasurementNoiseVarianceScale(settings);
    }

    /// <summary>
    /// Kalman state 初期化時の速度分散を下限付きで解決する。
    /// </summary>
    private static double GetInitialVelocityVariance(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.KalmanInitialVelocityVariance);
    }

    /// <summary>
    /// Kalman process noise scale を下限付きで解決する。
    /// </summary>
    private static double GetKalmanProcessNoiseScale(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.KalmanProcessNoiseScale);
    }

    /// <summary>
    /// Kalman measurement variance scale を下限付きで解決する。
    /// </summary>
    private static double GetMeasurementNoiseVarianceScale(TrackerEngineSettings settings)
    {
        return Math.Max(0.001d, settings.MeasurementNoiseVarianceScale);
    }

    /// <summary>
    /// robot merge 時に position uncertainty から重みを算出する。
    /// </summary>
    private static double GetRobotMergeWeight(RobotTrackState state)
    {
        return 1d / Math.Max(0.001d, state.PositionUncertaintyMm);
    }

    /// <summary>
    /// ball contact 判定に使う余白距離を mm 単位で解決する。
    /// </summary>
    private static double GetContactMarginMm(TrackerEngineSettings settings)
    {
        return settings.KickDetector.ContactMarginMm ?? TrackerEngineSettings.DefaultContactMarginMm;
    }

    /// <summary>
    /// kick 検出に使う速度 threshold を mm/s 単位で解決する。
    /// </summary>
    private static double GetKickDetectionSpeedThresholdMmPerS(TrackerEngineSettings settings)
    {
        return settings.KickDetector.KickSpeedThresholdMmPerS ?? TrackerEngineSettings.DefaultKickDetectionSpeedThresholdMmPerS;
    }

    /// <summary>
    /// chip kick 判定に使う高さ threshold を mm 単位で解決する。
    /// </summary>
    private static double GetChipHeightThresholdMm(TrackerEngineSettings settings)
    {
        return settings.KickDetector.ChipHeightThresholdMm ?? TrackerEngineSettings.DefaultChipHeightThresholdMm;
    }

    /// <summary>
    /// 秒を tracker 内部の ns timestamp 差分へ変換する。
    /// </summary>
    private static long ConvertSecondsToNanoseconds(double seconds)
    {
        return (long)Math.Round(seconds * 1_000_000_000d, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 現在時刻を tracker 内部の ns timestamp 形式で返す。
    /// </summary>
    private static long GetCurrentUnixTimeNanoseconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
    }
}
