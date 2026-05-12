namespace Tracker.Server.Tracking;

/// <summary>
/// diagnostics page が selected log / selected entry / source filter と comparison view-state を同期するための状態。
/// </summary>
public sealed class TrackerDiagnosticsComparisonUiState
{
    private const string AllFilterValue = "role:all";
    private const string ExternalFilterValue = "role:external";
    private const string OwnFilterValue = "role:own";
    private const string UnknownFilterValue = "role:unknown";
    private const string SourceLabelFilterPrefix = "source-label:";
    private const string VisionInputFieldSourceValue = "field:vision-input";
    private const string IbisTrackerFieldSourceValue = "field:ibis-tracker";
    private const string ExternalFieldSourceValue = "field:external";
    private const string UnknownFieldSourceValue = "field:unknown";
    private const string SourceLabelFieldSourcePrefix = "field-source-label:";
    private readonly TrackerDiagnosticsComparisonViewStateReader reader;

    /// <summary>
    /// diagnostics comparison UI の初期状態。
    /// </summary>
    public static TrackerDiagnosticsComparisonViewState InitialViewState { get; } =
        TrackerDiagnosticsComparisonViewState.Unavailable(
            diagnosticsLogPath: null,
            metadataPath: null,
            sidecarPath: null,
            TrackerDiagnosticsComparisonSidecarStatus.NoLogSelected,
            TrackerDiagnosticsComparisonSourceFilter.All,
            "Diagnostics log is not selected.");

    /// <summary>
    /// view-state reader を受け取り、UI 同期状態を初期化する。
    /// </summary>
    public TrackerDiagnosticsComparisonUiState(TrackerDiagnosticsComparisonViewStateReader reader)
    {
        this.reader = reader;
        ViewState = InitialViewState;
        SelectedSourceFilter = TrackerDiagnosticsComparisonSourceFilter.All;
        LeftFieldSource = TrackerDiagnosticsFieldSource.VisionInput;
        RightFieldSource = TrackerDiagnosticsFieldSource.IbisTracker;
    }

    /// <summary>
    /// 現在選択中の source filter。
    /// </summary>
    public TrackerDiagnosticsComparisonSourceFilter SelectedSourceFilter { get; private set; }

    /// <summary>
    /// 現在表示する comparison view-state。
    /// </summary>
    public TrackerDiagnosticsComparisonViewState ViewState { get; private set; }

    /// <summary>
    /// 左 Field の source selector 状態。
    /// </summary>
    public TrackerDiagnosticsFieldSource LeftFieldSource { get; private set; }

    /// <summary>
    /// 右 Field の source selector 状態。
    /// </summary>
    public TrackerDiagnosticsFieldSource RightFieldSource { get; private set; }

    /// <summary>
    /// 左 Field に tracker sidecar source を選んだ場合の解決結果。
    /// </summary>
    public TrackerDiagnosticsFieldSourceFrame? LeftTrackerFieldSourceFrame { get; private set; }

    /// <summary>
    /// 右 Field に tracker sidecar source を選んだ場合の解決結果。
    /// </summary>
    public TrackerDiagnosticsFieldSourceFrame? RightTrackerFieldSourceFrame { get; private set; }

    /// <summary>
    /// Tracker Comparison panel の折り畳み状態。
    /// </summary>
    public bool IsComparisonPanelCollapsed { get; private set; }

    /// <summary>
    /// source filter を All に戻す。
    /// </summary>
    public void ResetFilter()
    {
        SelectedSourceFilter = TrackerDiagnosticsComparisonSourceFilter.All;
    }

    /// <summary>
    /// log file 変更時に Field source selector を既定へ戻す。
    /// </summary>
    public void ResetForLogChange()
    {
        ResetFilter();
        LeftFieldSource = TrackerDiagnosticsFieldSource.VisionInput;
        RightFieldSource = TrackerDiagnosticsFieldSource.IbisTracker;
        LeftTrackerFieldSourceFrame = null;
        RightTrackerFieldSourceFrame = null;
    }

    /// <summary>
    /// Tracker Comparison panel の折り畳み状態を切り替える。
    /// </summary>
    public void ToggleComparisonPanelCollapsed()
    {
        IsComparisonPanelCollapsed = !IsComparisonPanelCollapsed;
    }

    /// <summary>
    /// selected log と表示済み selected entry から comparison view-state を読み直す。
    /// </summary>
    public void Load(string? diagnosticsLogPath, TrackerDiagnosticsLogEntry? selectedEntry)
    {
        var comparisonSelectedEntry = ToSelectedEntry(selectedEntry);
        ViewState = reader.Load(
            diagnosticsLogPath,
            comparisonSelectedEntry,
            SelectedSourceFilter);

        if (IsSelectedFilterAvailable(ViewState))
        {
            RefreshFieldSourceFrames(diagnosticsLogPath, comparisonSelectedEntry);
            return;
        }

        SelectedSourceFilter = TrackerDiagnosticsComparisonSourceFilter.All;
        ViewState = reader.Load(
            diagnosticsLogPath,
            comparisonSelectedEntry,
            SelectedSourceFilter);
        RefreshFieldSourceFrames(diagnosticsLogPath, comparisonSelectedEntry);
    }

    /// <summary>
    /// source filter を直接指定して comparison view-state を読み直す。
    /// </summary>
    public void SelectFilter(
        TrackerDiagnosticsComparisonSourceFilter filter,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        SelectedSourceFilter = filter;
        Load(diagnosticsLogPath, selectedEntry);
    }

    /// <summary>
    /// UI select の option value から source filter を選択し、comparison view-state を読み直す。
    /// </summary>
    public bool SelectFilterValue(
        string? selectedValue,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        if (!TryParseFilterValue(selectedValue, ViewState.SourceOptions, out var filter))
        {
            return false;
        }

        SelectFilter(filter, diagnosticsLogPath, selectedEntry);
        return true;
    }

    /// <summary>
    /// 左 Field source を直接指定して Field source frame を読み直す。
    /// </summary>
    public void SelectLeftFieldSource(
        TrackerDiagnosticsFieldSource source,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        LeftFieldSource = source;
        RefreshFieldSourceFrames(diagnosticsLogPath, ToSelectedEntry(selectedEntry));
    }

    /// <summary>
    /// 右 Field source を直接指定して Field source frame を読み直す。
    /// </summary>
    public void SelectRightFieldSource(
        TrackerDiagnosticsFieldSource source,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        RightFieldSource = source;
        RefreshFieldSourceFrames(diagnosticsLogPath, ToSelectedEntry(selectedEntry));
    }

    /// <summary>
    /// UI select の option value から左 Field source を選択する。
    /// </summary>
    public bool SelectLeftFieldSourceValue(
        string? selectedValue,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        if (!TryParseFieldSourceValue(selectedValue, ViewState.FieldSourceOptions, out var source))
        {
            return false;
        }

        SelectLeftFieldSource(source, diagnosticsLogPath, selectedEntry);
        return true;
    }

    /// <summary>
    /// UI select の option value から右 Field source を選択する。
    /// </summary>
    public bool SelectRightFieldSourceValue(
        string? selectedValue,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        if (!TryParseFieldSourceValue(selectedValue, ViewState.FieldSourceOptions, out var source))
        {
            return false;
        }

        SelectRightFieldSource(source, diagnosticsLogPath, selectedEntry);
        return true;
    }

    /// <summary>
    /// source filter を UI select の option value に変換する。
    /// </summary>
    public static string ToFilterValue(TrackerDiagnosticsComparisonSourceFilter filter)
    {
        return filter.Kind switch
        {
            TrackerDiagnosticsComparisonSourceFilterKind.All => AllFilterValue,
            TrackerDiagnosticsComparisonSourceFilterKind.External => ExternalFilterValue,
            TrackerDiagnosticsComparisonSourceFilterKind.Own => OwnFilterValue,
            TrackerDiagnosticsComparisonSourceFilterKind.Unknown => UnknownFilterValue,
            TrackerDiagnosticsComparisonSourceFilterKind.SourceLabel => SourceLabelFilterPrefix + filter.Value,
            _ => AllFilterValue,
        };
    }

    /// <summary>
    /// Field source を UI select の option value に変換する。
    /// </summary>
    public static string ToFieldSourceValue(TrackerDiagnosticsFieldSource source)
    {
        return source.Kind switch
        {
            TrackerDiagnosticsFieldSourceKind.VisionInput => VisionInputFieldSourceValue,
            TrackerDiagnosticsFieldSourceKind.IbisTracker => IbisTrackerFieldSourceValue,
            TrackerDiagnosticsFieldSourceKind.External => ExternalFieldSourceValue,
            TrackerDiagnosticsFieldSourceKind.Unknown => UnknownFieldSourceValue,
            TrackerDiagnosticsFieldSourceKind.SourceLabel => SourceLabelFieldSourcePrefix + source.Value,
            _ => VisionInputFieldSourceValue,
        };
    }

    private static bool TryParseFilterValue(
        string? selectedValue,
        IReadOnlyList<TrackerDiagnosticsComparisonSourceOption> sourceOptions,
        out TrackerDiagnosticsComparisonSourceFilter filter)
    {
        foreach (var option in sourceOptions)
        {
            if (string.Equals(ToFilterValue(option.Filter), selectedValue, StringComparison.Ordinal))
            {
                filter = option.Filter;
                return true;
            }
        }

        filter = TrackerDiagnosticsComparisonSourceFilter.All;
        return false;
    }

    private static bool TryParseFieldSourceValue(
        string? selectedValue,
        IReadOnlyList<TrackerDiagnosticsFieldSourceOption> sourceOptions,
        out TrackerDiagnosticsFieldSource source)
    {
        foreach (var option in sourceOptions)
        {
            if (string.Equals(ToFieldSourceValue(option.Source), selectedValue, StringComparison.Ordinal))
            {
                source = option.Source;
                return true;
            }
        }

        source = TrackerDiagnosticsFieldSource.VisionInput;
        return false;
    }

    private bool IsSelectedFilterAvailable(TrackerDiagnosticsComparisonViewState state)
    {
        return state.SourceOptions.Any(option => option.Filter == SelectedSourceFilter);
    }

    private void RefreshFieldSourceFrames(
        string? diagnosticsLogPath,
        TrackerDiagnosticsComparisonSelectedEntry? selectedEntry)
    {
        if (!IsFieldSourceAvailable(LeftFieldSource))
        {
            LeftFieldSource = TrackerDiagnosticsFieldSource.VisionInput;
        }

        if (!IsFieldSourceAvailable(RightFieldSource))
        {
            RightFieldSource = TrackerDiagnosticsFieldSource.IbisTracker;
        }

        LeftTrackerFieldSourceFrame = LoadTrackerFieldSourceFrame(diagnosticsLogPath, selectedEntry, LeftFieldSource);
        RightTrackerFieldSourceFrame = LoadTrackerFieldSourceFrame(diagnosticsLogPath, selectedEntry, RightFieldSource);
    }

    private TrackerDiagnosticsFieldSourceFrame? LoadTrackerFieldSourceFrame(
        string? diagnosticsLogPath,
        TrackerDiagnosticsComparisonSelectedEntry? selectedEntry,
        TrackerDiagnosticsFieldSource source)
    {
        return NeedsTrackerFieldSourceFrame(source)
            ? reader.LoadFieldSourceFrame(diagnosticsLogPath, selectedEntry, source)
            : null;
    }

    private bool IsFieldSourceAvailable(TrackerDiagnosticsFieldSource source)
    {
        return ViewState.FieldSourceOptions.Any(option => option.Source == source);
    }

    private static bool NeedsTrackerFieldSourceFrame(TrackerDiagnosticsFieldSource source)
    {
        return source.Kind is TrackerDiagnosticsFieldSourceKind.External
            or TrackerDiagnosticsFieldSourceKind.Unknown
            or TrackerDiagnosticsFieldSourceKind.SourceLabel;
    }

    private static TrackerDiagnosticsComparisonSelectedEntry? ToSelectedEntry(TrackerDiagnosticsLogEntry? selectedEntry)
    {
        return selectedEntry is null
            ? null
            : TrackerDiagnosticsComparisonSelectedEntry.FromDiagnosticsEntry(selectedEntry);
    }
}
