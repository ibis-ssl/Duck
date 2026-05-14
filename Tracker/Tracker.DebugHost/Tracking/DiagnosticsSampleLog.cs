using System.Text.Json;
using Tracker.Core;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// diagnostics sample sidecar JSONL に保存する 1 render tick の固定 snapshot。
/// </summary>
public sealed record DiagnosticsSampleRecord(
    int SchemaVersion,
    int SampleIndex,
    DateTimeOffset SampleReceivedAt,
    string SampleKind,
    uint? RawFrameNumber,
    uint? RawCameraId,
    bool WorldFrameCommitted,
    uint? RenderFrameNumber,
    long? RenderTickId = null,
    DateTimeOffset? RawReceivedAt = null,
    DateTimeOffset? TrackedReceivedAt = null,
    uint? TrackedFrameNumber = null,
    long? TrackedFrameTimestampNs = null,
    TrackerPacketSnapshotSemanticSummary? RawSemanticSummary = null,
    TrackerPacketSnapshotSemanticSummary? TrackedSemanticSummary = null)
{
    /// <summary>
    /// diagnostics sample sidecar schema version。
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// diagnostics sample timeline tick の種別名。
    /// </summary>
    public const string DiagnosticsSampleKind = "diagnostics-sample";

    /// <summary>
    /// R006 の fixed live display snapshot から sidecar record を作る。
    /// </summary>
    public static DiagnosticsSampleRecord FromRenderSnapshot(
        int sampleIndex,
        VisionLiveDisplayRenderSnapshot snapshot)
    {
        var rawFrameNumber = snapshot.RawSnapshot.Detection?.FrameNumber;
        var rawCameraId = snapshot.RawSnapshot.Detection?.CameraId;
        var trackedFrame = snapshot.TrackedSnapshot.LatestFrame;
        var trackedReceivedAt = snapshot.TrackedSnapshot.ReceivedAt?.ToUniversalTime();
        return new DiagnosticsSampleRecord(
            SchemaVersion: CurrentSchemaVersion,
            SampleIndex: sampleIndex,
            SampleReceivedAt: snapshot.SampledAt.ToUniversalTime(),
            SampleKind: DiagnosticsSampleKind,
            RawFrameNumber: rawFrameNumber,
            RawCameraId: rawCameraId,
            WorldFrameCommitted: trackedFrame is not null,
            RenderFrameNumber: trackedFrame?.FrameNumber,
            RenderTickId: snapshot.RenderTickId,
            RawReceivedAt: snapshot.RawSnapshot.ReceivedAt?.ToUniversalTime(),
            TrackedReceivedAt: trackedReceivedAt,
            TrackedFrameNumber: trackedFrame?.FrameNumber,
            TrackedFrameTimestampNs: trackedFrame?.DataTimestampNs,
            RawSemanticSummary: CreateRawSemanticSummary(snapshot.RawSnapshot),
            TrackedSemanticSummary: trackedFrame is null ? null : CreateTrackedSemanticSummary(trackedFrame));
    }

    private static TrackerPacketSnapshotSemanticSummary CreateRawSemanticSummary(VisionPacketSnapshot snapshot)
    {
        var balls = snapshot.AggregateDetection.Balls
            .Select((ball, index) => new TrackerPacketSnapshotBallSummary(
                Index: index,
                XMm: ball.X,
                YMm: ball.Y,
                ZMm: ball.Z,
                Visibility: ball.Confidence))
            .ToArray();
        var yellowRobots = snapshot.AggregateDetection.RobotsYellow
            .Select(robot => CreateRobotSummary("Yellow", robot));
        var blueRobots = snapshot.AggregateDetection.RobotsBlue
            .Select(robot => CreateRobotSummary("Blue", robot));
        var robots = yellowRobots.Concat(blueRobots).ToArray();

        return new TrackerPacketSnapshotSemanticSummary(
            BallCount: balls.Length,
            RobotCount: robots.Length,
            TrackedFrameNumber: snapshot.Detection?.FrameNumber ?? 0,
            TrackedFrameTimestampNs: 0,
            SourceUuid: string.Empty,
            SourceName: "Vision Input",
            SourceRole: "vision-input",
            SourceLabel: "Vision Input",
            Balls: balls,
            Robots: robots);
    }

    private static TrackerPacketSnapshotSemanticSummary CreateTrackedSemanticSummary(TrackerFrame frame)
    {
        var balls = frame.Balls
            .Select((ball, index) => new TrackerPacketSnapshotBallSummary(
                Index: index,
                XMm: ball.XMm,
                YMm: ball.YMm,
                ZMm: ball.ZMm,
                Visibility: (float)ball.Visibility))
            .ToArray();
        var robots = frame.Robots
            .Select(robot => new TrackerPacketSnapshotRobotSummary(
                Team: robot.Team.ToString(),
                RobotId: robot.RobotId,
                XMm: robot.XMm,
                YMm: robot.YMm,
                OrientationRad: (float)robot.OrientationRad,
                Visibility: (float)robot.Visibility))
            .ToArray();

        return new TrackerPacketSnapshotSemanticSummary(
            BallCount: balls.Length,
            RobotCount: robots.Length,
            TrackedFrameNumber: frame.FrameNumber,
            TrackedFrameTimestampNs: frame.DataTimestampNs,
            SourceUuid: string.Empty,
            SourceName: "ibis tracker",
            SourceRole: "own",
            SourceLabel: "ibis tracker",
            Balls: balls,
            Robots: robots);
    }

    private static TrackerPacketSnapshotRobotSummary CreateRobotSummary(
        string team,
        SSL_DetectionRobot robot)
    {
        return new TrackerPacketSnapshotRobotSummary(
            Team: team,
            RobotId: robot.RobotId,
            XMm: robot.X,
            YMm: robot.Y,
            OrientationRad: robot.Orientation,
            Visibility: robot.Confidence);
    }
}

/// <summary>
/// diagnostics sample sidecar の record 件数と失敗件数。
/// </summary>
public sealed record DiagnosticsSampleLogMetadataSnapshot(
    int RecordCount,
    int SkippedRecordCount,
    int ErrorCount);

/// <summary>
/// diagnostics sample sidecar JSONL reader。
/// </summary>
public static class DiagnosticsSampleLogReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// diagnostics sample sidecar JSONL を schema validation 付きで読み取る。
    /// </summary>
    public static IReadOnlyList<DiagnosticsSampleRecord> ReadRecords(string path)
    {
        var records = new List<DiagnosticsSampleRecord>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<DiagnosticsSampleRecord>(line, JsonOptions)
                ?? throw new InvalidDataException("Diagnostics sample record is empty.");
            if (record.SchemaVersion != DiagnosticsSampleRecord.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported diagnostics sample schema version '{record.SchemaVersion}'.");
            }

            records.Add(record with
            {
                SampleReceivedAt = record.SampleReceivedAt.ToUniversalTime(),
                SampleKind = string.IsNullOrWhiteSpace(record.SampleKind)
                    ? DiagnosticsSampleRecord.DiagnosticsSampleKind
                    : record.SampleKind,
                RawReceivedAt = record.RawReceivedAt?.ToUniversalTime(),
                TrackedReceivedAt = record.TrackedReceivedAt?.ToUniversalTime(),
            });
        }

        return records;
    }
}

/// <summary>
/// CaptureOn session folder の diagnostics sample sidecar JSONL へ diagnostics sample tick を追記する。
/// </summary>
public sealed class DiagnosticsSampleLogWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object gate = new();
    private readonly VisionPacketCaptureSession session;
    private readonly ILogger<DiagnosticsSampleLogWriter> logger;
    private StreamWriter? writer;
    private string? capturePath;
    private bool writeFailed;
    private int recordCount;
    private int skippedRecordCount;
    private int errorCount;

    /// <summary>
    /// capture session と logger を受け取り、必要になるまで file writer を遅延初期化する。
    /// </summary>
    public DiagnosticsSampleLogWriter(
        VisionPacketCaptureSession session,
        ILogger<DiagnosticsSampleLogWriter> logger)
    {
        this.session = session;
        this.logger = logger;
    }

    /// <summary>
    /// 現在書き込み中の diagnostics sample sidecar path。未開始または停止後は null。
    /// </summary>
    public string? CapturePath
    {
        get
        {
            lock (gate)
            {
                return capturePath;
            }
        }
    }

    /// <summary>
    /// sidecar に書き込めた sample record 数。
    /// </summary>
    public int RecordCount
    {
        get
        {
            lock (gate)
            {
                return recordCount;
            }
        }
    }

    /// <summary>
    /// fixed read-side snapshot を diagnostics sample sidecar に 1 record 追記する。
    /// </summary>
    public void CaptureSample(VisionLiveDisplayRenderSnapshot snapshot)
    {
        if (!session.Enabled)
        {
            Stop();
            return;
        }

        if (writeFailed)
        {
            return;
        }

        lock (gate)
        {
            try
            {
                EnsureWriter(snapshot.SampledAt);
                var record = DiagnosticsSampleRecord.FromRenderSnapshot(recordCount, snapshot);
                writer!.WriteLine(JsonSerializer.Serialize(record, JsonOptions));
                recordCount++;
                UpdateMetadata();
                if (session.FlushEachPacket)
                {
                    writer.Flush();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                writeFailed = true;
                skippedRecordCount++;
                errorCount++;
                UpdateMetadata();
                logger.LogWarning(ex, "Failed to write diagnostics sample sidecar {CapturePath}", capturePath);
            }
        }
    }

    /// <summary>
    /// writer buffer と metadata を flush する。
    /// </summary>
    public void Flush()
    {
        lock (gate)
        {
            writer?.Flush();
            UpdateMetadata();
        }
    }

    /// <summary>
    /// writer を停止して保持中の file handle を解放する。
    /// </summary>
    public void Dispose()
    {
        Stop();
    }

    /// <summary>
    /// diagnostics sample writer を停止し、次回 capture 時に新しい file を開始できる状態へ戻す。
    /// </summary>
    public void Stop()
    {
        lock (gate)
        {
            writer?.Dispose();
            writer = null;
            capturePath = null;
            writeFailed = false;
            recordCount = 0;
            skippedRecordCount = 0;
            errorCount = 0;
        }
    }

    private void EnsureWriter(DateTimeOffset receivedAt)
    {
        if (writer is not null)
        {
            return;
        }

        var sessionState = session.EnsureStarted(receivedAt)
            ?? throw new InvalidOperationException("Diagnostics sample capture session is disabled.");
        capturePath = sessionState.DiagnosticsSampleSidecarPath;
        var directory = Path.GetDirectoryName(capturePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        writer = new StreamWriter(new FileStream(capturePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        logger.LogInformation("Writing diagnostics samples to {CapturePath}", capturePath);
    }

    private void UpdateMetadata()
    {
        session.UpdateDiagnosticsSampleLogMetadata(new DiagnosticsSampleLogMetadataSnapshot(
            RecordCount: recordCount,
            SkippedRecordCount: skippedRecordCount,
            ErrorCount: errorCount));
    }
}
