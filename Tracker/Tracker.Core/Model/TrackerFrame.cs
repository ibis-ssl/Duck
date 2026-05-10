namespace Tracker.Core;

/// <summary>
/// Core engine が確定した 1 つの world frame を表す内部モデル。
/// UI、diagnostics、official tracker proto 生成の境界で共有する。
/// </summary>
public sealed class TrackerFrame
{
    /// <summary>
    /// Core engine 内で単調増加する frame number。state clear では 1 に戻さない。
    /// </summary>
    public uint FrameNumber { get; init; }

    /// <summary>
    /// world を構成した観測の基準時刻。単位は ns で、detection では TCapture を優先し、0 以下なら TSent を使う。
    /// </summary>
    public long DataTimestampNs { get; init; }

    /// <summary>
    /// engine がこの frame を確定したローカル処理時刻。単位は ns で、diagnostics 用に保持する。
    /// </summary>
    public long ProcessedAtNs { get; init; }

    /// <summary>
    /// frame 確定時点の field geometry snapshot。geometry 未受信の場合は null。
    /// </summary>
    public TrackerGeometrySnapshot? GeometrySnapshot { get; init; }

    /// <summary>
    /// tracked ball の出力一覧。先頭要素は primary ball として扱う。
    /// </summary>
    public IReadOnlyList<TrackedBallState> Balls { get; init; } = [];

    /// <summary>
    /// primary ball の内部 track id。出力対象 ball がない場合は null。
    /// </summary>
    public int? PrimaryBallTrackId { get; init; }

    /// <summary>
    /// tracked robot の出力一覧。team と robot id の安定順で並ぶ。
    /// </summary>
    public IReadOnlyList<TrackedRobotState> Robots { get; init; } = [];

    /// <summary>
    /// 継続中の kicked ball 状態。kick 未検出または停止後に出力しない場合は null。
    /// </summary>
    public KickEventState? KickedBall { get; init; }

    /// <summary>
    /// primary ball に対する最新 contact 状態。接触履歴がない場合は null。
    /// </summary>
    public BallContactState? LatestContact { get; init; }

    /// <summary>
    /// primary ball の field 外退出状態。field 内、geometry 不明、または退出未検出の場合は null。
    /// </summary>
    public BallLeftFieldState? BallLeftField { get; init; }

    /// <summary>
    /// frame 生成時の source や profile を示す補助 metadata。
    /// </summary>
    public TrackerFrameMetadata Metadata { get; init; } = new();

    /// <summary>
    /// この world frame に統合された raw detection frame の snapshot。
    /// </summary>
    public IReadOnlyList<TrackerSourceDetectionFrame> SourceDetections { get; init; } = [];
}

/// <summary>
/// TrackerFrame に付与する source と profile の補助情報。
/// </summary>
public sealed class TrackerFrameMetadata
{
    /// <summary>
    /// frame の出力元名。未設定の場合は null。
    /// </summary>
    public string? SourceName { get; init; }

    /// <summary>
    /// frame 確定時に有効だった tracker profile 名。未設定の場合は null。
    /// </summary>
    public string? ProfileName { get; init; }
}
