using System.Globalization;
using System.Text.Json;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// TRACKER-050 の diagnostics comparison reader / view-state contract を固定する。
/// </summary>
public class TrackerDiagnosticsComparisonViewStateTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public TrackerDiagnosticsComparisonViewStateTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// diagnostics log path から metadata と snapshot sidecar を解決し、source filter と selected entry comparison を UI 非依存 model として返すことを確認する。
    /// </summary>
    [Fact]
    public void Load_ResolvesSourcesFilterAndSelectedEntryComparisonFromDiagnosticsLogPath()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9100, 91_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9101, 91_004_000_000, ballCount: 2, robotCount: 2),
                SnapshotInput("external-b", "thirdparty-b", "external", 9102, 91_020_000_000, ballCount: 1, robotCount: 3),
                SnapshotInput("", "", "unknown", 9103, 91_010_000_000, ballCount: 3, robotCount: 0),
            ],
            isCreated: true,
            skippedRecordCount: 2,
            errorCount: 1);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var state = reader.Load(
            session.DiagnosticsPath,
            SelectedEntry(9100),
            TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel("thirdparty-a"));

        Assert.Equal(TrackerDiagnosticsComparisonSidecarStatus.Ready, state.SidecarStatus);
        Assert.Equal(Path.GetFullPath(session.MetadataPath), state.MetadataPath);
        Assert.Equal(Path.GetFullPath(session.SidecarPath), state.SidecarPath);
        Assert.Equal(4, state.RecordCount);
        Assert.Equal(2, state.SkippedRecordCount);
        Assert.Equal(1, state.ErrorCount);
        Assert.Contains(state.SourceOptions, option => option.Filter.Kind == TrackerDiagnosticsComparisonSourceFilterKind.All);
        Assert.Contains(state.SourceOptions, option => option.Filter.Kind == TrackerDiagnosticsComparisonSourceFilterKind.External && option.RecordCount == 2);
        Assert.Contains(state.SourceOptions, option => option.Filter.Kind == TrackerDiagnosticsComparisonSourceFilterKind.Own && option.RecordCount == 1);
        Assert.Contains(state.SourceOptions, option => option.Filter.Kind == TrackerDiagnosticsComparisonSourceFilterKind.Unknown && option.RecordCount == 1);
        Assert.Contains(state.SourceOptions, option => option.Filter.Kind == TrackerDiagnosticsComparisonSourceFilterKind.SourceLabel && option.Filter.Value == "thirdparty-a");

        Assert.NotNull(state.SelectedEntryComparison);
        var comparison = state.SelectedEntryComparison!;
        Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.Ready, comparison.Status);
        Assert.Equal(91_000_000_000, comparison.IbisOwnSnapshotTimestampNs);
        Assert.Equal("nearest-timestamp", comparison.MatchingRule);
        Assert.Equal("external", comparison.NearestSnapshotSourceRole);
        Assert.Equal("thirdparty-a", comparison.NearestSnapshotSourceLabel);
        Assert.Equal(9101u, comparison.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(91_004_000_000, comparison.NearestSnapshotTimestampNs);
        Assert.Equal(4_000_000, comparison.TimestampDeltaNs);
        Assert.True(comparison.RawPayloadRestored);
        Assert.Equal(2, comparison.BallCount);
        Assert.Equal(2, comparison.RobotCount);
    }

    /// <summary>
    /// own snapshot がない selected diagnostics entry でも既存 diagnostics 表示を止めず、comparison status として表現することを確認する。
    /// </summary>
    [Fact]
    public void Load_WhenSelectedEntryHasNoOwnSnapshot_ReturnsOwnSnapshotMissingStatus()
    {
        var session = CreateSession(
            [
                SnapshotInput("external-a", "thirdparty-a", "external", 9201, 92_004_000_000, ballCount: 1, robotCount: 1),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 9200);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var state = reader.Load(session.DiagnosticsPath, SelectedEntry(9200), TrackerDiagnosticsComparisonSourceFilter.All);

        Assert.Equal(TrackerDiagnosticsComparisonSidecarStatus.Ready, state.SidecarStatus);
        Assert.NotNull(state.SelectedEntryComparison);
        var comparison = state.SelectedEntryComparison!;
        Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.OwnSnapshotMissing, comparison.Status);
        Assert.Null(comparison.NearestSnapshotSourceLabel);
        Assert.Null(comparison.NearestSnapshotTrackedFrameNumber);
    }

    /// <summary>
    /// 同じ diagnostics / metadata / sidecar file state の連続 load では sidecar index を再構築しないことを確認する。
    /// </summary>
    [Fact]
    public void Load_WhenFileStateIsUnchanged_ReusesCachedSidecarIndex()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9100, 91_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9100, 91_004_000_000, ballCount: 2, robotCount: 2),
                SnapshotInput("ibis-runtime", "ibis", "own", 9101, 91_010_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9101, 91_014_000_000, ballCount: 3, robotCount: 3),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrames: [9100, 9101]);
        var buildCount = 0;
        var reader = new TrackerDiagnosticsComparisonViewStateReader(sidecarPath =>
        {
            buildCount++;
            return TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath).ToArray();
        });

        var first = reader.Load(session.DiagnosticsPath, SelectedEntry(9100), TrackerDiagnosticsComparisonSourceFilter.External);
        var second = reader.Load(session.DiagnosticsPath, SelectedEntry(9101), TrackerDiagnosticsComparisonSourceFilter.External);

        Assert.Equal(1, buildCount);
        Assert.Equal(TrackerDiagnosticsComparisonSidecarStatus.Ready, first.SidecarStatus);
        Assert.Equal(TrackerDiagnosticsComparisonSidecarStatus.Ready, second.SidecarStatus);
        Assert.Equal(9100u, first.SelectedEntryComparison?.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(9101u, second.SelectedEntryComparison?.NearestSnapshotTrackedFrameNumber);
    }

    /// <summary>
    /// Field source selector は描画可能な source だけを出し、comparison 用の All filter を混ぜないことを確認する。
    /// </summary>
    [Fact]
    public void Load_FieldSourceOptionsExcludeAllAndIncludeRenderableSources()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9100, 91_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9101, 91_004_000_000, ballCount: 2, robotCount: 2),
                SnapshotInput("", "", "unknown", 9102, 91_006_000_000, ballCount: 0, robotCount: 0),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var state = reader.Load(session.DiagnosticsPath, SelectedEntry(9100), TrackerDiagnosticsComparisonSourceFilter.All);

        Assert.DoesNotContain(state.FieldSourceOptions, option => string.Equals(option.Label, "All", StringComparison.Ordinal));
        Assert.Contains(state.FieldSourceOptions, option => option.Source.Kind == TrackerDiagnosticsFieldSourceKind.VisionInput && option.Label == "Vision Input");
        Assert.Contains(state.FieldSourceOptions, option => option.Source.Kind == TrackerDiagnosticsFieldSourceKind.IbisTracker && option.Label == "ibis tracker");
        Assert.Contains(state.FieldSourceOptions, option => option.Source.Kind == TrackerDiagnosticsFieldSourceKind.External && option.Label == "External");
        Assert.Contains(state.FieldSourceOptions, option => option.Source.Kind == TrackerDiagnosticsFieldSourceKind.Unknown && option.Label == "Unknown");
        Assert.Contains(
            state.FieldSourceOptions,
            option => option.Source.Kind == TrackerDiagnosticsFieldSourceKind.SourceLabel && option.Source.Value == "thirdparty-a");
    }

    /// <summary>
    /// external/source label Field は selected entry の own timestamp を基準に nearest snapshot から描画用 summary を作ることを確認する。
    /// </summary>
    [Fact]
    public void LoadFieldSourceFrame_ForSourceLabelUsesNearestSnapshotToSelectedOwnTimestamp()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9600, 96_010_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9601, 96_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9602, 96_013_000_000, ballCount: 2, robotCount: 2),
                SnapshotInput("external-b", "thirdparty-b", "external", 9603, 96_011_000_000, ballCount: 3, robotCount: 3),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 9600);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var frame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(9600),
            TrackerDiagnosticsFieldSource.ForSourceLabel("thirdparty-a"));

        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, frame.Status);
        Assert.Equal("nearest-timestamp", frame.MatchingRule);
        Assert.Equal(96_010_000_000, frame.IbisOwnSnapshotTimestampNs);
        Assert.Equal("external", frame.SourceRole);
        Assert.Equal("thirdparty-a", frame.SourceLabel);
        Assert.Equal(9602u, frame.TrackedFrameNumber);
        Assert.Equal(96_013_000_000, frame.TrackedFrameTimestampNs);
        Assert.Equal(3_000_000, frame.TimestampDeltaNs);
        Assert.NotNull(frame.SemanticSummary);
        Assert.Equal(2, frame.SemanticSummary!.Balls.Count);
        Assert.Equal(2, frame.SemanticSummary.Robots.Count);
    }

    /// <summary>
    /// source 変更や selected entry 変更で tracker sidecar JSONL を再読込せず、TRACKER-055 の cached index を再利用することを確認する。
    /// </summary>
    [Fact]
    public void LoadFieldSourceFrame_WhenSourceOrSelectedEntryChanges_ReusesCachedSidecarIndex()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9700, 97_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9701, 97_002_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("ibis-runtime", "ibis", "own", 9702, 97_010_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("", "", "unknown", 9703, 97_011_000_000, ballCount: 1, robotCount: 1),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrames: [9700, 9702]);
        var buildCount = 0;
        var reader = new TrackerDiagnosticsComparisonViewStateReader(sidecarPath =>
        {
            buildCount++;
            return TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath).ToArray();
        });

        var first = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(9700),
            TrackerDiagnosticsFieldSource.External);
        var second = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(9702),
            TrackerDiagnosticsFieldSource.Unknown);

        Assert.Equal(1, buildCount);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, first.Status);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, second.Status);
        Assert.Equal(9701u, first.TrackedFrameNumber);
        Assert.Equal(9703u, second.TrackedFrameNumber);
    }

    /// <summary>
    /// page state は log 変更で左右 Field source を既定に戻し、scrub / playback 相当の selected entry 更新では selector と折り畳み状態を保持する。
    /// </summary>
    [Fact]
    public void UiState_FieldSourceAndFoldState_ResetOnlyWhenLogChanges()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9800, 98_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9801, 98_002_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("ibis-runtime", "ibis", "own", 9802, 98_010_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9803, 98_011_000_000, ballCount: 1, robotCount: 1),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrames: [9800, 9802]);
        var uiState = new TrackerDiagnosticsComparisonUiState(new TrackerDiagnosticsComparisonViewStateReader());
        var displayedEntries = ReadDisplayedEntries(session.DiagnosticsPath);
        var firstEntry = displayedEntries[0];
        var secondEntry = displayedEntries[1];

        uiState.Load(session.DiagnosticsPath, firstEntry);
        Assert.Equal(TrackerDiagnosticsFieldSource.VisionInput, uiState.LeftFieldSource);
        Assert.Equal(TrackerDiagnosticsFieldSource.IbisTracker, uiState.RightFieldSource);
        Assert.False(uiState.IsComparisonPanelCollapsed);

        uiState.SelectLeftFieldSource(
            TrackerDiagnosticsFieldSource.External,
            session.DiagnosticsPath,
            firstEntry);
        uiState.SelectRightFieldSource(
            TrackerDiagnosticsFieldSource.ForSourceLabel("thirdparty-a"),
            session.DiagnosticsPath,
            firstEntry);
        uiState.ToggleComparisonPanelCollapsed();
        uiState.Load(session.DiagnosticsPath, secondEntry);

        Assert.Equal(TrackerDiagnosticsFieldSource.External, uiState.LeftFieldSource);
        Assert.Equal(TrackerDiagnosticsFieldSourceKind.SourceLabel, uiState.RightFieldSource.Kind);
        Assert.True(uiState.IsComparisonPanelCollapsed);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, uiState.LeftTrackerFieldSourceFrame?.Status);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, uiState.RightTrackerFieldSourceFrame?.Status);

        uiState.ResetForLogChange();

        Assert.Equal(TrackerDiagnosticsFieldSource.VisionInput, uiState.LeftFieldSource);
        Assert.Equal(TrackerDiagnosticsFieldSource.IbisTracker, uiState.RightFieldSource);
    }

    /// <summary>
    /// overlay mode と layer visibility は selected entry / source 変更では維持し、log 変更時だけ既定へ戻ることを確認する。
    /// </summary>
    [Fact]
    public void UiState_OverlayModeAndLayerVisibility_ResetOnlyWhenLogChanges()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9850, 98_500_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9851, 98_502_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("ibis-runtime", "ibis", "own", 9852, 98_510_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("", "", "unknown", 9853, 98_511_000_000, ballCount: 1, robotCount: 1),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrames: [9850, 9852]);
        var uiState = new TrackerDiagnosticsComparisonUiState(new TrackerDiagnosticsComparisonViewStateReader());
        var displayedEntries = ReadDisplayedEntries(session.DiagnosticsPath);

        uiState.Load(session.DiagnosticsPath, displayedEntries[0]);
        Assert.Equal(TrackerDiagnosticsFieldDisplayMode.Split, uiState.FieldDisplayMode);
        Assert.True(uiState.IsOverlayLayerAVisible);
        Assert.True(uiState.IsOverlayLayerBVisible);

        uiState.SelectFieldDisplayMode(TrackerDiagnosticsFieldDisplayMode.Overlay);
        uiState.SetOverlayLayerVisibility(TrackerDiagnosticsOverlayLayerKey.LayerA, isVisible: false);
        uiState.Load(session.DiagnosticsPath, displayedEntries[1]);
        uiState.SelectRightFieldSource(TrackerDiagnosticsFieldSource.Unknown, session.DiagnosticsPath, displayedEntries[1]);

        Assert.Equal(TrackerDiagnosticsFieldDisplayMode.Overlay, uiState.FieldDisplayMode);
        Assert.False(uiState.IsOverlayLayerAVisible);
        Assert.True(uiState.IsOverlayLayerBVisible);

        uiState.ResetForLogChange();

        Assert.Equal(TrackerDiagnosticsFieldDisplayMode.Split, uiState.FieldDisplayMode);
        Assert.True(uiState.IsOverlayLayerAVisible);
        Assert.True(uiState.IsOverlayLayerBVisible);
    }

    /// <summary>
    /// overlay layer source model は既存の左右 selector の 2 source をそのまま使い、Field source に All を含めないことを確認する。
    /// </summary>
    [Fact]
    public void UiState_CreateOverlayLayerSources_UsesLeftAndRightSelectorsWithoutAll()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9860, 98_600_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9861, 98_602_000_000, ballCount: 1, robotCount: 1),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 9860);
        var uiState = new TrackerDiagnosticsComparisonUiState(new TrackerDiagnosticsComparisonViewStateReader());
        var displayedEntry = ReadDisplayedEntries(session.DiagnosticsPath).Single();
        uiState.Load(session.DiagnosticsPath, displayedEntry);
        uiState.SelectLeftFieldSource(TrackerDiagnosticsFieldSource.External, session.DiagnosticsPath, displayedEntry);
        uiState.SelectRightFieldSource(
            TrackerDiagnosticsFieldSource.ForSourceLabel("thirdparty-a"),
            session.DiagnosticsPath,
            displayedEntry);

        var layers = uiState.CreateOverlayLayerSources().ToArray();

        Assert.Equal(2, layers.Length);
        Assert.Equal(TrackerDiagnosticsOverlayLayerKey.LayerA, layers[0].LayerKey);
        Assert.Equal(TrackerDiagnosticsFieldSource.External, layers[0].Source);
        Assert.Equal(TrackerDiagnosticsOverlayLayerKey.LayerB, layers[1].LayerKey);
        Assert.Equal(TrackerDiagnosticsFieldSourceKind.SourceLabel, layers[1].Source.Kind);
        Assert.DoesNotContain(
            uiState.ViewState.FieldSourceOptions,
            option => string.Equals(option.Label, "All", StringComparison.Ordinal));
    }

    /// <summary>
    /// 左右 Field selector が同じ source の overlay は二重描画を避けるため 1 layer に畳み、legend で同一 source と分かることを確認する。
    /// </summary>
    [Fact]
    public void UiState_CreateOverlayLayerSources_WhenSelectorsUseSameSource_ReturnsSingleSameSourceLayer()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9865, 98_650_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9866, 98_652_000_000, ballCount: 1, robotCount: 1),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 9865);
        var uiState = new TrackerDiagnosticsComparisonUiState(new TrackerDiagnosticsComparisonViewStateReader());
        var displayedEntry = ReadDisplayedEntries(session.DiagnosticsPath).Single();
        uiState.Load(session.DiagnosticsPath, displayedEntry);
        uiState.SelectLeftFieldSource(TrackerDiagnosticsFieldSource.External, session.DiagnosticsPath, displayedEntry);
        uiState.SelectRightFieldSource(TrackerDiagnosticsFieldSource.External, session.DiagnosticsPath, displayedEntry);

        var layer = Assert.Single(uiState.CreateOverlayLayerSources());

        Assert.Equal(TrackerDiagnosticsOverlayLayerKey.LayerA, layer.LayerKey);
        Assert.Equal("Layer A/B", layer.LayerName);
        Assert.Equal(TrackerDiagnosticsFieldSource.External, layer.Source);
        Assert.Equal("same source", layer.LegendNote);
        Assert.True(layer.IsVisible);
    }

    /// <summary>
    /// overlay mode / visibility 操作は TRACKER-056 の cached index と source frame を再利用し、sidecar JSONL を再読込しないことを確認する。
    /// </summary>
    [Fact]
    public void UiState_OverlayOperations_DoNotReloadSidecar()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9870, 98_700_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9871, 98_702_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("", "", "unknown", 9872, 98_704_000_000, ballCount: 1, robotCount: 1),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 9870);
        var buildCount = 0;
        var reader = new TrackerDiagnosticsComparisonViewStateReader(sidecarPath =>
        {
            buildCount++;
            return TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath).ToArray();
        });
        var uiState = new TrackerDiagnosticsComparisonUiState(reader);
        var displayedEntry = ReadDisplayedEntries(session.DiagnosticsPath).Single();

        uiState.Load(session.DiagnosticsPath, displayedEntry);
        uiState.SelectLeftFieldSource(TrackerDiagnosticsFieldSource.External, session.DiagnosticsPath, displayedEntry);
        uiState.SelectRightFieldSource(TrackerDiagnosticsFieldSource.Unknown, session.DiagnosticsPath, displayedEntry);
        uiState.SelectFieldDisplayMode(TrackerDiagnosticsFieldDisplayMode.Overlay);
        uiState.SetOverlayLayerVisibility(TrackerDiagnosticsOverlayLayerKey.LayerB, isVisible: false);

        Assert.Equal(1, buildCount);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, uiState.LeftTrackerFieldSourceFrame?.Status);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, uiState.RightTrackerFieldSourceFrame?.Status);
    }

    /// <summary>
    /// sidecar の file state が変わった場合は cache を破棄し、新しい index を構築することを確認する。
    /// </summary>
    [Fact]
    public void Load_WhenSidecarFileStateChanges_RebuildsSidecarIndex()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9100, 91_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9100, 91_004_000_000, ballCount: 2, robotCount: 2),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0);
        var buildCount = 0;
        var reader = new TrackerDiagnosticsComparisonViewStateReader(sidecarPath =>
        {
            buildCount++;
            return TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath).ToArray();
        });

        _ = reader.Load(session.DiagnosticsPath, SelectedEntry(9100), TrackerDiagnosticsComparisonSourceFilter.External);
        File.AppendAllText(session.SidecarPath, Environment.NewLine);
        File.SetLastWriteTimeUtc(session.SidecarPath, File.GetLastWriteTimeUtc(session.SidecarPath).AddSeconds(1));
        _ = reader.Load(session.DiagnosticsPath, SelectedEntry(9100), TrackerDiagnosticsComparisonSourceFilter.External);

        Assert.Equal(2, buildCount);
    }

    /// <summary>
    /// nearest timestamp が同一 timestamp の複数 record に一致する場合、旧実装と同じく ReceivedAt 昇順の先頭を選ぶことを確認する。
    /// </summary>
    [Fact]
    public void Load_WhenNearestTimestampHasDuplicates_UsesEarliestReceivedAtCandidate()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9500, 95_010_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9501, 95_000_000_000, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: 30),
                SnapshotInput("external-a", "thirdparty-a", "external", 9502, 95_000_000_000, ballCount: 2, robotCount: 2, receivedAtOffsetTicks: 10),
                SnapshotInput("external-a", "thirdparty-a", "external", 9503, 95_000_000_000, ballCount: 3, robotCount: 3, receivedAtOffsetTicks: 20),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 9500);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var state = reader.Load(session.DiagnosticsPath, SelectedEntry(9500), TrackerDiagnosticsComparisonSourceFilter.External);

        Assert.NotNull(state.SelectedEntryComparison);
        var comparison = state.SelectedEntryComparison!;
        Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.Ready, comparison.Status);
        Assert.Equal(9502u, comparison.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(95_000_000_000, comparison.NearestSnapshotTimestampNs);
        Assert.Equal(10_000_000, comparison.TimestampDeltaNs);
        Assert.Equal(2, comparison.BallCount);
        Assert.Equal(2, comparison.RobotCount);
    }

    /// <summary>
    /// diagnostics reader が長い log の先頭 entry を omit した後でも、表示済み list の先頭選択が full file 先頭ではなく表示中 entry に対応することを確認する。
    /// </summary>
    [Fact]
    public void Load_WhenDiagnosticsLogOmitsHeadEntries_UsesDisplayedEntrySelection()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9300, 93_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9300, 93_004_000_000, ballCount: 2, robotCount: 2),
                SnapshotInput("ibis-runtime", "ibis", "own", 9301, 93_010_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9301, 93_014_000_000, ballCount: 3, robotCount: 3),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrames: Enumerable.Range(9300, 10_001).Select(frame => (uint)frame).ToArray());
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var displayedEntry = ReadDisplayedEntries(session.DiagnosticsPath).First();

        var state = reader.Load(
            session.DiagnosticsPath,
            TrackerDiagnosticsComparisonSelectedEntry.FromDiagnosticsEntry(displayedEntry),
            TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel("thirdparty-a"));

        Assert.NotNull(state.SelectedEntryComparison);
        var comparison = state.SelectedEntryComparison!;
        Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.Ready, comparison.Status);
        Assert.Equal(2, comparison.EntryLineNumber);
        Assert.Equal(93_010_000_000, comparison.IbisOwnSnapshotTimestampNs);
        Assert.Equal(9301u, comparison.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(93_014_000_000, comparison.NearestSnapshotTimestampNs);
        Assert.Equal(3, comparison.BallCount);
        Assert.Equal(3, comparison.RobotCount);
    }

    /// <summary>
    /// diagnostics UI の同期 state が表示済み entry から comparison selected-entry を作り、full file index ずれを再発させないことを確認する。
    /// </summary>
    [Fact]
    public void UiState_Load_UsesDisplayedEntrySelectionForComparison()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9300, 93_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9300, 93_004_000_000, ballCount: 2, robotCount: 2),
                SnapshotInput("ibis-runtime", "ibis", "own", 9301, 93_010_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9301, 93_014_000_000, ballCount: 3, robotCount: 3),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrames: Enumerable.Range(9300, 10_001).Select(frame => (uint)frame).ToArray());
        var uiState = new TrackerDiagnosticsComparisonUiState(new TrackerDiagnosticsComparisonViewStateReader());
        var displayedEntry = ReadDisplayedEntries(session.DiagnosticsPath).First();

        uiState.SelectFilter(
            TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel("thirdparty-a"),
            session.DiagnosticsPath,
            displayedEntry);

        Assert.Equal(TrackerDiagnosticsComparisonSourceFilterKind.SourceLabel, uiState.SelectedSourceFilter.Kind);
        Assert.NotNull(uiState.ViewState.SelectedEntryComparison);
        var comparison = uiState.ViewState.SelectedEntryComparison!;
        Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.Ready, comparison.Status);
        Assert.Equal(2, comparison.EntryLineNumber);
        Assert.Equal(93_010_000_000, comparison.IbisOwnSnapshotTimestampNs);
        Assert.Equal(9301u, comparison.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(93_014_000_000, comparison.NearestSnapshotTimestampNs);
        Assert.Equal(3, comparison.BallCount);
        Assert.Equal(3, comparison.RobotCount);
    }

    /// <summary>
    /// diagnostics UI の source filter select 値から source label filter を選び、selected entry comparison を再計算できることを確認する。
    /// </summary>
    [Fact]
    public void UiState_SelectFilterValue_RecomputesComparisonForSourceLabelOption()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9100, 91_000_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("external-a", "thirdparty-a", "external", 9101, 91_004_000_000, ballCount: 2, robotCount: 2),
                SnapshotInput("external-b", "thirdparty-b", "external", 9102, 91_020_000_000, ballCount: 1, robotCount: 3),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0);
        var uiState = new TrackerDiagnosticsComparisonUiState(new TrackerDiagnosticsComparisonViewStateReader());
        var displayedEntry = ReadDisplayedEntries(session.DiagnosticsPath).Single();
        uiState.Load(session.DiagnosticsPath, displayedEntry);

        var selected = uiState.SelectFilterValue(
            TrackerDiagnosticsComparisonUiState.ToFilterValue(
                TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel("thirdparty-b")),
            session.DiagnosticsPath,
            displayedEntry);

        Assert.True(selected);
        Assert.Equal(TrackerDiagnosticsComparisonSourceFilterKind.SourceLabel, uiState.SelectedSourceFilter.Kind);
        Assert.Equal("thirdparty-b", uiState.SelectedSourceFilter.Value);
        Assert.NotNull(uiState.ViewState.SelectedEntryComparison);
        var comparison = uiState.ViewState.SelectedEntryComparison!;
        Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.Ready, comparison.Status);
        Assert.Equal("thirdparty-b", comparison.NearestSnapshotSourceLabel);
        Assert.Equal(9102u, comparison.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(91_020_000_000, comparison.NearestSnapshotTimestampNs);
        Assert.Equal(20_000_000, comparison.TimestampDeltaNs);
        Assert.Equal(1, comparison.BallCount);
        Assert.Equal(3, comparison.RobotCount);
    }

    /// <summary>
    /// sidecar の missing / empty / corrupt / metadata missing / sidecar not created を blocker にせず区別した status として返すことを確認する。
    /// </summary>
    [Theory]
    [InlineData("missing", TrackerDiagnosticsComparisonSidecarStatus.SidecarMissing)]
    [InlineData("empty", TrackerDiagnosticsComparisonSidecarStatus.SidecarEmpty)]
    [InlineData("corrupt", TrackerDiagnosticsComparisonSidecarStatus.SidecarCorrupt)]
    [InlineData("not-created", TrackerDiagnosticsComparisonSidecarStatus.SidecarNotCreated)]
    [InlineData("metadata-missing", TrackerDiagnosticsComparisonSidecarStatus.MetadataMissing)]
    public void Load_WhenSidecarIsUnavailable_ReturnsNonBlockingStatus(
        string unavailableCase,
        TrackerDiagnosticsComparisonSidecarStatus expectedStatus)
    {
        var session = CreateSession(
            [],
            isCreated: unavailableCase != "not-created",
            skippedRecordCount: 3,
            errorCount: 2);
        if (unavailableCase is "missing" or "not-created")
        {
            File.Delete(session.SidecarPath);
        }
        else if (unavailableCase == "empty")
        {
            File.WriteAllText(session.SidecarPath, "");
        }
        else if (unavailableCase == "corrupt")
        {
            File.WriteAllText(session.SidecarPath, "{not-json");
        }
        else if (unavailableCase == "metadata-missing")
        {
            File.Delete(session.MetadataPath);
        }

        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var state = reader.Load(session.DiagnosticsPath, SelectedEntry(9100), TrackerDiagnosticsComparisonSourceFilter.External);

        Assert.Equal(expectedStatus, state.SidecarStatus);
        Assert.DoesNotContain(
            state.SourceOptions,
            option => option.Filter.Kind == TrackerDiagnosticsComparisonSourceFilterKind.SourceLabel);
        Assert.Null(state.SelectedEntryComparison);
        if (unavailableCase == "metadata-missing")
        {
            Assert.Equal(0, state.SkippedRecordCount);
            Assert.Equal(0, state.ErrorCount);
        }
        else
        {
            Assert.Equal(3, state.SkippedRecordCount);
            Assert.Equal(2, state.ErrorCount);
        }
    }

    private TestSession CreateSession(
        IReadOnlyList<SnapshotInputData> snapshotInputs,
        bool isCreated,
        int skippedRecordCount,
        int errorCount,
        uint diagnosticsTrackedFrame = 9100,
        IReadOnlyList<uint>? diagnosticsTrackedFrames = null)
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-050-comparison-{Guid.NewGuid():N}");
        var sessionFolder = "comparison-session";
        var sessionFolderPath = Path.Combine(captureDirectory, sessionFolder);
        Directory.CreateDirectory(sessionFolderPath);

        var sidecarPath = Path.Combine(sessionFolderPath, TrackerPacketSnapshotLogReader.SidecarFileName);
        File.WriteAllLines(
            sidecarPath,
            snapshotInputs.Select(input =>
            {
                var packet = CreatePacket(input);
                return JsonSerializer.Serialize(TrackerPacketSnapshotRecord.FromPacket(
                    packet,
                    new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero).AddTicks(
                        input.ReceivedAtOffsetTicks ?? input.TimestampNs / 100),
                    remoteEndpoint: input.Role == "own" ? "self" : $"192.0.2.{input.FrameNumber % 100}:12010",
                    sourceRole: input.Role,
                    sourceLabel: string.IsNullOrWhiteSpace(input.SourceName) ? input.Role : input.SourceName));
            }));

        var diagnosticsPath = Path.Combine(sessionFolderPath, "comparison-session.tracker-diagnostics.log");
        File.WriteAllLines(
            diagnosticsPath,
            (diagnosticsTrackedFrames ?? [diagnosticsTrackedFrame])
            .Select((trackedFrame, index) =>
                $"2026-05-12T12:00:{index % 60:00}.0000000+00:00 Tracker diagnostics profile=sim rawFrame={9001 + index} rawCamera=0 rawBalls=1 rawBallDetails=[x=100,y=200,z=0,c=1] rawBlue=[] rawYellow=[] trackedFrame={trackedFrame} trackedBalls=1 trackedBallDetails=[#1:x=100,y=200,z=0,vis=1,q=1,cams=0] trackedRobots=1 trackedRobotDetails=[Y3:x=1200,y=-300,o=0,w=0,vis=1,q=1] ballOutVisibility=0 ballHalfLifeSec=1 ballLifetimeNs=1000000000"));

        var metadataPath = Path.Combine(sessionFolderPath, "comparison-session.metadata.json");
        File.WriteAllText(
            metadataPath,
            JsonSerializer.Serialize(new
            {
                SessionFolder = sessionFolder,
                DiagnosticsLogPath = Path.Combine(sessionFolder, Path.GetFileName(diagnosticsPath)),
                TrackerSnapshotSidecarPath = Path.Combine(sessionFolder, TrackerPacketSnapshotLogReader.SidecarFileName),
                TrackerSnapshotLog = new
                {
                    Format = "jsonl",
                    IsCreated = isCreated,
                    RecordCount = snapshotInputs.Count,
                    SkippedRecordCount = skippedRecordCount,
                    ErrorCount = errorCount,
                },
            }));

        return new TestSession(diagnosticsPath, metadataPath, sidecarPath);
    }

    private TrackerWrapperPacket CreatePacket(SnapshotInputData input)
    {
        var balls = Enumerable.Range(0, input.BallCount)
            .Select(index => fixture.CreateTrackedBall(
                trackId: index + 1,
                xMm: 100 + (index * 100),
                yMm: 200 + (index * 100)))
            .ToArray();
        var robots = Enumerable.Range(0, input.RobotCount)
            .Select(index => new TrackedRobotState
            {
                Team = index % 2 == 0 ? TrackerTeam.Yellow : TrackerTeam.Blue,
                RobotId = (uint)(index + 1),
                XMm = 1200 + (index * 100),
                YMm = -300 - (index * 100),
            })
            .ToArray();
        var frame = fixture.CreateFrame(
            frameNumber: input.FrameNumber,
            dataTimestampNs: input.TimestampNs,
            balls: balls,
            robots: robots,
            primaryBallTrackId: input.BallCount > 0 ? 1 : 0);
        return fixture.CreatePacketGenerator(input.SourceName, input.SourceUuid).Generate(frame);
    }

    private static SnapshotInputData SnapshotInput(
        string sourceUuid,
        string sourceName,
        string role,
        uint frameNumber,
        long timestampNs,
        int ballCount,
        int robotCount,
        long? receivedAtOffsetTicks = null)
    {
        return new SnapshotInputData(
            sourceUuid,
            sourceName,
            role,
            frameNumber,
            timestampNs,
            ballCount,
            robotCount,
            receivedAtOffsetTicks);
    }

    private static TrackerDiagnosticsComparisonSelectedEntry SelectedEntry(uint trackedFrame)
    {
        return new TrackerDiagnosticsComparisonSelectedEntry(1, trackedFrame.ToString(CultureInfo.InvariantCulture));
    }

    private static IReadOnlyList<TrackerDiagnosticsLogEntry> ReadDisplayedEntries(string diagnosticsPath, int maxEntries = 10_000)
    {
        var entries = new List<TrackerDiagnosticsLogEntry>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(diagnosticsPath))
        {
            lineNumber++;
            if (TrackerDiagnosticsLogReader.TryParseLine(line, lineNumber, out var entry))
            {
                entries.Add(entry);
            }
        }

        var omittedEntryCount = Math.Max(0, entries.Count - maxEntries);
        return entries
            .Skip(omittedEntryCount)
            .ToArray();
    }

    private sealed record TestSession(
        string DiagnosticsPath,
        string MetadataPath,
        string SidecarPath);

    private sealed record SnapshotInputData(
        string SourceUuid,
        string SourceName,
        string Role,
        uint FrameNumber,
        long TimestampNs,
        int BallCount,
        int RobotCount,
        long? ReceivedAtOffsetTicks = null);
}
