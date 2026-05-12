using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tracker.Server.Components.Vision;
using Tracker.Server.Tracking;

namespace Tracker.Server.Components.Pages;

/// <summary>
/// diagnostics log の選択、timeline、render snapshot、profile metadata modal を同期するページ状態。
/// </summary>
public partial class Diagnostics : IDisposable
{
    private IReadOnlyList<TrackerDiagnosticsLogFile> logFiles = [];
    private TrackerDiagnosticsLogSnapshot? snapshot;
    private IReadOnlyList<TrackerDiagnosticsLogEntry> entries = [];
    private string? selectedLogPath;
    private TrackerDiagnosticsLogEntry? selectedEntry;
    private TrackerRenderSnapshotView? selectedRenderSnapshot;
    private IReadOnlyDictionary<uint, TrackerRenderSnapshotView> renderSnapshotsByFrame =
        new Dictionary<uint, TrackerRenderSnapshotView>();
    private string? renderSnapshotError;
    private DiagnosticsProfileMetadataIndex profileMetadataIndex =
        DiagnosticsProfileMetadataIndex.Empty;
    private DiagnosticsProfileMetadataView? profileMetadata;
    private string? profileMetadataError;
    private bool isProfileSettingsModalOpen;
    private bool isRenderResizeActive;
    private double renderAreaHeightRem = DiagnosticsRenderLayoutState.DefaultHeightRem;
    private double renderResizeStartHeightRem;
    private double renderResizeStartY;
    private bool isTimelineResizeActive;
    private double timelineWidthRem = DiagnosticsRenderLayoutState.DefaultTimelineWidthRem;
    private double timelineResizeStartWidthRem;
    private double timelineResizeStartX;
    private CancellationTokenSource? playbackCancellationTokenSource;
    private DiagnosticsPlaybackMode playbackMode = DiagnosticsPlaybackMode.Stopped;
    private int fastForwardSpeedMultiplier = DiagnosticsPlaybackState.DefaultFastForwardSpeedMultiplier;
    private TrackerDiagnosticsComparisonUiState? comparisonUiState;

    private int MaxEntryIndex => Math.Max(0, entries.Count - 1);

    private int SelectedEntryIndex => selectedEntry is null
        ? 0
        : FindEntryIndex(selectedEntry);

    private bool CanPlayback => entries.Count > 1;

    private TrackerDiagnosticsComparisonViewState ComparisonViewState =>
        comparisonUiState?.ViewState ?? TrackerDiagnosticsComparisonUiState.InitialViewState;

    private TrackedVisionViewState trackedRenderView =>
        selectedRenderSnapshot is null
            ? TrackedVisionViewState.FromSnapshot(new TrackedSnapshot(null, null, "-", 0, 0))
            : TrackedVisionViewState.FromSnapshot(new TrackedSnapshot(
                selectedRenderSnapshot.Frame,
                selectedRenderSnapshot.ReceivedAt,
                selectedRenderSnapshot.Frame.Metadata.ProfileName ?? "-",
                0,
                0));

    /// <summary>
    /// 初回表示時に log 一覧と選択中 log の内容を読み込む。
    /// </summary>
    protected override void OnInitialized()
    {
        comparisonUiState = new TrackerDiagnosticsComparisonUiState(ComparisonReader);
        LoadFiles();
        LoadSelectedFile();
    }

    private Task ReloadAsync()
    {
        StopPlayback();
        LoadFiles();
        LoadSelectedFile();
        return Task.CompletedTask;
    }

    private Task OnLogFileChanged(ChangeEventArgs args)
    {
        var requestedLogPath = args.Value?.ToString();
        selectedLogPath = logFiles.FirstOrDefault(file => file.FullPath == requestedLogPath)?.FullPath;
        StopPlayback();
        comparisonUiState?.ResetForLogChange();
        LoadSelectedFile();
        return Task.CompletedTask;
    }

    private void LoadFiles()
    {
        logFiles = LogReader.ListFiles();
        if (selectedLogPath is null || !logFiles.Any(file => file.FullPath == selectedLogPath))
        {
            selectedLogPath = logFiles.FirstOrDefault()?.FullPath;
        }
    }

    // log 選択時は entry、profile metadata、render snapshot index、選択 snapshot を同じ log に同期する。
    private void LoadSelectedFile()
    {
        if (selectedLogPath is null)
        {
            snapshot = null;
            entries = [];
            selectedEntry = null;
            selectedRenderSnapshot = null;
            renderSnapshotsByFrame = new Dictionary<uint, TrackerRenderSnapshotView>();
            renderSnapshotError = null;
            profileMetadataIndex = DiagnosticsProfileMetadataIndex.Empty;
            profileMetadata = null;
            profileMetadataError = null;
            SyncComparisonState();
            return;
        }

        StopPlayback();
        snapshot = LogReader.ReadFile(selectedLogPath);
        entries = snapshot.Entries;
        selectedEntry = entries.FirstOrDefault();
        LoadProfileMetadataIndex();
        UpdateProfileMetadataForSelectedEntry();
        LoadRenderSnapshotIndex();
        LoadSelectedRenderSnapshot();
        SyncComparisonState();
    }

    // timeline / scrubber の選択 entry 変更に合わせて、metadata 表示と render snapshot を同じ frame に同期する。
    private void SelectEntry(TrackerDiagnosticsLogEntry entry)
    {
        selectedEntry = entry;
        UpdateProfileMetadataForSelectedEntry();
        LoadSelectedRenderSnapshot();
        SyncComparisonState();
    }

    private void OnTimelineScrubbed(ChangeEventArgs args)
    {
        StopPlayback();

        if (!int.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var index))
        {
            return;
        }

        SelectEntryByIndex(index);
    }

    private void OnTimelineWheel(WheelEventArgs args)
    {
        StopPlayback();

        if (entries.Count == 0)
        {
            return;
        }

        var step = args.CtrlKey ? 100 : args.ShiftKey ? 10 : 1;
        var direction = args.DeltaY > 0 || args.DeltaX > 0 ? 1 : -1;
        SelectEntryByIndex(SelectedEntryIndex + (direction * step));
    }

    // index 指定の選択更新でも click 選択と同じ同期順序を維持する。
    private void SelectEntryByIndex(int index)
    {
        if (entries.Count == 0)
        {
            selectedEntry = null;
            selectedRenderSnapshot = null;
            renderSnapshotsByFrame = new Dictionary<uint, TrackerRenderSnapshotView>();
            renderSnapshotError = null;
            profileMetadata = null;
            SyncComparisonState();
            return;
        }

        selectedEntry = entries[Math.Clamp(index, 0, entries.Count - 1)];
        UpdateProfileMetadataForSelectedEntry();
        LoadSelectedRenderSnapshot();
        SyncComparisonState();
    }

    private int FindEntryIndex(TrackerDiagnosticsLogEntry entry)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (ReferenceEquals(entries[index], entry))
            {
                return index;
            }
        }

        return 0;
    }

    private string ShellClass()
    {
        return selectedRenderSnapshot is null
            ? "diagnostics-shell"
            : "diagnostics-shell diagnostics-shell--render";
    }

    private string TimelineItemClass(TrackerDiagnosticsLogEntry entry)
    {
        var classes = new List<string> { "diagnostics-timeline__item" };
        if (ReferenceEquals(entry, selectedEntry))
        {
            classes.Add("is-selected");
        }

        return string.Join(" ", classes);
    }

    private string RenderAreaStyle()
    {
        return DiagnosticsRenderLayoutState.ToCssVariable(renderAreaHeightRem);
    }

    private string TimelineAreaStyle()
    {
        return DiagnosticsRenderLayoutState.ToTimelineCssVariable(timelineWidthRem);
    }

    private string RenderResizeHandleClass()
    {
        return isRenderResizeActive
            ? "diagnostics-render-resizer is-active"
            : "diagnostics-render-resizer";
    }

    private string TimelineResizeHandleClass()
    {
        return isTimelineResizeActive
            ? "diagnostics-timeline-resizer is-active"
            : "diagnostics-timeline-resizer";
    }

    private void OnRenderResizeStart(MouseEventArgs args)
    {
        isRenderResizeActive = true;
        renderResizeStartY = args.ClientY;
        renderResizeStartHeightRem = renderAreaHeightRem;
    }

    private void OnRenderResizeMove(MouseEventArgs args)
    {
        if (!isRenderResizeActive)
        {
            return;
        }

        renderAreaHeightRem = DiagnosticsRenderLayoutState.ApplyDragDeltaRem(
            renderResizeStartHeightRem,
            args.ClientY - renderResizeStartY);
    }

    private void OnRenderResizeEnd(MouseEventArgs _)
    {
        isRenderResizeActive = false;
    }

    private void OnRenderResizeKeyDown(KeyboardEventArgs args)
    {
        renderAreaHeightRem = args.Key switch
        {
            "ArrowUp" => DiagnosticsRenderLayoutState.ClampHeightRem(renderAreaHeightRem - 2),
            "ArrowDown" => DiagnosticsRenderLayoutState.ClampHeightRem(renderAreaHeightRem + 2),
            "Home" => DiagnosticsRenderLayoutState.MinHeightRem,
            "End" => DiagnosticsRenderLayoutState.MaxHeightRem,
            _ => renderAreaHeightRem,
        };
    }

    private void OnTimelineResizeStart(MouseEventArgs args)
    {
        isTimelineResizeActive = true;
        timelineResizeStartX = args.ClientX;
        timelineResizeStartWidthRem = timelineWidthRem;
    }

    private void OnTimelineResizeMove(MouseEventArgs args)
    {
        if (!isTimelineResizeActive)
        {
            return;
        }

        timelineWidthRem = DiagnosticsRenderLayoutState.ApplyTimelineDragDeltaRem(
            timelineResizeStartWidthRem,
            args.ClientX - timelineResizeStartX);
    }

    private void OnTimelineResizeEnd(MouseEventArgs _)
    {
        isTimelineResizeActive = false;
    }

    private void OnTimelineResizeKeyDown(KeyboardEventArgs args)
    {
        timelineWidthRem = args.Key switch
        {
            "ArrowLeft" => DiagnosticsRenderLayoutState.ClampTimelineWidthRem(timelineWidthRem - 2),
            "ArrowRight" => DiagnosticsRenderLayoutState.ClampTimelineWidthRem(timelineWidthRem + 2),
            "Home" => DiagnosticsRenderLayoutState.MinTimelineWidthRem,
            "End" => DiagnosticsRenderLayoutState.MaxTimelineWidthRem,
            _ => timelineWidthRem,
        };
    }

    private static string FormatTime(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string Display(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "-";
    }

    private static string Display(uint? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "-";
    }

    private static string Display(long? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "-";
    }

    private static string DisplayRawPayloadRestored(bool? value)
    {
        return value switch
        {
            true => "Restored",
            false => "Missing",
            _ => "-",
        };
    }

    private string ComparisonSourceFilterValue()
    {
        return TrackerDiagnosticsComparisonUiState.ToFilterValue(ComparisonViewState.SelectedSourceFilter);
    }

    private string LeftFieldSourceValue()
    {
        return TrackerDiagnosticsComparisonUiState.ToFieldSourceValue(
            comparisonUiState?.LeftFieldSource ?? TrackerDiagnosticsFieldSource.VisionInput);
    }

    private string RightFieldSourceValue()
    {
        return TrackerDiagnosticsComparisonUiState.ToFieldSourceValue(
            comparisonUiState?.RightFieldSource ?? TrackerDiagnosticsFieldSource.IbisTracker);
    }

    private string ComparisonPanelToggleLabel()
    {
        return comparisonUiState?.IsComparisonPanelCollapsed == true ? "Show" : "Hide";
    }

    private Task ToggleComparisonPanelAsync()
    {
        comparisonUiState?.ToggleComparisonPanelCollapsed();
        return Task.CompletedTask;
    }

    private Task OnComparisonSourceFilterChanged(ChangeEventArgs args)
    {
        comparisonUiState?.SelectFilterValue(args.Value?.ToString(), selectedLogPath, selectedEntry);
        return Task.CompletedTask;
    }

    private Task OnLeftFieldSourceChanged(ChangeEventArgs args)
    {
        comparisonUiState?.SelectLeftFieldSourceValue(args.Value?.ToString(), selectedLogPath, selectedEntry);
        return Task.CompletedTask;
    }

    private Task OnRightFieldSourceChanged(ChangeEventArgs args)
    {
        comparisonUiState?.SelectRightFieldSourceValue(args.Value?.ToString(), selectedLogPath, selectedEntry);
        return Task.CompletedTask;
    }

    private DiagnosticsFieldRenderModel LeftFieldRenderModel()
    {
        return CreateFieldRenderModel(
            comparisonUiState?.LeftFieldSource ?? TrackerDiagnosticsFieldSource.VisionInput,
            comparisonUiState?.LeftTrackerFieldSourceFrame);
    }

    private DiagnosticsFieldRenderModel RightFieldRenderModel()
    {
        return CreateFieldRenderModel(
            comparisonUiState?.RightFieldSource ?? TrackerDiagnosticsFieldSource.IbisTracker,
            comparisonUiState?.RightTrackerFieldSourceFrame);
    }

    private DiagnosticsFieldRenderModel CreateFieldRenderModel(
        TrackerDiagnosticsFieldSource source,
        TrackerDiagnosticsFieldSourceFrame? trackerFrame)
    {
        if (selectedRenderSnapshot is null)
        {
            return DiagnosticsFieldRenderModel.Empty(FieldSourceLabel(source), null, "Render snapshot was not found.");
        }

        var geometry = DiagnosticsFieldViewFactory.CreateGeometry(selectedRenderSnapshot.Frame.GeometrySnapshot);
        return source.Kind switch
        {
            TrackerDiagnosticsFieldSourceKind.VisionInput => new DiagnosticsFieldRenderModel(
                FieldSourceLabel(source),
                geometry,
                DiagnosticsFieldViewFactory.CreateRawBalls(selectedRenderSnapshot.Frame),
                DiagnosticsFieldViewFactory.CreateRawYellowRobots(selectedRenderSnapshot.Frame),
                DiagnosticsFieldViewFactory.CreateRawBlueRobots(selectedRenderSnapshot.Frame),
                Status: null),
            TrackerDiagnosticsFieldSourceKind.IbisTracker => new DiagnosticsFieldRenderModel(
                FieldSourceLabel(source),
                trackedRenderView.Geometry,
                trackedRenderView.Balls,
                trackedRenderView.RobotsYellow,
                trackedRenderView.RobotsBlue,
                Status: null),
            _ => new DiagnosticsFieldRenderModel(
                FieldSourceLabel(source),
                geometry,
                DiagnosticsFieldViewFactory.CreateTrackerSourceBalls(trackerFrame?.SemanticSummary),
                DiagnosticsFieldViewFactory.CreateTrackerSourceYellowRobots(trackerFrame?.SemanticSummary),
                DiagnosticsFieldViewFactory.CreateTrackerSourceBlueRobots(trackerFrame?.SemanticSummary),
                FieldSourceStatusText(trackerFrame)),
        };
    }

    private string FieldSourceLabel(TrackerDiagnosticsFieldSource source)
    {
        return ComparisonViewState.FieldSourceOptions
            .FirstOrDefault(option => option.Source == source)
            ?.Label ?? source.Kind.ToString();
    }

    private static string? FieldSourceStatusText(TrackerDiagnosticsFieldSourceFrame? frame)
    {
        if (frame is null || frame.Status == TrackerDiagnosticsFieldSourceFrameStatus.Ready)
        {
            return null;
        }

        if (frame.Status == TrackerDiagnosticsFieldSourceFrameStatus.DrawableEmpty)
        {
            return frame.Message ?? "No drawable balls or robots.";
        }

        return frame.Message ?? frame.Status.ToString();
    }

    private Task OnFastForwardSpeedChanged(ChangeEventArgs args)
    {
        if (!int.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var speedMultiplier))
        {
            return Task.CompletedTask;
        }

        fastForwardSpeedMultiplier = DiagnosticsPlaybackState.NormalizeSpeedMultiplier(speedMultiplier);
        if (playbackMode == DiagnosticsPlaybackMode.FastForward)
        {
            return StartPlaybackAsync(DiagnosticsPlaybackMode.FastForward);
        }

        return Task.CompletedTask;
    }

    private void SyncComparisonState()
    {
        comparisonUiState?.Load(selectedLogPath, selectedEntry);
    }

    private Task StartPlaybackAsync(DiagnosticsPlaybackMode mode)
    {
        if (!CanPlayback)
        {
            return Task.CompletedTask;
        }

        StopPlayback();
        playbackMode = mode;
        playbackCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = playbackCancellationTokenSource.Token;
        var speedMultiplier = GetPlaybackSpeedMultiplier(mode);
        _ = RunPlaybackAsync(mode, speedMultiplier, cancellationToken);
        return Task.CompletedTask;
    }

    private async Task RunPlaybackAsync(
        DiagnosticsPlaybackMode mode,
        int speedMultiplier,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var currentIndex = SelectedEntryIndex;
                var nextIndex = DiagnosticsPlaybackState.GetNextIndex(
                    currentIndex,
                    entries.Count,
                    mode,
                    speedMultiplier);
                var interval = GetPlaybackInterval(mode, currentIndex, nextIndex, speedMultiplier);
                await Task.Delay(interval, cancellationToken);

                await InvokeAsync(() =>
                {
                    if (!DiagnosticsPlaybackState.ShouldApplyTick(
                            playbackMode,
                            mode,
                            cancellationToken.IsCancellationRequested,
                            GetPlaybackSpeedMultiplier(playbackMode),
                            speedMultiplier))
                    {
                        return;
                    }

                    if (!CanPlayback)
                    {
                        StopPlayback();
                        return;
                    }

                    var nextIndex = DiagnosticsPlaybackState.GetNextIndex(
                        SelectedEntryIndex,
                        entries.Count,
                        mode,
                        speedMultiplier);

                    if (DiagnosticsPlaybackState.ShouldStopAtEnd(nextIndex, entries.Count))
                    {
                        StopPlayback();
                        SelectEntryByIndex(DiagnosticsPlaybackState.GetIndexAfterEndHandling(
                            nextIndex,
                            entries.Count));
                    }
                    else
                    {
                        SelectEntryByIndex(nextIndex);
                    }

                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private TimeSpan GetPlaybackInterval(
        DiagnosticsPlaybackMode mode,
        int currentIndex,
        int nextIndex,
        int speedMultiplier)
    {
        if (entries.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var current = entries[Math.Clamp(currentIndex, 0, entries.Count - 1)];
        var next = entries[Math.Clamp(nextIndex, 0, entries.Count - 1)];
        return DiagnosticsPlaybackState.GetInterval(mode, current.Timestamp, next.Timestamp, speedMultiplier);
    }

    private int GetPlaybackSpeedMultiplier(DiagnosticsPlaybackMode mode)
    {
        return mode == DiagnosticsPlaybackMode.FastForward
            ? fastForwardSpeedMultiplier
            : DiagnosticsPlaybackState.DefaultFastForwardSpeedMultiplier;
    }

    private void StopPlayback()
    {
        playbackMode = DiagnosticsPlaybackMode.Stopped;
        playbackCancellationTokenSource?.Cancel();
        playbackCancellationTokenSource?.Dispose();
        playbackCancellationTokenSource = null;
    }

    public void Dispose()
    {
        StopPlayback();
    }

    // 選択 entry の tracked frame 番号で render snapshot sidecar を引き、shell class と field 表示を同期する。
    private void LoadSelectedRenderSnapshot()
    {
        if (selectedLogPath is null || selectedEntry is null)
        {
            selectedRenderSnapshot = null;
            renderSnapshotError = null;
            return;
        }

        if (!uint.TryParse(selectedEntry.TrackedFrame, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameNumber))
        {
            selectedRenderSnapshot = null;
            renderSnapshotError = $"Tracked frame '{selectedEntry.TrackedFrame}' is not a numeric frame number.";
            return;
        }

        if (renderSnapshotsByFrame.TryGetValue(frameNumber, out var renderSnapshot))
        {
            selectedRenderSnapshot = renderSnapshot;
            renderSnapshotError = null;
            return;
        }

        selectedRenderSnapshot = null;
        renderSnapshotError = $"Render snapshot for tracked frame '{selectedEntry.TrackedFrame}' was not found.";
    }

    // 選択 log に対応する capture sidecar metadata を読み直し、古い modal 表示を閉じられる状態へ戻す。
    private void LoadProfileMetadataIndex()
    {
        profileMetadataIndex = DiagnosticsProfileMetadataLoader.Load(selectedLogPath);
        profileMetadata = null;
        profileMetadataError = profileMetadataIndex.Error;
    }

    // 選択 entry の profile 名に合わせて modal 用 metadata を作り直し、metadata 不整合時は modal を閉じる。
    private void UpdateProfileMetadataForSelectedEntry()
    {
        profileMetadata = null;

        if (!string.IsNullOrEmpty(profileMetadataError) || selectedEntry is null)
        {
            isProfileSettingsModalOpen = false;
            return;
        }

        var profileName = string.IsNullOrWhiteSpace(selectedEntry.ProfileName)
            ? profileMetadataIndex.ActiveProfileName ?? "-"
            : selectedEntry.ProfileName;
        var configuredProfile = profileMetadataIndex.ConfiguredProfilesByName.TryGetValue(profileName, out var profileJson)
            ? profileJson
            : $"Profile '{profileName}' was not found in metadata.";

        profileMetadata = new DiagnosticsProfileMetadataView(
            profileName,
            configuredProfile,
            profileMetadataIndex.ResolvedSettingsJson ?? "Resolved settings were not found in metadata.");
    }

    private void OpenProfileSettings()
    {
        if (profileMetadata is not null)
        {
            isProfileSettingsModalOpen = true;
        }
    }

    private void CloseProfileSettings()
    {
        isProfileSettingsModalOpen = false;
    }

    // 選択 log の diagnostics sidecar から render snapshot index を読み、frame 番号 lookup を更新する。
    private void LoadRenderSnapshotIndex()
    {
        renderSnapshotsByFrame = new Dictionary<uint, TrackerRenderSnapshotView>();
        renderSnapshotError = null;

        if (selectedLogPath is null)
        {
            return;
        }

        var result = RenderSnapshotReader.ReadIndex(selectedLogPath);
        if (result.Index is null)
        {
            renderSnapshotError = result.Error;
            return;
        }

        renderSnapshotsByFrame = result.Index.SnapshotsByFrame;
    }

    private sealed record DiagnosticsFieldRenderModel(
        string Title,
        SSL_GeometryData? Geometry,
        IReadOnlyList<SSL_DetectionBall> Balls,
        IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
        IReadOnlyList<SSL_DetectionRobot> RobotsBlue,
        string? Status)
    {
        public static DiagnosticsFieldRenderModel Empty(
            string title,
            SSL_GeometryData? geometry,
            string? status)
        {
            return new DiagnosticsFieldRenderModel(
                title,
                geometry,
                [],
                [],
                [],
                status);
        }
    }
}
