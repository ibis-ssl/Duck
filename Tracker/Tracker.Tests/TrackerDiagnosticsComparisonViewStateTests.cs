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
                    new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero).AddTicks(input.TimestampNs / 100),
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
        int robotCount)
    {
        return new SnapshotInputData(sourceUuid, sourceName, role, frameNumber, timestampNs, ballCount, robotCount);
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
        int RobotCount);
}
