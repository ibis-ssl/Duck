using Tracker.Server.Components.Pages;

namespace Tracker.Tests;

public class DiagnosticsRenderLayoutStateTests
{
    /// <summary>
    /// drag delta が rem に変換され、field 表示高さへ反映されることを確認する。
    /// </summary>
    [Fact]
    public void ApplyDragDelta_ConvertsPixelsToRem()
    {
        var height = DiagnosticsRenderLayoutState.ApplyDragDeltaRem(29, 160);

        Assert.Equal(39, height);
    }

    /// <summary>
    /// field 表示高さが detail 領域を壊すほど小さくならないことを確認する。
    /// </summary>
    [Fact]
    public void ApplyDragDelta_ClampsToMinimumHeight()
    {
        var height = DiagnosticsRenderLayoutState.ApplyDragDeltaRem(29, -400);

        Assert.Equal(DiagnosticsRenderLayoutState.MinHeightRem, height);
    }

    /// <summary>
    /// 4K などの高い viewport で field を広げても上限内に収めることを確認する。
    /// </summary>
    [Fact]
    public void ApplyDragDelta_ClampsToMaximumHeight()
    {
        var height = DiagnosticsRenderLayoutState.ApplyDragDeltaRem(29, 2_000);

        Assert.Equal(DiagnosticsRenderLayoutState.MaxHeightRem, height);
    }
}
