using System.Globalization;
using System.Text.Json;
using Google.Protobuf;

namespace Tracker.Server.Tracking;

/// <summary>
/// diagnostics log path から tracker snapshot sidecar を解決し、UI 非依存の comparison view-state を作る。
/// </summary>
public sealed class TrackerDiagnosticsComparisonViewStateReader
{
    private const string DiagnosticsLogSuffix = ".tracker-diagnostics.log";
    private const string MetadataSuffix = ".metadata.json";
    private readonly TrackerSnapshotReplayReader replayReader = new();

    /// <summary>
    /// diagnostics log path と表示済み selected entry から comparison view-state を読み取る。
    /// </summary>
    public TrackerDiagnosticsComparisonViewState Load(
        string? diagnosticsLogPath,
        TrackerDiagnosticsComparisonSelectedEntry? selectedEntry,
        TrackerDiagnosticsComparisonSourceFilter selectedSourceFilter)
    {
        if (string.IsNullOrWhiteSpace(diagnosticsLogPath))
        {
            return TrackerDiagnosticsComparisonViewState.Unavailable(
                diagnosticsLogPath,
                metadataPath: null,
                sidecarPath: null,
                TrackerDiagnosticsComparisonSidecarStatus.NoLogSelected,
                selectedSourceFilter,
                "Diagnostics log is not selected.");
        }

        var fullDiagnosticsLogPath = Path.GetFullPath(diagnosticsLogPath);
        var metadataPath = ResolveMetadataPath(fullDiagnosticsLogPath);
        if (metadataPath is null || !File.Exists(metadataPath))
        {
            return TrackerDiagnosticsComparisonViewState.Unavailable(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath: null,
                TrackerDiagnosticsComparisonSidecarStatus.MetadataMissing,
                selectedSourceFilter,
                "Capture metadata file was not found for this diagnostics log.");
        }

        if (!TryReadMetadata(metadataPath, out var metadata, out var metadataError))
        {
            return TrackerDiagnosticsComparisonViewState.Unavailable(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath: null,
                TrackerDiagnosticsComparisonSidecarStatus.MetadataCorrupt,
                selectedSourceFilter,
                metadataError);
        }

        var sourceOptions = CreateSourceOptions([]);
        if (metadata.TrackerSnapshotLog is null)
        {
            return CreateState(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath: null,
                TrackerDiagnosticsComparisonSidecarStatus.SnapshotMetadataMissing,
                selectedSourceFilter,
                sourceOptions,
                selectedEntryComparison: null,
                recordCount: 0,
                skippedRecordCount: 0,
                errorCount: 0,
                "Tracker snapshot metadata was not found.");
        }

        if (!metadata.TrackerSnapshotLog.IsCreated)
        {
            return CreateState(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath: ResolveSidecarPath(metadata, metadataPath),
                TrackerDiagnosticsComparisonSidecarStatus.SidecarNotCreated,
                selectedSourceFilter,
                sourceOptions,
                selectedEntryComparison: null,
                metadata.TrackerSnapshotLog.RecordCount,
                metadata.TrackerSnapshotLog.SkippedRecordCount,
                metadata.TrackerSnapshotLog.ErrorCount,
                "Tracker snapshot sidecar was not created for this capture session.");
        }

        var sidecarPath = ResolveSidecarPath(metadata, metadataPath);
        if (sidecarPath is null)
        {
            return CreateState(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath,
                TrackerDiagnosticsComparisonSidecarStatus.SidecarPathMissing,
                selectedSourceFilter,
                sourceOptions,
                selectedEntryComparison: null,
                metadata.TrackerSnapshotLog.RecordCount,
                metadata.TrackerSnapshotLog.SkippedRecordCount,
                metadata.TrackerSnapshotLog.ErrorCount,
                "Tracker snapshot sidecar path was not found in metadata.");
        }

        if (!File.Exists(sidecarPath))
        {
            return CreateState(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath,
                TrackerDiagnosticsComparisonSidecarStatus.SidecarMissing,
                selectedSourceFilter,
                sourceOptions,
                selectedEntryComparison: null,
                metadata.TrackerSnapshotLog.RecordCount,
                metadata.TrackerSnapshotLog.SkippedRecordCount,
                metadata.TrackerSnapshotLog.ErrorCount,
                "Tracker snapshot sidecar file was not found.");
        }

        IReadOnlyList<ComparisonSnapshot> snapshots;
        try
        {
            snapshots = replayReader.ReadSession(metadataPath)
                .SnapshotInputs
                .Select(CreateComparisonSnapshot)
                .OrderBy(snapshot => snapshot.TrackedFrameTimestampNs)
                .ThenBy(snapshot => snapshot.ReceivedAt)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or FormatException or InvalidProtocolBufferException)
        {
            return CreateState(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath,
                TrackerDiagnosticsComparisonSidecarStatus.SidecarCorrupt,
                selectedSourceFilter,
                sourceOptions,
                selectedEntryComparison: null,
                metadata.TrackerSnapshotLog.RecordCount,
                metadata.TrackerSnapshotLog.SkippedRecordCount,
                metadata.TrackerSnapshotLog.ErrorCount,
                $"Tracker snapshot sidecar could not be read: {ex.Message}");
        }

        if (snapshots.Count == 0)
        {
            return CreateState(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath,
                TrackerDiagnosticsComparisonSidecarStatus.SidecarEmpty,
                selectedSourceFilter,
                sourceOptions,
                selectedEntryComparison: null,
                metadata.TrackerSnapshotLog.RecordCount,
                metadata.TrackerSnapshotLog.SkippedRecordCount,
                metadata.TrackerSnapshotLog.ErrorCount,
                "Tracker snapshot sidecar did not contain records.");
        }

        sourceOptions = CreateSourceOptions(snapshots);
        var selectedEntryComparison = CreateSelectedEntryComparison(
            selectedEntry,
            selectedSourceFilter,
            snapshots);

        return CreateState(
            fullDiagnosticsLogPath,
            metadataPath,
            sidecarPath,
            TrackerDiagnosticsComparisonSidecarStatus.Ready,
            selectedSourceFilter,
            sourceOptions,
            selectedEntryComparison,
            metadata.TrackerSnapshotLog.RecordCount,
            metadata.TrackerSnapshotLog.SkippedRecordCount,
            metadata.TrackerSnapshotLog.ErrorCount,
            error: null);
    }

    private static TrackerDiagnosticsComparisonViewState CreateState(
        string? diagnosticsLogPath,
        string? metadataPath,
        string? sidecarPath,
        TrackerDiagnosticsComparisonSidecarStatus sidecarStatus,
        TrackerDiagnosticsComparisonSourceFilter selectedSourceFilter,
        IReadOnlyList<TrackerDiagnosticsComparisonSourceOption> sourceOptions,
        TrackerDiagnosticsComparisonEntryComparison? selectedEntryComparison,
        int recordCount,
        int skippedRecordCount,
        int errorCount,
        string? error)
    {
        return new TrackerDiagnosticsComparisonViewState(
            diagnosticsLogPath,
            metadataPath,
            sidecarPath,
            sidecarStatus,
            sourceOptions,
            selectedSourceFilter,
            selectedEntryComparison,
            recordCount,
            skippedRecordCount,
            errorCount,
            error);
    }

    private static TrackerDiagnosticsComparisonEntryComparison CreateSelectedEntryComparison(
        TrackerDiagnosticsComparisonSelectedEntry? selectedEntry,
        TrackerDiagnosticsComparisonSourceFilter selectedSourceFilter,
        IReadOnlyList<ComparisonSnapshot> snapshots)
    {
        if (selectedEntry is null)
        {
            return TrackerDiagnosticsComparisonEntryComparison.WithStatus(
                TrackerDiagnosticsComparisonEntryStatus.NoDiagnosticsEntrySelected);
        }

        if (!uint.TryParse(selectedEntry.TrackedFrame, NumberStyles.Integer, CultureInfo.InvariantCulture, out var trackedFrame))
        {
            return TrackerDiagnosticsComparisonEntryComparison.WithStatus(
                TrackerDiagnosticsComparisonEntryStatus.DiagnosticsTrackedFrameMissing,
                selectedEntry.LineNumber);
        }

        var ownSnapshot = snapshots
            .Where(snapshot =>
                snapshot.TrackedFrameNumber == trackedFrame &&
                string.Equals(snapshot.SourceRole, "own", StringComparison.OrdinalIgnoreCase))
            .OrderBy(snapshot => snapshot.TrackedFrameTimestampNs)
            .FirstOrDefault();
        if (ownSnapshot is null)
        {
            return TrackerDiagnosticsComparisonEntryComparison.WithStatus(
                TrackerDiagnosticsComparisonEntryStatus.OwnSnapshotMissing,
                selectedEntry.LineNumber);
        }

        var candidates = ApplyFilter(snapshots, selectedSourceFilter).ToArray();
        if (selectedSourceFilter.Kind == TrackerDiagnosticsComparisonSourceFilterKind.All)
        {
            var nonOwnCandidates = candidates
                .Where(snapshot => !string.Equals(snapshot.SourceRole, "own", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (nonOwnCandidates.Length > 0)
            {
                candidates = nonOwnCandidates;
            }
        }

        if (candidates.Length == 0)
        {
            return TrackerDiagnosticsComparisonEntryComparison.WithStatus(
                TrackerDiagnosticsComparisonEntryStatus.NoCandidateSnapshot,
                selectedEntry.LineNumber,
                ownSnapshot.TrackedFrameTimestampNs);
        }

        var nearest = candidates
            .OrderBy(snapshot => Math.Abs(snapshot.TrackedFrameTimestampNs - ownSnapshot.TrackedFrameTimestampNs))
            .ThenBy(snapshot => snapshot.TrackedFrameTimestampNs)
            .First();
        var timestampDeltaNs = Math.Abs(nearest.TrackedFrameTimestampNs - ownSnapshot.TrackedFrameTimestampNs);
        return new TrackerDiagnosticsComparisonEntryComparison(
            TrackerDiagnosticsComparisonEntryStatus.Ready,
            selectedEntry.LineNumber,
            "nearest-timestamp",
            ownSnapshot.TrackedFrameTimestampNs,
            nearest.SourceRole,
            nearest.SourceLabel,
            nearest.TrackedFrameTimestampNs,
            timestampDeltaNs,
            nearest.RawPayloadRestored,
            nearest.BallCount,
            nearest.RobotCount);
    }

    private static IReadOnlyList<TrackerDiagnosticsComparisonSourceOption> CreateSourceOptions(
        IReadOnlyList<ComparisonSnapshot> snapshots)
    {
        var options = new List<TrackerDiagnosticsComparisonSourceOption>
        {
            CreateRoleOption(TrackerDiagnosticsComparisonSourceFilter.All, "All", snapshots),
            CreateRoleOption(TrackerDiagnosticsComparisonSourceFilter.External, "External", snapshots),
            CreateRoleOption(TrackerDiagnosticsComparisonSourceFilter.Own, "Own", snapshots),
            CreateRoleOption(TrackerDiagnosticsComparisonSourceFilter.Unknown, "Unknown", snapshots),
        };
        options.AddRange(snapshots
            .GroupBy(snapshot => snapshot.SourceLabel, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new TrackerDiagnosticsComparisonSourceOption(
                TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel(group.Key),
                group.Key,
                group.Count())));

        return options;
    }

    private static TrackerDiagnosticsComparisonSourceOption CreateRoleOption(
        TrackerDiagnosticsComparisonSourceFilter filter,
        string label,
        IReadOnlyList<ComparisonSnapshot> snapshots)
    {
        var count = filter.Kind == TrackerDiagnosticsComparisonSourceFilterKind.All
            ? snapshots.Count
            : ApplyFilter(snapshots, filter).Count();
        return new TrackerDiagnosticsComparisonSourceOption(filter, label, count);
    }

    private static IEnumerable<ComparisonSnapshot> ApplyFilter(
        IEnumerable<ComparisonSnapshot> snapshots,
        TrackerDiagnosticsComparisonSourceFilter filter)
    {
        return filter.Kind switch
        {
            TrackerDiagnosticsComparisonSourceFilterKind.All => snapshots,
            TrackerDiagnosticsComparisonSourceFilterKind.External => snapshots.Where(snapshot => string.Equals(snapshot.SourceRole, "external", StringComparison.OrdinalIgnoreCase)),
            TrackerDiagnosticsComparisonSourceFilterKind.Own => snapshots.Where(snapshot => string.Equals(snapshot.SourceRole, "own", StringComparison.OrdinalIgnoreCase)),
            TrackerDiagnosticsComparisonSourceFilterKind.Unknown => snapshots.Where(snapshot => string.Equals(snapshot.SourceRole, "unknown", StringComparison.OrdinalIgnoreCase)),
            TrackerDiagnosticsComparisonSourceFilterKind.SourceLabel => snapshots.Where(snapshot => string.Equals(snapshot.SourceLabel, filter.Value, StringComparison.Ordinal)),
            _ => snapshots,
        };
    }

    private static ComparisonSnapshot CreateComparisonSnapshot(TrackerSnapshotReplayInput input)
    {
        var semanticSummary = input.ComparisonSource.SemanticSummary;

        return new ComparisonSnapshot(
            input.ReceivedAt,
            input.SourceRole,
            input.SourceLabel,
            input.TrackedFrameNumber,
            input.TrackedFrameTimestampNs,
            input.ComparisonSource.RawPayloadRestored,
            semanticSummary.BallCount,
            semanticSummary.RobotCount);
    }

    private static string? ResolveMetadataPath(string diagnosticsLogPath)
    {
        return diagnosticsLogPath.EndsWith(DiagnosticsLogSuffix, StringComparison.Ordinal)
            ? string.Concat(diagnosticsLogPath.AsSpan(0, diagnosticsLogPath.Length - DiagnosticsLogSuffix.Length), MetadataSuffix)
            : null;
    }

    private static string? ResolveSidecarPath(CaptureMetadata metadata, string metadataPath)
    {
        if (string.IsNullOrWhiteSpace(metadata.TrackerSnapshotSidecarPath))
        {
            return null;
        }

        var sessionDirectory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidDataException("Capture metadata path must have a parent directory.");
        var captureDirectory = ResolveCaptureDirectory(metadata, sessionDirectory);
        return Path.GetFullPath(Path.IsPathRooted(metadata.TrackerSnapshotSidecarPath)
            ? metadata.TrackerSnapshotSidecarPath
            : Path.Combine(captureDirectory, metadata.TrackerSnapshotSidecarPath));
    }

    private static string ResolveCaptureDirectory(CaptureMetadata metadata, string sessionDirectory)
    {
        if (string.IsNullOrWhiteSpace(metadata.SessionFolder))
        {
            return sessionDirectory;
        }

        var directoryName = Path.GetFileName(
            sessionDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(directoryName, metadata.SessionFolder, StringComparison.Ordinal)
            ? Path.GetDirectoryName(sessionDirectory) ?? sessionDirectory
            : sessionDirectory;
    }

    private static bool TryReadMetadata(string metadataPath, out CaptureMetadata metadata, out string? error)
    {
        try
        {
            metadata = JsonSerializer.Deserialize<CaptureMetadata>(
                    File.ReadAllText(metadataPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new CaptureMetadata();
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            metadata = new CaptureMetadata();
            error = $"Capture metadata could not be read: {ex.Message}";
            return false;
        }
    }

    private sealed record ComparisonSnapshot(
        DateTimeOffset ReceivedAt,
        string SourceRole,
        string SourceLabel,
        uint TrackedFrameNumber,
        long TrackedFrameTimestampNs,
        bool RawPayloadRestored,
        int BallCount,
        int RobotCount);

    private sealed class CaptureMetadata
    {
        public string SessionFolder { get; init; } = "";

        public string TrackerSnapshotSidecarPath { get; init; } = "";

        public TrackerSnapshotLogMetadata? TrackerSnapshotLog { get; init; }
    }

    private sealed class TrackerSnapshotLogMetadata
    {
        public bool IsCreated { get; init; }

        public int RecordCount { get; init; }

        public int SkippedRecordCount { get; init; }

        public int ErrorCount { get; init; }
    }
}

/// <summary>
/// diagnostics comparison sidecar の読み取り状態。
/// </summary>
public enum TrackerDiagnosticsComparisonSidecarStatus
{
    /// <summary>
    /// diagnostics log が未選択。
    /// </summary>
    NoLogSelected,

    /// <summary>
    /// diagnostics log に対応する metadata file がない。
    /// </summary>
    MetadataMissing,

    /// <summary>
    /// metadata JSON を読み取れない。
    /// </summary>
    MetadataCorrupt,

    /// <summary>
    /// metadata 内に tracker snapshot log metadata がない。
    /// </summary>
    SnapshotMetadataMissing,

    /// <summary>
    /// metadata は sidecar 未作成を示している。
    /// </summary>
    SidecarNotCreated,

    /// <summary>
    /// metadata 内に sidecar path がない。
    /// </summary>
    SidecarPathMissing,

    /// <summary>
    /// metadata の sidecar path に file が存在しない。
    /// </summary>
    SidecarMissing,

    /// <summary>
    /// sidecar に record がない。
    /// </summary>
    SidecarEmpty,

    /// <summary>
    /// sidecar JSONL を読み取れない。
    /// </summary>
    SidecarCorrupt,

    /// <summary>
    /// sidecar を読み取り、comparison model を作成できた。
    /// </summary>
    Ready,
}

/// <summary>
/// diagnostics comparison の source filter 種別。
/// </summary>
public enum TrackerDiagnosticsComparisonSourceFilterKind
{
    /// <summary>
    /// すべての source を対象にする。
    /// </summary>
    All,

    /// <summary>
    /// external source のみを対象にする。
    /// </summary>
    External,

    /// <summary>
    /// own source のみを対象にする。
    /// </summary>
    Own,

    /// <summary>
    /// unknown source のみを対象にする。
    /// </summary>
    Unknown,

    /// <summary>
    /// source label が一致する source のみを対象にする。
    /// </summary>
    SourceLabel,
}

/// <summary>
/// diagnostics comparison で選択中の source filter。
/// </summary>
/// <param name="Kind">filter の種別。</param>
/// <param name="Value">source label filter の label。role filter では null。</param>
public sealed record TrackerDiagnosticsComparisonSourceFilter(
    TrackerDiagnosticsComparisonSourceFilterKind Kind,
    string? Value)
{
    /// <summary>
    /// すべての source を対象にする filter。
    /// </summary>
    public static TrackerDiagnosticsComparisonSourceFilter All { get; } = new(
        TrackerDiagnosticsComparisonSourceFilterKind.All,
        Value: null);

    /// <summary>
    /// external source のみを対象にする filter。
    /// </summary>
    public static TrackerDiagnosticsComparisonSourceFilter External { get; } = new(
        TrackerDiagnosticsComparisonSourceFilterKind.External,
        Value: null);

    /// <summary>
    /// own source のみを対象にする filter。
    /// </summary>
    public static TrackerDiagnosticsComparisonSourceFilter Own { get; } = new(
        TrackerDiagnosticsComparisonSourceFilterKind.Own,
        Value: null);

    /// <summary>
    /// unknown source のみを対象にする filter。
    /// </summary>
    public static TrackerDiagnosticsComparisonSourceFilter Unknown { get; } = new(
        TrackerDiagnosticsComparisonSourceFilterKind.Unknown,
        Value: null);

    /// <summary>
    /// 指定 source label のみを対象にする filter を作る。
    /// </summary>
    public static TrackerDiagnosticsComparisonSourceFilter ForSourceLabel(string sourceLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);
        return new TrackerDiagnosticsComparisonSourceFilter(
            TrackerDiagnosticsComparisonSourceFilterKind.SourceLabel,
            sourceLabel);
    }
}

/// <summary>
/// UI が選べる diagnostics comparison source option。
/// </summary>
/// <param name="Filter">この option を選んだときに使う filter。</param>
/// <param name="Label">UI 表示用 label。</param>
/// <param name="RecordCount">filter に一致する snapshot record 数。</param>
public sealed record TrackerDiagnosticsComparisonSourceOption(
    TrackerDiagnosticsComparisonSourceFilter Filter,
    string Label,
    int RecordCount);

/// <summary>
/// diagnostics comparison が選択行を特定するために UI の表示済み entry から受け取る最小 model。
/// </summary>
/// <param name="LineNumber">元 diagnostics log 上の 1 始まり line number。</param>
/// <param name="TrackedFrame">表示済み diagnostics entry の trackedFrame 値。</param>
public sealed record TrackerDiagnosticsComparisonSelectedEntry(
    int LineNumber,
    string TrackedFrame)
{
    /// <summary>
    /// `TrackerDiagnosticsLogReader.ReadFile` が返した表示済み entry から comparison 用 selected-entry model を作る。
    /// </summary>
    public static TrackerDiagnosticsComparisonSelectedEntry FromDiagnosticsEntry(TrackerDiagnosticsLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new TrackerDiagnosticsComparisonSelectedEntry(entry.LineNumber, entry.TrackedFrame);
    }
}

/// <summary>
/// selected diagnostics entry に対する comparison 作成状態。
/// </summary>
public enum TrackerDiagnosticsComparisonEntryStatus
{
    /// <summary>
    /// selected entry の comparison を作成できた。
    /// </summary>
    Ready,

    /// <summary>
    /// diagnostics entry が選択されていない。
    /// </summary>
    NoDiagnosticsEntrySelected,

    /// <summary>
    /// selected entry model に対応する diagnostics entry がない。
    /// </summary>
    DiagnosticsEntryMissing,

    /// <summary>
    /// selected diagnostics entry に tracked frame number がない。
    /// </summary>
    DiagnosticsTrackedFrameMissing,

    /// <summary>
    /// selected diagnostics entry に対応する own snapshot がない。
    /// </summary>
    OwnSnapshotMissing,

    /// <summary>
    /// source filter 後に比較対象 snapshot がない。
    /// </summary>
    NoCandidateSnapshot,
}

/// <summary>
/// selected diagnostics entry と nearest tracker snapshot の comparison summary。
/// </summary>
/// <param name="Status">comparison 作成状態。</param>
/// <param name="EntryLineNumber">selected diagnostics entry の line number。</param>
/// <param name="MatchingRule">snapshot 対応付けに使った規則。現行実装では nearest-timestamp。</param>
/// <param name="IbisOwnSnapshotTimestampNs">基準にした ibis own snapshot の TrackedFrame.timestamp。</param>
/// <param name="NearestSnapshotSourceRole">nearest snapshot の source role。</param>
/// <param name="NearestSnapshotSourceLabel">nearest snapshot の source label。</param>
/// <param name="NearestSnapshotTimestampNs">nearest snapshot の TrackedFrame.timestamp。</param>
/// <param name="TimestampDeltaNs">基準 timestamp と nearest snapshot timestamp の絶対差分。</param>
/// <param name="RawPayloadRestored">nearest snapshot の raw payload を protobuf として復元できる場合は true。</param>
/// <param name="BallCount">nearest snapshot の semantic summary に含まれる ball 数。</param>
/// <param name="RobotCount">nearest snapshot の semantic summary に含まれる robot 数。</param>
public sealed record TrackerDiagnosticsComparisonEntryComparison(
    TrackerDiagnosticsComparisonEntryStatus Status,
    int? EntryLineNumber,
    string? MatchingRule,
    long? IbisOwnSnapshotTimestampNs,
    string? NearestSnapshotSourceRole,
    string? NearestSnapshotSourceLabel,
    long? NearestSnapshotTimestampNs,
    long? TimestampDeltaNs,
    bool? RawPayloadRestored,
    int? BallCount,
    int? RobotCount)
{
    /// <summary>
    /// comparison を作れない理由だけを持つ summary を作る。
    /// </summary>
    public static TrackerDiagnosticsComparisonEntryComparison WithStatus(
        TrackerDiagnosticsComparisonEntryStatus status,
        int? entryLineNumber = null,
        long? ibisOwnSnapshotTimestampNs = null)
    {
        return new TrackerDiagnosticsComparisonEntryComparison(
            status,
            entryLineNumber,
            MatchingRule: null,
            ibisOwnSnapshotTimestampNs,
            NearestSnapshotSourceRole: null,
            NearestSnapshotSourceLabel: null,
            NearestSnapshotTimestampNs: null,
            TimestampDeltaNs: null,
            RawPayloadRestored: null,
            BallCount: null,
            RobotCount: null);
    }
}

/// <summary>
/// diagnostics comparison panel が UI 非依存に参照できる view-state。
/// </summary>
/// <param name="DiagnosticsLogPath">読み込み元 diagnostics log path。</param>
/// <param name="MetadataPath">解決した capture metadata path。</param>
/// <param name="SidecarPath">解決した tracker snapshot sidecar path。</param>
/// <param name="SidecarStatus">sidecar 読み取り状態。</param>
/// <param name="SourceOptions">UI が選択できる source filter option。</param>
/// <param name="SelectedSourceFilter">現在選択中の source filter。</param>
/// <param name="SelectedEntryComparison">selected diagnostics entry の comparison summary。</param>
/// <param name="RecordCount">metadata が示す sidecar record 数。</param>
/// <param name="SkippedRecordCount">metadata が示す skipped record 数。</param>
/// <param name="ErrorCount">metadata が示す error 数。</param>
/// <param name="Error">sidecar status の補足 error message。</param>
public sealed record TrackerDiagnosticsComparisonViewState(
    string? DiagnosticsLogPath,
    string? MetadataPath,
    string? SidecarPath,
    TrackerDiagnosticsComparisonSidecarStatus SidecarStatus,
    IReadOnlyList<TrackerDiagnosticsComparisonSourceOption> SourceOptions,
    TrackerDiagnosticsComparisonSourceFilter SelectedSourceFilter,
    TrackerDiagnosticsComparisonEntryComparison? SelectedEntryComparison,
    int RecordCount,
    int SkippedRecordCount,
    int ErrorCount,
    string? Error)
{
    /// <summary>
    /// sidecar を読めない状態の view-state を作る。
    /// </summary>
    public static TrackerDiagnosticsComparisonViewState Unavailable(
        string? diagnosticsLogPath,
        string? metadataPath,
        string? sidecarPath,
        TrackerDiagnosticsComparisonSidecarStatus sidecarStatus,
        TrackerDiagnosticsComparisonSourceFilter selectedSourceFilter,
        string? error)
    {
        return new TrackerDiagnosticsComparisonViewState(
            diagnosticsLogPath,
            metadataPath,
            sidecarPath,
            sidecarStatus,
            SourceOptions:
            [
                new TrackerDiagnosticsComparisonSourceOption(TrackerDiagnosticsComparisonSourceFilter.All, "All", 0),
                new TrackerDiagnosticsComparisonSourceOption(TrackerDiagnosticsComparisonSourceFilter.External, "External", 0),
                new TrackerDiagnosticsComparisonSourceOption(TrackerDiagnosticsComparisonSourceFilter.Own, "Own", 0),
                new TrackerDiagnosticsComparisonSourceOption(TrackerDiagnosticsComparisonSourceFilter.Unknown, "Unknown", 0),
            ],
            selectedSourceFilter,
            SelectedEntryComparison: null,
            RecordCount: 0,
            SkippedRecordCount: 0,
            ErrorCount: 0,
            error);
    }
}
