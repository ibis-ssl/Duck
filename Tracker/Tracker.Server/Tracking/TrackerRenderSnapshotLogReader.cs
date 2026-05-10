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

    public TrackerRenderSnapshotLogReader(TrackerDiagnosticsLogReader diagnosticsLogReader)
    {
        this.diagnosticsLogReader = diagnosticsLogReader;
    }

    public TrackerRenderSnapshotLogResult ReadFrame(string diagnosticsLogPath, string trackedFrame)
    {
        var listedLog = diagnosticsLogReader.ListFiles()
            .FirstOrDefault(file => string.Equals(
                Path.GetFullPath(file.FullPath),
                Path.GetFullPath(diagnosticsLogPath),
                StringComparison.Ordinal));
        if (listedLog is null)
        {
            return new TrackerRenderSnapshotLogResult(null, "Diagnostics log is not in the readable log list.");
        }

        var renderSnapshotPath = ResolveRenderSnapshotPath(listedLog.FullPath);
        if (renderSnapshotPath is null || !File.Exists(renderSnapshotPath))
        {
            return new TrackerRenderSnapshotLogResult(null, "Render snapshot file was not found for this diagnostics log.");
        }

        if (!uint.TryParse(trackedFrame, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameNumber))
        {
            return new TrackerRenderSnapshotLogResult(null, $"Tracked frame '{trackedFrame}' is not a numeric frame number.");
        }

        try
        {
            foreach (var record in ReadRecords(renderSnapshotPath))
            {
                if (record.Frame.FrameNumber == frameNumber)
                {
                    return new TrackerRenderSnapshotLogResult(
                        new TrackerRenderSnapshotView(renderSnapshotPath, record.ReceivedAt, record.Frame),
                        Error: null);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            return new TrackerRenderSnapshotLogResult(null, $"Render snapshot file could not be read: {ex.Message}");
        }

        return new TrackerRenderSnapshotLogResult(null, $"Render snapshot for tracked frame '{trackedFrame}' was not found.");
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

public sealed record TrackerRenderSnapshotView(
    string FilePath,
    DateTimeOffset ReceivedAt,
    TrackerFrame Frame);
