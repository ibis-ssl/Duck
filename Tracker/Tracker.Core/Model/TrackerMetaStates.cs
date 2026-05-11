namespace Tracker.Core;

/// <summary>
/// AutoRef 向けに保持する kick event と kicked ball の継続状態。
/// official proto へ出す kicked_ball の元データとして使う。
/// </summary>
public sealed class KickEventState
{
    /// <summary>
    /// kick 開始位置の X 座標。単位は mm。
    /// </summary>
    public double StartXMm { get; init; }

    /// <summary>
    /// kick 開始位置の Y 座標。単位は mm。
    /// </summary>
    public double StartYMm { get; init; }

    /// <summary>
    /// kick 開始時刻。単位は ns。
    /// </summary>
    public long StartTimestampNs { get; init; }

    /// <summary>
    /// kick 開始時の X 方向速度。単位は mm/s。
    /// </summary>
    public double InitialVelocityXMmPerS { get; init; }

    /// <summary>
    /// kick 開始時の Y 方向速度。単位は mm/s。
    /// </summary>
    public double InitialVelocityYMmPerS { get; init; }

    /// <summary>
    /// kick 開始時の Z 方向速度。単位は mm/s。
    /// </summary>
    public double InitialVelocityZMmPerS { get; init; }

    /// <summary>
    /// kick 対象 ball の内部 track id。
    /// </summary>
    public int BallTrackId { get; init; }

    /// <summary>
    /// 最新 frame での平面速度。単位は mm/s。
    /// </summary>
    public double LatestSpeedMmPerS { get; init; }

    /// <summary>
    /// 最新更新時刻。単位は ns。
    /// </summary>
    public long LatestUpdateTimestampNs { get; init; }

    /// <summary>
    /// 停止位置の X 座標。停止位置が未確定、またはまだ移動中なら null。
    /// </summary>
    public double? StopXMm { get; init; }

    /// <summary>
    /// 停止位置の Y 座標。停止位置が未確定、またはまだ移動中なら null。
    /// </summary>
    public double? StopYMm { get; init; }

    /// <summary>
    /// 停止時刻。単位は ns。停止時刻が未確定、またはまだ移動中なら null。
    /// </summary>
    public long? StopTimestampNs { get; init; }

    /// <summary>
    /// kicker と推定した robot id。接触履歴から特定できない場合は null。
    /// </summary>
    public uint? KickerRobotId { get; init; }

    /// <summary>
    /// kick 種別候補。未分類の場合は null。
    /// </summary>
    public string? KickKind { get; init; }

    /// <summary>
    /// kicked ball がまだ移動中なら true。false の場合、official kicked_ball は出力しない。
    /// </summary>
    public bool IsStillMoving { get; init; }
}

/// <summary>
/// primary ball と robot の接触状態。
/// contact changed event と kick detector の入力として使う。
/// </summary>
public sealed class BallContactState
{
    /// <summary>
    /// 現在接触中なら true。
    /// </summary>
    public bool IsInContact { get; init; }

    /// <summary>
    /// 現在接触中の robot id。接触中でない場合は null。
    /// </summary>
    public uint? ContactingRobotId { get; init; }

    /// <summary>
    /// 現在接触中の team。接触中でない場合は Unknown。
    /// </summary>
    public TrackerTeam ContactingTeam { get; init; }

    /// <summary>
    /// 最後に接触した robot id。接触履歴がない場合は null。
    /// </summary>
    public uint? LastRobotId { get; init; }

    /// <summary>
    /// 最後に接触した team。接触履歴がない場合は Unknown。
    /// </summary>
    public TrackerTeam LastTeam { get; init; }

    /// <summary>
    /// 最後に接触した時刻。単位は ns。
    /// </summary>
    public long LastContactTimestampNs { get; init; }
}

/// <summary>
/// primary ball が field 外へ出た状態と横切り位置。
/// AutoRef の ball left field 判定境界で使う。
/// </summary>
public sealed class BallLeftFieldState
{
    /// <summary>
    /// field 外に出ている場合は true。
    /// </summary>
    public bool IsOutOfField { get; init; }

    /// <summary>
    /// 横切った boundary 名。判定できない、または field 内の場合は null。
    /// </summary>
    public string? BoundaryName { get; init; }

    /// <summary>
    /// boundary crossing の X 座標。単位は mm。
    /// </summary>
    public double CrossingXMm { get; init; }

    /// <summary>
    /// boundary crossing の Y 座標。単位は mm。
    /// </summary>
    public double CrossingYMm { get; init; }

    /// <summary>
    /// boundary crossing の推定時刻。単位は ns。
    /// </summary>
    public long CrossingTimestampNs { get; init; }
}
