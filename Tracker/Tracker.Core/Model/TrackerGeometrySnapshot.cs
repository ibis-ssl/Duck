namespace Tracker.Core;

/// <summary>
/// SSL-Vision geometry を Core engine 内部単位へ写した field snapshot。
/// geometry reset 判定と UI 表示の境界で使う。
/// </summary>
public sealed class TrackerGeometrySnapshot
{
    /// <summary>
    /// field の全長。単位は mm。
    /// </summary>
    public int FieldLengthMm { get; init; }

    /// <summary>
    /// field の全幅。単位は mm。
    /// </summary>
    public int FieldWidthMm { get; init; }

    /// <summary>
    /// goal mouth の幅。単位は mm。
    /// </summary>
    public int GoalWidthMm { get; init; }

    /// <summary>
    /// goal の奥行き。単位は mm。
    /// </summary>
    public int GoalDepthMm { get; init; }

    /// <summary>
    /// field 周辺 boundary の幅。単位は mm。
    /// </summary>
    public int BoundaryWidthMm { get; init; }

    /// <summary>
    /// goal line 側 boundary の幅。単位は mm で、proto に値がない場合は BoundaryWidthMm と同じ値を使う。
    /// </summary>
    public int BoundaryWidthGoalLineMm { get; init; }

    /// <summary>
    /// penalty area の奥行き。単位は mm。
    /// </summary>
    public int PenaltyAreaDepthMm { get; init; }

    /// <summary>
    /// penalty area の幅。単位は mm。
    /// </summary>
    public int PenaltyAreaWidthMm { get; init; }

    /// <summary>
    /// center circle の半径。単位は mm。
    /// </summary>
    public int CenterCircleRadiusMm { get; init; }

    /// <summary>
    /// line thickness。単位は mm で、proto に値がない場合は既定値を保持する。
    /// </summary>
    public int LineThicknessMm { get; init; }

    /// <summary>
    /// field の line segment 一覧。順序は SSL-Vision geometry の順序を維持する。
    /// </summary>
    public IReadOnlyList<TrackerGeometryLineSegment> FieldLines { get; init; } = [];

    /// <summary>
    /// field の circular arc 一覧。順序は SSL-Vision geometry の順序を維持する。
    /// </summary>
    public IReadOnlyList<TrackerGeometryCircularArc> FieldArcs { get; init; } = [];
}

/// <summary>
/// SSL-Vision field line を内部単位へ写した線分。
/// </summary>
public sealed class TrackerGeometryLineSegment
{
    /// <summary>
    /// geometry line 名。proto の Name をそのまま保持する。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 始点 X 座標。単位は mm。
    /// </summary>
    public double P1XMm { get; init; }

    /// <summary>
    /// 始点 Y 座標。単位は mm。
    /// </summary>
    public double P1YMm { get; init; }

    /// <summary>
    /// 終点 X 座標。単位は mm。
    /// </summary>
    public double P2XMm { get; init; }

    /// <summary>
    /// 終点 Y 座標。単位は mm。
    /// </summary>
    public double P2YMm { get; init; }

    /// <summary>
    /// 線幅。単位は mm。
    /// </summary>
    public double ThicknessMm { get; init; }

    /// <summary>
    /// SSL-Vision が提供する field shape 種別。
    /// </summary>
    public SSL_FieldShapeType Type { get; init; } = SSL_FieldShapeType.Undefined;
}

/// <summary>
/// SSL-Vision field arc を内部単位へ写した円弧。
/// </summary>
public sealed class TrackerGeometryCircularArc
{
    /// <summary>
    /// geometry arc 名。proto の Name をそのまま保持する。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 中心 X 座標。単位は mm。
    /// </summary>
    public double CenterXMm { get; init; }

    /// <summary>
    /// 中心 Y 座標。単位は mm。
    /// </summary>
    public double CenterYMm { get; init; }

    /// <summary>
    /// 半径。単位は mm。
    /// </summary>
    public double RadiusMm { get; init; }

    /// <summary>
    /// 開始角。単位は rad。
    /// </summary>
    public double A1Rad { get; init; }

    /// <summary>
    /// 終了角。単位は rad。
    /// </summary>
    public double A2Rad { get; init; }

    /// <summary>
    /// 線幅。単位は mm。
    /// </summary>
    public double ThicknessMm { get; init; }

    /// <summary>
    /// SSL-Vision が提供する field shape 種別。
    /// </summary>
    public SSL_FieldShapeType Type { get; init; } = SSL_FieldShapeType.Undefined;
}
