using Tracker.DebugHost.Components.Vision;

namespace Tracker.Tests;

/// <summary>
/// Issue #10 の Vision / diagnostics field 描画部整合 contract を固定する。
/// </summary>
public class VisionFieldRenderContractTests
{
    /// <summary>
    /// viewport state は drag 中の一時移動、commit 後の移動、wheel zoom、reset を 1 つの状態として扱う。
    /// </summary>
    [Fact]
    public void VisionFieldViewportState_AppliesDragWheelAndResetAsSingleState()
    {
        var state = new VisionFieldViewportState();

        state.BeginDrag(10, 20);
        state.DragTo(34, 55);

        Assert.Equal(24, state.ActiveTranslationX);
        Assert.Equal(35, state.ActiveTranslationY);
        Assert.Equal(24, state.TotalTranslationX);
        Assert.Equal(35, state.TotalTranslationY);

        state.CommitDrag();
        Assert.Equal(24, state.TranslationX);
        Assert.Equal(35, state.TranslationY);
        Assert.Equal(0, state.ActiveTranslationX);
        Assert.Equal(0, state.ActiveTranslationY);

        state.ApplyWheelDelta(-300);
        Assert.Equal(2, state.Zoom);
        state.ApplyWheelDelta(900);
        Assert.Equal(1, state.Zoom);

        state.Reset();
        Assert.Equal(1, state.Zoom);
        Assert.Equal(0, state.TotalTranslationX);
        Assert.Equal(0, state.TotalTranslationY);
        Assert.False(state.IsDragging);
    }

    /// <summary>
    /// split は split 用 component、overlay は overlay 用 component を別境界として使う。
    /// </summary>
    [Fact]
    public void VisionAndDiagnosticsFieldMarkup_UseSeparateSplitAndOverlayFieldComponents()
    {
        var homeMarkup = ReadRepositoryFile("Tracker/Tracker.DebugHost/Components/Pages/Home.razor");
        var diagnosticsMarkup = ReadRepositoryFile("Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor");
        var diagnosticsOverlayMarkup = ReadRepositoryFile("Tracker/Tracker.DebugHost/Components/Pages/DiagnosticsFieldOverlayCanvas.razor");
        var overlayComponentMarkup = ReadRepositoryFile("Tracker/Tracker.DebugHost/Components/Vision/VisionFieldOverlayCanvas.razor");

        var overlayMarkup = ExtractBetween(homeMarkup, "@if (comparisonMode == VisionLiveComparisonMode.Overlay)", "else");

        Assert.Contains("<VisionFieldCanvas", homeMarkup, StringComparison.Ordinal);
        Assert.Contains("<VisionFieldCanvas", diagnosticsMarkup, StringComparison.Ordinal);
        Assert.Contains("<VisionFieldOverlayCanvas", overlayMarkup, StringComparison.Ordinal);
        Assert.Contains("<VisionFieldOverlayCanvas", diagnosticsOverlayMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("<VisionFieldCanvas", overlayMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("vision-comparison-overlay-layer", overlayMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("legend", overlayComponentMarkup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<select", overlayComponentMarkup, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractBetween(string text, string start, string end)
    {
        var startIndex = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Start marker was not found: {start}");
        var endIndex = text.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(endIndex >= 0, $"End marker was not found: {end}");
        return text[startIndex..endIndex];
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Tracker/Tracker.DebugHost/Program.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing Tracker/Tracker.DebugHost/Program.cs was not found.");
    }
}
