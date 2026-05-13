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
        FieldDisplayMode = TrackerDiagnosticsFieldDisplayMode.Split;
        IsOverlayLayerAVisible = true;
        IsOverlayLayerBVisible = true;
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
    /// Field 表示 mode。
    /// </summary>
    public TrackerDiagnosticsFieldDisplayMode FieldDisplayMode { get; private set; }

    /// <summary>
    /// overlay Layer A の表示状態。
    /// </summary>
    public bool IsOverlayLayerAVisible { get; private set; }

    /// <summary>
    /// overlay Layer B の表示状態。
    /// </summary>
    public bool IsOverlayLayerBVisible { get; private set; }

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
        FieldDisplayMode = TrackerDiagnosticsFieldDisplayMode.Split;
        ResetOverlayLayerVisibility();
    }

    /// <summary>
    /// Tracker Comparison panel の折り畳み状態を切り替える。
    /// </summary>
    public void ToggleComparisonPanelCollapsed()
    {
        IsComparisonPanelCollapsed = !IsComparisonPanelCollapsed;
    }

    /// <summary>
    /// Field 表示 mode を切り替える。
    /// </summary>
    public void SelectFieldDisplayMode(TrackerDiagnosticsFieldDisplayMode displayMode)
    {
        FieldDisplayMode = displayMode;
    }

    /// <summary>
    /// overlay layer の表示状態を設定する。
    /// </summary>
    public void SetOverlayLayerVisibility(
        TrackerDiagnosticsOverlayLayerKey layerKey,
        bool isVisible)
    {
        switch (layerKey)
        {
            case TrackerDiagnosticsOverlayLayerKey.LayerA:
                IsOverlayLayerAVisible = isVisible;
                break;
            case TrackerDiagnosticsOverlayLayerKey.LayerB:
                IsOverlayLayerBVisible = isVisible;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(layerKey), layerKey, null);
        }
    }

    /// <summary>
    /// overlay layer source model を現在の左右 Field source selector から作る。
    /// </summary>
    public IReadOnlyList<TrackerDiagnosticsFieldOverlayLayerSource> CreateOverlayLayerSources()
    {
        if (LeftFieldSource == RightFieldSource)
        {
            return
            [
                new TrackerDiagnosticsFieldOverlayLayerSource(
                    TrackerDiagnosticsOverlayLayerKey.LayerA,
                    "Layer A/B",
                    LeftFieldSource,
                    IsOverlayLayerAVisible,
                    "same source"),
            ];
        }

        return
        [
            new TrackerDiagnosticsFieldOverlayLayerSource(
                TrackerDiagnosticsOverlayLayerKey.LayerA,
                "Layer A",
                LeftFieldSource,
                IsOverlayLayerAVisible),
            new TrackerDiagnosticsFieldOverlayLayerSource(
                TrackerDiagnosticsOverlayLayerKey.LayerB,
                "Layer B",
                RightFieldSource,
                IsOverlayLayerBVisible),
        ];
    }

    /// <summary>
    /// selected log と表示済み selected entry から comparison view-state を読み直す。
    /// </summary>
    public void Load(string? diagnosticsLogPath, TrackerDiagnosticsLogEntry? selectedEntry)
    {
        Load(diagnosticsLogPath, selectedEntry, selectedReplayTimeline: null);
    }

    /// <summary>
    /// selected log、表示済み selected entry、selected replay timeline tick から comparison view-state を読み直す。
    /// </summary>
    public void Load(
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry,
        TrackerDiagnosticsReplayTimelineSelection? selectedReplayTimeline)
    {
        var comparisonSelectedEntry = ToSelectedEntry(selectedEntry);
        ViewState = reader.Load(
            diagnosticsLogPath,
            comparisonSelectedEntry,
            selectedReplayTimeline,
            SelectedSourceFilter);

        if (IsSelectedFilterAvailable(ViewState))
        {
            RefreshFieldSourceFrames(diagnosticsLogPath, comparisonSelectedEntry, selectedReplayTimeline);
            return;
        }

        SelectedSourceFilter = TrackerDiagnosticsComparisonSourceFilter.All;
        ViewState = reader.Load(
            diagnosticsLogPath,
            comparisonSelectedEntry,
            selectedReplayTimeline,
            SelectedSourceFilter);
        RefreshFieldSourceFrames(diagnosticsLogPath, comparisonSelectedEntry, selectedReplayTimeline);
    }

    /// <summary>
    /// source filter を直接指定して comparison view-state を読み直す。
    /// </summary>
    public void SelectFilter(
        TrackerDiagnosticsComparisonSourceFilter filter,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        SelectFilter(filter, diagnosticsLogPath, selectedEntry, selectedReplayTimeline: null);
    }

    /// <summary>
    /// source filter を直接指定して comparison view-state を読み直す。
    /// </summary>
    public void SelectFilter(
        TrackerDiagnosticsComparisonSourceFilter filter,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry,
        TrackerDiagnosticsReplayTimelineSelection? selectedReplayTimeline)
    {
        SelectedSourceFilter = filter;
        Load(diagnosticsLogPath, selectedEntry, selectedReplayTimeline);
    }

    /// <summary>
    /// UI select の option value から source filter を選択し、comparison view-state を読み直す。
    /// </summary>
    public bool SelectFilterValue(
        string? selectedValue,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        return SelectFilterValue(selectedValue, diagnosticsLogPath, selectedEntry, selectedReplayTimeline: null);
    }

    /// <summary>
    /// UI select の option value から source filter を選択し、comparison view-state を読み直す。
    /// </summary>
    public bool SelectFilterValue(
        string? selectedValue,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry,
        TrackerDiagnosticsReplayTimelineSelection? selectedReplayTimeline)
    {
        if (!TryParseFilterValue(selectedValue, ViewState.SourceOptions, out var filter))
        {
            return false;
        }

        SelectFilter(filter, diagnosticsLogPath, selectedEntry, selectedReplayTimeline);
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
        SelectLeftFieldSource(source, diagnosticsLogPath, selectedEntry, selectedReplayTimeline: null);
    }

    /// <summary>
    /// 左 Field source を直接指定して Field source frame を読み直す。
    /// </summary>
    public void SelectLeftFieldSource(
        TrackerDiagnosticsFieldSource source,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry,
        TrackerDiagnosticsReplayTimelineSelection? selectedReplayTimeline)
    {
        LeftFieldSource = source;
        RefreshFieldSourceFrames(diagnosticsLogPath, ToSelectedEntry(selectedEntry), selectedReplayTimeline);
    }

    /// <summary>
    /// 右 Field source を直接指定して Field source frame を読み直す。
    /// </summary>
    public void SelectRightFieldSource(
        TrackerDiagnosticsFieldSource source,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        SelectRightFieldSource(source, diagnosticsLogPath, selectedEntry, selectedReplayTimeline: null);
    }

    /// <summary>
    /// 右 Field source を直接指定して Field source frame を読み直す。
    /// </summary>
    public void SelectRightFieldSource(
        TrackerDiagnosticsFieldSource source,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry,
        TrackerDiagnosticsReplayTimelineSelection? selectedReplayTimeline)
    {
        RightFieldSource = source;
        RefreshFieldSourceFrames(diagnosticsLogPath, ToSelectedEntry(selectedEntry), selectedReplayTimeline);
    }

    /// <summary>
    /// UI select の option value から左 Field source を選択する。
    /// </summary>
    public bool SelectLeftFieldSourceValue(
        string? selectedValue,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry)
    {
        return SelectLeftFieldSourceValue(selectedValue, diagnosticsLogPath, selectedEntry, selectedReplayTimeline: null);
    }

    /// <summary>
    /// UI select の option value から左 Field source を選択する。
    /// </summary>
    public bool SelectLeftFieldSourceValue(
        string? selectedValue,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry,
        TrackerDiagnosticsReplayTimelineSelection? selectedReplayTimeline)
    {
        if (!TryParseFieldSourceValue(selectedValue, ViewState.FieldSourceOptions, out var source))
        {
            return false;
        }

        SelectLeftFieldSource(source, diagnosticsLogPath, selectedEntry, selectedReplayTimeline);
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
        return SelectRightFieldSourceValue(selectedValue, diagnosticsLogPath, selectedEntry, selectedReplayTimeline: null);
    }

    /// <summary>
    /// UI select の option value から右 Field source を選択する。
    /// </summary>
    public bool SelectRightFieldSourceValue(
        string? selectedValue,
        string? diagnosticsLogPath,
        TrackerDiagnosticsLogEntry? selectedEntry,
        TrackerDiagnosticsReplayTimelineSelection? selectedReplayTimeline)
    {
        if (!TryParseFieldSourceValue(selectedValue, ViewState.FieldSourceOptions, out var source))
        {
            return false;
        }

        SelectRightFieldSource(source, diagnosticsLogPath, selectedEntry, selectedReplayTimeline);
        return true;
    }

    private void ResetOverlayLayerVisibility()
    {
        IsOverlayLayerAVisible = true;
        IsOverlayLayerBVisible = true;
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
        TrackerDiagnosticsComparisonSelectedEntry? selectedEntry,
        TrackerDiagnosticsReplayTimelineSelection? selectedReplayTimeline)
    {
        if (!IsFieldSourceAvailable(LeftFieldSource))
        {
            LeftFieldSource = TrackerDiagnosticsFieldSource.VisionInput;
        }

        if (!IsFieldSourceAvailable(RightFieldSource))
        {
            RightFieldSource = TrackerDiagnosticsFieldSource.IbisTracker;
        }

        LeftTrackerFieldSourceFrame = LoadTrackerFieldSourceFrame(
            diagnosticsLogPath,
            selectedEntry,
            selectedReplayTimeline,
            LeftFieldSource);
        RightTrackerFieldSourceFrame = LoadTrackerFieldSourceFrame(
            diagnosticsLogPath,
            selectedEntry,
            selectedReplayTimeline,
            RightFieldSource);
    }

    private TrackerDiagnosticsFieldSourceFrame? LoadTrackerFieldSourceFrame(
        string? diagnosticsLogPath,
        TrackerDiagnosticsComparisonSelectedEntry? selectedEntry,
        TrackerDiagnosticsReplayTimelineSelection? selectedReplayTimeline,
        TrackerDiagnosticsFieldSource source)
    {
        return NeedsTrackerFieldSourceFrame(source)
            ? reader.LoadFieldSourceFrame(diagnosticsLogPath, selectedEntry, selectedReplayTimeline, source)
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

/// <summary>
/// diagnostics Field の表示 mode。
/// </summary>
public enum TrackerDiagnosticsFieldDisplayMode
{
    /// <summary>
    /// 左右 Field を別々に表示する。
    /// </summary>
    Split,

    /// <summary>
    /// 左右 Field source を同一 Field に重ねて表示する。
    /// </summary>
    Overlay,
}

/// <summary>
/// diagnostics Field overlay の layer 識別子。
/// </summary>
public enum TrackerDiagnosticsOverlayLayerKey
{
    /// <summary>
    /// 左 Field source selector に対応する layer。
    /// </summary>
    LayerA,

    /// <summary>
    /// 右 Field source selector に対応する layer。
    /// </summary>
    LayerB,
}

/// <summary>
/// diagnostics Field overlay layer の source selection model。
/// </summary>
/// <param name="LayerKey">layer 識別子。</param>
/// <param name="LayerName">UI 表示用 layer 名。</param>
/// <param name="Source">layer に割り当てる Field source。</param>
/// <param name="IsVisible">layer が表示対象かどうか。</param>
/// <param name="LegendNote">legend に追加表示する短い補足。</param>
public sealed record TrackerDiagnosticsFieldOverlayLayerSource(
    TrackerDiagnosticsOverlayLayerKey LayerKey,
    string LayerName,
    TrackerDiagnosticsFieldSource Source,
    bool IsVisible,
    string? LegendNote = null);
