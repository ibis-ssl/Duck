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
/// diagnostics playback UI で表示する再生選択肢。
/// </summary>
/// <param name="Label">UI に表示する選択肢名。</param>
/// <param name="Mode">開始する playback mode。</param>
/// <param name="FastForwardSpeedMultiplier">早送り選択肢の倍率。等倍速では null。</param>
public sealed record DiagnosticsPlaybackChoice(
    string Label,
    DiagnosticsPlaybackMode Mode,
    int? FastForwardSpeedMultiplier);

/// <summary>
/// diagnostics timeline playback の index と interval の計算。
/// </summary>
public static class DiagnosticsPlaybackState
{
    private const int PlayStep = 1;
    private static readonly TimeSpan MinimumPlaybackInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan FastForwardMinimumInterval = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan PlayDisplayInterval = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30);

    /// <summary>
    /// 調査用早送りの既定倍率。
    /// </summary>
    public const int DefaultFastForwardSpeedMultiplier = 16;

    /// <summary>
    /// UI で選べる調査用早送り倍率。
    /// </summary>
    public static IReadOnlyList<int> FastForwardSpeedMultipliers { get; } = [4, 16, 64];

    /// <summary>
    /// UI で表示する等倍速と調査用早送りの選択肢。
    /// </summary>
    public static IReadOnlyList<DiagnosticsPlaybackChoice> PlaybackChoices { get; } =
    [
        new("等倍速", DiagnosticsPlaybackMode.Play, null),
        .. FastForwardSpeedMultipliers.Select(speedMultiplier => new DiagnosticsPlaybackChoice(
            $"{speedMultiplier}x",
            DiagnosticsPlaybackMode.FastForward,
            speedMultiplier)),
    ];

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
                FastForwardMinimumInterval.Ticks,
                normalizedInterval.Ticks / NormalizeSpeedMultiplier(speedMultiplier)))
            : normalizedInterval;
    }

    /// <summary>
    /// UI から渡された早送り倍率を選択可能な値へ丸める。
    /// </summary>
    public static int NormalizeSpeedMultiplier(int speedMultiplier)
    {
        return FastForwardSpeedMultipliers.Contains(speedMultiplier)
            ? speedMultiplier
            : DefaultFastForwardSpeedMultiplier;
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
