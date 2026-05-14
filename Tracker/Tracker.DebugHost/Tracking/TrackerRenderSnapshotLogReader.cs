using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Tracker.Core;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// diagnostics log に対応する render snapshot sidecar を読み、tracked frame 単位の表示 snapshot を返す reader。
/// </summary>
public sealed class TrackerRenderSnapshotLogReader
{
    private const int SchemaVersion = 1;
    private const string DiagnosticsLogSuffix = ".tracker-diagnostics.log";
    private const string RenderSnapshotSuffix = ".render-snapshots.jsonl.gz";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly TrackerDiagnosticsLogReader diagnosticsLogReader;
    private readonly object gate = new();
    private TrackerRenderSnapshotLogIndex? cachedIndex;

    /// <summary>
    /// diagnostics log reader を使って読み取り可能な log path を検証する reader を作成する。
    /// </summary>
    public TrackerRenderSnapshotLogReader(TrackerDiagnosticsLogReader diagnosticsLogReader)
    {
        this.diagnosticsLogReader = diagnosticsLogReader;
    }

    /// <summary>
    /// diagnostics log path と tracked frame 番号文字列から対応する render snapshot を読み取る。
    /// </summary>
    public TrackerRenderSnapshotLogResult ReadFrame(string diagnosticsLogPath, string trackedFrame)
    {
        if (!uint.TryParse(trackedFrame, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameNumber))
        {
            return new TrackerRenderSnapshotLogResult(null, $"Tracked frame '{trackedFrame}' is not a numeric frame number.");
        }

        var indexResult = ReadIndex(diagnosticsLogPath);
        if (indexResult.Index is null)
        {
            return new TrackerRenderSnapshotLogResult(null, indexResult.Error);
        }

        return indexResult.Index.SnapshotsByFrame.TryGetValue(frameNumber, out var snapshot)
            ? new TrackerRenderSnapshotLogResult(snapshot, Error: null)
            : new TrackerRenderSnapshotLogResult(null, $"Render snapshot for tracked frame '{trackedFrame}' was not found.");
    }

    /// <summary>
    /// diagnostics log に対応する render snapshot sidecar を index 化し、同じ file 状態なら cache を再利用する。
    /// </summary>
    public TrackerRenderSnapshotLogIndexResult ReadIndex(string diagnosticsLogPath)
    {
        var listedLog = diagnosticsLogReader.ListFiles()
            .FirstOrDefault(file => string.Equals(
                Path.GetFullPath(file.FullPath),
                Path.GetFullPath(diagnosticsLogPath),
                StringComparison.Ordinal));
        if (listedLog is null)
        {
            return new TrackerRenderSnapshotLogIndexResult(null, "Diagnostics log is not in the readable log list.");
        }

        var renderSnapshotPath = ResolveRenderSnapshotPath(listedLog.FullPath);
        if (renderSnapshotPath is null || !File.Exists(renderSnapshotPath))
        {
            return new TrackerRenderSnapshotLogIndexResult(null, "Render snapshot file was not found for this diagnostics log.");
        }

        var fullRenderSnapshotPath = Path.GetFullPath(renderSnapshotPath);
        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(fullRenderSnapshotPath);
        var fileLength = new FileInfo(fullRenderSnapshotPath).Length;
        lock (gate)
        {
            if (cachedIndex is not null &&
                string.Equals(cachedIndex.FilePath, fullRenderSnapshotPath, StringComparison.Ordinal) &&
                cachedIndex.LastWriteTimeUtc == lastWriteTimeUtc &&
                cachedIndex.FileLength == fileLength)
            {
                return new TrackerRenderSnapshotLogIndexResult(cachedIndex, Error: null);
            }
        }

        try
        {
            var snapshotsByFrame = new Dictionary<uint, TrackerRenderSnapshotView>();
            foreach (var record in ReadRecords(renderSnapshotPath))
            {
                snapshotsByFrame[record.Frame.FrameNumber] = new TrackerRenderSnapshotView(
                    fullRenderSnapshotPath,
                    record.ReceivedAt,
                    record.Frame);
            }

            var index = new TrackerRenderSnapshotLogIndex(
                fullRenderSnapshotPath,
                lastWriteTimeUtc,
                fileLength,
                snapshotsByFrame);
            lock (gate)
            {
                cachedIndex = index;
            }

            return new TrackerRenderSnapshotLogIndexResult(index, Error: null);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            return new TrackerRenderSnapshotLogIndexResult(null, $"Render snapshot file could not be read: {ex.Message}");
        }
    }

    /// <summary>
    /// render snapshot JSONL gzip file から schema version 検証済み record を順に読み取る。
    /// </summary>
    internal static IEnumerable<TrackerRenderSnapshotRecord> ReadRecords(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<TrackerRenderSnapshotRecord>(line, JsonOptions)
                ?? throw new InvalidDataException("Tracker render snapshot record is empty.");
            if (record.SchemaVersion != SchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported tracker render snapshot schema version '{record.SchemaVersion}'.");
            }

            if (record.Frame is null)
            {
                throw new InvalidDataException("Tracker render snapshot record is missing frame data.");
            }

            yield return record;
        }
    }

    /// <summary>
    /// diagnostics log path から同じ capture session の render snapshot sidecar path を解決する。
    /// </summary>
    internal static string? ResolveRenderSnapshotPath(string diagnosticsLogPath)
    {
        return diagnosticsLogPath.EndsWith(DiagnosticsLogSuffix, StringComparison.Ordinal)
            ? string.Concat(diagnosticsLogPath.AsSpan(0, diagnosticsLogPath.Length - DiagnosticsLogSuffix.Length), RenderSnapshotSuffix)
            : null;
    }
}

/// <summary>
/// tracked frame 単位の render snapshot 読み取り結果。
/// </summary>
/// <param name="Snapshot">読み取れた render snapshot。失敗時または未検出時は null。</param>
/// <param name="Error">読み取り失敗理由。成功時は null。</param>
public sealed record TrackerRenderSnapshotLogResult(
    TrackerRenderSnapshotView? Snapshot,
    string? Error);

/// <summary>
/// render snapshot sidecar の index 読み取り結果。
/// </summary>
/// <param name="Index">読み取れた index。失敗時は null。</param>
/// <param name="Error">読み取り失敗理由。成功時は null。</param>
public sealed record TrackerRenderSnapshotLogIndexResult(
    TrackerRenderSnapshotLogIndex? Index,
    string? Error);

/// <summary>
/// render snapshot sidecar file の状態と frame 番号別 snapshot map。
/// </summary>
/// <param name="FilePath">読み取った render snapshot sidecar の絶対 path。</param>
/// <param name="LastWriteTimeUtc">cache 判定に使う最終更新時刻。</param>
/// <param name="FileLength">cache 判定に使う file size。</param>
/// <param name="SnapshotsByFrame">tracked frame 番号から render snapshot への map。</param>
public sealed record TrackerRenderSnapshotLogIndex(
    string FilePath,
    DateTime LastWriteTimeUtc,
    long FileLength,
    IReadOnlyDictionary<uint, TrackerRenderSnapshotView> SnapshotsByFrame);

/// <summary>
/// UI が表示する render snapshot とその出所 metadata。
/// </summary>
/// <param name="FilePath">snapshot を読み取った sidecar file path。</param>
/// <param name="ReceivedAt">tracked frame を受信した時刻。</param>
/// <param name="Frame">表示対象の tracker frame。</param>
public sealed record TrackerRenderSnapshotView(
    string FilePath,
    DateTimeOffset ReceivedAt,
    TrackerFrame Frame);
