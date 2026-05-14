using System.Globalization;
using Tracker.Core;
using Tracker.DebugHost.Components.Pages;
using Tracker.DebugHost.Components.Vision;
using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

/// <summary>
/// diagnostics Field 表示用 mapper の最小 contract を固定する。
/// </summary>
public class DiagnosticsFieldViewFactoryTests
{
    /// <summary>
    /// tracker packet semantic summary の ball 座標と visibility を SSL detection DTO へ写すことを確認する。
    /// </summary>
    [Fact]
    public void CreateTrackerSourceBalls_MapsCoordinatesAndVisibility()
    {
        var summary = CreateSummary(
            balls:
            [
                new TrackerPacketSnapshotBallSummary(0, XMm: 120.5, YMm: -220.25, ZMm: 12.75, Visibility: 0.42f),
            ],
            robots: []);

        var balls = DiagnosticsFieldViewFactory.CreateTrackerSourceBalls(summary);

        var ball = Assert.Single(balls);
        Assert.Equal(0.42f, ball.Confidence);
        Assert.Equal(120.5f, ball.X);
        Assert.Equal(-220.25f, ball.Y);
        Assert.Equal(12.75f, ball.Z);
    }

    /// <summary>
    /// tracker packet semantic summary の robot は yellow / blue に分離し、unknown team を描画対象から除外することを確認する。
    /// </summary>
    [Fact]
    public void CreateTrackerSourceRobots_SplitsYellowBlueAndExcludesUnknownTeam()
    {
        var summary = CreateSummary(
            balls: [],
            robots:
            [
                new TrackerPacketSnapshotRobotSummary("Yellow", 3, XMm: 1000, YMm: 2000, OrientationRad: 0.5f, Visibility: 0.9f),
                new TrackerPacketSnapshotRobotSummary("Blue", 7, XMm: -1100, YMm: -2100, OrientationRad: -0.25f, Visibility: 0.8f),
                new TrackerPacketSnapshotRobotSummary("Unknown", 99, XMm: 1, YMm: 2, OrientationRad: 3, Visibility: 0.7f),
            ]);

        var yellow = DiagnosticsFieldViewFactory.CreateTrackerSourceYellowRobots(summary);
        var blue = DiagnosticsFieldViewFactory.CreateTrackerSourceBlueRobots(summary);

        var yellowRobot = Assert.Single(yellow);
        Assert.Equal(3u, yellowRobot.RobotId);
        Assert.Equal(1000f, yellowRobot.X);
        Assert.Equal(2000f, yellowRobot.Y);
        Assert.Equal(0.5f, yellowRobot.Orientation);
        Assert.Equal(0.9f, yellowRobot.Confidence);

        var blueRobot = Assert.Single(blue);
        Assert.Equal(7u, blueRobot.RobotId);
        Assert.Equal(-1100f, blueRobot.X);
        Assert.Equal(-2100f, blueRobot.Y);
        Assert.Equal(-0.25f, blueRobot.Orientation);
        Assert.Equal(0.8f, blueRobot.Confidence);
    }

    /// <summary>
    /// overlay model は片方の layer が missing でも ready layer の drawable objects を残すことを確認する。
    /// </summary>
    [Fact]
    public void CreateOverlayRenderModel_WhenOneLayerMissing_KeepsReadyLayer()
    {
        var renderSnapshot = CreateRenderSnapshot(withGeometry: true);
        var trackedRenderView = TrackedVisionViewState.FromSnapshot(new TrackedSnapshot(
            renderSnapshot.Frame,
            renderSnapshot.ReceivedAt,
            "sim",
            PublishSuccessCount: 0,
            PublishFailureCount: 0));
        var comparisonViewState = CreateComparisonViewState();
        var missingLayerFrame = TrackerDiagnosticsFieldSourceFrame.WithStatus(
            TrackerDiagnosticsFieldSourceFrameStatus.CandidateMissing,
            TrackerDiagnosticsFieldSource.External,
            "No tracker snapshot matched the selected Field source.");
        var readyIbisFrame = CreateReadyFieldFrame(
            TrackerDiagnosticsFieldSource.IbisTracker,
            CreateSummary(
                [
                    new TrackerPacketSnapshotBallSummary(0, 100, 200, 0, 0.9f),
                ],
                [
                    new TrackerPacketSnapshotRobotSummary("Yellow", 3, 1000, 2000, 0, 0.85f),
                ]));

        var model = DiagnosticsFieldOverlayRenderModelFactory.Create(
            DiagnosticsFieldViewFactory.CreateGeometry(renderSnapshot.Frame.GeometrySnapshot),
            renderSnapshot,
            trackedRenderView,
            comparisonViewState,
            [
                new TrackerDiagnosticsFieldOverlayLayerSource(
                    TrackerDiagnosticsOverlayLayerKey.LayerA,
                    "Layer A",
                    TrackerDiagnosticsFieldSource.External,
                    IsVisible: true),
                new TrackerDiagnosticsFieldOverlayLayerSource(
                    TrackerDiagnosticsOverlayLayerKey.LayerB,
                    "Layer B",
                    TrackerDiagnosticsFieldSource.IbisTracker,
                    IsVisible: true),
            ],
            missingLayerFrame,
            readyIbisFrame);

        Assert.NotNull(model.Geometry);
        var layerA = Assert.Single(model.Layers, layer => layer.LayerKey == TrackerDiagnosticsOverlayLayerKey.LayerA);
        var layerB = Assert.Single(model.Layers, layer => layer.LayerKey == TrackerDiagnosticsOverlayLayerKey.LayerB);
        Assert.Equal("CandidateMissing", layerA.Status);
        Assert.Equal(0, layerA.DrawableCount);
        Assert.Equal("Ready", layerB.Status);
        Assert.Equal(2, layerB.DrawableCount);
        Assert.Single(layerB.Balls);
        Assert.Single(layerB.RobotsYellow);
    }

    /// <summary>
    /// raw geometry が渡されない overlay は geometry なし empty state を返すことを確認する。
    /// </summary>
    [Fact]
    public void CreateOverlayRenderModel_WhenRenderSnapshotHasNoGeometry_ReturnsGeometryEmptyState()
    {
        var renderSnapshot = CreateRenderSnapshot(withGeometry: false);
        var trackedRenderView = TrackedVisionViewState.FromSnapshot(new TrackedSnapshot(
            renderSnapshot.Frame,
            renderSnapshot.ReceivedAt,
            "sim",
            PublishSuccessCount: 0,
            PublishFailureCount: 0));

        var model = DiagnosticsFieldOverlayRenderModelFactory.Create(
            geometry: null,
            renderSnapshot,
            trackedRenderView,
            CreateComparisonViewState(),
            [
                new TrackerDiagnosticsFieldOverlayLayerSource(
                    TrackerDiagnosticsOverlayLayerKey.LayerA,
                    "Layer A",
                    TrackerDiagnosticsFieldSource.IbisTracker,
                    IsVisible: true),
            ],
            layerAFrame: null,
            layerBFrame: null);

        Assert.Null(model.Geometry);
        Assert.Equal("Raw SSL-Vision geometry was not found.", model.EmptyState);
        Assert.Single(model.Layers);
    }

    /// <summary>
    /// overlay layer source の legend 補足を描画 model に渡し、同一 source 表示を UI で出せることを確認する。
    /// </summary>
    [Fact]
    public void CreateOverlayRenderModel_CarriesLayerLegendNote()
    {
        var renderSnapshot = CreateRenderSnapshot(withGeometry: true);
        var trackedRenderView = TrackedVisionViewState.FromSnapshot(new TrackedSnapshot(
            renderSnapshot.Frame,
            renderSnapshot.ReceivedAt,
            "sim",
            PublishSuccessCount: 0,
            PublishFailureCount: 0));

        var model = DiagnosticsFieldOverlayRenderModelFactory.Create(
            DiagnosticsFieldViewFactory.CreateGeometry(renderSnapshot.Frame.GeometrySnapshot),
            renderSnapshot,
            trackedRenderView,
            CreateComparisonViewState(),
            [
                new TrackerDiagnosticsFieldOverlayLayerSource(
                    TrackerDiagnosticsOverlayLayerKey.LayerA,
                    "Layer A/B",
                    TrackerDiagnosticsFieldSource.IbisTracker,
                    IsVisible: true,
                    LegendNote: "same source"),
            ],
            layerAFrame: null,
            layerBFrame: null);

        var layer = Assert.Single(model.Layers);
        Assert.Equal("same source", layer.LegendNote);
    }

    private static TrackerPacketSnapshotSemanticSummary CreateSummary(
        IReadOnlyList<TrackerPacketSnapshotBallSummary> balls,
        IReadOnlyList<TrackerPacketSnapshotRobotSummary> robots)
    {
        return new TrackerPacketSnapshotSemanticSummary(
            BallCount: balls.Count,
            RobotCount: robots.Count,
            TrackedFrameNumber: 1,
            TrackedFrameTimestampNs: 1_000,
            SourceUuid: "source-1",
            SourceName: "source",
            SourceRole: "external",
            SourceLabel: "source",
            Balls: balls,
            Robots: robots);
    }

    private static TrackerDiagnosticsFieldSourceFrame CreateReadyFieldFrame(
        TrackerDiagnosticsFieldSource source,
        TrackerPacketSnapshotSemanticSummary summary)
    {
        return new TrackerDiagnosticsFieldSourceFrame(
            TrackerDiagnosticsFieldSourceFrameStatus.Ready,
            source,
            EntryLineNumber: 1,
            MatchingRule: "diagnostics-sample-sidecar",
            IbisOwnSnapshotTimestampNs: null,
            SourceRole: summary.SourceRole,
            SourceLabel: summary.SourceLabel,
            TrackedFrameNumber: summary.TrackedFrameNumber,
            TrackedFrameTimestampNs: summary.TrackedFrameTimestampNs,
            TimestampDeltaNs: 0,
            RawPayloadRestored: false,
            summary,
            Message: null);
    }

    private static TrackerRenderSnapshotView CreateRenderSnapshot(bool withGeometry)
    {
        return new TrackerRenderSnapshotView(
            "render-snapshots.jsonl.gz",
            DateTimeOffset.Parse("2026-05-13T00:00:00Z", CultureInfo.InvariantCulture),
            new TrackerFrame
            {
                FrameNumber = 100,
                GeometrySnapshot = withGeometry
                    ? new TrackerGeometrySnapshot
                    {
                        FieldLengthMm = 9000,
                        FieldWidthMm = 6000,
                        GoalWidthMm = 1000,
                        GoalDepthMm = 180,
                        BoundaryWidthMm = 300,
                        BoundaryWidthGoalLineMm = 300,
                        PenaltyAreaDepthMm = 1000,
                        PenaltyAreaWidthMm = 2000,
                        CenterCircleRadiusMm = 500,
                        LineThicknessMm = 10,
                    }
                    : null,
                Balls =
                [
                    new TrackedBallState
                    {
                        InternalTrackId = 1,
                        XMm = 100,
                        YMm = 200,
                        Visibility = 0.9f,
                    },
                ],
                Robots =
                [
                    new TrackedRobotState
                    {
                        Team = TrackerTeam.Yellow,
                        RobotId = 3,
                        XMm = 1000,
                        YMm = 2000,
                        Visibility = 0.85f,
                    },
                ],
                Metadata = new TrackerFrameMetadata { ProfileName = "sim" },
            });
    }

    private static TrackerDiagnosticsComparisonViewState CreateComparisonViewState()
    {
        return new TrackerDiagnosticsComparisonViewState(
            DiagnosticsLogPath: "diagnostics.log",
            MetadataPath: "metadata.json",
            SidecarPath: "tracker-packets.jsonl",
            TrackerDiagnosticsComparisonSidecarStatus.Ready,
            SourceOptions: [],
            FieldSourceOptions:
            [
                new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.VisionInput, "Vision Input", 0),
                new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.IbisTracker, "ibis tracker", 1),
                new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.External, "External", 0),
            ],
            TrackerDiagnosticsComparisonSourceFilter.All,
            SelectedEntryComparison: null,
            ReplayTimeline: [],
            RecordCount: 1,
            SkippedRecordCount: 0,
            ErrorCount: 0,
            Error: null);
    }
}
