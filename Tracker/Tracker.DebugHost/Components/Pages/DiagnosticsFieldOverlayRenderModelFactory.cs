using Tracker.DebugHost.Components.Vision;
using Tracker.DebugHost.Tracking;

namespace Tracker.DebugHost.Components.Pages;

/// <summary>
/// diagnostics Field overlay 用の描画 model を作成する。
/// </summary>
internal static class DiagnosticsFieldOverlayRenderModelFactory
{
    private const string LayerAAccentColor = "#68d8ff";
    private const string LayerBAccentColor = "#ff7ad9";

    /// <summary>
    /// 現在の render snapshot と左右 Field source selector から overlay 描画 model を作る。
    /// </summary>
    public static DiagnosticsFieldOverlayRenderModel Create(
        TrackerRenderSnapshotView? selectedRenderSnapshot,
        TrackedVisionViewState trackedRenderView,
        TrackerDiagnosticsComparisonViewState comparisonViewState,
        IReadOnlyList<TrackerDiagnosticsFieldOverlayLayerSource> layerSources,
        TrackerDiagnosticsFieldSourceFrame? layerAFrame,
        TrackerDiagnosticsFieldSourceFrame? layerBFrame)
    {
        var geometry = selectedRenderSnapshot is null
            ? null
            : DiagnosticsFieldViewFactory.CreateGeometry(selectedRenderSnapshot.Frame.GeometrySnapshot);
        var layers = layerSources
            .Select(layer => CreateLayer(
                selectedRenderSnapshot,
                trackedRenderView,
                comparisonViewState,
                layer,
                layer.LayerKey == TrackerDiagnosticsOverlayLayerKey.LayerA ? layerAFrame : layerBFrame))
            .ToArray();

        return new DiagnosticsFieldOverlayRenderModel(
            geometry,
            geometry is null ? "Render snapshot geometry was not found." : null,
            layers);
    }

    private static DiagnosticsFieldOverlayLayerRenderModel CreateLayer(
        TrackerRenderSnapshotView? selectedRenderSnapshot,
        TrackedVisionViewState trackedRenderView,
        TrackerDiagnosticsComparisonViewState comparisonViewState,
        TrackerDiagnosticsFieldOverlayLayerSource layerSource,
        TrackerDiagnosticsFieldSourceFrame? trackerFrame)
    {
        var fieldModel = CreateFieldRenderModel(
            selectedRenderSnapshot,
            trackedRenderView,
            comparisonViewState,
            layerSource.Source,
            trackerFrame);
        var drawableCount = fieldModel.Balls.Count + fieldModel.RobotsYellow.Count + fieldModel.RobotsBlue.Count;

        return new DiagnosticsFieldOverlayLayerRenderModel(
            layerSource.LayerKey,
            layerSource.LayerName,
            fieldModel.Title,
            OverlayLayerStatus(layerSource.Source, trackerFrame),
            trackerFrame?.TimestampDeltaNs,
            drawableCount,
            layerSource.IsVisible,
            layerSource.LegendNote,
            OverlayLayerAccentColor(layerSource.LayerKey),
            fieldModel.Balls,
            fieldModel.RobotsYellow,
            fieldModel.RobotsBlue);
    }

    private static DiagnosticsFieldRenderData CreateFieldRenderModel(
        TrackerRenderSnapshotView? selectedRenderSnapshot,
        TrackedVisionViewState trackedRenderView,
        TrackerDiagnosticsComparisonViewState comparisonViewState,
        TrackerDiagnosticsFieldSource source,
        TrackerDiagnosticsFieldSourceFrame? trackerFrame)
    {
        return source.Kind switch
        {
            TrackerDiagnosticsFieldSourceKind.VisionInput => new DiagnosticsFieldRenderData(
                FieldSourceLabel(comparisonViewState, source),
                DiagnosticsFieldViewFactory.CreateTrackerSourceBalls(trackerFrame?.SemanticSummary),
                DiagnosticsFieldViewFactory.CreateTrackerSourceYellowRobots(trackerFrame?.SemanticSummary),
                DiagnosticsFieldViewFactory.CreateTrackerSourceBlueRobots(trackerFrame?.SemanticSummary)),
            TrackerDiagnosticsFieldSourceKind.IbisTracker => new DiagnosticsFieldRenderData(
                FieldSourceLabel(comparisonViewState, source),
                DiagnosticsFieldViewFactory.CreateTrackerSourceBalls(trackerFrame?.SemanticSummary),
                DiagnosticsFieldViewFactory.CreateTrackerSourceYellowRobots(trackerFrame?.SemanticSummary),
                DiagnosticsFieldViewFactory.CreateTrackerSourceBlueRobots(trackerFrame?.SemanticSummary)),
            _ => new DiagnosticsFieldRenderData(
                FieldSourceLabel(comparisonViewState, source),
                DiagnosticsFieldViewFactory.CreateTrackerSourceBalls(trackerFrame?.SemanticSummary),
                DiagnosticsFieldViewFactory.CreateTrackerSourceYellowRobots(trackerFrame?.SemanticSummary),
                DiagnosticsFieldViewFactory.CreateTrackerSourceBlueRobots(trackerFrame?.SemanticSummary)),
        };
    }

    private static string FieldSourceLabel(
        TrackerDiagnosticsComparisonViewState comparisonViewState,
        TrackerDiagnosticsFieldSource source)
    {
        return comparisonViewState.FieldSourceOptions
            .FirstOrDefault(option => option.Source == source)
            ?.Label ?? source.Kind.ToString();
    }

    private static string OverlayLayerStatus(
        TrackerDiagnosticsFieldSource source,
        TrackerDiagnosticsFieldSourceFrame? frame)
    {
        return frame?.Status.ToString() ?? TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable.ToString();
    }

    private static string OverlayLayerAccentColor(TrackerDiagnosticsOverlayLayerKey layerKey)
    {
        return layerKey == TrackerDiagnosticsOverlayLayerKey.LayerA ? LayerAAccentColor : LayerBAccentColor;
    }

    private sealed record DiagnosticsFieldRenderData(
        string Title,
        IReadOnlyList<SSL_DetectionBall> Balls,
        IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
        IReadOnlyList<SSL_DetectionRobot> RobotsBlue);
}
