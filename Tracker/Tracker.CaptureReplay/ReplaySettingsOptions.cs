using Tracker.Core;

/// <summary>
/// Tracker.Server appsettings.json と capture metadata の両方を受ける replay settings root。
/// </summary>
internal sealed class ReplaySettingsFile
{
    /// <summary>
    /// appsettings.json の Tracker section を表す。
    /// </summary>
    public ReplaySettingsFile? Tracker { get; set; }

    /// <summary>
    /// capture metadata の TrackerOptions section を表す。
    /// </summary>
    public ReplaySettingsFile? TrackerOptions { get; set; }

    /// <summary>
    /// capture metadata に保存済みの解決済み tracker options を表す。
    /// </summary>
    public ReplayResolvedOptions? ResolvedTrackerOptions { get; set; }

    /// <summary>
    /// runtime profile switch で適用された override 値を表す。
    /// </summary>
    public TrackerRuntimeOverrides? RuntimeOverrides { get; set; }

    /// <summary>
    /// profile 名から profile 設定への対応を表す。
    /// </summary>
    public Dictionary<string, TrackerProfileOptions>? Profiles { get; set; }
}

/// <summary>
/// Capture metadata 内の解決済み tracker settings を表す。
/// </summary>
internal sealed class ReplayResolvedOptions
{
    /// <summary>
    /// replay にそのまま使う engine settings。
    /// </summary>
    public TrackerEngineSettings? EngineSettings { get; set; }
}

/// <summary>
/// appsettings.json の profile 単位の tracker 設定を表す。
/// </summary>
internal sealed class TrackerProfileOptions
{
    /// <summary>
    /// reorder / merge など engine 全体の設定。
    /// </summary>
    public TrackerEngineOptions? Engine { get; set; }

    /// <summary>
    /// robot tracker の profile 設定。
    /// </summary>
    public TrackerRobotTrackerOptions? RobotTracker { get; set; }

    /// <summary>
    /// ball tracker の profile 設定。
    /// </summary>
    public TrackerBallTrackerOptions? BallTracker { get; set; }

    /// <summary>
    /// kick detector の profile 設定。
    /// </summary>
    public TrackerKickDetectorOptions? KickDetector { get; set; }
}

/// <summary>
/// appsettings.json の engine section を replay 用に読むための DTO。
/// </summary>
internal sealed class TrackerEngineOptions
{
    /// <summary>
    /// reorder buffer の待機幅を ns 単位で表す。
    /// </summary>
    public long? ReorderWindowNs { get; set; }

    /// <summary>
    /// merge 対象 frame の許容幅を ns 単位で表す。
    /// </summary>
    public long? MergeWindowNs { get; set; }

    /// <summary>
    /// geometry reset 判定で使う field length 差分のしきい値を mm 単位で表す。
    /// </summary>
    public int? GeometryResetFieldLengthThresholdMm { get; set; }

    /// <summary>
    /// geometry reset 判定で使う field width 差分のしきい値を mm 単位で表す。
    /// </summary>
    public int? GeometryResetFieldWidthThresholdMm { get; set; }
}

/// <summary>
/// robot / ball tracker の共通 profile option を表す。
/// </summary>
internal class TrackerRobotTrackerOptions
{
    /// <summary>
    /// Kalman filter の process noise。
    /// </summary>
    public double? ProcessNoise { get; set; }

    /// <summary>
    /// Kalman filter の measurement noise。
    /// </summary>
    public double? MeasurementNoise { get; set; }

    /// <summary>
    /// visibility 減衰の half-life を seconds 単位で表す。
    /// </summary>
    public double? VisibilityHalfLifeSeconds { get; set; }

    /// <summary>
    /// output へ出す visibility の下限。
    /// </summary>
    public double? OutputVisibilityThreshold { get; set; }

    /// <summary>
    /// tracker の association gate。
    /// </summary>
    public double? Gate { get; set; }

    /// <summary>
    /// outlier とみなす距離上限を mm 単位で表す。
    /// </summary>
    public double? OutlierLimitMm { get; set; }
}

/// <summary>
/// ball tracker 固有の profile option を表す。
/// </summary>
internal sealed class TrackerBallTrackerOptions : TrackerRobotTrackerOptions
{
    /// <summary>
    /// ball track の寿命を ns 単位で表す。
    /// </summary>
    public long? TrackLifetimeNs { get; set; }
}

/// <summary>
/// kick detector の profile option を表す。
/// </summary>
internal sealed class TrackerKickDetectorOptions
{
    /// <summary>
    /// kick 判定の速度しきい値を mm/s 単位で表す。
    /// </summary>
    public double? KickSpeedThresholdMmPerS { get; set; }

    /// <summary>
    /// chip kick 判定の高さしきい値を mm 単位で表す。
    /// </summary>
    public double? ChipHeightThresholdMm { get; set; }

    /// <summary>
    /// robot と ball の contact 判定 margin を mm 単位で表す。
    /// </summary>
    public double? ContactMarginMm { get; set; }
}

/// <summary>
/// Capture replay CLI が profile settings に重ねる一時 override 値。
/// </summary>
internal sealed record TrackerSettingOverrides(
    double? BallGate,
    double? BallOutlierLimitMm,
    double? BallOutputVisibility,
    long? BallTrackLifetimeNs,
    long? MergeWindowNs,
    long? ReorderWindowNs);
