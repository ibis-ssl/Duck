using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Components.Vision;

/// <summary>
/// Vision field の zoom / pan / drag 状態。
/// </summary>
public sealed class VisionFieldViewportState
{
    private SvgPoint? dragStartPoint;

    /// <summary>
    /// 現在の zoom 倍率。
    /// </summary>
    public double Zoom { get; private set; } = 1;

    /// <summary>
    /// commit 済み X 方向移動量。
    /// </summary>
    public double TranslationX { get; private set; }

    /// <summary>
    /// commit 済み Y 方向移動量。
    /// </summary>
    public double TranslationY { get; private set; }

    /// <summary>
    /// drag 中の X 方向一時移動量。
    /// </summary>
    public double ActiveTranslationX { get; private set; }

    /// <summary>
    /// drag 中の Y 方向一時移動量。
    /// </summary>
    public double ActiveTranslationY { get; private set; }

    /// <summary>
    /// drag 中かどうか。
    /// </summary>
    public bool IsDragging => dragStartPoint is not null;

    /// <summary>
    /// 描画に使う合計 X 方向移動量。
    /// </summary>
    public double TotalTranslationX => TranslationX + ActiveTranslationX;

    /// <summary>
    /// 描画に使う合計 Y 方向移動量。
    /// </summary>
    public double TotalTranslationY => TranslationY + ActiveTranslationY;

    /// <summary>
    /// viewport state を初期値へ戻す。
    /// </summary>
    public void Reset()
    {
        Zoom = 1;
        TranslationX = 0;
        TranslationY = 0;
        ActiveTranslationX = 0;
        ActiveTranslationY = 0;
        dragStartPoint = null;
    }

    /// <summary>
    /// wheel delta から zoom 倍率を更新する。
    /// </summary>
    public void ApplyWheelDelta(double deltaY)
    {
        Zoom = Math.Max(1, Zoom - (deltaY / 300));
    }

    /// <summary>
    /// drag を開始する。
    /// </summary>
    public void BeginDrag(double clientX, double clientY)
    {
        dragStartPoint = new SvgPoint(clientX, clientY);
        ActiveTranslationX = 0;
        ActiveTranslationY = 0;
    }

    /// <summary>
    /// drag 中の一時移動量を更新する。
    /// </summary>
    public void DragTo(double clientX, double clientY)
    {
        if (dragStartPoint is null)
        {
            return;
        }

        ActiveTranslationX = clientX - dragStartPoint.Value.X;
        ActiveTranslationY = clientY - dragStartPoint.Value.Y;
    }

    /// <summary>
    /// drag 中の一時移動量を commit 済み移動量へ反映する。
    /// </summary>
    public void CommitDrag()
    {
        if (dragStartPoint is null)
        {
            return;
        }

        TranslationX += ActiveTranslationX;
        TranslationY += ActiveTranslationY;
        ActiveTranslationX = 0;
        ActiveTranslationY = 0;
        dragStartPoint = null;
    }
}
