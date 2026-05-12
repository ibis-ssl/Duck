using System.Reflection;
using System.Text.Json;
using Google.Protobuf;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TRACKER-047 の diagnostics / replay / playback 統合 contract を production 実装前に固定する。
/// </summary>
public class TrackerReplayIntegrationTddTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public TrackerReplayIntegrationTddTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// 何を確認しているか: metadata の relative path から session folder 内 tracker snapshot sidecar を解決し、統合 replay 入力として読めることを確認する。
    /// </summary>
    [Fact]
    public void ReadSession_ResolvesMetadataRelativeSnapshotSidecarForReplayInput()
    {
        var session = CreateSession(
            [
                SnapshotInput("external-a", "thirdparty-a", "external", 7102, 12_102_000_000),
            ]);
        var reader = CreateReplayReader();

        var replaySession = InvokeReadSession(reader, session.MetadataPath);
        var inputs = GetEnumerableProperty(replaySession, "SnapshotInputs").ToArray();

        Assert.Single(inputs);
        Assert.Equal(
            Path.GetFullPath(session.SidecarPath),
            Path.GetFullPath(GetStringProperty(replaySession, "TrackerSnapshotSidecarPath")));
        Assert.Equal("external", GetStringProperty(inputs[0], "SourceRole"));
        Assert.Equal(12_102_000_000, GetLongProperty(inputs[0], "TrackedFrameTimestampNs"));
    }

    /// <summary>
    /// 何を確認しているか: own / external / unknown tracker source の snapshot を timestamp 順の replay/playback 入力として扱えることを確認する。
    /// </summary>
    [Fact]
    public void ReadSession_OrdersOwnExternalAndUnknownSnapshotInputsByTrackedTimestamp()
    {
        var session = CreateSession(
            [
                SnapshotInput("ibis-runtime", "ibis", "own", 7105, 12_105_000_000),
                SnapshotInput("external-b", "thirdparty-b", "external", 7101, 12_101_000_000),
                SnapshotInput("", "", "unknown", 7103, 12_103_000_000),
            ]);
        var reader = CreateReplayReader();

        var replaySession = InvokeReadSession(reader, session.MetadataPath);
        var inputs = GetEnumerableProperty(replaySession, "SnapshotInputs").ToArray();

        Assert.Equal(["external", "unknown", "own"], inputs.Select(input => GetStringProperty(input, "SourceRole")).ToArray());
        Assert.Equal([12_101_000_000, 12_103_000_000, 12_105_000_000], inputs.Select(input => GetLongProperty(input, "TrackedFrameTimestampNs")).ToArray());
    }

    /// <summary>
    /// 何を確認しているか: 表示用 snapshot と比較用元データを別 contract として保持し、raw payload 復元状態と semantic summary を replay 入力から確認できることを確認する。
    /// </summary>
    [Fact]
    public void ReadSession_SeparatesDisplaySnapshotFromComparisonSourceData()
    {
        var session = CreateSession(
            [
                SnapshotInput("external-c", "thirdparty-c", "external", 7110, 12_110_000_000),
            ]);
        var reader = CreateReplayReader();

        var replaySession = InvokeReadSession(reader, session.MetadataPath);
        var input = Assert.Single(GetEnumerableProperty(replaySession, "SnapshotInputs"));
        var displaySnapshot = GetRequiredPropertyValue(input, "DisplaySnapshot");
        var comparisonSource = GetRequiredPropertyValue(input, "ComparisonSource");
        var semanticSummary = GetRequiredPropertyValue(comparisonSource, "SemanticSummary");

        Assert.NotSame(displaySnapshot.GetType(), comparisonSource.GetType());
        Assert.Contains("source=thirdparty-c", GetStringProperty(displaySnapshot, "Summary"));
        Assert.True(GetBoolProperty(comparisonSource, "RawPayloadRestored"));
        Assert.Equal(2, GetIntProperty(semanticSummary, "BallCount"));
        Assert.Equal(2, GetIntProperty(semanticSummary, "RobotCount"));
    }

    /// <summary>
    /// 何を確認しているか: ibis 詳細ログと tracker packet snapshot の重複保持を前提に、同時刻近傍で比較できる summary を取得できることを確認する。
    /// </summary>
    [Fact]
    public void ReadSession_BuildsNearestTimestampSummaryBetweenIbisDiagnosticsAndTrackerSnapshots()
    {
        var session = CreateSession(
            [
                SnapshotInput("external-d", "thirdparty-d", "external", 7120, 12_199_000_000),
                SnapshotInput("ibis-runtime", "ibis", "own", 7121, 12_201_000_000),
            ],
            diagnosticsTimestamp: new DateTimeOffset(2026, 5, 12, 12, 0, 12, 200, TimeSpan.Zero),
            diagnosticsTrackedFrame: 7121);
        var reader = CreateReplayReader();

        var replaySession = InvokeReadSession(reader, session.MetadataPath);
        var summaries = GetEnumerableProperty(replaySession, "ComparisonSummaries").ToArray();

        var summary = Assert.Single(summaries);
        Assert.Equal("nearest-timestamp", GetStringProperty(summary, "MatchingRule"));
        Assert.Equal(12_201_000_000, GetLongProperty(summary, "IbisDiagnosticsTimestampNs"));
        Assert.Equal("external", GetStringProperty(summary, "NearestSnapshotSourceRole"));
        Assert.Equal("thirdparty-d", GetStringProperty(summary, "NearestSnapshotSourceLabel"));
        Assert.Equal(12_199_000_000, GetLongProperty(summary, "NearestSnapshotTimestampNs"));
        Assert.True(GetBoolProperty(summary, "NearestSnapshotRawPayloadRestored"));
        Assert.Equal(2, GetIntProperty(summary, "NearestSnapshotBallCount"));
        Assert.Equal(2, GetIntProperty(summary, "NearestSnapshotRobotCount"));
    }

    /// <summary>
    /// 何を確認しているか: diagnostics log 行頭の receivedAt ではなく、ibis own snapshot の data timestamp を基準に nearest summary を作ることを確認する。
    /// </summary>
    [Fact]
    public void ReadSession_UsesIbisDataTimestampInsteadOfDiagnosticsReceivedAtForNearestSummary()
    {
        var session = CreateSession(
            [
                SnapshotInput("external-clock", "received-clock", "external", 7201, 12_200_000_000),
                SnapshotInput("ibis-runtime", "ibis", "own", 7202, 99_000_000_000),
                SnapshotInput("external-data", "data-nearest", "external", 7203, 99_001_000_000),
            ],
            diagnosticsTimestamp: new DateTimeOffset(2026, 5, 12, 12, 0, 12, 200, TimeSpan.Zero),
            diagnosticsTrackedFrame: 7202);
        var reader = CreateReplayReader();

        var replaySession = InvokeReadSession(reader, session.MetadataPath);
        var summaries = GetEnumerableProperty(replaySession, "ComparisonSummaries").ToArray();

        var summary = Assert.Single(summaries);
        Assert.Equal(99_000_000_000, GetLongProperty(summary, "IbisDiagnosticsTimestampNs"));
        Assert.Equal("external", GetStringProperty(summary, "NearestSnapshotSourceRole"));
        Assert.Equal("data-nearest", GetStringProperty(summary, "NearestSnapshotSourceLabel"));
        Assert.Equal(99_001_000_000, GetLongProperty(summary, "NearestSnapshotTimestampNs"));
    }

    private object CreateReplayReader()
    {
        var readerType = GetRequiredServerType("Tracker.Server.Tracking.TrackerSnapshotReplayReader");
        return Activator.CreateInstance(readerType)
            ?? throw new InvalidOperationException("TrackerSnapshotReplayReader must have a public parameterless constructor.");
    }

    private static object InvokeReadSession(object reader, string metadataPath)
    {
        var method = reader.GetType().GetMethod("ReadSession", BindingFlags.Public | BindingFlags.Instance, [typeof(string)]);
        Assert.NotNull(method);
        return method!.Invoke(reader, [metadataPath])
            ?? throw new InvalidOperationException("ReadSession must return a replay session object.");
    }

    private TestSession CreateSession(
        IReadOnlyList<SnapshotInputData> snapshotInputs,
        DateTimeOffset? diagnosticsTimestamp = null,
        uint? diagnosticsTrackedFrame = null)
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-047-replay-{Guid.NewGuid():N}");
        var sessionFolder = "test-session";
        var sessionFolderPath = Path.Combine(captureDirectory, sessionFolder);
        Directory.CreateDirectory(sessionFolderPath);

        var sidecarPath = Path.Combine(sessionFolderPath, TrackerPacketSnapshotLogReader.SidecarFileName);
        var lines = snapshotInputs.Select(input =>
        {
            var packet = CreatePacket(input);
            return JsonSerializer.Serialize(TrackerPacketSnapshotRecord.FromPacket(
                packet,
                new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero).AddTicks(input.TimestampNs / 100),
                remoteEndpoint: input.Role == "own" ? "self" : $"192.0.2.{input.FrameNumber % 100}:12010",
                sourceRole: input.Role,
                sourceLabel: string.IsNullOrWhiteSpace(input.SourceName) ? input.Role : input.SourceName));
        });
        File.WriteAllLines(sidecarPath, lines);

        var diagnosticsPath = Path.Combine(sessionFolderPath, "test-session.tracker-diagnostics.log");
        var timestamp = diagnosticsTimestamp ?? new DateTimeOffset(2026, 5, 12, 12, 0, 12, 102, TimeSpan.Zero);
        File.WriteAllText(
            diagnosticsPath,
            $"{timestamp:O} Tracker diagnostics profile=sim rawFrame=7001 rawCamera=0 rawBalls=2 rawBallDetails=[x=100,y=200,z=0,c=1] rawBlue=[] rawYellow=[] trackedFrame={diagnosticsTrackedFrame ?? 900} trackedBalls=2 trackedBallDetails=[#1:x=100,y=200,z=0,vis=1,q=1,cams=0] trackedRobots=2 trackedRobotDetails=[Y3:x=1200,y=-300,o=0,w=0,vis=1,q=1] ballOutVisibility=0 ballHalfLifeSec=1 ballLifetimeNs=1000000000{Environment.NewLine}");

        var metadataPath = Path.Combine(sessionFolderPath, "test-session.metadata.json");
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
                    IsCreated = true,
                    RecordCount = snapshotInputs.Count,
                    SkippedRecordCount = 0,
                    ErrorCount = 0,
                },
            }));

        return new TestSession(metadataPath, sidecarPath);
    }

    private TrackerWrapperPacket CreatePacket(SnapshotInputData input)
    {
        var frame = fixture.CreateFrame(
            frameNumber: input.FrameNumber,
            dataTimestampNs: input.TimestampNs,
            balls:
            [
                fixture.CreateTrackedBall(trackId: 10, xMm: 100, yMm: 200),
                fixture.CreateTrackedBall(trackId: 20, xMm: 300, yMm: 400),
            ],
            robots:
            [
                new TrackedRobotState { Team = TrackerTeam.Yellow, RobotId = 3, XMm = 1200, YMm = -300 },
                new TrackedRobotState { Team = TrackerTeam.Blue, RobotId = 7, XMm = -500, YMm = 900 },
            ],
            primaryBallTrackId: 10);
        return fixture.CreatePacketGenerator(input.SourceName, input.SourceUuid).Generate(frame);
    }

    private static SnapshotInputData SnapshotInput(
        string sourceUuid,
        string sourceName,
        string role,
        uint frameNumber,
        long timestampNs)
    {
        return new SnapshotInputData(sourceUuid, sourceName, role, frameNumber, timestampNs);
    }

    private static IEnumerable<object> GetEnumerableProperty(object target, string propertyName)
    {
        var value = GetRequiredPropertyValue(target, propertyName);
        Assert.IsAssignableFrom<System.Collections.IEnumerable>(value);
        return ((System.Collections.IEnumerable)value).Cast<object>();
    }

    private static object GetRequiredPropertyValue(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return property!.GetValue(target)
            ?? throw new InvalidOperationException($"{propertyName} must not be null.");
    }

    private static string GetStringProperty(object target, string propertyName)
    {
        return Assert.IsType<string>(GetRequiredPropertyValue(target, propertyName));
    }

    private static int GetIntProperty(object target, string propertyName)
    {
        return Assert.IsType<int>(GetRequiredPropertyValue(target, propertyName));
    }

    private static long GetLongProperty(object target, string propertyName)
    {
        return Assert.IsType<long>(GetRequiredPropertyValue(target, propertyName));
    }

    private static bool GetBoolProperty(object target, string propertyName)
    {
        return Assert.IsType<bool>(GetRequiredPropertyValue(target, propertyName));
    }

    private static Type GetRequiredServerType(string fullName)
    {
        var type = typeof(TrackerDiagnosticsLogReader).Assembly.GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type!;
    }

    private sealed record TestSession(string MetadataPath, string SidecarPath);

    private sealed record SnapshotInputData(
        string SourceUuid,
        string SourceName,
        string Role,
        uint FrameNumber,
        long TimestampNs);
}
