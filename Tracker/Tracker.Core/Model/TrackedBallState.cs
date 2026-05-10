namespace Tracker.Core;

/// <summary>
/// Core engine が統合した tracked ball の状態。
/// 内部 world model と proto 変換の境界で使う。
/// </summary>
public sealed class TrackedBallState
{
    /// <summary>
    /// engine 内で割り当てる merged ball の内部 track id。
    /// </summary>
    public int InternalTrackId { get; init; }

    /// <summary>
    /// ball の X 座標。単位は mm。
    /// </summary>
    public double XMm { get; init; }

    /// <summary>
    /// ball の Y 座標。単位は mm。
    /// </summary>
    public double YMm { get; init; }

    /// <summary>
    /// ball の Z 座標。単位は mm。
    /// </summary>
    public double ZMm { get; init; }

    /// <summary>
    /// X 方向速度。単位は mm/s。
    /// </summary>
    public double VXMmPerS { get; init; }

    /// <summary>
    /// Y 方向速度。単位は mm/s。
    /// </summary>
    public double VYMmPerS { get; init; }

    /// <summary>
    /// Z 方向速度。単位は mm/s。
    /// </summary>
    public double VZMmPerS { get; init; }

    /// <summary>
    /// 出力 visibility。0 から 1 の範囲で、欠測予測では時間経過により減衰する。
    /// </summary>
    public float Visibility { get; init; }

    /// <summary>
    /// この ball state に寄与した camera id 一覧。
    /// </summary>
    public IReadOnlyList<uint> SourceCameraIds { get; init; } = [];

    /// <summary>
    /// flying ball として扱う場合は true。
    /// </summary>
    public bool IsFlying { get; init; }

    /// <summary>
    /// 最後に観測された event timestamp。単位は ns。
    /// </summary>
    public long LastVisibleTimestampNs { get; init; }

    /// <summary>
    /// merge と出力判定で使う品質値。大きいほど信頼度が高い。
    /// </summary>
    public double Quality { get; init; }
}
