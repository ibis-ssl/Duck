namespace Tracker.Server.Components.Pages;

/// <summary>
/// diagnostics timeline の再生モード。
/// </summary>
public enum DiagnosticsPlaybackMode
{
    /// <summary>
    /// 再生していない状態。
    /// </summary>
    Stopped,

    /// <summary>
    /// 30fps 相当の表示更新で wall-clock 経過時間に対応する replay timeline tick へ追従する通常再生。
    /// </summary>
    Play,

    /// <summary>
    /// timeline tick を間引かず、timestamp delta を短縮する早送り。
    /// </summary>
    FastForward,
}

/// <summary>
/// diagnostics playback UI で表示する速度選択肢。
/// </summary>
/// <param name="Label">UI に表示する速度名。</param>
/// <param name="Mode">transport button で開始する playback mode。</param>
/// <param name="FastForwardSpeedMultiplier">早送り選択肢の倍率。等倍速では null。</param>
public sealed record DiagnosticsPlaybackSpeedChoice(
    string Label,
    DiagnosticsPlaybackMode Mode,
    int? FastForwardSpeedMultiplier);

/// <summary>
/// 速度 tab 選択後に diagnostics playback UI へ反映する状態遷移。
/// </summary>
/// <param name="SelectedPlaybackSpeedLabel">選択表示に使う速度名。</param>
/// <param name="RestartMode">再生中 mode を切り替える場合の開始 mode。停止中または切替不要なら null。</param>
/// <param name="FastForwardSpeedMultiplier">保持する早送り倍率。</param>
public sealed record DiagnosticsPlaybackSpeedTransition(
    string SelectedPlaybackSpeedLabel,
    DiagnosticsPlaybackMode? RestartMode,
    int FastForwardSpeedMultiplier);

/// <summary>
/// transport button 押下後に diagnostics playback UI へ反映する開始状態。
/// </summary>
/// <param name="Mode">開始する playback mode。</param>
/// <param name="SelectedPlaybackSpeedLabel">選択表示に使う速度名。</param>
/// <param name="FastForwardSpeedMultiplier">保持する早送り倍率。</param>
public sealed record DiagnosticsPlaybackStart(
    DiagnosticsPlaybackMode Mode,
    string SelectedPlaybackSpeedLabel,
    int FastForwardSpeedMultiplier);

/// <summary>
/// diagnostics timeline playback の index と interval の計算。
/// </summary>
public static class DiagnosticsPlaybackState
{
    private const int PlayStep = 1;
    private static readonly TimeSpan MinimumPlaybackInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan FastForwardTimerFloor = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan PlayDisplayInterval = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30);

    /// <summary>
    /// 調査用早送りの既定倍率。
    /// </summary>
    public const int DefaultFastForwardSpeedMultiplier = 16;

    /// <summary>
    /// 可変早送り倍率の最小値。
    /// </summary>
    public const int MinFastForwardSpeedMultiplier = 2;

    /// <summary>
    /// 可変早送り倍率の最大値。
    /// </summary>
    public const int MaxFastForwardSpeedMultiplier = 1024;

    /// <summary>
    /// 等倍速の表示名。
    /// </summary>
    public const string NormalPlaybackSpeedLabel = "等倍速";

    /// <summary>
    /// UI で shortcut として選べる調査用早送り倍率。
    /// </summary>
    public static IReadOnlyList<int> FastForwardSpeedMultipliers { get; } = [4, 16, 64];

    /// <summary>
    /// UI で表示する等倍速と調査用早送りの速度選択肢。
    /// </summary>
    public static IReadOnlyList<DiagnosticsPlaybackSpeedChoice> PlaybackSpeedChoices { get; } =
    [
        new(NormalPlaybackSpeedLabel, DiagnosticsPlaybackMode.Play, null),
        .. FastForwardSpeedMultipliers.Select(speedMultiplier => new DiagnosticsPlaybackSpeedChoice(
            $"{speedMultiplier}x",
            DiagnosticsPlaybackMode.FastForward,
            speedMultiplier)),
    ];

    /// <summary>
    /// 速度 tab 選択時に表示と active playback mode を矛盾させない状態遷移を返す。
    /// </summary>
    public static DiagnosticsPlaybackSpeedTransition ResolveSpeedChoiceTransition(
        DiagnosticsPlaybackMode currentMode,
        int currentFastForwardSpeedMultiplier,
        DiagnosticsPlaybackSpeedChoice choice)
    {
        var speedMultiplier = choice.FastForwardSpeedMultiplier is { } selectedSpeedMultiplier
            ? NormalizeSpeedMultiplier(selectedSpeedMultiplier)
            : currentFastForwardSpeedMultiplier;

        DiagnosticsPlaybackMode? restartMode = currentMode switch
        {
            DiagnosticsPlaybackMode.Stopped => null,
            DiagnosticsPlaybackMode.FastForward when choice.Mode == DiagnosticsPlaybackMode.FastForward =>
                DiagnosticsPlaybackMode.FastForward,
            _ when currentMode != choice.Mode => choice.Mode,
            _ => null,
        };

        return new DiagnosticsPlaybackSpeedTransition(choice.Label, restartMode, speedMultiplier);
    }

    /// <summary>
    /// Play button 押下時に、現在選択中の速度から開始 mode を解決する。
    /// </summary>
    public static DiagnosticsPlaybackStart ResolvePlayButtonStart(
        string selectedPlaybackSpeedLabel,
        int currentFastForwardSpeedMultiplier)
    {
        var normalizedSpeedMultiplier = NormalizeSpeedMultiplier(currentFastForwardSpeedMultiplier);
        if (selectedPlaybackSpeedLabel == NormalPlaybackSpeedLabel)
        {
            return new DiagnosticsPlaybackStart(
                DiagnosticsPlaybackMode.Play,
                NormalPlaybackSpeedLabel,
                normalizedSpeedMultiplier);
        }

        return new DiagnosticsPlaybackStart(
            DiagnosticsPlaybackMode.FastForward,
            FormatFastForwardSpeedLabel(normalizedSpeedMultiplier),
            normalizedSpeedMultiplier);
    }

    /// <summary>
    /// 早送り倍率 input 変更時に、選択表示と active playback mode を矛盾させない状態遷移を返す。
    /// </summary>
    public static DiagnosticsPlaybackSpeedTransition ResolveFastForwardMultiplierTransition(
        DiagnosticsPlaybackMode currentMode,
        int requestedFastForwardSpeedMultiplier)
    {
        var speedMultiplier = NormalizeSpeedMultiplier(requestedFastForwardSpeedMultiplier);
        var restartMode = currentMode switch
        {
            DiagnosticsPlaybackMode.Stopped => (DiagnosticsPlaybackMode?)null,
            _ => DiagnosticsPlaybackMode.FastForward,
        };

        return new DiagnosticsPlaybackSpeedTransition(
            FormatFastForwardSpeedLabel(speedMultiplier),
            restartMode,
            speedMultiplier);
    }

    /// <summary>
    /// 早送り倍率の表示名を返す。
    /// </summary>
    public static string FormatFastForwardSpeedLabel(int speedMultiplier)
    {
        return $"{NormalizeSpeedMultiplier(speedMultiplier)}x";
    }

    /// <summary>
    /// playback mode に応じた次の replay timeline index を返す。
    /// </summary>
    public static int GetNextIndex(
        int currentIndex,
        int entryCount,
        DiagnosticsPlaybackMode mode,
        int speedMultiplier = DefaultFastForwardSpeedMultiplier)
    {
        if (entryCount <= 0)
        {
            return 0;
        }

        return Math.Clamp(currentIndex + PlayStep, 0, entryCount - 1);
    }

    /// <summary>
    /// 等倍速 Play の wall-clock 経過時間に対応する replay timeline index を返す。
    /// </summary>
    public static int GetRealtimePlayIndex(
        int currentIndex,
        IReadOnlyList<DateTimeOffset> timelineTimestamps,
        DateTimeOffset startReceivedAt,
        DateTimeOffset startWallClock,
        DateTimeOffset currentWallClock)
    {
        if (timelineTimestamps.Count == 0)
        {
            return 0;
        }

        var elapsed = currentWallClock - startWallClock;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var targetReceivedAt = startReceivedAt + elapsed;
        var latestIndex = FindLatestIndexAtOrBefore(timelineTimestamps, targetReceivedAt);
        return Math.Clamp(Math.Max(currentIndex, latestIndex), 0, timelineTimestamps.Count - 1);
    }

    /// <summary>
    /// 指定 index が最後の entry に到達しているかを返す。
    /// </summary>
    public static bool ShouldStopAtEnd(int index, int entryCount)
    {
        return entryCount <= 0 || index >= entryCount - 1;
    }

    /// <summary>
    /// playback が末尾に到達した後に表示する entry index を返す。
    /// </summary>
    public static int GetIndexAfterEndHandling(int index, int entryCount)
    {
        if (entryCount <= 0)
        {
            return 0;
        }

        return ShouldStopAtEnd(index, entryCount) ? 0 : index;
    }

    /// <summary>
    /// 遅延後に到着した playback tick を現在の再生状態へ反映してよいかを返す。
    /// </summary>
    public static bool ShouldApplyTick(
        DiagnosticsPlaybackMode activeMode,
        DiagnosticsPlaybackMode tickMode,
        bool isCancellationRequested,
        int activeSpeedMultiplier = DefaultFastForwardSpeedMultiplier,
        int tickSpeedMultiplier = DefaultFastForwardSpeedMultiplier)
    {
        return !isCancellationRequested &&
               activeMode == tickMode &&
               activeSpeedMultiplier == tickSpeedMultiplier;
    }

    /// <summary>
    /// playback mode と entry timestamp 差分に応じた更新間隔を返す。
    /// </summary>
    public static TimeSpan GetInterval(
        DiagnosticsPlaybackMode mode,
        DateTimeOffset currentTimestamp,
        DateTimeOffset nextTimestamp,
        int speedMultiplier = DefaultFastForwardSpeedMultiplier)
    {
        if (mode == DiagnosticsPlaybackMode.Play)
        {
            return PlayDisplayInterval;
        }

        var realTimeInterval = nextTimestamp - currentTimestamp;
        if (realTimeInterval <= TimeSpan.Zero)
        {
            realTimeInterval = MinimumPlaybackInterval;
        }

        var normalizedInterval = realTimeInterval < MinimumPlaybackInterval
            ? MinimumPlaybackInterval
            : realTimeInterval;

        return mode == DiagnosticsPlaybackMode.FastForward
            ? TimeSpan.FromTicks(Math.Max(
                FastForwardTimerFloor.Ticks,
                normalizedInterval.Ticks / NormalizeSpeedMultiplier(speedMultiplier)))
            : normalizedInterval;
    }

    /// <summary>
    /// UI から渡された早送り倍率を有効範囲へ丸める。
    /// </summary>
    public static int NormalizeSpeedMultiplier(int speedMultiplier)
    {
        return Math.Clamp(speedMultiplier, MinFastForwardSpeedMultiplier, MaxFastForwardSpeedMultiplier);
    }

    private static int FindLatestIndexAtOrBefore(
        IReadOnlyList<DateTimeOffset> timelineTimestamps,
        DateTimeOffset targetReceivedAt)
    {
        var result = 0;
        var low = 0;
        var high = timelineTimestamps.Count - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (timelineTimestamps[middle] <= targetReceivedAt)
            {
                result = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return result;
    }
}
