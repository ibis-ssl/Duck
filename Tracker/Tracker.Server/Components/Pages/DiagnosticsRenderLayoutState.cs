using System.Globalization;

namespace Tracker.Server.Components.Pages;

/// <summary>
/// diagnostics render snapshot の field/detail 比率を制御する表示高さ計算。
/// </summary>
public static class DiagnosticsRenderLayoutState
{
    /// <summary>
    /// detail 領域を残すための field 表示高さの下限。
    /// </summary>
    public const double MinHeightRem = 18;

    /// <summary>
    /// 4K viewport でも操作可能な範囲へ収める field 表示高さの上限。
    /// </summary>
    public const double MaxHeightRem = 80;

    /// <summary>
    /// render snapshot 表示開始時の field 表示高さ。
    /// </summary>
    public const double DefaultHeightRem = 32;

    /// <summary>
    /// frame timeline を縮小しても選択操作を維持するための幅の下限。
    /// </summary>
    public const double MinTimelineWidthRem = 12;

    /// <summary>
    /// frame timeline が detail 領域を圧迫しすぎないための幅の上限。
    /// </summary>
    public const double MaxTimelineWidthRem = 36;

    /// <summary>
    /// diagnostics 初期表示時の frame timeline 幅。
    /// </summary>
    public const double DefaultTimelineWidthRem = 21;

    private const double RootFontSizePx = 16;

    /// <summary>
    /// drag の縦方向 pixel 差分を rem に変換し、表示高さの許容範囲へ収める。
    /// </summary>
    public static double ApplyDragDeltaRem(double startHeightRem, double deltaPixels)
    {
        return ClampHeightRem(startHeightRem + (deltaPixels / RootFontSizePx));
    }

    /// <summary>
    /// field 表示高さを許容範囲へ収める。
    /// </summary>
    public static double ClampHeightRem(double heightRem)
    {
        return Math.Clamp(heightRem, MinHeightRem, MaxHeightRem);
    }

    /// <summary>
    /// drag の横方向 pixel 差分を rem に変換し、frame timeline 幅の許容範囲へ収める。
    /// </summary>
    public static double ApplyTimelineDragDeltaRem(double startWidthRem, double deltaPixels)
    {
        return ClampTimelineWidthRem(startWidthRem + (deltaPixels / RootFontSizePx));
    }

    /// <summary>
    /// frame timeline 幅を許容範囲へ収める。
    /// </summary>
    public static double ClampTimelineWidthRem(double widthRem)
    {
        return Math.Clamp(widthRem, MinTimelineWidthRem, MaxTimelineWidthRem);
    }

    /// <summary>
    /// CSS custom property として field 表示高さを渡す style 文字列を作る。
    /// </summary>
    public static string ToCssVariable(double heightRem)
    {
        return $"--diagnostics-render-height: {ClampHeightRem(heightRem).ToString("0.###", CultureInfo.InvariantCulture)}rem;";
    }

    /// <summary>
    /// CSS custom property として frame timeline 幅を渡す style 文字列を作る。
    /// </summary>
    public static string ToTimelineCssVariable(double widthRem)
    {
        return $"--diagnostics-timeline-width: {ClampTimelineWidthRem(widthRem).ToString("0.###", CultureInfo.InvariantCulture)}rem;";
    }
}
