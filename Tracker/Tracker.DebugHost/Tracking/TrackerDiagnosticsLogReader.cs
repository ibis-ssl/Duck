using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// tracker diagnostics log file を列挙し、UI 表示用 snapshot へ parse する。
/// </summary>
public sealed class TrackerDiagnosticsLogReader
{
    private const string DiagnosticsLogSuffix = ".tracker-diagnostics.log";
    private const string MetadataSuffix = ".metadata.json";

    private static readonly Regex FieldRegex = new(
        @"(?<key>[A-Za-z][A-Za-z0-9]*)=(?<value>\[[^\]]*\]|\S*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly VisionPacketCaptureOptions packetCaptureOptions;
    private readonly TrackerDiagnosticsOptions diagnosticsOptions;

    /// <summary>
    /// capture sidecar directory と明示 diagnostics file path を参照する reader を作る。
    /// </summary>
    public TrackerDiagnosticsLogReader(
        IOptions<VisionReceiverOptions> visionReceiverOptions,
        TrackerDiagnosticsOptions diagnosticsOptions)
    {
        packetCaptureOptions = visionReceiverOptions.Value.PacketCapture;
        this.diagnosticsOptions = diagnosticsOptions;
    }

    /// <summary>
    /// capture sidecar と明示 log path から表示可能な diagnostics log file を新しい順に返す。
    /// </summary>
    public IReadOnlyList<TrackerDiagnosticsLogFile> ListFiles()
    {
        var files = new Dictionary<string, TrackerDiagnosticsLogFile>(StringComparer.Ordinal);
        var captureDirectoryPath = ResolveDirectoryPath(packetCaptureOptions.DirectoryPath);
        AddFiles(files, captureDirectoryPath, $"*{DiagnosticsLogSuffix}");
        AddFiles(files, captureDirectoryPath, "tracker-diagnostics-*.log");
        AddMetadataBackedDiagnosticsLogs(files, captureDirectoryPath);

        if (!string.IsNullOrWhiteSpace(diagnosticsOptions.FilePath))
        {
            var filePath = ResolveFilePath(diagnosticsOptions.FilePath);
            if (File.Exists(filePath))
            {
                files[filePath] = CreateLogFile(filePath, new FileInfo(filePath));
            }
        }

        return files.Values
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();
    }

    /// <summary>
    /// 許可された diagnostics log file を読み、末尾側の最大件数を snapshot として返す。
    /// </summary>
    public TrackerDiagnosticsLogSnapshot ReadFile(string fileName, int maxEntries = 10_000)
    {
        var requestedPath = Path.GetFullPath(ResolveFilePath(fileName));
        var listedFile = ListFiles()
            .FirstOrDefault(file => string.Equals(
                Path.GetFullPath(file.FullPath),
                requestedPath,
                StringComparison.Ordinal));
        var safeFileName = Path.GetFileName(requestedPath);
        if (listedFile is null)
        {
            return new TrackerDiagnosticsLogSnapshot(safeFileName, [], $"Log file '{safeFileName}' was not found.");
        }

        var path = listedFile.FullPath;
        if (!File.Exists(path))
        {
            return ReadMetadataBackedFile(path, safeFileName, maxEntries);
        }

        var entries = new List<TrackerDiagnosticsLogEntry>();
        var skippedLineCount = 0;
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (TryParseLine(line, lineNumber, out var entry))
            {
                entries.Add(entry);
                continue;
            }

            skippedLineCount++;
        }

        var omittedEntryCount = Math.Max(0, entries.Count - maxEntries);
        if (omittedEntryCount > 0)
        {
            entries = entries
                .Skip(omittedEntryCount)
                .ToList();
        }

        return new TrackerDiagnosticsLogSnapshot(
            safeFileName,
            entries,
            Error: null,
            SkippedLineCount: skippedLineCount,
            OmittedEntryCount: omittedEntryCount);
    }

    /// <summary>
    /// TrackerCoordinator が出力する diagnostics log 1 行を互換 schema として parse する。
    /// </summary>
    internal static bool TryParseLine(string line, int lineNumber, out TrackerDiagnosticsLogEntry entry)
    {
        entry = TrackerDiagnosticsLogEntry.Empty(lineNumber, line);

        var separatorIndex = line.IndexOf(' ');
        if (separatorIndex <= 0 ||
            !DateTimeOffset.TryParse(
                line[..separatorIndex],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return false;
        }

        var fields = FieldRegex
            .Matches(line[(separatorIndex + 1)..])
            .ToDictionary(
                match => match.Groups["key"].Value,
                match => TrimBracketedValue(match.Groups["value"].Value),
                StringComparer.Ordinal);

        entry = new TrackerDiagnosticsLogEntry(
            lineNumber,
            timestamp,
            GetString(fields, "profile"),
            GetString(fields, "rawFrame"),
            GetString(fields, "rawCamera"),
            GetInt(fields, "rawBalls"),
            GetString(fields, "rawBallDetails"),
            GetString(fields, "rawBlue"),
            GetString(fields, "rawYellow"),
            GetString(fields, "trackedFrame"),
            GetInt(fields, "trackedBalls"),
            GetString(fields, "trackedBallDetails"),
            GetInt(fields, "trackedRobots"),
            GetString(fields, "trackedRobotDetails"),
            GetString(fields, "ballOutVisibility"),
            GetString(fields, "ballHalfLifeSec"),
            GetString(fields, "ballLifetimeNs"),
            line);
        return true;
    }

    private static string ResolveDirectoryPath(string directoryPath)
    {
        return Path.IsPathRooted(directoryPath)
            ? directoryPath
            : Path.Combine(AppContext.BaseDirectory, directoryPath);
    }

    private static string ResolveFilePath(string fileName)
    {
        return Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(AppContext.BaseDirectory, fileName);
    }

    private static void AddFiles(
        IDictionary<string, TrackerDiagnosticsLogFile> files,
        string directoryPath,
        string searchPattern)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.AllDirectories))
        {
            files[path] = CreateLogFile(path, new FileInfo(path));
        }
    }

    private static void AddMetadataBackedDiagnosticsLogs(
        IDictionary<string, TrackerDiagnosticsLogFile> files,
        string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var metadataPath in Directory.EnumerateFiles(directoryPath, $"*{MetadataSuffix}", SearchOption.AllDirectories))
        {
            if (!TryResolveMetadataBackedPaths(metadataPath, out var diagnosticsLogPath, out var sampleSidecarPath) ||
                !File.Exists(sampleSidecarPath) ||
                files.ContainsKey(diagnosticsLogPath))
            {
                continue;
            }

            var metadataInfo = new FileInfo(metadataPath);
            var sampleInfo = new FileInfo(sampleSidecarPath);
            files[diagnosticsLogPath] = new TrackerDiagnosticsLogFile(
                Path.GetFileName(diagnosticsLogPath),
                diagnosticsLogPath,
                sampleInfo.Length,
                metadataInfo.LastWriteTimeUtc > sampleInfo.LastWriteTimeUtc
                    ? metadataInfo.LastWriteTimeUtc
                    : sampleInfo.LastWriteTimeUtc);
        }
    }

    private TrackerDiagnosticsLogSnapshot ReadMetadataBackedFile(
        string diagnosticsLogPath,
        string safeFileName,
        int maxEntries)
    {
        var metadataPath = ResolveMetadataPath(diagnosticsLogPath);
        if (metadataPath is null ||
            !TryResolveMetadataBackedPaths(metadataPath, out _, out var sampleSidecarPath) ||
            !File.Exists(sampleSidecarPath))
        {
            return new TrackerDiagnosticsLogSnapshot(
                safeFileName,
                [],
                $"Log file '{safeFileName}' was not found.");
        }

        try
        {
            var entries = DiagnosticsSampleLogReader.ReadRecords(sampleSidecarPath)
                .Select(CreateEntry)
                .ToList();
            var omittedEntryCount = Math.Max(0, entries.Count - maxEntries);
            if (omittedEntryCount > 0)
            {
                entries = entries
                    .Skip(omittedEntryCount)
                    .ToList();
            }

            return new TrackerDiagnosticsLogSnapshot(
                safeFileName,
                entries,
                Error: null,
                SkippedLineCount: 0,
                OmittedEntryCount: omittedEntryCount);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            return new TrackerDiagnosticsLogSnapshot(
                safeFileName,
                [],
                $"Diagnostics sample sidecar could not be read: {ex.Message}");
        }
    }

    private static TrackerDiagnosticsLogEntry CreateEntry(DiagnosticsSampleRecord record)
    {
        var rawSummary = record.RawSemanticSummary;
        var trackedSummary = record.TrackedSemanticSummary;
        var trackedFrame = record.TrackedFrameNumber ?? record.RenderFrameNumber;
        return new TrackerDiagnosticsLogEntry(
            record.SampleIndex + 1,
            record.SampleReceivedAt,
            "",
            Display(record.RawFrameNumber),
            Display(record.RawCameraId),
            rawSummary?.BallCount ?? 0,
            FormatBallDetails(rawSummary),
            FormatRobotDetails(rawSummary, "Blue"),
            FormatRobotDetails(rawSummary, "Yellow"),
            Display(trackedFrame),
            trackedSummary?.BallCount ?? 0,
            FormatBallDetails(trackedSummary),
            trackedSummary?.RobotCount ?? 0,
            FormatRobotDetails(trackedSummary, team: null),
            "",
            "",
            "",
            FormattableString.Invariant(
                $"{record.SampleReceivedAt:O} diagnostics-sample sampleIndex={record.SampleIndex} rawFrame={Display(record.RawFrameNumber)} trackedFrame={Display(trackedFrame)}"));
    }

    private static string FormatBallDetails(TrackerPacketSnapshotSemanticSummary? summary)
    {
        return summary is null
            ? ""
            : string.Join("; ", summary.Balls.Select(ball => FormattableString.Invariant(
                $"#{ball.Index}:x={ball.XMm:0.#},y={ball.YMm:0.#},z={ball.ZMm:0.#},vis={ball.Visibility:0.###}")));
    }

    private static string FormatRobotDetails(TrackerPacketSnapshotSemanticSummary? summary, string? team)
    {
        if (summary is null)
        {
            return "";
        }

        var robots = string.IsNullOrWhiteSpace(team)
            ? summary.Robots
            : summary.Robots.Where(robot => string.Equals(robot.Team, team, StringComparison.OrdinalIgnoreCase));
        return string.Join("; ", robots.Select(robot => FormattableString.Invariant(
            $"{FormatTeam(robot.Team)}{robot.RobotId}:x={robot.XMm:0.#},y={robot.YMm:0.#},o={robot.OrientationRad:0.###},vis={robot.Visibility:0.###}")));
    }

    private static string FormatTeam(string team)
    {
        return team.Equals("Yellow", StringComparison.OrdinalIgnoreCase)
            ? "Y"
            : team.Equals("Blue", StringComparison.OrdinalIgnoreCase)
                ? "B"
                : $"{team}:";
    }

    private static string Display(uint? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    private static TrackerDiagnosticsLogFile CreateLogFile(string path, FileInfo info)
    {
        return new TrackerDiagnosticsLogFile(
            Path.GetFileName(path),
            Path.GetFullPath(path),
            info.Length,
            info.LastWriteTimeUtc);
    }

    private static string? ResolveMetadataPath(string diagnosticsLogPath)
    {
        return diagnosticsLogPath.EndsWith(DiagnosticsLogSuffix, StringComparison.Ordinal)
            ? string.Concat(diagnosticsLogPath.AsSpan(0, diagnosticsLogPath.Length - DiagnosticsLogSuffix.Length), MetadataSuffix)
            : null;
    }

    private static bool TryResolveMetadataBackedPaths(
        string metadataPath,
        out string diagnosticsLogPath,
        out string sampleSidecarPath)
    {
        diagnosticsLogPath = "";
        sampleSidecarPath = "";
        if (!File.Exists(metadataPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var root = document.RootElement;
            if (!TryGetString(root, "DiagnosticsSampleSidecarPath", out var samplePath) ||
                string.IsNullOrWhiteSpace(samplePath) ||
                !IsDiagnosticsSampleCreated(root))
            {
                return false;
            }

            if (!TryGetString(root, "DiagnosticsLogPath", out var logPath) ||
                string.IsNullOrWhiteSpace(logPath))
            {
                logPath = metadataPath.EndsWith(MetadataSuffix, StringComparison.Ordinal)
                    ? string.Concat(metadataPath.AsSpan(0, metadataPath.Length - MetadataSuffix.Length), DiagnosticsLogSuffix)
                    : $"{metadataPath}{DiagnosticsLogSuffix}";
            }

            var baseDirectory = ResolveCaptureDirectory(root, metadataPath);
            diagnosticsLogPath = ResolveMetadataRelativePath(baseDirectory, logPath);
            sampleSidecarPath = ResolveMetadataRelativePath(baseDirectory, samplePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsDiagnosticsSampleCreated(JsonElement root)
    {
        if (!root.TryGetProperty("DiagnosticsSampleLog", out var log))
        {
            return true;
        }

        return !log.TryGetProperty("IsCreated", out var isCreated) || isCreated.GetBoolean();
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = "";
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? "";
        return true;
    }

    private static string ResolveCaptureDirectory(JsonElement root, string metadataPath)
    {
        var metadataDirectory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidDataException("Capture metadata path must have a parent directory.");
        if (!TryGetString(root, "SessionFolder", out var sessionFolder) ||
            string.IsNullOrWhiteSpace(sessionFolder))
        {
            return metadataDirectory;
        }

        return string.Equals(Path.GetFileName(metadataDirectory), sessionFolder, StringComparison.Ordinal)
            ? Path.GetDirectoryName(metadataDirectory) ?? metadataDirectory
            : metadataDirectory;
    }

    private static string ResolveMetadataRelativePath(string baseDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(baseDirectory, path));
    }

    private static string TrimBracketedValue(string value)
    {
        return value.Length >= 2 && value[0] == '[' && value[^1] == ']'
            ? value[1..^1]
            : value;
    }

    private static string GetString(IReadOnlyDictionary<string, string> fields, string key)
    {
        return fields.TryGetValue(key, out var value) ? value : "";
    }

    private static int GetInt(IReadOnlyDictionary<string, string> fields, string key)
    {
        return fields.TryGetValue(key, out var value) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

}

/// <summary>
/// Diagnostics UI に表示する diagnostics log file の metadata。
/// </summary>
public sealed record TrackerDiagnosticsLogFile(
    string FileName,
    string FullPath,
    long SizeBytes,
    DateTime LastWriteTimeUtc);

/// <summary>
/// diagnostics log file の parse 結果と skipped / omitted 件数。
/// </summary>
public sealed record TrackerDiagnosticsLogSnapshot(
    string FileName,
    IReadOnlyList<TrackerDiagnosticsLogEntry> Entries,
    string? Error,
    int SkippedLineCount = 0,
    int OmittedEntryCount = 0);

/// <summary>
/// diagnostics log 1 行の raw detection と tracked output の比較情報。
/// </summary>
public sealed record TrackerDiagnosticsLogEntry(
    int LineNumber,
    DateTimeOffset Timestamp,
    string ProfileName,
    string RawFrame,
    string RawCamera,
    int RawBallCount,
    string RawBallDetails,
    string RawBlueDetails,
    string RawYellowDetails,
    string TrackedFrame,
    int TrackedBallCount,
    string TrackedBallDetails,
    int TrackedRobotCount,
    string TrackedRobotDetails,
    string BallOutputVisibility,
    string BallVisibilityHalfLifeSeconds,
    string BallTrackLifetimeNs,
    string RawLine)
{
    /// <summary>
    /// tracked output に複数 ball が出ているかを示す。
    /// </summary>
    public bool HasMultipleTrackedBalls => TrackedBallCount > 1;

    /// <summary>
    /// raw ball 数と tracked ball 数に差があるかを示す。
    /// </summary>
    public bool HasRawBallMismatch => RawBallCount != TrackedBallCount;

    /// <summary>
    /// parse 失敗行を line number と raw text だけ保持する entry として表す。
    /// </summary>
    public static TrackerDiagnosticsLogEntry Empty(int lineNumber, string rawLine)
    {
        return new TrackerDiagnosticsLogEntry(
            lineNumber,
            DateTimeOffset.MinValue,
            "",
            "",
            "",
            0,
            "",
            "",
            "",
            "",
            0,
            "",
            0,
            "",
            "",
            "",
            "",
            rawLine);
    }
}
