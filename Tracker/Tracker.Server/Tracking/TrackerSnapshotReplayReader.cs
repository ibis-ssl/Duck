using System.Text.Json;
using Google.Protobuf;

namespace Tracker.Server.Tracking;

/// <summary>
/// CaptureOn metadata から tracker packet snapshot sidecar を解決し、diagnostics / replay / playback 用の入力へ変換する。
/// </summary>
public sealed class TrackerSnapshotReplayReader
{
    /// <summary>
    /// CaptureOn session metadata を読み、session folder 内の tracker snapshot sidecar と diagnostics summary を返す。
    /// </summary>
    public TrackerSnapshotReplaySession ReadSession(string metadataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataPath);

        var fullMetadataPath = Path.GetFullPath(metadataPath);
        using var document = JsonDocument.Parse(File.ReadAllText(fullMetadataPath));
        var root = document.RootElement;
        var sessionDirectory = Path.GetDirectoryName(fullMetadataPath)
            ?? throw new InvalidDataException("Capture metadata path must have a parent directory.");
        var captureDirectory = ResolveCaptureDirectory(root, sessionDirectory);
        var sidecarPath = ResolveArtifactPath(
            root,
            captureDirectory,
            sessionDirectory,
            "TrackerSnapshotSidecarPath",
            TrackerPacketSnapshotLogReader.SidecarFileName)
            ?? throw new InvalidDataException("Tracker snapshot sidecar path could not be resolved.");
        var diagnosticsPath = ResolveArtifactPath(
            root,
            captureDirectory,
            sessionDirectory,
            "DiagnosticsLogPath",
            null);

        var inputs = File.Exists(sidecarPath)
            ? TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath)
                .Select(CreateReplayInput)
                .OrderBy(input => input.TrackedFrameTimestampNs)
                .ThenBy(input => input.ReceivedAt)
                .ToArray()
            : [];
        var summaries = BuildComparisonSummaries(diagnosticsPath, inputs);

        return new TrackerSnapshotReplaySession(
            fullMetadataPath,
            sidecarPath,
            diagnosticsPath,
            inputs,
            summaries);
    }

    private static TrackerSnapshotReplayInput CreateReplayInput(TrackerPacketSnapshotRecord record)
    {
        var normalizedRecord = record.EnsureSemanticSummary();
        var semanticSummary = normalizedRecord.SemanticSummary
            ?? TrackerPacketSnapshotSemanticSummary.FromRecord(normalizedRecord);
        var rawPayloadRestored = CanRestoreRawPayload(normalizedRecord.PayloadBase64);
        var displaySnapshot = new TrackerSnapshotDisplaySnapshot(
            normalizedRecord.Summary,
            normalizedRecord.SourceRole,
            normalizedRecord.SourceLabel,
            normalizedRecord.TrackedFrameNumber,
            normalizedRecord.TrackedFrameTimestampNs);
        var comparisonSource = new TrackerSnapshotComparisonSource(
            rawPayloadRestored,
            normalizedRecord.PayloadBase64,
            semanticSummary);

        return new TrackerSnapshotReplayInput(
            normalizedRecord.ReceivedAt,
            normalizedRecord.RemoteEndpoint,
            normalizedRecord.SourceUuid,
            normalizedRecord.SourceName,
            TrackerPacketSnapshotRecord.NormalizeSourceRole(normalizedRecord.SourceRole),
            TrackerPacketSnapshotRecord.NormalizeSourceLabel(
                normalizedRecord.SourceLabel,
                normalizedRecord.SourceName,
                normalizedRecord.SourceUuid,
                normalizedRecord.RemoteEndpoint,
                normalizedRecord.SourceRole),
            normalizedRecord.TrackedFrameNumber,
            normalizedRecord.TrackedFrameTimestampNs,
            displaySnapshot,
            comparisonSource);
    }

    private static IReadOnlyList<TrackerSnapshotComparisonSummary> BuildComparisonSummaries(
        string? diagnosticsPath,
        IReadOnlyList<TrackerSnapshotReplayInput> inputs)
    {
        if (string.IsNullOrWhiteSpace(diagnosticsPath) ||
            !File.Exists(diagnosticsPath) ||
            inputs.Count == 0)
        {
            return [];
        }

        var summaries = new List<TrackerSnapshotComparisonSummary>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(diagnosticsPath))
        {
            lineNumber++;
            if (!TrackerDiagnosticsLogReader.TryParseLine(line, lineNumber, out var entry))
            {
                continue;
            }

            var diagnosticsTimestampNs = ToMinuteRelativeTimestampNs(entry.Timestamp);
            var nearest = inputs
                .OrderBy(input => Math.Abs(input.TrackedFrameTimestampNs - diagnosticsTimestampNs))
                .ThenBy(input => input.TrackedFrameTimestampNs)
                .First();
            var semanticSummary = nearest.ComparisonSource.SemanticSummary;
            summaries.Add(new TrackerSnapshotComparisonSummary(
                "nearest-timestamp",
                diagnosticsTimestampNs,
                nearest.SourceRole,
                nearest.SourceLabel,
                nearest.TrackedFrameTimestampNs,
                nearest.ComparisonSource.RawPayloadRestored,
                semanticSummary.BallCount,
                semanticSummary.RobotCount));
        }

        return summaries;
    }

    private static bool CanRestoreRawPayload(string payloadBase64)
    {
        try
        {
            var payload = Convert.FromBase64String(payloadBase64);
            _ = TrackerWrapperPacket.Parser.ParseFrom(payload);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidProtocolBufferException)
        {
            return false;
        }
    }

    private static string ResolveCaptureDirectory(JsonElement metadata, string sessionDirectory)
    {
        if (!metadata.TryGetProperty("SessionFolder", out var sessionFolderElement))
        {
            return sessionDirectory;
        }

        var sessionFolder = sessionFolderElement.GetString();
        if (string.IsNullOrWhiteSpace(sessionFolder))
        {
            return sessionDirectory;
        }

        var directoryName = Path.GetFileName(
            sessionDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(directoryName, sessionFolder, StringComparison.Ordinal)
            ? Path.GetDirectoryName(sessionDirectory) ?? sessionDirectory
            : sessionDirectory;
    }

    private static string? ResolveArtifactPath(
        JsonElement metadata,
        string captureDirectory,
        string sessionDirectory,
        string propertyName,
        string? fallbackFileName)
    {
        if (metadata.TryGetProperty(propertyName, out var pathElement))
        {
            var path = pathElement.GetString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                return Path.GetFullPath(Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(captureDirectory, path));
            }
        }

        return fallbackFileName is null
            ? null
            : Path.GetFullPath(Path.Combine(sessionDirectory, fallbackFileName));
    }

    private static long ToMinuteRelativeTimestampNs(DateTimeOffset timestamp)
    {
        return (timestamp.TimeOfDay.Ticks % TimeSpan.TicksPerMinute) * 100L;
    }
}

/// <summary>
/// CaptureOn session から読み出した tracker snapshot replay 入力一式。
/// </summary>
public sealed record TrackerSnapshotReplaySession(
    string MetadataPath,
    string TrackerSnapshotSidecarPath,
    string? DiagnosticsLogPath,
    IReadOnlyList<TrackerSnapshotReplayInput> SnapshotInputs,
    IReadOnlyList<TrackerSnapshotComparisonSummary> ComparisonSummaries);

/// <summary>
/// diagnostics / replay / playback が時系列に扱う tracker snapshot 1 件。
/// </summary>
public sealed record TrackerSnapshotReplayInput(
    DateTimeOffset ReceivedAt,
    string RemoteEndpoint,
    string SourceUuid,
    string SourceName,
    string SourceRole,
    string SourceLabel,
    uint TrackedFrameNumber,
    long TrackedFrameTimestampNs,
    TrackerSnapshotDisplaySnapshot DisplaySnapshot,
    TrackerSnapshotComparisonSource ComparisonSource);

/// <summary>
/// 画面表示用に整形済みの tracker snapshot summary。
/// </summary>
public sealed record TrackerSnapshotDisplaySnapshot(
    string Summary,
    string SourceRole,
    string SourceLabel,
    uint TrackedFrameNumber,
    long TrackedFrameTimestampNs);

/// <summary>
/// 比較用元データとして復元可能な raw payload と raw 由来 semantic summary。
/// </summary>
public sealed record TrackerSnapshotComparisonSource(
    bool RawPayloadRestored,
    string PayloadBase64,
    TrackerPacketSnapshotSemanticSummary SemanticSummary);

/// <summary>
/// ibis diagnostics 1 行と近傍 tracker snapshot の比較 summary。
/// </summary>
public sealed record TrackerSnapshotComparisonSummary(
    string MatchingRule,
    long IbisDiagnosticsTimestampNs,
    string NearestSnapshotSourceRole,
    string NearestSnapshotSourceLabel,
    long NearestSnapshotTimestampNs,
    bool NearestSnapshotRawPayloadRestored,
    int NearestSnapshotBallCount,
    int NearestSnapshotRobotCount);
