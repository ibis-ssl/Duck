using System.Globalization;
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

            if (!TryGetIbisDataTimestampNs(entry, inputs, out var ibisDataTimestampNs))
            {
                continue;
            }

            var nearestCandidates = inputs
                .Where(input => !string.Equals(input.SourceRole, "own", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (nearestCandidates.Length == 0)
            {
                nearestCandidates = inputs.ToArray();
            }

            var nearest = nearestCandidates
                .OrderBy(input => Math.Abs(input.TrackedFrameTimestampNs - ibisDataTimestampNs))
                .ThenBy(input => input.TrackedFrameTimestampNs)
                .First();
            var semanticSummary = nearest.ComparisonSource.SemanticSummary;
            summaries.Add(new TrackerSnapshotComparisonSummary(
                "nearest-timestamp",
                ibisDataTimestampNs,
                nearest.SourceRole,
                nearest.SourceLabel,
                nearest.TrackedFrameTimestampNs,
                nearest.ComparisonSource.RawPayloadRestored,
                semanticSummary.BallCount,
                semanticSummary.RobotCount));
        }

        return summaries;
    }

    private static bool TryGetIbisDataTimestampNs(
        TrackerDiagnosticsLogEntry entry,
        IReadOnlyList<TrackerSnapshotReplayInput> inputs,
        out long timestampNs)
    {
        timestampNs = 0;
        if (!uint.TryParse(entry.TrackedFrame, NumberStyles.Integer, CultureInfo.InvariantCulture, out var trackedFrameNumber))
        {
            return false;
        }

        var ownSnapshot = inputs
            .Where(input =>
                input.TrackedFrameNumber == trackedFrameNumber &&
                string.Equals(input.SourceRole, "own", StringComparison.OrdinalIgnoreCase))
            .OrderBy(input => input.TrackedFrameTimestampNs)
            .FirstOrDefault();
        if (ownSnapshot is null)
        {
            return false;
        }

        timestampNs = ownSnapshot.TrackedFrameTimestampNs;
        return true;
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

}

/// <summary>
/// CaptureOn session から読み出した tracker snapshot replay 入力一式。
/// </summary>
/// <param name="MetadataPath">読み込み元の CaptureOn metadata JSON の絶対 path。</param>
/// <param name="TrackerSnapshotSidecarPath">metadata から解決した tracker snapshot sidecar JSONL の絶対 path。</param>
/// <param name="DiagnosticsLogPath">metadata から解決した diagnostics log の絶対 path。metadata に path がない場合は null。</param>
/// <param name="SnapshotInputs">diagnostics / replay / playback が timestamp 順に扱う tracker snapshot 入力。</param>
/// <param name="ComparisonSummaries">ibis diagnostics frame と tracker snapshot を timestamp 近傍規則で対応付けた summary。</param>
public sealed record TrackerSnapshotReplaySession(
    string MetadataPath,
    string TrackerSnapshotSidecarPath,
    string? DiagnosticsLogPath,
    IReadOnlyList<TrackerSnapshotReplayInput> SnapshotInputs,
    IReadOnlyList<TrackerSnapshotComparisonSummary> ComparisonSummaries);

/// <summary>
/// diagnostics / replay / playback が時系列に扱う tracker snapshot 1 件。
/// </summary>
/// <param name="ReceivedAt">tracker packet を受信した wall-clock UTC 時刻。data timestamp ではない。</param>
/// <param name="RemoteEndpoint">packet の送信元 endpoint 表示。</param>
/// <param name="SourceUuid">official tracker packet の source UUID。</param>
/// <param name="SourceName">official tracker packet の source name。</param>
/// <param name="SourceRole">own、external、unknown などの保存後分類。</param>
/// <param name="SourceLabel">UI、replay、diagnostics 表示で使う source label。</param>
/// <param name="TrackedFrameNumber">snapshot 側 TrackedFrame.frame_number。</param>
/// <param name="TrackedFrameTimestampNs">snapshot 側 TrackedFrame.timestamp を ns に変換した data timestamp。</param>
/// <param name="DisplaySnapshot">表示用に整形済みの snapshot summary。</param>
/// <param name="ComparisonSource">比較用に保持する raw payload と semantic summary。</param>
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
/// <param name="Summary">source、role、frame、ball / robot 数を含む短い表示 summary。</param>
/// <param name="SourceRole">own、external、unknown などの保存後分類。</param>
/// <param name="SourceLabel">UI、replay、diagnostics 表示で使う source label。</param>
/// <param name="TrackedFrameNumber">snapshot 側 TrackedFrame.frame_number。</param>
/// <param name="TrackedFrameTimestampNs">snapshot 側 TrackedFrame.timestamp を ns に変換した data timestamp。</param>
public sealed record TrackerSnapshotDisplaySnapshot(
    string Summary,
    string SourceRole,
    string SourceLabel,
    uint TrackedFrameNumber,
    long TrackedFrameTimestampNs);

/// <summary>
/// 比較用元データとして復元可能な raw payload と raw 由来 semantic summary。
/// </summary>
/// <param name="RawPayloadRestored">raw tracker packet payload を protobuf として復元できる場合は true。</param>
/// <param name="PayloadBase64">replay や再比較に使う raw tracker packet payload の base64 表現。</param>
/// <param name="SemanticSummary">raw payload または record metadata から作った比較用 semantic summary。</param>
public sealed record TrackerSnapshotComparisonSource(
    bool RawPayloadRestored,
    string PayloadBase64,
    TrackerPacketSnapshotSemanticSummary SemanticSummary);

/// <summary>
/// ibis diagnostics 1 行と近傍 tracker snapshot の比較 summary。
/// </summary>
/// <param name="MatchingRule">snapshot 対応付けに使った規則。現行実装では nearest-timestamp。</param>
/// <param name="IbisDiagnosticsTimestampNs">ibis own snapshot の TrackedFrame.timestamp から得た committed frame data timestamp。</param>
/// <param name="NearestSnapshotSourceRole">近傍 snapshot の source role。</param>
/// <param name="NearestSnapshotSourceLabel">近傍 snapshot の source label。</param>
/// <param name="NearestSnapshotTimestampNs">近傍 snapshot 側 TrackedFrame.timestamp を ns に変換した data timestamp。</param>
/// <param name="NearestSnapshotRawPayloadRestored">近傍 snapshot の raw payload を protobuf として復元できる場合は true。</param>
/// <param name="NearestSnapshotBallCount">近傍 snapshot の semantic summary に含まれる ball 数。</param>
/// <param name="NearestSnapshotRobotCount">近傍 snapshot の semantic summary に含まれる robot 数。</param>
public sealed record TrackerSnapshotComparisonSummary(
    string MatchingRule,
    long IbisDiagnosticsTimestampNs,
    string NearestSnapshotSourceRole,
    string NearestSnapshotSourceLabel,
    long NearestSnapshotTimestampNs,
    bool NearestSnapshotRawPayloadRestored,
    int NearestSnapshotBallCount,
    int NearestSnapshotRobotCount);
