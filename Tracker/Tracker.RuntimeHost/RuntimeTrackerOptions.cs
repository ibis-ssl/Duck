using Tracker.Core;

namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost の tracker pipeline と official packet publish 設定。
/// </summary>
public sealed class RuntimeTrackerOptions
{
    /// <summary>
    /// tracker 設定 section 名。
    /// </summary>
    public const string SectionName = "Tracker";

    /// <summary>
    /// tracker pipeline を有効化するかどうか。
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// official tracker packet を UDP publish するかどうか。
    /// </summary>
    public bool PublishUdp { get; init; } = true;

    /// <summary>
    /// official tracker packet に埋め込む source name。
    /// </summary>
    public string SourceName { get; init; } = "ibisduck-tracker";

    /// <summary>
    /// official tracker packet に埋め込む uuid。
    /// </summary>
    public string Uuid { get; init; } = "ibisduck-tracker";

    /// <summary>
    /// 起動時に選択する profile 名。
    /// </summary>
    public string ActiveProfileName { get; init; } = "default";

    /// <summary>
    /// profile 名を key にした tracker profile 設定一覧。
    /// </summary>
    public Dictionary<string, RuntimeTrackerProfileOptions> Profiles { get; init; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new(),
        };
}

/// <summary>
/// RuntimeHost の 1 つの tracker profile 設定。
/// </summary>
public sealed class RuntimeTrackerProfileOptions
{
    /// <summary>
    /// profile 単位の official packet publish 宛先。
    /// </summary>
    public RuntimeTrackerPublishOptions Publish { get; init; } = new();

    /// <summary>
    /// profile 単位の tracker engine 設定。
    /// </summary>
    public RuntimeTrackerEngineOptions Engine { get; init; } = new();

    /// <summary>
    /// robot tracker の profile override。
    /// </summary>
    public TrackerRobotTrackerOverrides RobotTracker { get; init; } = new();

    /// <summary>
    /// ball tracker の profile override。
    /// </summary>
    public TrackerBallTrackerOverrides BallTracker { get; init; } = new();

    /// <summary>
    /// kick detector の profile override。
    /// </summary>
    public TrackerKickDetectorOverrides KickDetector { get; init; } = new()
    {
        KickSpeedThresholdMmPerS = TrackerEngineSettings.DefaultKickDetectionSpeedThresholdMmPerS,
        ChipHeightThresholdMm = TrackerEngineSettings.DefaultChipHeightThresholdMm,
        ContactMarginMm = TrackerEngineSettings.DefaultContactMarginMm,
    };
}

/// <summary>
/// RuntimeHost の profile 単位の official packet publish 宛先設定。
/// </summary>
public sealed class RuntimeTrackerPublishOptions
{
    /// <summary>
    /// tracker packet を送信する multicast address。
    /// </summary>
    public string MulticastAddress { get; init; } = "224.5.23.2";

    /// <summary>
    /// tracker packet を送信する UDP port。
    /// </summary>
    public int Port { get; init; } = 10010;
}

/// <summary>
/// RuntimeHost の profile 単位の tracker engine 基本設定。
/// </summary>
public sealed class RuntimeTrackerEngineOptions
{
    /// <summary>
    /// event-time reorder buffer が遅延 packet を待つ幅。単位は ns。
    /// </summary>
    public long ReorderWindowNs { get; init; } = 100_000_000;

    /// <summary>
    /// 複数 camera detection を 1 world frame にまとめる時間幅。単位は ns。
    /// </summary>
    public long MergeWindowNs { get; init; } = 20_000_000;

    /// <summary>
    /// field length 差分で geometry reset を発火する threshold。単位は mm。
    /// </summary>
    public int GeometryResetFieldLengthThresholdMm { get; init; } = 500;

    /// <summary>
    /// field width 差分で geometry reset を発火する threshold。単位は mm。
    /// </summary>
    public int GeometryResetFieldWidthThresholdMm { get; init; } = 500;

    /// <summary>
    /// Kalman state 初期化時の速度分散。
    /// </summary>
    public double KalmanInitialVelocityVariance { get; init; } =
        TrackerEngineSettings.DefaultKalmanInitialVelocityVariance;

    /// <summary>
    /// Kalman predict の process noise に掛ける倍率。
    /// </summary>
    public double KalmanProcessNoiseScale { get; init; } =
        TrackerEngineSettings.DefaultKalmanProcessNoiseScale;

    /// <summary>
    /// Kalman measurement variance に掛ける倍率。
    /// </summary>
    public double MeasurementNoiseVarianceScale { get; init; } =
        TrackerEngineSettings.DefaultMeasurementNoiseVarianceScale;
}
