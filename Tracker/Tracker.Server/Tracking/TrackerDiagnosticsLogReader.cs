using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Tracker.Server.Vision;

namespace Tracker.Server.Tracking;

public sealed class TrackerDiagnosticsLogReader
{
    private static readonly Regex FieldRegex = new(
        @"(?<key>[A-Za-z][A-Za-z0-9]*)=(?<value>\[[^\]]*\]|\S*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly VisionPacketCaptureOptions packetCaptureOptions;
    private readonly TrackerDiagnosticsOptions diagnosticsOptions;

    public TrackerDiagnosticsLogReader(
        IOptions<VisionReceiverOptions> visionReceiverOptions,
        TrackerDiagnosticsOptions diagnosticsOptions)
    {
        packetCaptureOptions = visionReceiverOptions.Value.PacketCapture;
        this.diagnosticsOptions = diagnosticsOptions;
    }

    public IReadOnlyList<TrackerDiagnosticsLogFile> ListFiles()
    {
        var files = new Dictionary<string, FileInfo>(StringComparer.Ordinal);
        var captureDirectoryPath = ResolveDirectoryPath(packetCaptureOptions.DirectoryPath);
        AddFiles(files, captureDirectoryPath, "*.tracker-diagnostics.log");
        AddFiles(files, captureDirectoryPath, "tracker-diagnostics-*.log");

        if (!string.IsNullOrWhiteSpace(diagnosticsOptions.FilePath))
        {
            var filePath = ResolveFilePath(diagnosticsOptions.FilePath);
            if (File.Exists(filePath))
            {
                files[filePath] = new FileInfo(filePath);
            }
        }

        return files.Values
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new TrackerDiagnosticsLogFile(
                file.Name,
                file.FullName,
                file.Length,
                file.LastWriteTimeUtc))
            .ToList();
    }

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
        IDictionary<string, FileInfo> files,
        string directoryPath,
        string searchPattern)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directoryPath, searchPattern))
        {
            files[path] = new FileInfo(path);
        }
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

public sealed record TrackerDiagnosticsLogFile(
    string FileName,
    string FullPath,
    long SizeBytes,
    DateTime LastWriteTimeUtc);

public sealed record TrackerDiagnosticsLogSnapshot(
    string FileName,
    IReadOnlyList<TrackerDiagnosticsLogEntry> Entries,
    string? Error,
    int SkippedLineCount = 0,
    int OmittedEntryCount = 0);

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
    public bool HasMultipleTrackedBalls => TrackedBallCount > 1;

    public bool HasRawBallMismatch => RawBallCount != TrackedBallCount;

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
