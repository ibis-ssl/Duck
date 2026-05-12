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
    private const int SnapshotSchemaVersion = 1;
    private const int MaxCachedIndexes = 2;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Func<string, IReadOnlyList<TrackerPacketSnapshotRecord>> sidecarRecordReader;
    private readonly object cacheLock = new();
    private readonly Dictionary<ComparisonIndexCacheKey, ComparisonSnapshotIndex> indexCache = [];
    private readonly LinkedList<ComparisonIndexCacheKey> indexLru = [];

    /// <summary>
    /// 既定の sidecar JSONL reader で comparison view-state reader を初期化する。
    /// </summary>
    public TrackerDiagnosticsComparisonViewStateReader()
        : this(ReadSidecarRecords)
    {
    }

    internal TrackerDiagnosticsComparisonViewStateReader(
        Func<string, IReadOnlyList<TrackerPacketSnapshotRecord>> sidecarRecordReader)
    {
        this.sidecarRecordReader = sidecarRecordReader;
    }

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

        var sourceOptions = CreateEmptySourceOptions();
        var fieldSourceOptions = CreateEmptyFieldSourceOptions();
        if (metadata.TrackerSnapshotLog is null)
        {
            return CreateState(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath: null,
                TrackerDiagnosticsComparisonSidecarStatus.SnapshotMetadataMissing,
                selectedSourceFilter,
                sourceOptions,
                fieldSourceOptions,
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
                fieldSourceOptions,
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
                fieldSourceOptions,
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
                fieldSourceOptions,
                selectedEntryComparison: null,
                metadata.TrackerSnapshotLog.RecordCount,
                metadata.TrackerSnapshotLog.SkippedRecordCount,
                metadata.TrackerSnapshotLog.ErrorCount,
                "Tracker snapshot sidecar file was not found.");
        }

        ComparisonSnapshotIndex comparisonIndex;
        var cacheKey = ComparisonIndexCacheKey.Create(fullDiagnosticsLogPath, metadataPath, sidecarPath);
        try
        {
            comparisonIndex = GetOrBuildIndex(cacheKey, sidecarPath);
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
                fieldSourceOptions,
                selectedEntryComparison: null,
                metadata.TrackerSnapshotLog.RecordCount,
                metadata.TrackerSnapshotLog.SkippedRecordCount,
                metadata.TrackerSnapshotLog.ErrorCount,
                $"Tracker snapshot sidecar could not be read: {ex.Message}");
        }

        if (comparisonIndex.SnapshotCount == 0)
        {
            return CreateState(
                fullDiagnosticsLogPath,
                metadataPath,
                sidecarPath,
                TrackerDiagnosticsComparisonSidecarStatus.SidecarEmpty,
                selectedSourceFilter,
                sourceOptions,
                fieldSourceOptions,
                selectedEntryComparison: null,
                metadata.TrackerSnapshotLog.RecordCount,
                metadata.TrackerSnapshotLog.SkippedRecordCount,
                metadata.TrackerSnapshotLog.ErrorCount,
                "Tracker snapshot sidecar did not contain records.");
        }

        sourceOptions = comparisonIndex.SourceOptions;
        fieldSourceOptions = comparisonIndex.FieldSourceOptions;
        var selectedEntryComparison = CreateSelectedEntryComparison(
            selectedEntry,
            selectedSourceFilter,
            comparisonIndex);

        return CreateState(
            fullDiagnosticsLogPath,
            metadataPath,
            sidecarPath,
            TrackerDiagnosticsComparisonSidecarStatus.Ready,
            selectedSourceFilter,
            sourceOptions,
            fieldSourceOptions,
            selectedEntryComparison,
            metadata.TrackerSnapshotLog.RecordCount,
            metadata.TrackerSnapshotLog.SkippedRecordCount,
            metadata.TrackerSnapshotLog.ErrorCount,
            error: null);
    }

    /// <summary>
    /// diagnostics log path と selected entry から Field source 用の tracker snapshot frame を読み取る。
    /// </summary>
    public TrackerDiagnosticsFieldSourceFrame LoadFieldSourceFrame(
        string? diagnosticsLogPath,
        TrackerDiagnosticsComparisonSelectedEntry? selectedEntry,
        TrackerDiagnosticsFieldSource fieldSource)
    {
        if (fieldSource.Kind == TrackerDiagnosticsFieldSourceKind.VisionInput)
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.VisionInput,
                fieldSource,
                "Vision Input uses the selected render snapshot.");
        }

        if (fieldSource.Kind == TrackerDiagnosticsFieldSourceKind.IbisTracker)
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.IbisTrackerRenderSnapshot,
                fieldSource,
                "ibis tracker uses the selected render snapshot.");
        }

        if (string.IsNullOrWhiteSpace(diagnosticsLogPath))
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable,
                fieldSource,
                "Diagnostics log is not selected.");
        }

        var fullDiagnosticsLogPath = Path.GetFullPath(diagnosticsLogPath);
        var metadataPath = ResolveMetadataPath(fullDiagnosticsLogPath);
        if (metadataPath is null || !File.Exists(metadataPath))
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable,
                fieldSource,
                "Capture metadata file was not found for this diagnostics log.");
        }

        if (!TryReadMetadata(metadataPath, out var metadata, out var metadataError))
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable,
                fieldSource,
                metadataError);
        }

        var sidecarPath = ResolveSidecarPath(metadata, metadataPath);
        if (metadata.TrackerSnapshotLog is null || !metadata.TrackerSnapshotLog.IsCreated ||
            sidecarPath is null || !File.Exists(sidecarPath))
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable,
                fieldSource,
                "Tracker snapshot sidecar is not available.");
        }

        ComparisonSnapshotIndex comparisonIndex;
        var cacheKey = ComparisonIndexCacheKey.Create(fullDiagnosticsLogPath, metadataPath, sidecarPath);
        try
        {
            comparisonIndex = GetOrBuildIndex(cacheKey, sidecarPath);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or FormatException or InvalidProtocolBufferException)
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable,
                fieldSource,
                $"Tracker snapshot sidecar could not be read: {ex.Message}");
        }

        if (comparisonIndex.SnapshotCount == 0)
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.SidecarUnavailable,
                fieldSource,
                "Tracker snapshot sidecar did not contain records.");
        }

        return CreateFieldSourceFrame(selectedEntry, fieldSource, comparisonIndex);
    }

    private ComparisonSnapshotIndex GetOrBuildIndex(
        ComparisonIndexCacheKey cacheKey,
        string sidecarPath)
    {
        lock (cacheLock)
        {
            if (indexCache.TryGetValue(cacheKey, out var cachedIndex))
            {
                TouchCacheKey(cacheKey);
                return cachedIndex;
            }
        }

        var builtIndex = BuildIndex(sidecarPath);

        lock (cacheLock)
        {
            if (indexCache.TryGetValue(cacheKey, out var cachedIndex))
            {
                TouchCacheKey(cacheKey);
                return cachedIndex;
            }

            indexCache[cacheKey] = builtIndex;
            indexLru.AddFirst(cacheKey);
            while (indexLru.Count > MaxCachedIndexes)
            {
                var keyToRemove = indexLru.Last!.Value;
                indexLru.RemoveLast();
                indexCache.Remove(keyToRemove);
            }
        }

        return builtIndex;
    }

    private void TouchCacheKey(ComparisonIndexCacheKey cacheKey)
    {
        var node = indexLru.Find(cacheKey);
        if (node is null)
        {
            indexLru.AddFirst(cacheKey);
            return;
        }

        indexLru.Remove(node);
        indexLru.AddFirst(node);
    }

    private ComparisonSnapshotIndex BuildIndex(string sidecarPath)
    {
        var snapshots = sidecarRecordReader(sidecarPath)
            .Select(CreateComparisonSnapshot)
            .OrderBy(snapshot => snapshot.TrackedFrameTimestampNs)
            .ThenBy(snapshot => snapshot.ReceivedAt)
            .ToArray();
        return new ComparisonSnapshotIndex(snapshots);
    }

    private static TrackerDiagnosticsComparisonViewState CreateState(
        string? diagnosticsLogPath,
        string? metadataPath,
        string? sidecarPath,
        TrackerDiagnosticsComparisonSidecarStatus sidecarStatus,
        TrackerDiagnosticsComparisonSourceFilter selectedSourceFilter,
        IReadOnlyList<TrackerDiagnosticsComparisonSourceOption> sourceOptions,
        IReadOnlyList<TrackerDiagnosticsFieldSourceOption> fieldSourceOptions,
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
            fieldSourceOptions,
            selectedSourceFilter,
            selectedEntryComparison,
            recordCount,
            skippedRecordCount,
            errorCount,
            error);
    }

    private static IReadOnlyList<TrackerDiagnosticsComparisonSourceOption> CreateEmptySourceOptions()
    {
        return
        [
            new TrackerDiagnosticsComparisonSourceOption(TrackerDiagnosticsComparisonSourceFilter.All, "All", 0),
            new TrackerDiagnosticsComparisonSourceOption(TrackerDiagnosticsComparisonSourceFilter.External, "External", 0),
            new TrackerDiagnosticsComparisonSourceOption(TrackerDiagnosticsComparisonSourceFilter.Own, "Own", 0),
            new TrackerDiagnosticsComparisonSourceOption(TrackerDiagnosticsComparisonSourceFilter.Unknown, "Unknown", 0),
        ];
    }

    private static IReadOnlyList<TrackerDiagnosticsFieldSourceOption> CreateEmptyFieldSourceOptions()
    {
        return
        [
            new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.VisionInput, "Vision Input", 0),
            new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.IbisTracker, "ibis tracker", 0),
            new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.External, "External", 0),
            new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.Unknown, "Unknown", 0),
        ];
    }

    private static IReadOnlyList<TrackerPacketSnapshotRecord> ReadSidecarRecords(string sidecarPath)
    {
        var records = new List<TrackerPacketSnapshotRecord>();
        foreach (var line in File.ReadLines(sidecarPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<TrackerPacketSnapshotRecord>(line, JsonOptions)
                ?? throw new InvalidDataException("Tracker packet snapshot record is empty.");
            if (record.SchemaVersion != SnapshotSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported tracker packet snapshot schema version '{record.SchemaVersion}'.");
            }

            records.Add(record);
        }

        return records;
    }

    private static ComparisonSnapshot CreateComparisonSnapshot(TrackerPacketSnapshotRecord record)
    {
        var semanticSummary = CreateSemanticSummary(record, out var rawPayloadRestored);
        var sourceRole = TrackerPacketSnapshotRecord.NormalizeSourceRole(record.SourceRole);
        var sourceLabel = TrackerPacketSnapshotRecord.NormalizeSourceLabel(
            record.SourceLabel,
            record.SourceName,
            record.SourceUuid,
            record.RemoteEndpoint,
            sourceRole);

        return new ComparisonSnapshot(
            record.ReceivedAt,
            sourceRole,
            sourceLabel,
            record.TrackedFrameNumber,
            record.TrackedFrameTimestampNs,
            rawPayloadRestored,
            semanticSummary.BallCount,
            semanticSummary.RobotCount,
            semanticSummary);
    }

    private static TrackerPacketSnapshotSemanticSummary CreateSemanticSummary(
        TrackerPacketSnapshotRecord record,
        out bool rawPayloadRestored)
    {
        if (record.SemanticSummary is not null)
        {
            rawPayloadRestored = !string.IsNullOrWhiteSpace(record.PayloadBase64);
            return record.SemanticSummary;
        }

        try
        {
            var payload = Convert.FromBase64String(record.PayloadBase64);
            var packet = TrackerWrapperPacket.Parser.ParseFrom(payload);
            rawPayloadRestored = true;
            return TrackerPacketSnapshotSemanticSummary.FromPacket(
                packet,
                TrackerPacketSnapshotRecord.NormalizeSourceRole(record.SourceRole),
                TrackerPacketSnapshotRecord.NormalizeSourceLabel(
                    record.SourceLabel,
                    record.SourceName,
                    record.SourceUuid,
                    record.RemoteEndpoint,
                    record.SourceRole));
        }
        catch (Exception ex) when (ex is FormatException or InvalidProtocolBufferException)
        {
            rawPayloadRestored = false;
            return TrackerPacketSnapshotSemanticSummary.FromRecord(record);
        }
    }

    private static TrackerDiagnosticsComparisonEntryComparison CreateSelectedEntryComparison(
        TrackerDiagnosticsComparisonSelectedEntry? selectedEntry,
        TrackerDiagnosticsComparisonSourceFilter selectedSourceFilter,
        ComparisonSnapshotIndex index)
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

        var ownSnapshot = index.GetOwnSnapshot(trackedFrame);
        if (ownSnapshot is null)
        {
            return TrackerDiagnosticsComparisonEntryComparison.WithStatus(
                TrackerDiagnosticsComparisonEntryStatus.OwnSnapshotMissing,
                selectedEntry.LineNumber);
        }

        var nearest = index.FindNearestCandidate(selectedSourceFilter, ownSnapshot.TrackedFrameTimestampNs);
        if (nearest is null)
        {
            return TrackerDiagnosticsComparisonEntryComparison.WithStatus(
                TrackerDiagnosticsComparisonEntryStatus.NoCandidateSnapshot,
                selectedEntry.LineNumber,
                ownSnapshot.TrackedFrameTimestampNs);
        }

        var timestampDeltaNs = Math.Abs(nearest.TrackedFrameTimestampNs - ownSnapshot.TrackedFrameTimestampNs);
        return new TrackerDiagnosticsComparisonEntryComparison(
            TrackerDiagnosticsComparisonEntryStatus.Ready,
            selectedEntry.LineNumber,
            "nearest-timestamp",
            ownSnapshot.TrackedFrameTimestampNs,
            nearest.SourceRole,
            nearest.SourceLabel,
            nearest.TrackedFrameNumber,
            nearest.TrackedFrameTimestampNs,
            timestampDeltaNs,
            nearest.RawPayloadRestored,
            nearest.BallCount,
            nearest.RobotCount);
    }

    private static TrackerDiagnosticsFieldSourceFrame CreateFieldSourceFrame(
        TrackerDiagnosticsComparisonSelectedEntry? selectedEntry,
        TrackerDiagnosticsFieldSource fieldSource,
        ComparisonSnapshotIndex index)
    {
        if (selectedEntry is null)
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.NoDiagnosticsEntrySelected,
                fieldSource,
                "Diagnostics entry is not selected.");
        }

        if (!uint.TryParse(selectedEntry.TrackedFrame, NumberStyles.Integer, CultureInfo.InvariantCulture, out var trackedFrame))
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.DiagnosticsTrackedFrameMissing,
                fieldSource,
                $"Tracked frame '{selectedEntry.TrackedFrame}' is not numeric.",
                selectedEntry.LineNumber);
        }

        var ownSnapshot = index.GetOwnSnapshot(trackedFrame);
        if (ownSnapshot is null)
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.OwnBaselineMissing,
                fieldSource,
                "ibis own baseline snapshot was not found for the selected diagnostics entry.",
                selectedEntry.LineNumber);
        }

        var nearest = index.FindNearestFieldSourceCandidate(fieldSource, ownSnapshot.TrackedFrameTimestampNs);
        if (nearest is null)
        {
            return TrackerDiagnosticsFieldSourceFrame.WithStatus(
                TrackerDiagnosticsFieldSourceFrameStatus.CandidateMissing,
                fieldSource,
                "No tracker snapshot matched the selected Field source.",
                selectedEntry.LineNumber,
                ownSnapshot.TrackedFrameTimestampNs);
        }

        var timestampDeltaNs = Math.Abs(nearest.TrackedFrameTimestampNs - ownSnapshot.TrackedFrameTimestampNs);
        var status = nearest.BallCount == 0 && nearest.RobotCount == 0
            ? TrackerDiagnosticsFieldSourceFrameStatus.DrawableEmpty
            : TrackerDiagnosticsFieldSourceFrameStatus.Ready;
        return new TrackerDiagnosticsFieldSourceFrame(
            status,
            fieldSource,
            selectedEntry.LineNumber,
            "nearest-timestamp",
            ownSnapshot.TrackedFrameTimestampNs,
            nearest.SourceRole,
            nearest.SourceLabel,
            nearest.TrackedFrameNumber,
            nearest.TrackedFrameTimestampNs,
            timestampDeltaNs,
            nearest.RawPayloadRestored,
            nearest.SemanticSummary,
            status == TrackerDiagnosticsFieldSourceFrameStatus.DrawableEmpty
                ? "Tracker snapshot matched, but it has no drawable balls or robots."
                : null);
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
        int RobotCount,
        TrackerPacketSnapshotSemanticSummary SemanticSummary);

    private sealed class ComparisonSnapshotIndex
    {
        private readonly ComparisonSnapshot[] allSnapshots;
        private readonly ComparisonSnapshot[] externalSnapshots;
        private readonly ComparisonSnapshot[] ownSnapshots;
        private readonly ComparisonSnapshot[] unknownSnapshots;
        private readonly ComparisonSnapshot[] nonOwnSnapshots;
        private readonly IReadOnlyDictionary<uint, ComparisonSnapshot[]> ownSnapshotsByFrame;
        private readonly IReadOnlyDictionary<string, ComparisonSnapshot[]> snapshotsBySourceLabel;

        public ComparisonSnapshotIndex(ComparisonSnapshot[] snapshots)
        {
            allSnapshots = snapshots;
            externalSnapshots = FilterByRole(snapshots, "external");
            ownSnapshots = FilterByRole(snapshots, "own");
            unknownSnapshots = FilterByRole(snapshots, "unknown");
            nonOwnSnapshots = snapshots
                .Where(snapshot => !string.Equals(snapshot.SourceRole, "own", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            ownSnapshotsByFrame = ownSnapshots
                .GroupBy(snapshot => snapshot.TrackedFrameNumber)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(snapshot => snapshot.TrackedFrameTimestampNs).ThenBy(snapshot => snapshot.ReceivedAt).ToArray());
            snapshotsBySourceLabel = snapshots
                .GroupBy(snapshot => snapshot.SourceLabel, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToArray(),
                    StringComparer.Ordinal);
            SourceOptions = CreateSourceOptions();
            FieldSourceOptions = CreateFieldSourceOptions();
        }

        public int SnapshotCount => allSnapshots.Length;

        public IReadOnlyList<TrackerDiagnosticsComparisonSourceOption> SourceOptions { get; }

        public IReadOnlyList<TrackerDiagnosticsFieldSourceOption> FieldSourceOptions { get; }

        public ComparisonSnapshot? GetOwnSnapshot(uint trackedFrame)
        {
            return ownSnapshotsByFrame.TryGetValue(trackedFrame, out var snapshots)
                ? snapshots.FirstOrDefault()
                : null;
        }

        public ComparisonSnapshot? FindNearestCandidate(
            TrackerDiagnosticsComparisonSourceFilter filter,
            long targetTimestampNs)
        {
            var candidates = GetCandidates(filter);
            return candidates.Length == 0 ? null : FindNearest(candidates, targetTimestampNs);
        }

        public ComparisonSnapshot? FindNearestFieldSourceCandidate(
            TrackerDiagnosticsFieldSource fieldSource,
            long targetTimestampNs)
        {
            var filter = fieldSource.ToComparisonFilter();
            return filter is null ? null : FindNearestCandidate(filter, targetTimestampNs);
        }

        private IReadOnlyList<TrackerDiagnosticsComparisonSourceOption> CreateSourceOptions()
        {
            var options = new List<TrackerDiagnosticsComparisonSourceOption>
            {
                new(TrackerDiagnosticsComparisonSourceFilter.All, "All", allSnapshots.Length),
                new(TrackerDiagnosticsComparisonSourceFilter.External, "External", externalSnapshots.Length),
                new(TrackerDiagnosticsComparisonSourceFilter.Own, "Own", ownSnapshots.Length),
                new(TrackerDiagnosticsComparisonSourceFilter.Unknown, "Unknown", unknownSnapshots.Length),
            };
            options.AddRange(snapshotsBySourceLabel
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new TrackerDiagnosticsComparisonSourceOption(
                    TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel(pair.Key),
                    pair.Key,
                    pair.Value.Length)));

            return options;
        }

        private IReadOnlyList<TrackerDiagnosticsFieldSourceOption> CreateFieldSourceOptions()
        {
            var options = new List<TrackerDiagnosticsFieldSourceOption>
            {
                new(TrackerDiagnosticsFieldSource.VisionInput, "Vision Input", 0),
                new(TrackerDiagnosticsFieldSource.IbisTracker, "ibis tracker", ownSnapshots.Length),
                new(TrackerDiagnosticsFieldSource.External, "External", externalSnapshots.Length),
                new(TrackerDiagnosticsFieldSource.Unknown, "Unknown", unknownSnapshots.Length),
            };
            options.AddRange(snapshotsBySourceLabel
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new TrackerDiagnosticsFieldSourceOption(
                    TrackerDiagnosticsFieldSource.ForSourceLabel(pair.Key),
                    pair.Key,
                    pair.Value.Length)));

            return options;
        }

        private ComparisonSnapshot[] GetCandidates(TrackerDiagnosticsComparisonSourceFilter filter)
        {
            return filter.Kind switch
            {
                TrackerDiagnosticsComparisonSourceFilterKind.All =>
                    nonOwnSnapshots.Length > 0 ? nonOwnSnapshots : allSnapshots,
                TrackerDiagnosticsComparisonSourceFilterKind.External => externalSnapshots,
                TrackerDiagnosticsComparisonSourceFilterKind.Own => ownSnapshots,
                TrackerDiagnosticsComparisonSourceFilterKind.Unknown => unknownSnapshots,
                TrackerDiagnosticsComparisonSourceFilterKind.SourceLabel =>
                    filter.Value is not null && snapshotsBySourceLabel.TryGetValue(filter.Value, out var snapshots)
                        ? snapshots
                        : [],
                _ => allSnapshots,
            };
        }

        private static ComparisonSnapshot[] FilterByRole(
            IEnumerable<ComparisonSnapshot> snapshots,
            string role)
        {
            return snapshots
                .Where(snapshot => string.Equals(snapshot.SourceRole, role, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private static ComparisonSnapshot FindNearest(
            IReadOnlyList<ComparisonSnapshot> candidates,
            long targetTimestampNs)
        {
            var insertionIndex = FindInsertionIndex(candidates, targetTimestampNs);
            ComparisonSnapshot? best = null;
            if (insertionIndex < candidates.Count)
            {
                best = candidates[insertionIndex];
            }

            if (insertionIndex > 0)
            {
                var previousTimestampStartIndex = FindTimestampStartIndex(candidates, insertionIndex - 1);
                best = PickNearest(best, candidates[previousTimestampStartIndex], targetTimestampNs);
            }

            return best!;
        }

        private static int FindInsertionIndex(
            IReadOnlyList<ComparisonSnapshot> candidates,
            long targetTimestampNs)
        {
            var lower = 0;
            var upper = candidates.Count;
            while (lower < upper)
            {
                var middle = lower + ((upper - lower) / 2);
                if (candidates[middle].TrackedFrameTimestampNs < targetTimestampNs)
                {
                    lower = middle + 1;
                }
                else
                {
                    upper = middle;
                }
            }

            return lower;
        }

        private static int FindTimestampStartIndex(
            IReadOnlyList<ComparisonSnapshot> candidates,
            int index)
        {
            var timestampNs = candidates[index].TrackedFrameTimestampNs;
            var lower = 0;
            var upper = index;
            while (lower < upper)
            {
                var middle = lower + ((upper - lower) / 2);
                if (candidates[middle].TrackedFrameTimestampNs < timestampNs)
                {
                    lower = middle + 1;
                }
                else
                {
                    upper = middle;
                }
            }

            return lower;
        }

        private static ComparisonSnapshot PickNearest(
            ComparisonSnapshot? current,
            ComparisonSnapshot candidate,
            long targetTimestampNs)
        {
            if (current is null)
            {
                return candidate;
            }

            var currentDelta = Math.Abs(current.TrackedFrameTimestampNs - targetTimestampNs);
            var candidateDelta = Math.Abs(candidate.TrackedFrameTimestampNs - targetTimestampNs);
            if (candidateDelta < currentDelta)
            {
                return candidate;
            }

            return candidateDelta == currentDelta &&
                   candidate.TrackedFrameTimestampNs < current.TrackedFrameTimestampNs
                ? candidate
                : current;
        }
    }

    private sealed record ComparisonIndexCacheKey(
        FileState DiagnosticsLog,
        FileState Metadata,
        FileState Sidecar)
    {
        public static ComparisonIndexCacheKey Create(
            string diagnosticsLogPath,
            string metadataPath,
            string sidecarPath)
        {
            return new ComparisonIndexCacheKey(
                FileState.FromPath(diagnosticsLogPath),
                FileState.FromPath(metadataPath),
                FileState.FromPath(sidecarPath));
        }
    }

    private sealed record FileState(
        string Path,
        bool Exists,
        long LastWriteTimeUtcTicks,
        long Length)
    {
        public static FileState FromPath(string path)
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            var info = new FileInfo(fullPath);
            return info.Exists
                ? new FileState(fullPath, Exists: true, info.LastWriteTimeUtc.Ticks, info.Length)
                : new FileState(fullPath, Exists: false, LastWriteTimeUtcTicks: 0, Length: 0);
        }
    }

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
/// diagnostics Field source selector の source 種別。
/// </summary>
public enum TrackerDiagnosticsFieldSourceKind
{
    /// <summary>
    /// 選択中 render snapshot の raw vision input。
    /// </summary>
    VisionInput,

    /// <summary>
    /// 選択中 render snapshot の ibis tracker output。
    /// </summary>
    IbisTracker,

    /// <summary>
    /// tracker packet sidecar の external source。
    /// </summary>
    External,

    /// <summary>
    /// tracker packet sidecar の unknown source。
    /// </summary>
    Unknown,

    /// <summary>
    /// tracker packet sidecar の source label。
    /// </summary>
    SourceLabel,
}

/// <summary>
/// diagnostics Field に描画する source 選択。
/// </summary>
/// <param name="Kind">Field source の種別。</param>
/// <param name="Value">source label 選択時の label。role 選択では null。</param>
public sealed record TrackerDiagnosticsFieldSource(
    TrackerDiagnosticsFieldSourceKind Kind,
    string? Value)
{
    /// <summary>
    /// 選択中 render snapshot の raw vision input。
    /// </summary>
    public static TrackerDiagnosticsFieldSource VisionInput { get; } = new(
        TrackerDiagnosticsFieldSourceKind.VisionInput,
        Value: null);

    /// <summary>
    /// 選択中 render snapshot の ibis tracker output。
    /// </summary>
    public static TrackerDiagnosticsFieldSource IbisTracker { get; } = new(
        TrackerDiagnosticsFieldSourceKind.IbisTracker,
        Value: null);

    /// <summary>
    /// tracker packet sidecar の external source。
    /// </summary>
    public static TrackerDiagnosticsFieldSource External { get; } = new(
        TrackerDiagnosticsFieldSourceKind.External,
        Value: null);

    /// <summary>
    /// tracker packet sidecar の unknown source。
    /// </summary>
    public static TrackerDiagnosticsFieldSource Unknown { get; } = new(
        TrackerDiagnosticsFieldSourceKind.Unknown,
        Value: null);

    /// <summary>
    /// 指定 source label の Field source を作る。
    /// </summary>
    public static TrackerDiagnosticsFieldSource ForSourceLabel(string sourceLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);
        return new TrackerDiagnosticsFieldSource(
            TrackerDiagnosticsFieldSourceKind.SourceLabel,
            sourceLabel);
    }

    internal TrackerDiagnosticsComparisonSourceFilter? ToComparisonFilter()
    {
        return Kind switch
        {
            TrackerDiagnosticsFieldSourceKind.IbisTracker => TrackerDiagnosticsComparisonSourceFilter.Own,
            TrackerDiagnosticsFieldSourceKind.External => TrackerDiagnosticsComparisonSourceFilter.External,
            TrackerDiagnosticsFieldSourceKind.Unknown => TrackerDiagnosticsComparisonSourceFilter.Unknown,
            TrackerDiagnosticsFieldSourceKind.SourceLabel when Value is not null =>
                TrackerDiagnosticsComparisonSourceFilter.ForSourceLabel(Value),
            _ => null,
        };
    }
}

/// <summary>
/// UI が選べる diagnostics Field source option。
/// </summary>
/// <param name="Source">この option を選んだときに使う Field source。</param>
/// <param name="Label">UI 表示用 label。</param>
/// <param name="RecordCount">source に一致する tracker snapshot record 数。render snapshot source では 0。</param>
public sealed record TrackerDiagnosticsFieldSourceOption(
    TrackerDiagnosticsFieldSource Source,
    string Label,
    int RecordCount);

/// <summary>
/// diagnostics Field source frame の作成状態。
/// </summary>
public enum TrackerDiagnosticsFieldSourceFrameStatus
{
    /// <summary>
    /// Field source frame を作成できた。
    /// </summary>
    Ready,

    /// <summary>
    /// Vision Input は render snapshot から直接描画する。
    /// </summary>
    VisionInput,

    /// <summary>
    /// ibis tracker は render snapshot から直接描画する。
    /// </summary>
    IbisTrackerRenderSnapshot,

    /// <summary>
    /// diagnostics entry が選択されていない。
    /// </summary>
    NoDiagnosticsEntrySelected,

    /// <summary>
    /// selected diagnostics entry の tracked frame が数値ではない。
    /// </summary>
    DiagnosticsTrackedFrameMissing,

    /// <summary>
    /// selected diagnostics entry に対応する own baseline snapshot がない。
    /// </summary>
    OwnBaselineMissing,

    /// <summary>
    /// 選択 Field source に一致する candidate snapshot がない。
    /// </summary>
    CandidateMissing,

    /// <summary>
    /// candidate snapshot はあるが描画可能な ball / robot がない。
    /// </summary>
    DrawableEmpty,

    /// <summary>
    /// tracker packet sidecar が利用できない。
    /// </summary>
    SidecarUnavailable,
}

/// <summary>
/// tracker packet sidecar から解決した diagnostics Field source frame。
/// </summary>
/// <param name="Status">Field source frame の作成状態。</param>
/// <param name="Source">要求された Field source。</param>
/// <param name="EntryLineNumber">selected diagnostics entry の line number。</param>
/// <param name="MatchingRule">snapshot 対応付けに使った規則。</param>
/// <param name="IbisOwnSnapshotTimestampNs">基準にした ibis own snapshot timestamp。</param>
/// <param name="SourceRole">nearest snapshot の source role。</param>
/// <param name="SourceLabel">nearest snapshot の source label。</param>
/// <param name="TrackedFrameNumber">nearest snapshot の tracked frame number。</param>
/// <param name="TrackedFrameTimestampNs">nearest snapshot の tracked frame timestamp。</param>
/// <param name="TimestampDeltaNs">基準 timestamp と nearest snapshot timestamp の絶対差分。</param>
/// <param name="RawPayloadRestored">nearest snapshot の raw payload を protobuf として復元できる場合は true。</param>
/// <param name="SemanticSummary">Field 描画に使う最小 semantic summary。</param>
/// <param name="Message">非 Ready 状態の補足 message。</param>
public sealed record TrackerDiagnosticsFieldSourceFrame(
    TrackerDiagnosticsFieldSourceFrameStatus Status,
    TrackerDiagnosticsFieldSource Source,
    int? EntryLineNumber,
    string? MatchingRule,
    long? IbisOwnSnapshotTimestampNs,
    string? SourceRole,
    string? SourceLabel,
    uint? TrackedFrameNumber,
    long? TrackedFrameTimestampNs,
    long? TimestampDeltaNs,
    bool? RawPayloadRestored,
    TrackerPacketSnapshotSemanticSummary? SemanticSummary,
    string? Message)
{
    /// <summary>
    /// Field source frame を作れない理由だけを持つ状態を作る。
    /// </summary>
    public static TrackerDiagnosticsFieldSourceFrame WithStatus(
        TrackerDiagnosticsFieldSourceFrameStatus status,
        TrackerDiagnosticsFieldSource source,
        string? message,
        int? entryLineNumber = null,
        long? ibisOwnSnapshotTimestampNs = null)
    {
        return new TrackerDiagnosticsFieldSourceFrame(
            status,
            source,
            entryLineNumber,
            MatchingRule: null,
            ibisOwnSnapshotTimestampNs,
            SourceRole: null,
            SourceLabel: null,
            TrackedFrameNumber: null,
            TrackedFrameTimestampNs: null,
            TimestampDeltaNs: null,
            RawPayloadRestored: null,
            SemanticSummary: null,
            message);
    }
}

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
/// <param name="NearestSnapshotTrackedFrameNumber">nearest snapshot の TrackedFrame.frame_number。</param>
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
    uint? NearestSnapshotTrackedFrameNumber,
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
            NearestSnapshotTrackedFrameNumber: null,
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
/// <param name="FieldSourceOptions">左右 Field が選択できる source option。All は含めない。</param>
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
    IReadOnlyList<TrackerDiagnosticsFieldSourceOption> FieldSourceOptions,
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
            FieldSourceOptions:
            [
                new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.VisionInput, "Vision Input", 0),
                new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.IbisTracker, "ibis tracker", 0),
                new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.External, "External", 0),
                new TrackerDiagnosticsFieldSourceOption(TrackerDiagnosticsFieldSource.Unknown, "Unknown", 0),
            ],
            selectedSourceFilter,
            SelectedEntryComparison: null,
            RecordCount: 0,
            SkippedRecordCount: 0,
            ErrorCount: 0,
            error);
    }
}
