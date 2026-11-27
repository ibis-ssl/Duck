using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>
    /// 外部比較データが不完全でも、正常な sample の再生と既定2表示元を維持する。
    /// </summary>
    [Theory]
    [InlineData("payload-missing")]
    [InlineData("summary-incomplete")]
    [InlineData("snapshot-path-invalid")]
    [InlineData("alignment-path-invalid")]
    [InlineData("alignment-index-missing")]
    [InlineData("alignment-index-missing-record")]
    public void Load_WithIncompleteExternalData_PreservesDiagnosticsSamples(string incompleteCase)
    {
        var session = CreateDiagnosticsSampleSession(
            [SampleInput(0, TimeSpan.Zero, rawFrameNumber: 2000, worldFrameCommitted: true, renderFrameNumber: 1000)]);
        var metadata = JsonNode.Parse(File.ReadAllText(session.MetadataPath))!.AsObject();
        var snapshotPath = AddTrackerSnapshot(session, metadata, "external");
        var snapshot = JsonNode.Parse(File.ReadAllText(snapshotPath))!.AsObject();

        switch (incompleteCase)
        {
            case "payload-missing":
                snapshot.Remove("payloadBase64");
                snapshot.Remove("semanticSummary");
                File.WriteAllText(snapshotPath, snapshot.ToJsonString());
                break;
            case "summary-incomplete":
                snapshot["semanticSummary"]!.AsObject().Remove("balls");
                File.WriteAllText(snapshotPath, snapshot.ToJsonString());
                break;
            case "snapshot-path-invalid":
                metadata["TrackerSnapshotSidecarPath"] = "invalid\0path";
                break;
            default:
                var alignmentPath = Path.Combine(Path.GetDirectoryName(session.MetadataPath)!, "tracker-snapshot-alignment.jsonl");
                metadata["TrackerSnapshotAlignmentPath"] = incompleteCase == "alignment-path-invalid" ? "invalid\0path" : alignmentPath;
                metadata["TrackerSnapshotAlignmentLog"] = JsonSerializer.SerializeToNode(new { IsCreated = true, RecordCount = 1 });
                File.WriteAllText(alignmentPath, JsonSerializer.Serialize(new
                {
                    schemaVersion = 2,
                    replayTimelineIndex = 0,
                    replayTimelineReceivedAt = BaseTime,
                    sourceRole = "external",
                    sourceLabel = "external",
                    trackerSnapshotRecordIndex = incompleteCase == "alignment-index-missing" ? (int?)null : 4,
                    status = "ready",
                }));
                break;
        }
        File.WriteAllText(session.MetadataPath, metadata.ToJsonString());
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var state = reader.Load(session.DiagnosticsPath, selectedEntry: null, TrackerDiagnosticsComparisonSourceFilter.External);
        Assert.Equal(TrackerDiagnosticsComparisonSidecarStatus.Ready, state.SidecarStatus);
        Assert.Contains("external tracker comparison is unavailable", state.Error, StringComparison.OrdinalIgnoreCase);
        var tick = TrackerDiagnosticsReplayTimelineSelection.FromTick(Assert.Single(state.ReplayTimeline));
        foreach (var source in new[] { TrackerDiagnosticsFieldSource.VisionInput, TrackerDiagnosticsFieldSource.IbisTracker })
        {
            var frame = reader.LoadFieldSourceFrame(session.DiagnosticsPath, selectedEntry: null, tick, source);
            Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, frame.Status);
            Assert.Equal("diagnostics-sample-sidecar", frame.MatchingRule);
            Assert.Equal(BaseTime, frame.SourceSnapshotReceivedAt);
        }
        var external = reader.LoadFieldSourceFrame(session.DiagnosticsPath, selectedEntry: null, tick, TrackerDiagnosticsFieldSource.External);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable, external.Status);
    }

    /// <summary>
    /// 外部記録の必須時刻・変換境界・読取失敗を選択 tick の比較結果と Field の両方で確認する。
    /// </summary>
    [Theory]
    [InlineData("snapshot-time-missing", false)]
    [InlineData("snapshot-delta-overflow", false)]
    [InlineData("snapshot-delta-boundary", true)]
    [InlineData("alignment-source-time-missing", false)]
    [InlineData("alignment-delta-missing", false)]
    [InlineData("alignment-delta-overflow", false)]
    [InlineData("alignment-delta-negative", false)]
    [InlineData("alignment-delta-boundary", true)]
    [InlineData("snapshot-read-denied", false)]
    [InlineData("alignment-read-denied", false)]
    public void Load_WithExternalTimeOrReadBoundary_PreservesDiagnosticsSamples(string boundaryCase, bool externalReady)
    {
        var session = CreateDiagnosticsSampleSession(
            [SampleInput(0, TimeSpan.Zero, rawFrameNumber: 2000, worldFrameCommitted: true, renderFrameNumber: 1000)]);
        var metadata = JsonNode.Parse(File.ReadAllText(session.MetadataPath))!.AsObject();
        var snapshotPath = AddTrackerSnapshot(session, metadata, "external");
        var snapshot = JsonNode.Parse(File.ReadAllText(snapshotPath))!.AsObject();
        const long maxDeltaTicks = long.MaxValue / 100;

        if (boundaryCase == "snapshot-time-missing")
        {
            snapshot.Remove("receivedAt");
        }
        else if (boundaryCase is "snapshot-delta-overflow" or "snapshot-delta-boundary")
        {
            snapshot["receivedAt"] = JsonSerializer.SerializeToNode(
                BaseTime.AddTicks(-maxDeltaTicks - (externalReady ? 0 : 1)));
        }
        File.WriteAllText(snapshotPath, snapshot.ToJsonString());

        if (boundaryCase.StartsWith("alignment-", StringComparison.Ordinal))
        {
            var alignmentPath = Path.Combine(Path.GetDirectoryName(session.MetadataPath)!, "tracker-snapshot-alignment.jsonl");
            long? delta = boundaryCase switch
            {
                "alignment-delta-missing" => null,
                "alignment-delta-overflow" => maxDeltaTicks + 1,
                "alignment-delta-negative" => -1,
                "alignment-delta-boundary" => maxDeltaTicks,
                _ => 0,
            };
            File.WriteAllText(alignmentPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                replayTimelineIndex = 0,
                replayTimelineReceivedAt = BaseTime,
                sourceRole = "external",
                sourceLabel = "external",
                trackerSnapshotRecordIndex = 0,
                trackerSnapshotReceivedAt = boundaryCase == "alignment-source-time-missing" ? (DateTimeOffset?)null : BaseTime,
                receivedAtDeltaTicks = delta,
                status = "ready",
            }));
            metadata["TrackerSnapshotAlignmentPath"] = alignmentPath;
            metadata["TrackerSnapshotAlignmentLog"] = JsonSerializer.SerializeToNode(new { IsCreated = true, RecordCount = 1 });
        }
        File.WriteAllText(session.MetadataPath, metadata.ToJsonString());

        // 実ファイルの権限を変更せず、既存 reader 注入点で日常的なアクセス拒否を再現する。
        var reader = boundaryCase switch
        {
            "snapshot-read-denied" => new TrackerDiagnosticsComparisonViewStateReader(
                _ => throw new UnauthorizedAccessException("snapshot read denied")),
            "alignment-read-denied" => new TrackerDiagnosticsComparisonViewStateReader(
                path => TrackerPacketSnapshotLogReader.ReadRecords(path).ToArray(),
                _ => throw new UnauthorizedAccessException("alignment read denied")),
            _ => new TrackerDiagnosticsComparisonViewStateReader(),
        };
        var selected = new TrackerDiagnosticsReplayTimelineSelection(0, null, BaseTime);
        var state = reader.Load(session.DiagnosticsPath, selectedEntry: null, selected, TrackerDiagnosticsComparisonSourceFilter.External);
        Assert.Equal(TrackerDiagnosticsComparisonSidecarStatus.Ready, state.SidecarStatus);
        Assert.Equal(BaseTime, Assert.Single(state.ReplayTimeline).ReceivedAt);
        foreach (var source in new[] { TrackerDiagnosticsFieldSource.VisionInput, TrackerDiagnosticsFieldSource.IbisTracker })
        {
            var frame = reader.LoadFieldSourceFrame(session.DiagnosticsPath, selectedEntry: null, selected, source);
            Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, frame.Status);
            Assert.Equal(BaseTime, frame.SourceSnapshotReceivedAt);
        }
        var external = reader.LoadFieldSourceFrame(session.DiagnosticsPath, selectedEntry: null, selected, TrackerDiagnosticsFieldSource.External);
        if (externalReady)
        {
            Assert.Null(state.Error);
            Assert.Equal(TrackerDiagnosticsComparisonEntryStatus.Ready, state.SelectedEntryComparison!.Status);
            Assert.Equal(maxDeltaTicks * 100, state.SelectedEntryComparison.TimestampDeltaNs);
            Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, external.Status);
            Assert.Equal(maxDeltaTicks * 100, external.TimestampDeltaNs);
        }
        else
        {
            Assert.Contains("external tracker comparison is unavailable", state.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Null(state.SelectedEntryComparison);
            Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable, external.Status);
        }
    }

    /// <summary>
    /// 新規 sample の欠落を旧 own snapshot によって正常と誤認させない。
    /// </summary>
    [Theory]
    [InlineData("not-created", TrackerDiagnosticsComparisonSidecarStatus.SidecarNotCreated)]
    [InlineData("path-missing", TrackerDiagnosticsComparisonSidecarStatus.SidecarPathMissing)]
    [InlineData("file-missing", TrackerDiagnosticsComparisonSidecarStatus.SidecarMissing)]
    [InlineData("empty", TrackerDiagnosticsComparisonSidecarStatus.SidecarEmpty)]
    [InlineData("corrupt", TrackerDiagnosticsComparisonSidecarStatus.SidecarCorrupt)]
    [InlineData("path-only-missing", TrackerDiagnosticsComparisonSidecarStatus.SidecarMissing)]
    public void Load_WithUnavailableDeclaredSample_DoesNotFallbackToOwnSnapshot(
        string unavailableCase, TrackerDiagnosticsComparisonSidecarStatus expectedStatus)
    {
        var session = CreateDiagnosticsSampleSession(
            [SampleInput(0, TimeSpan.Zero, rawFrameNumber: 2000, worldFrameCommitted: true, renderFrameNumber: 1000)]);
        var metadata = JsonNode.Parse(File.ReadAllText(session.MetadataPath))!.AsObject();
        AddTrackerSnapshot(session, metadata, "own");
        var samplePath = Path.Combine(Path.GetDirectoryName(session.MetadataPath)!, "diagnostics-samples.jsonl");
        switch (unavailableCase)
        {
            case "not-created":
                metadata["DiagnosticsSampleLog"]!["IsCreated"] = false;
                File.Delete(samplePath);
                break;
            case "path-missing":
                metadata.Remove("DiagnosticsSampleSidecarPath");
                break;
            case "path-only-missing":
                metadata.Remove("DiagnosticsSampleLog");
                File.Delete(samplePath);
                break;
            case "file-missing":
                File.Delete(samplePath);
                break;
            case "empty":
                File.WriteAllText(samplePath, "");
                break;
            case "corrupt":
                File.WriteAllText(samplePath, "{not-json");
                break;
        }
        File.WriteAllText(session.MetadataPath, metadata.ToJsonString());
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var selectedEntry = new TrackerDiagnosticsComparisonSelectedEntry(1, "1000");
        var state = reader.Load(session.DiagnosticsPath, selectedEntry, TrackerDiagnosticsComparisonSourceFilter.All);
        Assert.Equal(expectedStatus, state.SidecarStatus);
        Assert.Empty(state.ReplayTimeline);
        var frame = reader.LoadFieldSourceFrame(session.DiagnosticsPath, selectedEntry, TrackerDiagnosticsFieldSource.IbisTracker);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable, frame.Status);
    }

    private static string AddTrackerSnapshot(TestSession session, JsonObject metadata, string role)
    {
        var snapshotPath = Path.Combine(Path.GetDirectoryName(session.MetadataPath)!, "tracker-packet-snapshots.jsonl");
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            receivedAt = BaseTime,
            sourceRole = role,
            sourceLabel = role,
            trackedFrameNumber = 1000,
            trackedFrameTimestampNs = 1000000L,
            payloadBase64 = "",
            semanticSummary = CreateSemanticSummary(role, role, 1000, 1000000L, 200, 300),
        }));
        metadata["TrackerSnapshotSidecarPath"] = snapshotPath;
        metadata["TrackerSnapshotLog"] = JsonSerializer.SerializeToNode(new { IsCreated = true, RecordCount = 1 });
        return snapshotPath;
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
