using Tracker.Core;

namespace Tracker.Server.Tracking;

/// <summary>
/// appsettings の Tracker section に対応する tracker 全体の設定 schema。
/// </summary>
public sealed class TrackerOptions
{
    /// <summary>
    /// tracker pipeline を有効化するかどうか。
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// tracker wrapper packet を UDP publish するかどうか。
    /// </summary>
    public bool PublishUdp { get; init; } = true;

    /// <summary>
    /// live official tracker packet receiver の起動設定。
    /// </summary>
    public TrackerReceiveOptions Receive { get; init; } = new();

    /// <summary>
    /// publish する wrapper packet の source name。
    /// </summary>
    public string SourceName { get; init; } = "ibisduck-tracker";

    /// <summary>
    /// publish する wrapper packet の uuid。
    /// </summary>
    public string Uuid { get; init; } = "ibisduck-tracker";

    /// <summary>
    /// 起動時に選択する profile 名。
    /// </summary>
    public string ActiveProfileName { get; init; } = "default";

    /// <summary>
    /// tracker diagnostics と capture sidecar の出力設定。
    /// </summary>
    public TrackerDiagnosticsOptions Diagnostics { get; init; } = new();

    /// <summary>
    /// profile 値に重ねる runtime override の初期値。
    /// </summary>
    public TrackerRuntimeOverrides RuntimeOverrides { get; init; } = new();

    /// <summary>
    /// profile 名を key にした tracker profile 設定一覧。
    /// </summary>
    public Dictionary<string, TrackerProfileOptions> Profiles { get; init; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new(),
        };
}

/// <summary>
/// tracker diagnostics log の出力先を表す appsettings schema。
/// </summary>
public sealed class TrackerDiagnosticsOptions
{
    /// <summary>
    /// diagnostics log を固定出力する path。未指定時は capture sidecar または自動生成 path を使う。
    /// </summary>
    public string? FilePath { get; init; }
}

/// <summary>
/// live official tracker packet receiver の appsettings schema。
/// </summary>
public sealed class TrackerReceiveOptions
{
    /// <summary>
    /// official tracker packet の UDP 受信を有効化するかどうか。
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// receiver が監視する multicast group address。未指定時は起動時に解決済みの publish address を使う。
    /// </summary>
    public string? MulticastAddress { get; init; }

    /// <summary>
    /// receiver が監視する UDP port。未指定時は起動時に解決済みの publish port を使う。
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// multicast join に使う local IPv4 address。未指定時は候補 interface から選ぶ。
    /// </summary>
    public string? InterfaceAddress { get; init; }
}

/// <summary>
/// 1 つの tracker profile に含まれる publish、engine、検出器 override の設定 schema。
/// </summary>
public sealed class TrackerProfileOptions
{
    /// <summary>
    /// profile 単位の publish 宛先設定。
    /// </summary>
    public TrackerPublishProfileOptions Publish { get; init; } = new();

    /// <summary>
    /// profile 単位の engine 基本設定。
    /// </summary>
    public TrackerEngineProfileOptions Engine { get; init; } = new();

    /// <summary>
    /// robot tracker に適用する profile override。
    /// </summary>
    public TrackerRobotTrackerOverrides RobotTracker { get; init; } = new();

    /// <summary>
    /// ball tracker に適用する profile override。
    /// </summary>
    public TrackerBallTrackerOverrides BallTracker { get; init; } = new();

    /// <summary>
    /// kick detector に適用する profile override。
    /// </summary>
    public TrackerKickDetectorOverrides KickDetector { get; init; } = new();
}

/// <summary>
/// profile ごとの tracker packet publish 宛先を表す appsettings schema。
/// </summary>
public sealed class TrackerPublishProfileOptions
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
/// profile ごとの tracker engine 基本設定を表す appsettings schema。
/// </summary>
public sealed class TrackerEngineProfileOptions
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
    public double KalmanInitialVelocityVariance { get; init; } = TrackerEngineSettings.DefaultKalmanInitialVelocityVariance;

    /// <summary>
    /// Kalman predict の process noise に掛ける倍率。
    /// </summary>
    public double KalmanProcessNoiseScale { get; init; } = TrackerEngineSettings.DefaultKalmanProcessNoiseScale;

    /// <summary>
    /// Kalman measurement variance に掛ける倍率。
    /// </summary>
    public double MeasurementNoiseVarianceScale { get; init; } = TrackerEngineSettings.DefaultMeasurementNoiseVarianceScale;
}
