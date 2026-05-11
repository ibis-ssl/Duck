namespace Tracker.Core;

/// <summary>
/// Core engine が統合した tracked robot の状態。
/// camera-local robot track を team と robot id 単位で merge した結果を表す。
/// </summary>
public sealed class TrackedRobotState
{
    /// <summary>
    /// robot の team。
    /// </summary>
    public TrackerTeam Team { get; init; }

    /// <summary>
    /// team 内の robot id。
    /// </summary>
    public uint RobotId { get; init; }

    /// <summary>
    /// robot 中心の X 座標。単位は mm。
    /// </summary>
    public double XMm { get; init; }

    /// <summary>
    /// robot 中心の Y 座標。単位は mm。
    /// </summary>
    public double YMm { get; init; }

    /// <summary>
    /// robot orientation。単位は rad。
    /// </summary>
    public double OrientationRad { get; init; }

    /// <summary>
    /// X 方向速度。単位は mm/s。
    /// </summary>
    public double VXMmPerS { get; init; }

    /// <summary>
    /// Y 方向速度。単位は mm/s。
    /// </summary>
    public double VYMmPerS { get; init; }

    /// <summary>
    /// 角速度。単位は rad/s。
    /// </summary>
    public double AngularVelocityRadPerS { get; init; }

    /// <summary>
    /// 出力 visibility。0 から 1 の範囲で、欠測予測では時間経過により減衰する。
    /// </summary>
    public float Visibility { get; init; }

    /// <summary>
    /// merge と出力判定で使う品質値。大きいほど信頼度が高い。
    /// </summary>
    public double Quality { get; init; }

    /// <summary>
    /// この frame の primary ball と接触中なら true。
    /// </summary>
    public bool HasBallContact { get; init; }
}

/// <summary>
/// SSL-Vision detection と official tracker proto の team 境界で使う robot team。
/// </summary>
public enum TrackerTeam
{
    /// <summary>
    /// team を特定できない状態。
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// Yellow team。
    /// </summary>
    Yellow = 1,
    /// <summary>
    /// Blue team。
    /// </summary>
    Blue = 2,
}
