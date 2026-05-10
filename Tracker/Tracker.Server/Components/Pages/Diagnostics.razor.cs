using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tracker.Server.Components.Vision;
using Tracker.Server.Tracking;

namespace Tracker.Server.Components.Pages;

/// <summary>
/// diagnostics log の選択、timeline、render snapshot、profile metadata modal を同期するページ状態。
/// </summary>
public partial class Diagnostics
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

    private int MaxEntryIndex => Math.Max(0, entries.Count - 1);

    private int SelectedEntryIndex => selectedEntry is null
        ? 0
        : FindEntryIndex(selectedEntry);

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
        LoadFiles();
        LoadSelectedFile();
    }

    private Task ReloadAsync()
    {
        LoadFiles();
        LoadSelectedFile();
        return Task.CompletedTask;
    }

    private Task OnLogFileChanged(ChangeEventArgs args)
    {
        var requestedLogPath = args.Value?.ToString();
        selectedLogPath = logFiles.FirstOrDefault(file => file.FullPath == requestedLogPath)?.FullPath;
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
            return;
        }

        snapshot = LogReader.ReadFile(selectedLogPath);
        entries = snapshot.Entries;
        selectedEntry = entries.FirstOrDefault();
        LoadProfileMetadataIndex();
        UpdateProfileMetadataForSelectedEntry();
        LoadRenderSnapshotIndex();
        LoadSelectedRenderSnapshot();
    }

    // timeline / scrubber の選択 entry 変更に合わせて、metadata 表示と render snapshot を同じ frame に同期する。
    private void SelectEntry(TrackerDiagnosticsLogEntry entry)
    {
        selectedEntry = entry;
        UpdateProfileMetadataForSelectedEntry();
        LoadSelectedRenderSnapshot();
    }

    private void OnTimelineScrubbed(ChangeEventArgs args)
    {
        if (!int.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var index))
        {
            return;
        }

        SelectEntryByIndex(index);
    }

    private void OnTimelineWheel(WheelEventArgs args)
    {
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
            return;
        }

        selectedEntry = entries[Math.Clamp(index, 0, entries.Count - 1)];
        UpdateProfileMetadataForSelectedEntry();
        LoadSelectedRenderSnapshot();
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

    private static string FormatTime(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
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
}
