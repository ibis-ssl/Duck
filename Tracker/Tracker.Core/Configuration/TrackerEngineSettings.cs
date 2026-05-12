namespace Tracker.Core;

/// <summary>
/// Core tracker engine の resolved settings snapshot。
/// profile 切替時はこの snapshot を基準に runtime overrides を別途保持する。
/// </summary>
public sealed class TrackerEngineSettings
{
    /// <summary>
    /// kick 検出に使う既定の平面速度 threshold。単位は mm/s。
    /// </summary>
    public const double DefaultKickDetectionSpeedThresholdMmPerS = 800d;
    /// <summary>
    /// chip kick 判定に使う既定の高さ threshold。単位は mm。
    /// </summary>
    public const double DefaultChipHeightThresholdMm = 120d;
    /// <summary>
    /// ball と robot の contact 判定に足す既定 margin。単位は mm。
    /// </summary>
    public const double DefaultContactMarginMm = 25d;
    /// <summary>
    /// Kalman filter の初期速度分散。
    /// </summary>
    public const double DefaultKalmanInitialVelocityVariance = 10_000d;
    /// <summary>
    /// Kalman predict で process noise に掛ける既定 scale。
    /// </summary>
    public const double DefaultKalmanProcessNoiseScale = 10_000d;
    /// <summary>
    /// 観測 noise variance に掛ける既定 scale。
    /// </summary>
    public const double DefaultMeasurementNoiseVarianceScale = 100d;
    /// <summary>
    /// robot 向き観測 noise の既定値。単位は rad。
    /// </summary>
    public const double DefaultRobotOrientationMeasurementNoiseRad = 0.05d;
    /// <summary>
    /// robot 向き Kalman predict の既定 process noise。
    /// </summary>
    public const double DefaultRobotOrientationProcessNoise = 0.05d;
    /// <summary>
    /// robot 向き Kalman state 初期化時の既定角速度分散。
    /// </summary>
    public const double DefaultRobotInitialAngularVelocityVariance = 10d;
    /// <summary>
    /// robot 角速度 clamp の既定値。単位は rad/s。
    /// </summary>
    public const double DefaultRobotAngularVelocityLimitRadPerS = Math.PI * 2d;
    /// <summary>
    /// robot id が既存別 ID track 近傍へ突然入れ替わったとみなす既定距離。単位は mm。
    /// </summary>
    public const double DefaultRobotIdentitySwitchDistanceMm = 135d;

    /// <summary>
    /// この settings snapshot の profile 名。
    /// </summary>
    public string ProfileName { get; init; } = "default";

    /// <summary>
    /// event-time reorder buffer の待機幅。単位は ns。
    /// </summary>
    public long ReorderWindowNs { get; init; }

    /// <summary>
    /// 複数 camera detection を同一 world frame にまとめる merge window。単位は ns。
    /// </summary>
    public long MergeWindowNs { get; init; }

    /// <summary>
    /// field length 差分で geometry reset を行う threshold。単位は mm。
    /// </summary>
    public int GeometryResetFieldLengthThresholdMm { get; init; }

    /// <summary>
    /// field width 差分で geometry reset を行う threshold。単位は mm。
    /// </summary>
    public int GeometryResetFieldWidthThresholdMm { get; init; }

    /// <summary>
    /// Kalman axis の初期速度分散。0 以下は helper 側で最小値に丸める。
    /// </summary>
    public double KalmanInitialVelocityVariance { get; init; } = DefaultKalmanInitialVelocityVariance;

    /// <summary>
    /// Kalman predict の process noise scale。0 以下は helper 側で最小値に丸める。
    /// </summary>
    public double KalmanProcessNoiseScale { get; init; } = DefaultKalmanProcessNoiseScale;

    /// <summary>
    /// measurement noise を variance として使うための scale。0 以下は helper 側で最小値に丸める。
    /// </summary>
    public double MeasurementNoiseVarianceScale { get; init; } = DefaultMeasurementNoiseVarianceScale;

    /// <summary>
    /// robot tracker の resolved override 値。null property は engine 既定値を使う。
    /// </summary>
    public TrackerRobotTrackerOverrides RobotTracker { get; init; } = new();

    /// <summary>
    /// ball tracker の resolved override 値。null property は engine 既定値を使う。
    /// </summary>
    public TrackerBallTrackerOverrides BallTracker { get; init; } = new();

    /// <summary>
    /// kick detector の resolved override 値。既定では detector の標準値を持つ。
    /// </summary>
    public TrackerKickDetectorOverrides KickDetector { get; init; } = new()
    {
        KickSpeedThresholdMmPerS = DefaultKickDetectionSpeedThresholdMmPerS,
        ChipHeightThresholdMm = DefaultChipHeightThresholdMm,
        ContactMarginMm = DefaultContactMarginMm,
    };
}
