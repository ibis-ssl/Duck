using Tracker.DebugHost.Tracking;

namespace Tracker.DebugHost.Components.Pages;

/// <summary>
/// diagnostics Field overlay component に渡す描画 model。
/// </summary>
/// <param name="Geometry">overlay Field の geometry。null の場合は geometry なし empty state を表示する。</param>
/// <param name="EmptyState">Field 全体を描画できない理由。</param>
/// <param name="Layers">overlay layer の描画 model。</param>
public sealed record DiagnosticsFieldOverlayRenderModel(
    SSL_GeometryData? Geometry,
    string? EmptyState,
    IReadOnlyList<DiagnosticsFieldOverlayLayerRenderModel> Layers);

/// <summary>
/// diagnostics Field overlay の 1 layer 描画 model。
/// </summary>
/// <param name="LayerKey">layer 識別子。</param>
/// <param name="LayerName">UI 表示用 layer 名。</param>
/// <param name="SourceLabel">source selector の表示名。</param>
/// <param name="Status">layer の状態。</param>
/// <param name="NearestDeltaNs">nearest timestamp delta。</param>
/// <param name="DrawableCount">描画可能 object 数。</param>
/// <param name="IsVisible">layer が表示対象かどうか。</param>
/// <param name="LegendNote">legend に追加表示する短い補足。</param>
/// <param name="AccentColor">layer 識別用の色。</param>
/// <param name="Balls">layer に描画する balls。</param>
/// <param name="RobotsYellow">layer に描画する yellow robots。</param>
/// <param name="RobotsBlue">layer に描画する blue robots。</param>
public sealed record DiagnosticsFieldOverlayLayerRenderModel(
    TrackerDiagnosticsOverlayLayerKey LayerKey,
    string LayerName,
    string SourceLabel,
    string Status,
    long? NearestDeltaNs,
    int DrawableCount,
    bool IsVisible,
    string? LegendNote,
    string AccentColor,
    IReadOnlyList<SSL_DetectionBall> Balls,
    IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
    IReadOnlyList<SSL_DetectionRobot> RobotsBlue);
