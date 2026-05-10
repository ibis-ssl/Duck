using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Tracker.Core;

namespace Tracker.Server.Tracking;

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

    public TrackerRenderSnapshotLogReader(TrackerDiagnosticsLogReader diagnosticsLogReader)
    {
        this.diagnosticsLogReader = diagnosticsLogReader;
    }

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

    internal static string? ResolveRenderSnapshotPath(string diagnosticsLogPath)
    {
        return diagnosticsLogPath.EndsWith(DiagnosticsLogSuffix, StringComparison.Ordinal)
            ? string.Concat(diagnosticsLogPath.AsSpan(0, diagnosticsLogPath.Length - DiagnosticsLogSuffix.Length), RenderSnapshotSuffix)
            : null;
    }
}

public sealed record TrackerRenderSnapshotLogResult(
    TrackerRenderSnapshotView? Snapshot,
    string? Error);

public sealed record TrackerRenderSnapshotLogIndexResult(
    TrackerRenderSnapshotLogIndex? Index,
    string? Error);

public sealed record TrackerRenderSnapshotLogIndex(
    string FilePath,
    DateTime LastWriteTimeUtc,
    long FileLength,
    IReadOnlyDictionary<uint, TrackerRenderSnapshotView> SnapshotsByFrame);

public sealed record TrackerRenderSnapshotView(
    string FilePath,
    DateTimeOffset ReceivedAt,
    TrackerFrame Frame);
