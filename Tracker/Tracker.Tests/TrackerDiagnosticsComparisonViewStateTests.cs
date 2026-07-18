using System.Globalization;
using System.Text.Json;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
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
    /// 保存済み alignment がある場合は own / external の data timestamp 時刻系が非重複でも、selected diagnostics entry に対応する ER-FORCE snapshot を Field source に使うことを確認する。
    /// </summary>
    [Fact]
    public void LoadFieldSourceFrame_WithSavedAlignment_UsesExternalSnapshotWhenDataTimestampRangesDoNotOverlap()
    {
        var diagnosticsReceivedAt = new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero).AddMilliseconds(25);
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9900, 81_686_157_011_402, ballCount: 1, robotCount: 1),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 9901, 1_778_620_918_834_101_760, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(200).Ticks),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 9902, 1_778_620_918_844_101_760, ballCount: 2, robotCount: 2, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(30).Ticks),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 9900,
            alignmentRecords:
            [
                AlignmentInput(
                    diagnosticsLineNumber: 1,
                    diagnosticsTrackedFrameNumber: 9900,
                    diagnosticsReceivedAt,
                    ownSnapshotTimestampNs: 81_686_157_011_402,
                    sourceRole: "external",
                    sourceLabel: "ER-FORCE",
                    sourceUuid: "er-force-uuid",
                    remoteEndpoint: "192.0.2.2:12010",
                    trackerSnapshotRecordIndex: 2,
                    trackerSnapshotReceivedAt: diagnosticsReceivedAt.AddMilliseconds(5),
                    trackerSnapshotTrackedFrameNumber: 9902,
                    trackerSnapshotTimestampNs: 1_778_620_918_844_101_760),
            ]);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var frame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(9900),
            TrackerDiagnosticsFieldSource.ForSourceLabel("ER-FORCE"));

        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, frame.Status);
        Assert.Equal("saved-session-alignment", frame.MatchingRule);
        Assert.Equal("external", frame.SourceRole);
        Assert.Equal("ER-FORCE", frame.SourceLabel);
        Assert.Equal(9902u, frame.TrackedFrameNumber);
        Assert.True(frame.TrackedFrameTimestampNs > 1_778_620_918_834_101_760);
        Assert.Equal(2, frame.SemanticSummary?.Balls.Count);
        Assert.Equal(2, frame.SemanticSummary?.Robots.Count);
        Assert.NotNull(frame.TimestampDeltaNs);
        Assert.True(
            frame.TimestampDeltaNs <= TimeSpan.FromMilliseconds(10).Ticks * 100,
            $"capture-time delta must stay within fixture tolerance, actual={frame.TimestampDeltaNs}");
    }

    /// <summary>
    /// source label aggregate の External 選択でも、保存済み alignment の source key / remote endpoint を使って代表 ER-FORCE snapshot を選ぶことを確認する。
    /// </summary>
    [Fact]
    public void Load_WithSavedAlignment_AggregatesSameLabelRemoteEndpointSourcesByCaptureTime()
    {
        var diagnosticsReceivedAt = new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero).AddMilliseconds(100);
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 9910, 81_686_200_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 9911, 1_778_620_919_000_000_000, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(130).Ticks),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 9912, 1_778_620_919_010_000_000, ballCount: 3, robotCount: 3, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(102).Ticks),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 9910,
            alignmentRecords:
            [
                AlignmentInput(
                    diagnosticsLineNumber: 1,
                    diagnosticsTrackedFrameNumber: 9910,
                    diagnosticsReceivedAt,
                    ownSnapshotTimestampNs: 81_686_200_000_000,
                    sourceRole: "external",
                    sourceLabel: "ER-FORCE",
                    sourceUuid: "er-force-uuid",
                    remoteEndpoint: "192.0.2.11:12010",
                    trackerSnapshotRecordIndex: 1,
                    trackerSnapshotReceivedAt: diagnosticsReceivedAt.AddMilliseconds(30),
                    trackerSnapshotTrackedFrameNumber: 9911,
                    trackerSnapshotTimestampNs: 1_778_620_919_000_000_000),
                AlignmentInput(
                    diagnosticsLineNumber: 1,
                    diagnosticsTrackedFrameNumber: 9910,
                    diagnosticsReceivedAt,
                    ownSnapshotTimestampNs: 81_686_200_000_000,
                    sourceRole: "external",
                    sourceLabel: "ER-FORCE",
                    sourceUuid: "er-force-uuid",
                    remoteEndpoint: "192.0.2.12:12010",
                    trackerSnapshotRecordIndex: 2,
                    trackerSnapshotReceivedAt: diagnosticsReceivedAt.AddMilliseconds(2),
                    trackerSnapshotTrackedFrameNumber: 9912,
                    trackerSnapshotTimestampNs: 1_778_620_919_010_000_000),
            ]);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var state = reader.Load(
            session.DiagnosticsPath,
            SelectedEntry(9910),
            TrackerDiagnosticsComparisonSourceFilter.External);
        var frame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(9910),
            TrackerDiagnosticsFieldSource.External);

        Assert.NotNull(state.SelectedEntryComparison);
        Assert.Equal("saved-session-alignment", state.SelectedEntryComparison!.MatchingRule);
        Assert.Equal(9912u, state.SelectedEntryComparison.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(3, state.SelectedEntryComparison.RobotCount);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, frame.Status);
        Assert.Equal("saved-session-alignment", frame.MatchingRule);
        Assert.Equal(9912u, frame.TrackedFrameNumber);
        Assert.True(frame.TimestampDeltaNs <= TimeSpan.FromMilliseconds(5).Ticks * 100);
    }

    /// <summary>
    /// selected replay timeline tick がある場合、同じ diagnostics line 内の先頭 record ではなく timeline index の alignment を使うことを確認する。
    /// </summary>
    [Fact]
    public void Load_WithSelectedReplayTimeline_UsesTimelineRecordForComparisonAndFieldSource()
    {
        var diagnosticsReceivedAt = new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.Zero);
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 1000, 81_686_200_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3000, 1_778_620_918_834_101_760, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: 0),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3001, 1_778_620_918_834_101_761, ballCount: 2, robotCount: 2, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(20).Ticks),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 1000,
            alignmentRecords:
            [
                AlignmentInput(
                    diagnosticsLineNumber: 1,
                    diagnosticsTrackedFrameNumber: 1000,
                    diagnosticsReceivedAt,
                    ownSnapshotTimestampNs: 81_686_200_000_000,
                    sourceRole: "external",
                    sourceLabel: "ER-FORCE",
                    sourceUuid: "er-force-uuid",
                    remoteEndpoint: "192.0.2.0:12010",
                    trackerSnapshotRecordIndex: 1,
                    trackerSnapshotReceivedAt: diagnosticsReceivedAt,
                    trackerSnapshotTrackedFrameNumber: 3000,
                    trackerSnapshotTimestampNs: 1_778_620_918_834_101_760),
                AlignmentInput(
                    diagnosticsLineNumber: 1,
                    diagnosticsTrackedFrameNumber: 1000,
                    diagnosticsReceivedAt: diagnosticsReceivedAt.AddMilliseconds(20),
                    ownSnapshotTimestampNs: 81_686_200_000_000,
                    sourceRole: "external",
                    sourceLabel: "ER-FORCE",
                    sourceUuid: "er-force-uuid",
                    remoteEndpoint: "192.0.2.20:12010",
                    trackerSnapshotRecordIndex: 2,
                    trackerSnapshotReceivedAt: diagnosticsReceivedAt.AddMilliseconds(20),
                    trackerSnapshotTrackedFrameNumber: 3001,
                    trackerSnapshotTimestampNs: 1_778_620_918_834_101_761),
            ]);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var selectedTimeline = new TrackerDiagnosticsReplayTimelineSelection(
            ReplayTimelineIndex: 1,
            DiagnosticsLineNumber: 1,
            ReceivedAt: diagnosticsReceivedAt.AddMilliseconds(20));

        var state = reader.Load(
            session.DiagnosticsPath,
            SelectedEntry(1000),
            selectedTimeline,
            TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel("ER-FORCE"));
        var frame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(1000),
            selectedTimeline,
            TrackerDiagnosticsFieldSource.ForSourceLabel("ER-FORCE"));

        Assert.Equal(2, state.ReplayTimeline.Count);
        Assert.Equal(3001u, state.SelectedEntryComparison?.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(3001u, frame.TrackedFrameNumber);
        Assert.Equal(2, frame.SemanticSummary?.Balls.Count);
    }

    /// <summary>
    /// fast tracker tick 上で別 source を選んでも、source ごとの latest-before alignment record から snapshot を引けることを確認する。
    /// </summary>
    [Fact]
    public void LoadFieldSourceFrame_WithSelectedReplayTimeline_UsesLatestBeforeRecordForSelectedSource()
    {
        var receivedAt = new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.Zero);
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 1000, 81_686_200_000_000, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: 0),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3000, 1_778_620_918_834_101_760, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: 0),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3001, 1_778_620_918_834_101_761, ballCount: 2, robotCount: 2, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(20).Ticks),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3002, 1_778_620_918_834_101_762, ballCount: 3, robotCount: 3, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(40).Ticks),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 1000,
            alignmentRecords:
            [
                AlignmentInput(1, 1000, receivedAt, 81_686_200_000_000, "own", "ibis", "ibis-runtime", "self", 0, receivedAt, 1000, 81_686_200_000_000, replayTimelineIndex: 0),
                AlignmentInput(1, 1000, receivedAt, 81_686_200_000_000, "external", "ER-FORCE", "er-force-uuid", "192.0.2.0:12010", 1, receivedAt, 3000, 1_778_620_918_834_101_760, replayTimelineIndex: 0),
                AlignmentInput(1, 1000, receivedAt.AddMilliseconds(20), 81_686_200_000_000, "own", "ibis", "ibis-runtime", "self", 0, receivedAt, 1000, 81_686_200_000_000, replayTimelineIndex: 1),
                AlignmentInput(1, 1000, receivedAt.AddMilliseconds(20), 81_686_200_000_000, "external", "ER-FORCE", "er-force-uuid", "192.0.2.20:12010", 2, receivedAt.AddMilliseconds(20), 3001, 1_778_620_918_834_101_761, replayTimelineIndex: 1),
                AlignmentInput(1, 1000, receivedAt.AddMilliseconds(40), 81_686_200_000_000, "own", "ibis", "ibis-runtime", "self", 0, receivedAt, 1000, 81_686_200_000_000, replayTimelineIndex: 2),
                AlignmentInput(1, 1000, receivedAt.AddMilliseconds(40), 81_686_200_000_000, "external", "ER-FORCE", "er-force-uuid", "192.0.2.40:12010", 3, receivedAt.AddMilliseconds(40), 3002, 1_778_620_918_834_101_762, replayTimelineIndex: 2),
            ]);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var selectedTimeline = new TrackerDiagnosticsReplayTimelineSelection(
            ReplayTimelineIndex: 2,
            DiagnosticsLineNumber: 1,
            ReceivedAt: receivedAt.AddMilliseconds(40));

        var state = reader.Load(
            session.DiagnosticsPath,
            SelectedEntry(1000),
            selectedTimeline,
            TrackerDiagnosticsComparisonSourceFilter.Own);
        var externalFrame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(1000),
            selectedTimeline,
            TrackerDiagnosticsFieldSource.ForSourceLabel("ER-FORCE"));

        Assert.Equal("saved-session-alignment", state.SelectedEntryComparison?.MatchingRule);
        Assert.Equal(1000u, state.SelectedEntryComparison?.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(81_686_200_000_000, state.SelectedEntryComparison?.NearestSnapshotTimestampNs);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, externalFrame.Status);
        Assert.Equal("saved-session-alignment", externalFrame.MatchingRule);
        Assert.Equal(3002u, externalFrame.TrackedFrameNumber);
        Assert.True(externalFrame.TrackedFrameTimestampNs > 1_000_000_000_000_000_000);
    }

    /// <summary>
    /// selected replay timeline tick に対象 source の alignment が無い場合、timeline を動かさず直前 sample を latest-before hold として使うことを確認する。
    /// </summary>
    [Fact]
    public void Load_WithSelectedReplayTimeline_WhenSourceMissingAtSelectedTick_UsesLatestBeforeSnapshotWithoutMovingSelectedTime()
    {
        var receivedAt = new DateTimeOffset(2026, 5, 13, 9, 30, 0, TimeSpan.Zero);
        var selectedReceivedAt = receivedAt.AddMilliseconds(40);
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 1100, 81_686_210_000_000, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: 0),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3100, 1_778_620_918_834_301_760, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: 0),
                SnapshotInput("ibis-runtime", "ibis", "own", 1101, 81_686_210_020_000, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(20).Ticks),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3101, 1_778_620_918_834_301_761, ballCount: 2, robotCount: 2, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(20).Ticks),
                SnapshotInput("ibis-runtime", "ibis", "own", 1102, 81_686_210_040_000, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(40).Ticks),
                SnapshotInput("mage-uuid", "MAGE", "external", 4100, 1_778_620_918_834_301_762, ballCount: 3, robotCount: 3, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(40).Ticks),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 1102,
            alignmentRecords:
            [
                AlignmentInput(1, 1100, receivedAt, 81_686_210_000_000, "own", "ibis", "ibis-runtime", "self", 0, receivedAt, 1100, 81_686_210_000_000, replayTimelineIndex: 0),
                AlignmentInput(1, 1100, receivedAt, 81_686_210_000_000, "external", "ER-FORCE", "er-force-uuid", "192.0.2.0:12010", 1, receivedAt, 3100, 1_778_620_918_834_301_760, replayTimelineIndex: 0),
                AlignmentInput(1, 1101, receivedAt.AddMilliseconds(20), 81_686_210_020_000, "own", "ibis", "ibis-runtime", "self", 2, receivedAt.AddMilliseconds(20), 1101, 81_686_210_020_000, replayTimelineIndex: 1),
                AlignmentInput(1, 1101, receivedAt.AddMilliseconds(20), 81_686_210_020_000, "external", "ER-FORCE", "er-force-uuid", "192.0.2.20:12010", 3, receivedAt.AddMilliseconds(20), 3101, 1_778_620_918_834_301_761, replayTimelineIndex: 1),
                AlignmentInput(1, 1102, selectedReceivedAt, 81_686_210_040_000, "own", "ibis", "ibis-runtime", "self", 4, selectedReceivedAt, 1102, 81_686_210_040_000, replayTimelineIndex: 2),
                AlignmentInput(1, 1102, selectedReceivedAt, 81_686_210_040_000, "external", "MAGE", "mage-uuid", "192.0.2.40:12010", 5, selectedReceivedAt, 4100, 1_778_620_918_834_301_762, replayTimelineIndex: 2),
            ]);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var selectedTimeline = new TrackerDiagnosticsReplayTimelineSelection(2, 1, selectedReceivedAt);

        var state = reader.Load(
            session.DiagnosticsPath,
            SelectedEntry(1102),
            selectedTimeline,
            TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel("ER-FORCE"));
        var frame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(1102),
            selectedTimeline,
            TrackerDiagnosticsFieldSource.ForSourceLabel("ER-FORCE"));

        var selectedTick = Assert.Single(state.ReplayTimeline, tick => tick.ReplayTimelineIndex == 2);
        Assert.Equal(selectedReceivedAt, selectedTick.ReceivedAt);
        Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.Ready, state.SelectedEntryComparison?.Status);
        Assert.Equal("latest-before", state.SelectedEntryComparison?.MatchingRule);
        Assert.Equal(3101u, state.SelectedEntryComparison?.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(20_000_000, state.SelectedEntryComparison?.TimestampDeltaNs);
        AssertObjectProperty(
            state.SelectedEntryComparison!,
            "NearestSnapshotReceivedAt",
            receivedAt.AddMilliseconds(20));
        AssertObjectProperty(
            state.SelectedEntryComparison!,
            "SelectedReplayTimelineReceivedAt",
            selectedReceivedAt);
        AssertObjectProperty(state.SelectedEntryComparison!, "IsLatestBefore", true);
        AssertObjectProperty(state.SelectedEntryComparison!, "IsStale", true);
        AssertObjectProperty(state.SelectedEntryComparison!, "StalenessDeltaNs", 20_000_000L);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, frame.Status);
        Assert.Equal("latest-before", frame.MatchingRule);
        Assert.Equal(3101u, frame.TrackedFrameNumber);
        Assert.Equal(20_000_000, frame.TimestampDeltaNs);
        AssertObjectProperty(frame, "SourceSnapshotReceivedAt", receivedAt.AddMilliseconds(20));
        AssertObjectProperty(frame, "SelectedReplayTimelineReceivedAt", selectedReceivedAt);
        AssertObjectProperty(frame, "IsLatestBefore", true);
        AssertObjectProperty(frame, "IsStale", true);
        AssertObjectProperty(frame, "StalenessDeltaNs", 20_000_000L);
        Assert.Equal(2, frame.SemanticSummary?.Balls.Count);
    }

    /// <summary>
    /// sample replay timeline だけがあり alignment sidecar が無い場合でも、tracker snapshot sidecar の受信時刻から latest-before を選ぶこと。
    /// </summary>
    [Fact]
    public void LoadFieldSourceFrame_WithSelectedReplayTimelineWithoutAlignment_UsesLatestBeforeSnapshotByReceivedAt()
    {
        var receivedAt = new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);
        var selectedReceivedAt = receivedAt.AddMilliseconds(40);
        var session = CreateSession(
            [
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3100, 1_778_620_918_834_301_760, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: 0),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3101, 1_778_620_918_834_301_761, ballCount: 2, robotCount: 2, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(20).Ticks),
                SnapshotInput("late-force-uuid", "LATE-FORCE", "external", 4100, 1_778_620_918_834_301_762, ballCount: 3, robotCount: 3, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(60).Ticks),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 1102);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var selectedTimeline = new TrackerDiagnosticsReplayTimelineSelection(2, 1, selectedReceivedAt);

        var state = reader.Load(
            session.DiagnosticsPath,
            SelectedEntry(1102),
            selectedTimeline,
            TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel("ER-FORCE"));
        var frame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(1102),
            selectedTimeline,
            TrackerDiagnosticsFieldSource.ForSourceLabel("ER-FORCE"));

        Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.Ready, state.SelectedEntryComparison?.Status);
        Assert.Equal("latest-before", state.SelectedEntryComparison?.MatchingRule);
        Assert.Equal(3101u, state.SelectedEntryComparison?.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(receivedAt.AddMilliseconds(20), state.SelectedEntryComparison?.NearestSnapshotReceivedAt);
        Assert.Equal(selectedReceivedAt, state.SelectedEntryComparison?.SelectedReplayTimelineReceivedAt);
        Assert.Equal(20_000_000, state.SelectedEntryComparison?.TimestampDeltaNs);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, frame.Status);
        Assert.Equal("latest-before", frame.MatchingRule);
        Assert.Equal(3101u, frame.TrackedFrameNumber);
        Assert.Equal(receivedAt.AddMilliseconds(20), frame.SourceSnapshotReceivedAt);
        Assert.Equal(selectedReceivedAt, frame.SelectedReplayTimelineReceivedAt);
        Assert.Equal(20_000_000, frame.TimestampDeltaNs);
        Assert.Equal(2, frame.SemanticSummary?.Balls.Count);
    }

    /// <summary>
    /// selected tick 以前に同じ source が無い場合だけ missing とし、future snapshot へ fallback しないことを確認する。
    /// </summary>
    [Fact]
    public void Load_WithSelectedReplayTimeline_WhenOnlyFutureSourceSnapshotExists_ReturnsMissingWithoutFutureFallback()
    {
        var receivedAt = new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);
        var selectedReceivedAt = receivedAt.AddMilliseconds(40);
        var futureReceivedAt = receivedAt.AddMilliseconds(60);
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 1200, 81_686_220_000_000, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: 0),
                SnapshotInput("ibis-runtime", "ibis", "own", 1201, 81_686_220_040_000, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(40).Ticks),
                SnapshotInput("mage-uuid", "MAGE", "external", 4200, 1_778_620_918_834_401_760, ballCount: 3, robotCount: 3, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(40).Ticks),
                SnapshotInput("late-uuid", "LATE-TRACKER", "external", 5200, 1_778_620_918_834_401_761, ballCount: 4, robotCount: 4, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(60).Ticks),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 1201,
            alignmentRecords:
            [
                AlignmentInput(1, 1200, receivedAt, 81_686_220_000_000, "own", "ibis", "ibis-runtime", "self", 0, receivedAt, 1200, 81_686_220_000_000, replayTimelineIndex: 0),
                AlignmentInput(1, 1201, selectedReceivedAt, 81_686_220_040_000, "own", "ibis", "ibis-runtime", "self", 1, selectedReceivedAt, 1201, 81_686_220_040_000, replayTimelineIndex: 1),
                AlignmentInput(1, 1201, selectedReceivedAt, 81_686_220_040_000, "external", "MAGE", "mage-uuid", "192.0.2.40:12010", 2, selectedReceivedAt, 4200, 1_778_620_918_834_401_760, replayTimelineIndex: 1),
                AlignmentInput(1, 1201, futureReceivedAt, 81_686_220_040_000, "external", "LATE-TRACKER", "late-uuid", "192.0.2.60:12010", 3, futureReceivedAt, 5200, 1_778_620_918_834_401_761, replayTimelineIndex: 2),
            ]);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var selectedTimeline = new TrackerDiagnosticsReplayTimelineSelection(1, 1, selectedReceivedAt);

        var state = reader.Load(
            session.DiagnosticsPath,
            SelectedEntry(1201),
            selectedTimeline,
            TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel("LATE-TRACKER"));
        var frame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(1201),
            selectedTimeline,
            TrackerDiagnosticsFieldSource.ForSourceLabel("LATE-TRACKER"));

        Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.NoCandidateSnapshot, state.SelectedEntryComparison?.Status);
        Assert.Null(state.SelectedEntryComparison?.NearestSnapshotTrackedFrameNumber);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.CandidateMissing, frame.Status);
        Assert.Null(frame.TrackedFrameNumber);
        Assert.Null(frame.SemanticSummary);
    }

    /// <summary>
    /// tick / scrub / source selector 変更で tracker sidecar と alignment sidecar を再読込しないことを確認する。
    /// </summary>
    [Fact]
    public void LoadAndFieldSourceChanges_WhenFileStateIsUnchanged_ReusesSnapshotAndAlignmentIndexes()
    {
        var diagnosticsReceivedAt = new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.Zero);
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 1010, 81_686_201_000_000, ballCount: 1, robotCount: 1),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3010, 1_778_620_918_834_201_760, ballCount: 1, robotCount: 1, receivedAtOffsetTicks: 0),
                SnapshotInput("er-force-uuid", "ER-FORCE", "external", 3011, 1_778_620_918_834_201_761, ballCount: 2, robotCount: 2, receivedAtOffsetTicks: TimeSpan.FromMilliseconds(20).Ticks),
            ],
            isCreated: true,
            skippedRecordCount: 0,
            errorCount: 0,
            diagnosticsTrackedFrame: 1010,
            alignmentRecords:
            [
                AlignmentInput(1, 1010, diagnosticsReceivedAt, 81_686_201_000_000, "external", "ER-FORCE", "er-force-uuid", "192.0.2.0:12010", 1, diagnosticsReceivedAt, 3010, 1_778_620_918_834_201_760),
                AlignmentInput(1, 1010, diagnosticsReceivedAt.AddMilliseconds(20), 81_686_201_000_000, "external", "ER-FORCE", "er-force-uuid", "192.0.2.20:12010", 2, diagnosticsReceivedAt.AddMilliseconds(20), 3011, 1_778_620_918_834_201_761),
            ]);
        var sidecarBuildCount = 0;
        var alignmentBuildCount = 0;
        var reader = new TrackerDiagnosticsComparisonViewStateReader(
            sidecarPath =>
            {
                sidecarBuildCount++;
                return TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath).ToArray();
            },
            alignmentPath =>
            {
                alignmentBuildCount++;
                return TrackerSnapshotAlignmentLogReader.ReadRecords(alignmentPath).ToArray();
            });

        _ = reader.Load(
            session.DiagnosticsPath,
            SelectedEntry(1010),
            new TrackerDiagnosticsReplayTimelineSelection(0, 1, diagnosticsReceivedAt),
            TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel("ER-FORCE"));
        _ = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            SelectedEntry(1010),
            new TrackerDiagnosticsReplayTimelineSelection(1, 1, diagnosticsReceivedAt.AddMilliseconds(20)),
            TrackerDiagnosticsFieldSource.ForSourceLabel("ER-FORCE"));

        Assert.Equal(1, sidecarBuildCount);
        Assert.Equal(1, alignmentBuildCount);
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
        IReadOnlyList<uint>? diagnosticsTrackedFrames = null,
        IReadOnlyList<AlignmentInputData>? alignmentRecords = null)
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
                    remoteEndpoint: ResolveRemoteEndpoint(input),
                    sourceRole: input.Role,
                    sourceLabel: string.IsNullOrWhiteSpace(input.SourceName) ? input.Role : input.SourceName));
            }));

        if (alignmentRecords is not null)
        {
            var alignmentPath = Path.Combine(sessionFolderPath, "tracker-snapshot-alignment.jsonl");
            File.WriteAllLines(
                alignmentPath,
                alignmentRecords.Select((input, index) => JsonSerializer.Serialize(new
                {
                    schemaVersion = 2,
                    replayTimelineIndex = input.ReplayTimelineIndex ?? index,
                    replayTimelineReceivedAt = input.DiagnosticsReceivedAt,
                    replayTimelineKind = "diagnostics-entry",
                    diagnosticsLineNumber = input.DiagnosticsLineNumber,
                    renderFrameNumber = input.DiagnosticsTrackedFrameNumber,
                    renderReceivedAt = input.DiagnosticsReceivedAt,
                    renderMatchRule = "exact",
                    sourceKey = TrackerSnapshotAlignmentRecord.CreateSourceKey(
                        input.SourceRole,
                        input.SourceLabel,
                        input.SourceUuid,
                        input.RemoteEndpoint),
                    sourceRole = input.SourceRole,
                    sourceLabel = input.SourceLabel,
                    sourceUuid = input.SourceUuid,
                    remoteEndpoint = input.RemoteEndpoint,
                    trackerSnapshotRecordIndex = input.TrackerSnapshotRecordIndex,
                    trackerSnapshotReceivedAt = input.TrackerSnapshotReceivedAt,
                    trackerSnapshotTrackedFrameNumber = input.TrackerSnapshotTrackedFrameNumber,
                    trackerSnapshotTimestampNs = input.TrackerSnapshotTimestampNs,
                    matchingRule = "saved-session-alignment",
                    receivedAtDeltaTicks = Math.Abs((input.TrackerSnapshotReceivedAt - input.DiagnosticsReceivedAt).Ticks),
                    status = "ready",
                })));
        }

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
                TrackerSnapshotAlignmentPath = alignmentRecords is null
                    ? null
                    : Path.Combine(sessionFolder, "tracker-snapshot-alignment.jsonl"),
                TrackerSnapshotLog = new
                {
                    Format = "jsonl",
                    IsCreated = isCreated,
                    RecordCount = snapshotInputs.Count,
                    SkippedRecordCount = skippedRecordCount,
                    ErrorCount = errorCount,
                },
                TrackerSnapshotAlignmentLog = alignmentRecords is null
                    ? null
                    : new
                    {
                        Format = "jsonl",
                        IsCreated = true,
                        RecordCount = alignmentRecords.Count,
                        SkippedRecordCount = 0,
                        ErrorCount = 0,
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

    private static AlignmentInputData AlignmentInput(
        int diagnosticsLineNumber,
        uint diagnosticsTrackedFrameNumber,
        DateTimeOffset diagnosticsReceivedAt,
        long ownSnapshotTimestampNs,
        string sourceRole,
        string sourceLabel,
        string sourceUuid,
        string remoteEndpoint,
        int trackerSnapshotRecordIndex,
        DateTimeOffset trackerSnapshotReceivedAt,
        uint trackerSnapshotTrackedFrameNumber,
        long trackerSnapshotTimestampNs,
        int? replayTimelineIndex = null)
    {
        return new AlignmentInputData(
            diagnosticsLineNumber,
            diagnosticsTrackedFrameNumber,
            diagnosticsReceivedAt,
            ownSnapshotTimestampNs,
            sourceRole,
            sourceLabel,
            sourceUuid,
            remoteEndpoint,
            trackerSnapshotRecordIndex,
            trackerSnapshotReceivedAt,
            trackerSnapshotTrackedFrameNumber,
            trackerSnapshotTimestampNs,
            replayTimelineIndex);
    }

    private static string ResolveRemoteEndpoint(SnapshotInputData input)
    {
        return input.Role == "own" ? "self" : $"192.0.2.{input.FrameNumber % 100}:12010";
    }

    private static TrackerDiagnosticsComparisonSelectedEntry SelectedEntry(uint trackedFrame)
    {
        return new TrackerDiagnosticsComparisonSelectedEntry(1, trackedFrame.ToString(CultureInfo.InvariantCulture));
    }

    private static void AssertObjectProperty<T>(object instance, string propertyName, T expected)
    {
        var property = instance.GetType().GetProperty(propertyName);
        Assert.True(property is not null, $"{instance.GetType().Name}.{propertyName} property must exist.");
        var value = Assert.IsType<T>(property!.GetValue(instance));
        Assert.Equal(expected, value);
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

    private sealed record AlignmentInputData(
        int DiagnosticsLineNumber,
        uint DiagnosticsTrackedFrameNumber,
        DateTimeOffset DiagnosticsReceivedAt,
        long OwnSnapshotTimestampNs,
        string SourceRole,
        string SourceLabel,
        string SourceUuid,
        string RemoteEndpoint,
        int TrackerSnapshotRecordIndex,
        DateTimeOffset TrackerSnapshotReceivedAt,
        uint TrackerSnapshotTrackedFrameNumber,
        long TrackerSnapshotTimestampNs,
        int? ReplayTimelineIndex);
}
