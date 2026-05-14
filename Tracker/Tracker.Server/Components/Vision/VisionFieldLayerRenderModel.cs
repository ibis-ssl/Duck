namespace Tracker.Server.Components.Vision;

/// <summary>
/// Vision field surface に描画する object layer。
/// </summary>
/// <param name="LayerName">layer の表示名。</param>
/// <param name="IsVisible">layer が描画対象かどうか。</param>
/// <param name="MarkerStroke">marker の強調 stroke。null の場合は標準 stroke を使う。</param>
/// <param name="Balls">layer に描画する balls。</param>
/// <param name="RobotsYellow">layer に描画する yellow robots。</param>
/// <param name="RobotsBlue">layer に描画する blue robots。</param>
/// <param name="CssClass">layer group に追加する CSS class。</param>
public sealed record VisionFieldLayerRenderModel(
    string LayerName,
    bool IsVisible,
    string? MarkerStroke,
    IReadOnlyList<SSL_DetectionBall> Balls,
    IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
    IReadOnlyList<SSL_DetectionRobot> RobotsBlue,
    string? CssClass = null)
{
    /// <summary>
    /// 描画可能 object 数。
    /// </summary>
    public int DrawableCount => Balls.Count + RobotsYellow.Count + RobotsBlue.Count;
}
