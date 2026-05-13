using Tracker.Server.Components.Pages;

namespace Tracker.Tests;

public class DiagnosticsPlaybackStateTests
{
    /// <summary>
    /// 通常再生では 1 frame ずつ次の index へ進むことを確認する。
    /// </summary>
    [Fact]
    public void GetNextIndex_ForPlay_AdvancesOneFrame()
    {
        var next = DiagnosticsPlaybackState.GetNextIndex(3, entryCount: 10, DiagnosticsPlaybackMode.Play);

        Assert.Equal(4, next);
    }

    /// <summary>
    /// 早送りでも unified replay timeline tick は間引かず 1 tick ずつ進むことを確認する。
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(64)]
    public void GetNextIndex_ForFastForward_AdvancesOneReplayTimelineTick(int speedMultiplier)
    {
        var next = DiagnosticsPlaybackState.GetNextIndex(
            3,
            entryCount: 40,
            DiagnosticsPlaybackMode.FastForward,
            speedMultiplier);

        Assert.Equal(4, next);
    }

    /// <summary>
    /// Fast Forward 16x でも 0ms -> 20ms -> 40ms の replay timeline tick を飛ばさないことを確認する。
    /// </summary>
    [Fact]
    public void GetNextIndex_ForFastForward16x_DoesNotSkipFastTimelineTicks()
    {
        var timelineOffsetsMs = new[] { 0, 20, 40, 60, 80 };

        var first = DiagnosticsPlaybackState.GetNextIndex(
            currentIndex: 0,
            entryCount: timelineOffsetsMs.Length,
            DiagnosticsPlaybackMode.FastForward,
            speedMultiplier: 16);
        var second = DiagnosticsPlaybackState.GetNextIndex(
            first,
            entryCount: timelineOffsetsMs.Length,
            DiagnosticsPlaybackMode.FastForward,
            speedMultiplier: 16);

        Assert.Equal(20, timelineOffsetsMs[first]);
        Assert.Equal(40, timelineOffsetsMs[second]);
    }

    /// <summary>
    /// 最後を超える場合は末尾 index に丸めることを確認する。
    /// </summary>
    [Fact]
    public void GetNextIndex_WhenStepExceedsEnd_ClampsToLastIndex()
    {
        var next = DiagnosticsPlaybackState.GetNextIndex(
            8,
            entryCount: 10,
            DiagnosticsPlaybackMode.FastForward,
            speedMultiplier: 64);

        Assert.Equal(9, next);
    }

    /// <summary>
    /// entry がない場合は選択なしを表す 0 に戻すことを確認する。
    /// </summary>
    [Fact]
    public void GetNextIndex_WhenEntryCountIsZero_ReturnsZero()
    {
        var next = DiagnosticsPlaybackState.GetNextIndex(5, entryCount: 0, DiagnosticsPlaybackMode.Play);

        Assert.Equal(0, next);
    }

    /// <summary>
    /// 最後の index に到達している場合は再生を止めるべきことを確認する。
    /// </summary>
    [Fact]
    public void ShouldStopAtEnd_WhenAtLastIndex_ReturnsTrue()
    {
        Assert.True(DiagnosticsPlaybackState.ShouldStopAtEnd(9, entryCount: 10));
    }

    /// <summary>
    /// 末尾到達後は先頭 index に戻すことを確認する。
    /// </summary>
    [Fact]
    public void GetIndexAfterEndHandling_WhenAtLastIndex_ReturnsFirstIndex()
    {
        var index = DiagnosticsPlaybackState.GetIndexAfterEndHandling(9, entryCount: 10);

        Assert.Equal(0, index);
    }

    /// <summary>
    /// 末尾到達前は再生中の index を維持することを確認する。
    /// </summary>
    [Fact]
    public void GetIndexAfterEndHandling_WhenBeforeLastIndex_ReturnsCurrentIndex()
    {
        var index = DiagnosticsPlaybackState.GetIndexAfterEndHandling(7, entryCount: 10);

        Assert.Equal(7, index);
    }

    /// <summary>
    /// active mode と tick mode が一致し、キャンセルされていない tick だけ反映することを確認する。
    /// </summary>
    [Fact]
    public void ShouldApplyTick_WhenActiveAndNotCanceled_ReturnsTrue()
    {
        var shouldApply = DiagnosticsPlaybackState.ShouldApplyTick(
            DiagnosticsPlaybackMode.Play,
            DiagnosticsPlaybackMode.Play,
            isCancellationRequested: false);

        Assert.True(shouldApply);
    }

    /// <summary>
    /// 停止後に遅れて到着した tick は反映しないことを確認する。
    /// </summary>
    [Fact]
    public void ShouldApplyTick_WhenCanceled_ReturnsFalse()
    {
        var shouldApply = DiagnosticsPlaybackState.ShouldApplyTick(
            DiagnosticsPlaybackMode.Play,
            DiagnosticsPlaybackMode.Play,
            isCancellationRequested: true);

        Assert.False(shouldApply);
    }

    /// <summary>
    /// playback mode 切替前の古い tick は反映しないことを確認する。
    /// </summary>
    [Fact]
    public void ShouldApplyTick_WhenModeChanged_ReturnsFalse()
    {
        var shouldApply = DiagnosticsPlaybackState.ShouldApplyTick(
            DiagnosticsPlaybackMode.FastForward,
            DiagnosticsPlaybackMode.Play,
            isCancellationRequested: false);

        Assert.False(shouldApply);
    }

    /// <summary>
    /// 通常再生は現在 entry と次 entry の timestamp 差分で進むことを確認する。
    /// </summary>
    [Fact]
    public void GetInterval_ForPlay_UsesEntryTimestampDelta()
    {
        var current = DateTimeOffset.Parse("2026-05-12T00:00:00.100+09:00");
        var next = DateTimeOffset.Parse("2026-05-12T00:00:00.133+09:00");

        var interval = DiagnosticsPlaybackState.GetInterval(
            DiagnosticsPlaybackMode.Play,
            current,
            next);

        Assert.Equal(TimeSpan.FromMilliseconds(33), interval);
    }

    /// <summary>
    /// 通常再生は長い timestamp 差分でも実時間の interval を維持することを確認する。
    /// </summary>
    [Fact]
    public void GetInterval_ForPlay_UsesLongTimestampDeltaWithoutMaximumClamp()
    {
        var current = DateTimeOffset.Parse("2026-05-12T00:00:00.000+09:00");
        var next = DateTimeOffset.Parse("2026-05-12T00:00:03.500+09:00");

        var interval = DiagnosticsPlaybackState.GetInterval(
            DiagnosticsPlaybackMode.Play,
            current,
            next);

        Assert.Equal(TimeSpan.FromMilliseconds(3500), interval);
    }

    /// <summary>
    /// 早送りは選択した調査用速度に応じて interval を短くすることを確認する。
    /// </summary>
    [Theory]
    [InlineData(4, 400)]
    [InlineData(16, 100)]
    [InlineData(64, 30)]
    public void GetInterval_ForFastForward_UsesSelectedSpeedMultiplier(
        int speedMultiplier,
        int expectedMilliseconds)
    {
        var current = DateTimeOffset.Parse("2026-05-12T00:00:00.000+09:00");
        var next = DateTimeOffset.Parse("2026-05-12T00:00:01.600+09:00");

        var interval = DiagnosticsPlaybackState.GetInterval(
            DiagnosticsPlaybackMode.FastForward,
            current,
            next,
            speedMultiplier);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), interval);
    }

    /// <summary>
    /// 早送りの interval は過小になりすぎないことを確認する。
    /// </summary>
    [Fact]
    public void GetInterval_ForFastForward_UsesMinimumInterval()
    {
        var current = DateTimeOffset.Parse("2026-05-12T00:00:00.000+09:00");
        var next = DateTimeOffset.Parse("2026-05-12T00:00:00.040+09:00");

        var interval = DiagnosticsPlaybackState.GetInterval(
            DiagnosticsPlaybackMode.FastForward,
            current,
            next,
            speedMultiplier: 64);

        Assert.Equal(TimeSpan.FromMilliseconds(30), interval);
    }

    /// <summary>
    /// speed 変更前の古い早送り tick は反映しないことを確認する。
    /// </summary>
    [Fact]
    public void ShouldApplyTick_WhenFastForwardSpeedChanged_ReturnsFalse()
    {
        var shouldApply = DiagnosticsPlaybackState.ShouldApplyTick(
            DiagnosticsPlaybackMode.FastForward,
            DiagnosticsPlaybackMode.FastForward,
            isCancellationRequested: false,
            activeSpeedMultiplier: 64,
            tickSpeedMultiplier: 16);

        Assert.False(shouldApply);
    }
}
