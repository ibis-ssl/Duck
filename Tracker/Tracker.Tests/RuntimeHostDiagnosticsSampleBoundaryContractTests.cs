using System.Text.Json;
using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

/// <summary>
/// RUNTIME-HOST-003 の diagnostics sample boundary / legacy degraded contract を Red test として固定する。
/// </summary>
public class RuntimeHostDiagnosticsSampleBoundaryContractTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 5, 14, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// diagnostics sample tick が WorldFrameCommitted/render snapshot cadence に依存せず replay timeline に残ることを確認する。
    /// </summary>
    [Fact]
    public void LoadReplayTimeline_UsesDiagnosticsSampleTicksEvenWhenWorldFrameCommittedDoesNotAdvance()
    {
        var session = CreateDiagnosticsSampleSession(
            [
                SampleInput(0, TimeSpan.Zero, rawFrameNumber: 2000, worldFrameCommitted: true, renderFrameNumber: 1000),
                SampleInput(1, TimeSpan.FromMilliseconds(10), rawFrameNumber: 2001, worldFrameCommitted: false, renderFrameNumber: 1000),
                SampleInput(2, TimeSpan.FromMilliseconds(20), rawFrameNumber: 2002, worldFrameCommitted: false, renderFrameNumber: 1000),
            ]);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var timeline = reader.LoadReplayTimeline(session.DiagnosticsPath);

        Assert.Equal(3, timeline.Count);
        Assert.Equal([0, 1, 2], timeline.Select(tick => tick.ReplayTimelineIndex).ToArray());
        Assert.Equal(
            [0, 10, 20],
            timeline.Select(tick => (int)(tick.ReceivedAt - BaseTime).TotalMilliseconds).ToArray());
        Assert.All(timeline, tick => Assert.Equal("diagnostics-sample", tick.Kind));
    }

    /// <summary>
    /// Diagnostics の Vision Input が旧 render snapshot ではなく diagnostics sample sidecar から復元されることを確認する。
    /// </summary>
    [Fact]
    public void LoadFieldSourceFrame_ForVisionInputRestoresFromDiagnosticsSampleSidecar()
    {
        var session = CreateDiagnosticsSampleSession(
            [
                SampleInput(0, TimeSpan.Zero, rawFrameNumber: 2100, worldFrameCommitted: true, renderFrameNumber: 1000),
                SampleInput(1, TimeSpan.FromMilliseconds(10), rawFrameNumber: 2101, worldFrameCommitted: false, renderFrameNumber: 1000),
            ]);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var selectedTimeline = new TrackerDiagnosticsReplayTimelineSelection(1, 1, BaseTime.AddMilliseconds(10));

        var frame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            new TrackerDiagnosticsComparisonSelectedEntry(1, "1000"),
            selectedTimeline,
            TrackerDiagnosticsFieldSource.VisionInput);

        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, frame.Status);
        Assert.Equal("diagnostics-sample-sidecar", frame.MatchingRule);
        Assert.Equal(BaseTime.AddMilliseconds(10), frame.SourceSnapshotReceivedAt);
        Assert.Equal(1, frame.SemanticSummary?.BallCount);
        Assert.Equal(101, frame.SemanticSummary?.Balls.Single().XMm);
    }

    /// <summary>
    /// Diagnostics UI state が Vision Input / ibis tracker も sample sidecar frame として reader から読むことを確認する。
    /// </summary>
    [Fact]
    public void UiState_ForVisionInputAndIbisTrackerLoadsDiagnosticsSampleFrames()
    {
        var session = CreateDiagnosticsSampleSession(
            [
                SampleInput(0, TimeSpan.Zero, rawFrameNumber: 2200, worldFrameCommitted: true, renderFrameNumber: 1200),
                SampleInput(1, TimeSpan.FromMilliseconds(10), rawFrameNumber: 2201, worldFrameCommitted: true, renderFrameNumber: 1201),
            ]);
        var uiState = new TrackerDiagnosticsComparisonUiState(new TrackerDiagnosticsComparisonViewStateReader());
        var selectedEntry = CreateDisplayedEntry(lineNumber: 1, trackedFrame: "1201");
        var selectedTimeline = new TrackerDiagnosticsReplayTimelineSelection(1, 1, BaseTime.AddMilliseconds(10));

        uiState.Load(session.DiagnosticsPath, selectedEntry, selectedTimeline);

        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, uiState.LeftTrackerFieldSourceFrame?.Status);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, uiState.RightTrackerFieldSourceFrame?.Status);
        Assert.Equal("diagnostics-sample-sidecar", uiState.LeftTrackerFieldSourceFrame?.MatchingRule);
        Assert.Equal("diagnostics-sample-sidecar", uiState.RightTrackerFieldSourceFrame?.MatchingRule);
        Assert.Equal("Vision Input", uiState.LeftTrackerFieldSourceFrame?.SourceLabel);
        Assert.Equal("ibis tracker", uiState.RightTrackerFieldSourceFrame?.SourceLabel);
        Assert.Equal(101, uiState.LeftTrackerFieldSourceFrame?.SemanticSummary?.Balls.Single().XMm);
        Assert.Equal(201, uiState.RightTrackerFieldSourceFrame?.SemanticSummary?.Balls.Single().XMm);
    }

    /// <summary>
    /// sample sidecar がない session で Vision Input が旧 render snapshot fallback に戻らないことを確認する。
    /// </summary>
    [Fact]
    public void LoadFieldSourceFrame_ForVisionInputWithoutDiagnosticsSampleDoesNotFallbackToRenderSnapshot()
    {
        var session = CreateLegacyRenderSnapshotOnlySession();
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var frame = reader.LoadFieldSourceFrame(
            session.DiagnosticsPath,
            new TrackerDiagnosticsComparisonSelectedEntry(1, "1000"),
            TrackerDiagnosticsFieldSource.VisionInput);

        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable, frame.Status);
        Assert.DoesNotContain("render snapshot", frame.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 旧 render snapshot sidecar だけの session は unsupported / degraded legacy として扱うことを確認する。
    /// </summary>
    [Fact]
    public void Load_WithOnlyLegacyRenderSnapshotSidecarReportsUnsupportedDegradedLegacy()
    {
        var session = CreateLegacyRenderSnapshotOnlySession();
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var state = reader.Load(
            session.DiagnosticsPath,
            new TrackerDiagnosticsComparisonSelectedEntry(1, "1000"),
            TrackerDiagnosticsComparisonSourceFilter.All);

        Assert.Contains("unsupported", state.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("degraded legacy", state.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.ReplayTimeline);
    }

    private static TestSession CreateDiagnosticsSampleSession(IReadOnlyList<SampleInputData> samples)
    {
        var session = CreateSessionDirectory("runtime-host-003-sample");
        var diagnosticsPath = Path.Combine(session.SessionFolderPath, "runtime-host-003.tracker-diagnostics.log");
        var metadataPath = Path.Combine(session.SessionFolderPath, "runtime-host-003.metadata.json");
        var sampleSidecarPath = Path.Combine(session.SessionFolderPath, "diagnostics-samples.jsonl");

        File.WriteAllLines(
            sampleSidecarPath,
            samples.Select(sample => JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                sampleIndex = sample.SampleIndex,
                sampleReceivedAt = BaseTime.Add(sample.Offset),
                sampleKind = "diagnostics-sample",
                rawFrameNumber = sample.RawFrameNumber,
                rawCameraId = 0,
                worldFrameCommitted = sample.WorldFrameCommitted,
                renderFrameNumber = sample.RenderFrameNumber,
                trackedFrameNumber = sample.RenderFrameNumber,
                trackedFrameTimestampNs = sample.RenderFrameNumber * 1000L,
                rawSemanticSummary = CreateSemanticSummary(
                    "Vision Input",
                    "vision-input",
                    sample.RawFrameNumber,
                    timestampNs: 0,
                    ballX: 100 + sample.SampleIndex,
                    robotX: 1200),
                trackedSemanticSummary = CreateSemanticSummary(
                    "ibis tracker",
                    "own",
                    sample.RenderFrameNumber,
                    sample.RenderFrameNumber * 1000L,
                    ballX: 200 + sample.SampleIndex,
                    robotX: 1300),
            })));
        WriteDiagnosticsLog(diagnosticsPath, trackedFrame: 1000);
        File.WriteAllText(
            metadataPath,
            JsonSerializer.Serialize(new
            {
                SessionFolder = session.SessionFolder,
                DiagnosticsLogPath = Path.Combine(session.SessionFolder, Path.GetFileName(diagnosticsPath)),
                DiagnosticsSampleSidecarPath = Path.Combine(session.SessionFolder, Path.GetFileName(sampleSidecarPath)),
                DiagnosticsSampleLog = new
                {
                    Format = "jsonl",
                    IsCreated = true,
                    RecordCount = samples.Count,
                },
            }));

        return new TestSession(diagnosticsPath, metadataPath);
    }

    private static TestSession CreateLegacyRenderSnapshotOnlySession()
    {
        var session = CreateSessionDirectory("runtime-host-003-legacy");
        var diagnosticsPath = Path.Combine(session.SessionFolderPath, "runtime-host-003.tracker-diagnostics.log");
        var metadataPath = Path.Combine(session.SessionFolderPath, "runtime-host-003.metadata.json");
        var renderSnapshotPath = Path.Combine(session.SessionFolderPath, "runtime-host-003.render-snapshots.jsonl.gz");

        WriteDiagnosticsLog(diagnosticsPath, trackedFrame: 1000);
        File.WriteAllText(renderSnapshotPath, "legacy render snapshot placeholder");
        File.WriteAllText(
            metadataPath,
            JsonSerializer.Serialize(new
            {
                SessionFolder = session.SessionFolder,
                DiagnosticsLogPath = Path.Combine(session.SessionFolder, Path.GetFileName(diagnosticsPath)),
                RenderSnapshotPath = Path.Combine(session.SessionFolder, Path.GetFileName(renderSnapshotPath)),
            }));

        return new TestSession(diagnosticsPath, metadataPath);
    }

    private static SessionDirectory CreateSessionDirectory(string prefix)
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        var sessionFolder = "session";
        var sessionFolderPath = Path.Combine(captureDirectory, sessionFolder);
        Directory.CreateDirectory(sessionFolderPath);
        return new SessionDirectory(sessionFolder, sessionFolderPath);
    }

    private static void WriteDiagnosticsLog(string diagnosticsPath, uint trackedFrame)
    {
        File.WriteAllText(
            diagnosticsPath,
            $"2026-05-14T08:00:00.0000000+00:00 Tracker diagnostics profile=sim rawFrame=2000 rawCamera=0 rawBalls=1 rawBallDetails=[x=100,y=200,z=0,c=1] rawBlue=[] rawYellow=[] trackedFrame={trackedFrame} trackedBalls=1 trackedBallDetails=[#1:x=100,y=200,z=0,vis=1,q=1,cams=0] trackedRobots=1 trackedRobotDetails=[Y3:x=1200,y=-300,o=0,w=0,vis=1,q=1] ballOutVisibility=0 ballHalfLifeSec=1 ballLifetimeNs=1000000000");
    }

    private static object CreateSemanticSummary(
        string sourceLabel,
        string sourceRole,
        uint frameNumber,
        long timestampNs,
        double ballX,
        double robotX)
    {
        return new
        {
            ballCount = 1,
            robotCount = 1,
            trackedFrameNumber = frameNumber,
            trackedFrameTimestampNs = timestampNs,
            sourceUuid = "",
            sourceName = sourceLabel,
            sourceRole,
            sourceLabel,
            balls = new[]
            {
                new
                {
                    index = 0,
                    xMm = ballX,
                    yMm = 20,
                    zMm = 0,
                    visibility = 1.0f,
                },
            },
            robots = new[]
            {
                new
                {
                    team = "Yellow",
                    robotId = 3,
                    xMm = robotX,
                    yMm = -300,
                    orientationRad = 0.5f,
                    visibility = 1.0f,
                },
            },
        };
    }

    private static TrackerDiagnosticsLogEntry CreateDisplayedEntry(int lineNumber, string trackedFrame)
    {
        return new TrackerDiagnosticsLogEntry(
            lineNumber,
            BaseTime,
            "sim",
            "2000",
            "0",
            RawBallCount: 1,
            RawBallDetails: "",
            RawBlueDetails: "",
            RawYellowDetails: "",
            trackedFrame,
            TrackedBallCount: 1,
            TrackedBallDetails: "",
            TrackedRobotCount: 1,
            TrackedRobotDetails: "",
            BallOutputVisibility: "0",
            BallVisibilityHalfLifeSeconds: "1",
            BallTrackLifetimeNs: "1000000000",
            RawLine: "");
    }

    private static SampleInputData SampleInput(
        int sampleIndex,
        TimeSpan offset,
        uint rawFrameNumber,
        bool worldFrameCommitted,
        uint renderFrameNumber)
    {
        return new SampleInputData(sampleIndex, offset, rawFrameNumber, worldFrameCommitted, renderFrameNumber);
    }

    private sealed record SampleInputData(
        int SampleIndex,
        TimeSpan Offset,
        uint RawFrameNumber,
        bool WorldFrameCommitted,
        uint RenderFrameNumber);

    private sealed record SessionDirectory(string SessionFolder, string SessionFolderPath);

    private sealed record TestSession(string DiagnosticsPath, string MetadataPath);
}
