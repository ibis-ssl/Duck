namespace Tracker.Core;

/// <summary>
/// 有効 profile の resolved settings snapshot に対する一時上書き値。
/// null property は profile 側の値または engine 既定値を維持する。
/// </summary>
public sealed class TrackerRuntimeOverrides
{
    /// <summary>
    /// publish 設定の runtime override。null property は profile snapshot の値を維持する。
    /// </summary>
    public TrackerPublishOverrides Publish { get; init; } = new();

    /// <summary>
    /// robot tracker 設定の runtime override。null property は profile snapshot の値を維持する。
    /// </summary>
    public TrackerRobotTrackerOverrides RobotTracker { get; init; } = new();

    /// <summary>
    /// ball tracker 設定の runtime override。null property は profile snapshot の値を維持する。
    /// </summary>
    public TrackerBallTrackerOverrides BallTracker { get; init; } = new();

    /// <summary>
    /// kick detector 設定の runtime override。null property は profile snapshot の値を維持する。
    /// </summary>
    public TrackerKickDetectorOverrides KickDetector { get; init; } = new();
}

/// <summary>
/// official tracker packet publish の runtime override。
/// </summary>
public sealed class TrackerPublishOverrides
{
    /// <summary>
    /// multicast address。null の場合は profile snapshot の値を維持する。
    /// </summary>
    public string? MulticastAddress { get; init; }

    /// <summary>
    /// UDP port。null の場合は profile snapshot の値を維持する。
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// official packet の source_name。null の場合は profile snapshot の値を維持する。
    /// </summary>
    public string? SourceName { get; init; }

    /// <summary>
    /// official packet の uuid。null の場合は profile snapshot の値を維持する。
    /// </summary>
    public string? Uuid { get; init; }
}

/// <summary>
/// robot tracking の runtime override と resolved tracker 設定で共有する値。
/// </summary>
public sealed class TrackerRobotTrackerOverrides
{
    /// <summary>
    /// robot Kalman predict の process noise。null の場合は engine 既定値を使う。
    /// </summary>
    public double? ProcessNoise { get; init; }

    /// <summary>
    /// robot 観測更新の measurement noise。null の場合は engine 既定値を使う。
    /// </summary>
    public double? MeasurementNoise { get; init; }

    /// <summary>
    /// robot visibility decay の half-life。単位は seconds。null の場合は engine 既定値を使う。
    /// </summary>
    public double? VisibilityHalfLifeSeconds { get; init; }

    /// <summary>
    /// robot 出力 visibility threshold。null の場合は 0 として扱う。
    /// </summary>
    public double? OutputVisibilityThreshold { get; init; }

    /// <summary>
    /// robot movement gate の倍率。null の場合は engine 既定 gate を使う。
    /// </summary>
    public double? Gate { get; init; }

    /// <summary>
    /// robot outlier 除外距離の上限。単位は mm。null の場合は gate 距離だけを使う。
    /// </summary>
    public double? OutlierLimitMm { get; init; }

    /// <summary>
    /// 既存別 ID track 近傍への sudden robot id switch を抑制する距離。単位は mm。
    /// null の場合は engine 既定値を使う。
    /// </summary>
    public double? IdentitySwitchDistanceMm { get; init; }

    /// <summary>
    /// robot 向き観測 noise。単位は rad。null の場合は engine 既定値を使う。
    /// </summary>
    public double? OrientationMeasurementNoiseRad { get; init; }

    /// <summary>
    /// robot 向き Kalman predict の process noise。null の場合は engine 既定値を使う。
    /// </summary>
    public double? OrientationProcessNoise { get; init; }

    /// <summary>
    /// robot 向き Kalman state 初期化時の角速度分散。null の場合は engine 既定値を使う。
    /// </summary>
    public double? InitialAngularVelocityVariance { get; init; }

    /// <summary>
    /// robot 角速度 clamp。単位は rad/s。null の場合は engine 既定値を使う。
    /// </summary>
    public double? AngularVelocityLimitRadPerS { get; init; }
}

/// <summary>
/// ball tracking の runtime override と resolved tracker 設定で共有する値。
/// </summary>
public sealed class TrackerBallTrackerOverrides
{
    /// <summary>
    /// ball Kalman predict の process noise。null の場合は engine 既定値を使う。
    /// </summary>
    public double? ProcessNoise { get; init; }

    /// <summary>
    /// ball 観測更新の measurement noise。null の場合は engine 既定値を使う。
    /// </summary>
    public double? MeasurementNoise { get; init; }

    /// <summary>
    /// ball visibility decay の half-life。単位は seconds。null の場合は engine 既定値を使う。
    /// </summary>
    public double? VisibilityHalfLifeSeconds { get; init; }

    /// <summary>
    /// ball 出力 visibility threshold。null の場合は 0 として扱う。
    /// </summary>
    public double? OutputVisibilityThreshold { get; init; }

    /// <summary>
    /// ball track match と merge gate の倍率。null の場合は engine 既定 gate を使う。
    /// </summary>
    public double? Gate { get; init; }

    /// <summary>
    /// ball outlier 除外距離の上限。単位は mm。null の場合は gate 距離だけを使う。
    /// </summary>
    public double? OutlierLimitMm { get; init; }

    /// <summary>
    /// ball track を欠測後に保持する寿命。単位は ns。null の場合は visibility decay のみで落とす。
    /// </summary>
    public long? TrackLifetimeNs { get; init; }
}

/// <summary>
/// kick detector の runtime override と resolved tracker 設定で共有する値。
/// </summary>
public sealed class TrackerKickDetectorOverrides
{
    /// <summary>
    /// kick 検出に必要な平面速度 threshold。単位は mm/s。null の場合は engine 既定値を使う。
    /// </summary>
    public double? KickSpeedThresholdMmPerS { get; init; }

    /// <summary>
    /// chip kick とみなす高さ threshold。単位は mm。null の場合は engine 既定値を使う。
    /// </summary>
    public double? ChipHeightThresholdMm { get; init; }

    /// <summary>
    /// ball と robot の contact 判定に足す margin。単位は mm。null の場合は engine 既定値を使う。
    /// </summary>
    public double? ContactMarginMm { get; init; }
}
