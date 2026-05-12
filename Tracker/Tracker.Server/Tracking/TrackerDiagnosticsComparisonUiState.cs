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
    /// source filter を All に戻す。
    /// </summary>
    public void ResetFilter()
    {
        SelectedSourceFilter = TrackerDiagnosticsComparisonSourceFilter.All;
    }

    /// <summary>
    /// selected log と表示済み selected entry から comparison view-state を読み直す。
    /// </summary>
    public void Load(string? diagnosticsLogPath, TrackerDiagnosticsLogEntry? selectedEntry)
    {
        ViewState = reader.Load(
            diagnosticsLogPath,
            ToSelectedEntry(selectedEntry),
            SelectedSourceFilter);

        if (IsSelectedFilterAvailable(ViewState))
        {
            return;
        }

        SelectedSourceFilter = TrackerDiagnosticsComparisonSourceFilter.All;
        ViewState = reader.Load(
            diagnosticsLogPath,
            ToSelectedEntry(selectedEntry),
            SelectedSourceFilter);
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

    private bool IsSelectedFilterAvailable(TrackerDiagnosticsComparisonViewState state)
    {
        return state.SourceOptions.Any(option => option.Filter == SelectedSourceFilter);
    }

    private static TrackerDiagnosticsComparisonSelectedEntry? ToSelectedEntry(TrackerDiagnosticsLogEntry? selectedEntry)
    {
        return selectedEntry is null
            ? null
            : TrackerDiagnosticsComparisonSelectedEntry.FromDiagnosticsEntry(selectedEntry);
    }
}
