using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fizzy.McapSharp;
using Microsoft.Extensions.Options;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// 診断ログファイルを列挙し、画面表示用のスナップショットへ読み込む。
/// </summary>
public sealed class TrackerDiagnosticsLogReader
{
    private const string DefaultDiagnosticsDirectoryName = "diagnostic-logs";
    private const string DiagnosticsLogSuffix = ".tracker-diagnostics.log";
    private const string MetadataSuffix = ".metadata.json";

    private static readonly Regex FieldRegex = new(
        @"(?<key>[A-Za-z][A-Za-z0-9]*)=(?<value>\[[^\]]*\]|\S*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly VisionPacketCaptureOptions packetCaptureOptions;
    private readonly TrackerDiagnosticsOptions diagnosticsOptions;
    private readonly Func<string, string, SearchOption, IEnumerable<string>> enumerateFiles;

    /// <summary>
    /// キャプチャー sidecar の保存先と明示された診断ファイルのパスを使う reader を作る。
    /// </summary>
    public TrackerDiagnosticsLogReader(
        IOptions<VisionReceiverOptions> visionReceiverOptions,
        TrackerDiagnosticsOptions diagnosticsOptions)
        : this(visionReceiverOptions, diagnosticsOptions, Directory.EnumerateFiles)
    {
    }

    /// <summary>列挙障害と MCAP 直接読取の独立性を確認する reader 境界。</summary>
    internal TrackerDiagnosticsLogReader(
        IOptions<VisionReceiverOptions> visionReceiverOptions,
        TrackerDiagnosticsOptions diagnosticsOptions,
        Func<string, string, SearchOption, IEnumerable<string>> enumerateFiles)
    {
        packetCaptureOptions = visionReceiverOptions.Value.PacketCapture;
        this.diagnosticsOptions = diagnosticsOptions;
        this.enumerateFiles = enumerateFiles;
    }

    /// <summary>
    /// キャプチャー sidecar と明示パスから、表示できる診断ログファイルを更新日時の新しい順に返す。
    /// </summary>
    public IReadOnlyList<TrackerDiagnosticsLogFile> ListFiles()
    {
        return ListFilesIsolated().Files;
    }

    private TrackerDiagnosticsLogListing ListFilesIsolated()
    {
        var files = new Dictionary<string, TrackerDiagnosticsLogFile>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var captureDirectoryPath = ResolveDirectoryPath(packetCaptureOptions.DirectoryPath);
        var diagnosticsDirectoryPath = ResolveDiagnosticsDirectoryPath();
        TryAddFiles(files, diagnosticsDirectoryPath, "*.mcap", "MCAP", warnings);
        TryAddFiles(files, captureDirectoryPath, $"*{DiagnosticsLogSuffix}", "CaptureOn の診断ログ", warnings);
        TryAddFiles(files, captureDirectoryPath, "tracker-diagnostics-*.log", "CaptureOn の診断ログ", warnings);
        try
        {
            AddMetadataBackedDiagnosticsLogs(files, captureDirectoryPath);
        }
        catch (Exception exception) when (IsListingException(exception))
        {
            warnings.Add($"CaptureOn の記録情報を列挙できませんでした: {exception.Message}");
        }

        if (!string.IsNullOrWhiteSpace(diagnosticsOptions.FilePath))
        {
            var filePath = ResolveFilePath(diagnosticsOptions.FilePath);
            if (File.Exists(filePath) && !IsIncompleteFile(filePath))
            {
                files[filePath] = CreateLogFile(filePath, new FileInfo(filePath));
            }
        }

        return new TrackerDiagnosticsLogListing(
            files.Values.OrderByDescending(file => file.LastWriteTimeUtc).ToList(),
            warnings.Count == 0 ? null : string.Join(" ", warnings));
    }

    /// <summary>
    /// 共通一覧が失敗した場合も診断 MCAP を選べる一覧を返し、列挙障害を警告として残す。
    /// </summary>
    internal TrackerDiagnosticsLogListing ListFilesWithMcapFallback()
    {
        return ListFilesIsolated();
    }

    /// <summary>
    /// 許可された診断ログファイルを読み、末尾側の最大件数をスナップショットとして返す。
    /// </summary>
    public TrackerDiagnosticsLogSnapshot ReadFile(string fileName, int maxEntries = 10_000)
    {
        var requestedPath = Path.GetFullPath(ResolveFilePath(fileName));
        var safeFileName = Path.GetFileName(requestedPath);
        if (IsIncompleteFile(requestedPath))
        {
            return new TrackerDiagnosticsLogSnapshot(safeFileName, [], $"Incomplete log file '{safeFileName}' is not available.");
        }

        if (string.Equals(Path.GetExtension(requestedPath), ".mcap", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsConfiguredMcapPath(requestedPath) || !File.Exists(requestedPath))
            {
                return new TrackerDiagnosticsLogSnapshot(safeFileName, [], $"Log file '{safeFileName}' was not found.");
            }

            try
            {
                return ReadMcapFile(requestedPath, safeFileName, maxEntries);
            }
            catch (Exception exception)
            {
                return new TrackerDiagnosticsLogSnapshot(safeFileName, [], exception.Message);
            }
        }

        var listedFile = ListFiles()
            .FirstOrDefault(file => string.Equals(
                Path.GetFullPath(file.FullPath),
                requestedPath,
                StringComparison.Ordinal));
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

    private static bool IsIncompleteFile(string path) =>
        path.EndsWith(".partial", StringComparison.OrdinalIgnoreCase);

    private string ResolveDiagnosticsDirectoryPath() =>
        string.IsNullOrWhiteSpace(diagnosticsOptions.DirectoryPath)
            ? Path.Combine(AppContext.BaseDirectory, DefaultDiagnosticsDirectoryName)
            : ResolveDirectoryPath(diagnosticsOptions.DirectoryPath);

    private bool IsConfiguredMcapPath(string requestedPath)
    {
        var diagnosticsDirectory = Path.GetFullPath(ResolveDiagnosticsDirectoryPath());
        var relativePath = Path.GetRelativePath(diagnosticsDirectory, requestedPath);
        if (!Path.IsPathRooted(relativePath) &&
            relativePath != ".." &&
            !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(diagnosticsOptions.FilePath))
        {
            return false;
        }

        var explicitPath = Path.GetFullPath(ResolveFilePath(diagnosticsOptions.FilePath));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(requestedPath, explicitPath, comparison);
    }

    private void AddFiles(
        IDictionary<string, TrackerDiagnosticsLogFile> files,
        string directoryPath,
        string searchPattern)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var path in enumerateFiles(directoryPath, searchPattern, SearchOption.AllDirectories))
        {
            if (!IsIncompleteFile(path))
            {
                files[path] = CreateLogFile(path, new FileInfo(path));
            }
        }
    }

    private void TryAddFiles(
        IDictionary<string, TrackerDiagnosticsLogFile> files,
        string directoryPath,
        string searchPattern,
        string sourceName,
        ICollection<string> warnings)
    {
        try
        {
            AddFiles(files, directoryPath, searchPattern);
        }
        catch (Exception exception) when (IsListingException(exception))
        {
            warnings.Add($"{sourceName}を列挙できませんでした: {exception.Message}");
        }
    }

    private static bool IsListingException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or
            ArgumentException or NotSupportedException;

    private void AddMetadataBackedDiagnosticsLogs(
        IDictionary<string, TrackerDiagnosticsLogFile> files,
        string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var metadataPath in enumerateFiles(directoryPath, $"*{MetadataSuffix}", SearchOption.AllDirectories))
        {
            if (!TryResolveMetadataBackedPaths(metadataPath, out var diagnosticsLogPath, out var sampleSidecarPath) ||
                string.Equals(Path.GetExtension(diagnosticsLogPath), ".mcap", StringComparison.OrdinalIgnoreCase) ||
                IsIncompleteFile(diagnosticsLogPath) ||
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

    private static TrackerDiagnosticsLogSnapshot ReadMcapFile(string path, string safeFileName, int maxEntries)
    {
        var entries = new List<TrackerDiagnosticsLogEntry>();
        var skippedMessageCount = 0;
        var messageNumber = 0;
        var reader = new McapReaderFactory(path);
        foreach (var message in reader.ReadMessages(new McapMessageFilter { Topic = "/diagnostics" }))
        {
            messageNumber++;
            try
            {
                if (!string.Equals(message.Channel.Schema?.Name, "foxglove.Log", StringComparison.Ordinal))
                {
                    skippedMessageCount++;
                    continue;
                }

                using var document = JsonDocument.Parse(message.Data);
                var root = document.RootElement;
                var timestampValue = root.GetProperty("timestamp");
                var seconds = timestampValue.GetProperty("sec").GetInt64();
                var nanoseconds = timestampValue.GetProperty("nsec").GetInt32();
                var timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nanoseconds / 100);
                var level = root.GetProperty("level").GetInt32();
                var text = root.GetProperty("message").GetString() ?? "";
                var name = root.GetProperty("name").GetString() ?? "";
                var file = root.GetProperty("file").GetString() ?? "";
                var line = root.GetProperty("line").GetInt32();
                entries.Add(new TrackerDiagnosticsLogEntry(
                    messageNumber,
                    timestamp,
                    name,
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
                    text,
                    level,
                    name,
                    text,
                    file,
                    line));
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
            {
                skippedMessageCount++;
            }
        }

        var orderedEntries = entries
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => entry.LineNumber)
            .ToList();
        var omittedEntryCount = Math.Max(0, orderedEntries.Count - maxEntries);
        if (omittedEntryCount > 0)
        {
            orderedEntries = orderedEntries.Skip(omittedEntryCount).ToList();
        }

        return new TrackerDiagnosticsLogSnapshot(
            safeFileName,
            orderedEntries,
            Error: null,
            SkippedLineCount: skippedMessageCount,
            OmittedEntryCount: omittedEntryCount);
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
/// 診断画面に表示するログファイルの情報。
/// </summary>
public sealed record TrackerDiagnosticsLogFile(
    string FileName,
    string FullPath,
    long SizeBytes,
    DateTime LastWriteTimeUtc);

/// <summary>ログ一覧と、一覧取得時に画面へ伝える警告。</summary>
internal sealed record TrackerDiagnosticsLogListing(
    IReadOnlyList<TrackerDiagnosticsLogFile> Files,
    string? Warning);

/// <summary>
/// 診断ログファイルの読み取り結果と、読み飛ばし・省略件数。
/// </summary>
public sealed record TrackerDiagnosticsLogSnapshot(
    string FileName,
    IReadOnlyList<TrackerDiagnosticsLogEntry> Entries,
    string? Error,
    int SkippedLineCount = 0,
    int OmittedEntryCount = 0);

/// <summary>
/// 診断ログ1行から読み取った元の検出情報と追跡出力の比較情報。
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
    string RawLine,
    int? FoxgloveLevel = null,
    string? SourceName = null,
    string? Message = null,
    string? SourceFile = null,
    int? SourceLine = null)
{
    /// <summary>
    /// 追跡出力に複数のボールが含まれるかを示す。
    /// </summary>
    public bool HasMultipleTrackedBalls => TrackedBallCount > 1;

    /// <summary>
    /// 元のボール数と追跡後のボール数に差があるかを示す。
    /// </summary>
    public bool HasRawBallMismatch => RawBallCount != TrackedBallCount;

    /// <summary>
    /// 読み取りに失敗した行を、行番号と元のテキストだけを保持する項目として表す。
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
