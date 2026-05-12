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
    /// 1 entry ずつ進める通常再生。
    /// </summary>
    Play,

    /// <summary>
    /// 複数 entry ずつ進める早送り。
    /// </summary>
    FastForward,
}

/// <summary>
/// diagnostics timeline playback の index と interval の計算。
/// </summary>
public static class DiagnosticsPlaybackState
{
    private const int PlayStep = 1;
    private static readonly TimeSpan MinimumPlaybackInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan FastForwardMinimumInterval = TimeSpan.FromMilliseconds(30);

    /// <summary>
    /// 調査用早送りの既定倍率。
    /// </summary>
    public const int DefaultFastForwardSpeedMultiplier = 16;

    /// <summary>
    /// UI で選べる調査用早送り倍率。
    /// </summary>
    public static IReadOnlyList<int> FastForwardSpeedMultipliers { get; } = [4, 16, 64];

    /// <summary>
    /// playback mode に応じた次の entry index を返す。
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

        var step = mode == DiagnosticsPlaybackMode.FastForward
            ? GetFastForwardStep(speedMultiplier)
            : PlayStep;
        return Math.Clamp(currentIndex + step, 0, entryCount - 1);
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

    private static int GetFastForwardStep(int speedMultiplier)
    {
        return Math.Max(PlayStep, NormalizeSpeedMultiplier(speedMultiplier) / 4);
    }
}
