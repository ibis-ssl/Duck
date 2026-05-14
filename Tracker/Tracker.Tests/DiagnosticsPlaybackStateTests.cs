using Tracker.DebugHost.Components.Pages;

namespace Tracker.Tests;

public class DiagnosticsPlaybackStateTests
{
    /// <summary>
    /// playback speed choice は等倍速と preset 早送り倍率をこの順で公開することを確認する。
    /// </summary>
    [Fact]
    public void PlaybackSpeedChoices_ExposeNormalPlayAndFastForwardChoices()
    {
        Assert.Collection(
            DiagnosticsPlaybackState.PlaybackSpeedChoices,
            choice =>
            {
                Assert.Equal("等倍速", choice.Label);
                Assert.Equal(DiagnosticsPlaybackMode.Play, choice.Mode);
                Assert.Null(choice.FastForwardSpeedMultiplier);
            },
            choice =>
            {
                Assert.Equal("4x", choice.Label);
                Assert.Equal(DiagnosticsPlaybackMode.FastForward, choice.Mode);
                Assert.Equal(4, choice.FastForwardSpeedMultiplier);
            },
            choice =>
            {
                Assert.Equal("16x", choice.Label);
                Assert.Equal(DiagnosticsPlaybackMode.FastForward, choice.Mode);
                Assert.Equal(16, choice.FastForwardSpeedMultiplier);
            },
            choice =>
            {
                Assert.Equal("64x", choice.Label);
                Assert.Equal(DiagnosticsPlaybackMode.FastForward, choice.Mode);
                Assert.Equal(64, choice.FastForwardSpeedMultiplier);
            });
    }

    /// <summary>
    /// 早送り倍率は固定 preset membership ではなく範囲 clamp で正規化することを確認する。
    /// </summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(128, 128)]
    [InlineData(256, 256)]
    [InlineData(1024, 1024)]
    [InlineData(2048, 1024)]
    public void NormalizeSpeedMultiplier_ClampsVariableMultiplierRange(
        int requestedMultiplier,
        int expectedMultiplier)
    {
        Assert.Equal(
            expectedMultiplier,
            DiagnosticsPlaybackState.NormalizeSpeedMultiplier(requestedMultiplier));
    }

    /// <summary>
    /// diagnostics playback markup が等倍速の隣に早送り speed select を戻さないことを確認する。
    /// </summary>
    [Fact]
    public void DiagnosticsPlaybackMarkup_DoesNotUseFastForwardSpeedSelect()
    {
        var markup = File.ReadAllText(FindRepositoryFile("Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor"));
        var oldSpeedSelectClass = "diagnostics-playback__" + "speed";
        var oldSpeedSelectLabel = "Fast forward " + "speed";

        Assert.DoesNotContain(oldSpeedSelectClass, markup, StringComparison.Ordinal);
        Assert.DoesNotContain(oldSpeedSelectLabel, markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<select", ExtractPlaybackControlsMarkup(markup), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// diagnostics playback markup は transport buttons と speed tabs を分けて描画することを確認する。
    /// </summary>
    [Fact]
    public void DiagnosticsPlaybackMarkup_SeparatesTransportButtonsAndSpeedTabs()
    {
        var markup = File.ReadAllText(FindRepositoryFile("Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor"));
        var playbackControls = ExtractPlaybackControlsMarkup(markup);
        var oldActionHandler = "OnPlayback" + "ChoiceClicked";

        Assert.Contains("diagnostics-playback__transport", playbackControls, StringComparison.Ordinal);
        Assert.Contains("diagnostics-playback-tabs", playbackControls, StringComparison.Ordinal);
        Assert.Contains("DiagnosticsPlaybackState.PlaybackSpeedChoices", playbackControls, StringComparison.Ordinal);
        Assert.Contains("StartSelectedPlaybackAsync", playbackControls, StringComparison.Ordinal);
        Assert.Contains("StartFastForwardPlaybackAsync", playbackControls, StringComparison.Ordinal);
        Assert.DoesNotContain(oldActionHandler, playbackControls, StringComparison.Ordinal);
    }

    /// <summary>
    /// diagnostics playback markup は固定 tabs だけでなく compact な可変倍率 control を描画することを確認する。
    /// </summary>
    [Fact]
    public void DiagnosticsPlaybackMarkup_ExposesCompactVariableMultiplierControl()
    {
        var markup = File.ReadAllText(FindRepositoryFile("Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor"));
        var playbackControls = ExtractPlaybackControlsMarkup(markup);

        Assert.Contains("diagnostics-playback-multiplier", playbackControls, StringComparison.Ordinal);
        Assert.Contains("早送り倍率", playbackControls, StringComparison.Ordinal);
        Assert.Contains("type=\"number\"", playbackControls, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// diagnostics playback markup が数値の等倍ラベルを表示しないことを確認する。
    /// </summary>
    [Fact]
    public void DiagnosticsPlaybackMarkup_DoesNotExposeNumericNormalSpeedLabel()
    {
        var markup = File.ReadAllText(FindRepositoryFile("Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor"));
        var numericNormalSpeedLabel = "1" + "x";

        Assert.DoesNotContain(numericNormalSpeedLabel, markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// active transport button の停止表示が transport button 側に限定されることを確認する。
    /// </summary>
    [Fact]
    public void DiagnosticsPlaybackComponent_DoesNotTurnSpeedTabsIntoStopActions()
    {
        var code = File.ReadAllText(
            FindRepositoryFile("Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor.cs"));
        var oldActiveLabel = "{choice.Label} " + "Stop";

        Assert.DoesNotContain("{choice.Label} 停止", code, StringComparison.Ordinal);
        Assert.DoesNotContain("StopPlayback();", ExtractMethodBody(code, "OnPlaybackSpeedChoiceClicked"), StringComparison.Ordinal);
        Assert.DoesNotContain(oldActiveLabel, code, StringComparison.Ordinal);
    }

    /// <summary>
    /// FastForward 中に等倍速 tab を選ぶと、表示と実 mode を Play へ揃えることを確認する。
    /// </summary>
    [Fact]
    public void ResolveSpeedChoiceTransition_WhenFastForwardSelectsNormalSpeed_SwitchesToPlay()
    {
        var normalChoice = DiagnosticsPlaybackState.PlaybackSpeedChoices.Single(
            choice => choice.Label == DiagnosticsPlaybackState.NormalPlaybackSpeedLabel);

        var transition = DiagnosticsPlaybackState.ResolveSpeedChoiceTransition(
            DiagnosticsPlaybackMode.FastForward,
            currentFastForwardSpeedMultiplier: 16,
            normalChoice);

        Assert.Equal(DiagnosticsPlaybackState.NormalPlaybackSpeedLabel, transition.SelectedPlaybackSpeedLabel);
        Assert.Equal(DiagnosticsPlaybackMode.Play, transition.RestartMode);
        Assert.Equal(16, transition.FastForwardSpeedMultiplier);
    }

    /// <summary>
    /// Play 中に fast tab を選ぶと、表示と実 mode を FastForward へ揃えることを確認する。
    /// </summary>
    [Fact]
    public void ResolveSpeedChoiceTransition_WhenPlaySelectsFastSpeed_SwitchesToFastForward()
    {
        var fastChoice = DiagnosticsPlaybackState.PlaybackSpeedChoices.Single(choice => choice.Label == "4x");

        var transition = DiagnosticsPlaybackState.ResolveSpeedChoiceTransition(
            DiagnosticsPlaybackMode.Play,
            currentFastForwardSpeedMultiplier: 16,
            fastChoice);

        Assert.Equal("4x", transition.SelectedPlaybackSpeedLabel);
        Assert.Equal(DiagnosticsPlaybackMode.FastForward, transition.RestartMode);
        Assert.Equal(4, transition.FastForwardSpeedMultiplier);
    }

    /// <summary>
    /// fast multiplier 選択中の Play button は等倍速ではなく FastForward として開始することを確認する。
    /// </summary>
    [Fact]
    public void ResolvePlayButtonStart_WhenFastMultiplierSelected_StartsFastForward()
    {
        var start = DiagnosticsPlaybackState.ResolvePlayButtonStart(
            selectedPlaybackSpeedLabel: "128x",
            currentFastForwardSpeedMultiplier: 128);

        Assert.Equal(DiagnosticsPlaybackMode.FastForward, start.Mode);
        Assert.Equal("128x", start.SelectedPlaybackSpeedLabel);
        Assert.Equal(128, start.FastForwardSpeedMultiplier);
    }

    /// <summary>
    /// 等倍速選択中の Play button は TRACKER-060 の realtime Play として開始することを確認する。
    /// </summary>
    [Fact]
    public void ResolvePlayButtonStart_WhenNormalSpeedSelected_StartsRealtimePlay()
    {
        var start = DiagnosticsPlaybackState.ResolvePlayButtonStart(
            DiagnosticsPlaybackState.NormalPlaybackSpeedLabel,
            currentFastForwardSpeedMultiplier: 128);

        Assert.Equal(DiagnosticsPlaybackMode.Play, start.Mode);
        Assert.Equal(DiagnosticsPlaybackState.NormalPlaybackSpeedLabel, start.SelectedPlaybackSpeedLabel);
        Assert.Equal(128, start.FastForwardSpeedMultiplier);
    }

    /// <summary>
    /// 停止中に早送り倍率 input を変更しても再生開始しないことを確認する。
    /// </summary>
    [Fact]
    public void ResolveFastForwardMultiplierTransition_WhenStopped_SelectsMultiplierWithoutStarting()
    {
        var transition = DiagnosticsPlaybackState.ResolveFastForwardMultiplierTransition(
            DiagnosticsPlaybackMode.Stopped,
            requestedFastForwardSpeedMultiplier: 128);

        Assert.Equal("128x", transition.SelectedPlaybackSpeedLabel);
        Assert.Null(transition.RestartMode);
        Assert.Equal(128, transition.FastForwardSpeedMultiplier);
    }

    /// <summary>
    /// FastForward 中に早送り倍率 input を変更すると新倍率で FastForward を restart することを確認する。
    /// </summary>
    [Fact]
    public void ResolveFastForwardMultiplierTransition_WhenFastForward_RestartsFastForward()
    {
        var transition = DiagnosticsPlaybackState.ResolveFastForwardMultiplierTransition(
            DiagnosticsPlaybackMode.FastForward,
            requestedFastForwardSpeedMultiplier: 256);

        Assert.Equal("256x", transition.SelectedPlaybackSpeedLabel);
        Assert.Equal(DiagnosticsPlaybackMode.FastForward, transition.RestartMode);
        Assert.Equal(256, transition.FastForwardSpeedMultiplier);
    }

    /// <summary>
    /// Play 中に早送り倍率 input を変更すると表示だけ fast にせず FastForward へ切り替えることを確認する。
    /// </summary>
    [Fact]
    public void ResolveFastForwardMultiplierTransition_WhenPlay_SwitchesToFastForward()
    {
        var transition = DiagnosticsPlaybackState.ResolveFastForwardMultiplierTransition(
            DiagnosticsPlaybackMode.Play,
            requestedFastForwardSpeedMultiplier: 512);

        Assert.Equal("512x", transition.SelectedPlaybackSpeedLabel);
        Assert.Equal(DiagnosticsPlaybackMode.FastForward, transition.RestartMode);
        Assert.Equal(512, transition.FastForwardSpeedMultiplier);
    }

    /// <summary>
    /// 停止中に等倍速 tab を選んでも再生開始しないことを確認する。
    /// </summary>
    [Fact]
    public void ResolveSpeedChoiceTransition_WhenStoppedSelectsNormalSpeed_DoesNotStartPlayback()
    {
        var normalChoice = DiagnosticsPlaybackState.PlaybackSpeedChoices.Single(
            choice => choice.Label == DiagnosticsPlaybackState.NormalPlaybackSpeedLabel);

        var transition = DiagnosticsPlaybackState.ResolveSpeedChoiceTransition(
            DiagnosticsPlaybackMode.Stopped,
            currentFastForwardSpeedMultiplier: 16,
            normalChoice);

        Assert.Equal(DiagnosticsPlaybackState.NormalPlaybackSpeedLabel, transition.SelectedPlaybackSpeedLabel);
        Assert.Null(transition.RestartMode);
        Assert.Equal(16, transition.FastForwardSpeedMultiplier);
    }

    /// <summary>
    /// 早送りでも unified replay timeline tick は間引かず 1 tick ずつ進むことを確認する。
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(1024)]
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
    /// 等倍速 Play は timestamp delta ではなく30fps相当の表示間隔で更新することを確認する。
    /// </summary>
    [Fact]
    public void GetInterval_ForPlay_UsesThirtyFpsDisplayInterval()
    {
        var current = DateTimeOffset.Parse("2026-05-12T00:00:00.000+09:00");
        var next = DateTimeOffset.Parse("2026-05-12T00:00:03.500+09:00");

        var interval = DiagnosticsPlaybackState.GetInterval(
            DiagnosticsPlaybackMode.Play,
            current,
            next);

        Assert.Equal(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30), interval);
    }

    /// <summary>
    /// 等倍速 Play は timer 回数ではなく wall-clock 経過時間に対応する latest timeline tick へ追従することを確認する。
    /// </summary>
    [Fact]
    public void GetRealtimePlayIndex_WhenTimelineIs200Hz_UsesWallClockElapsedTime()
    {
        var startReceivedAt = DateTimeOffset.Parse("2026-05-12T00:00:00.000+09:00");
        var timeline = Enumerable.Range(0, 241)
            .Select(offset => startReceivedAt.AddMilliseconds(offset * 5))
            .ToArray();

        var index = DiagnosticsPlaybackState.GetRealtimePlayIndex(
            currentIndex: 0,
            timeline,
            startReceivedAt,
            startWallClock: startReceivedAt,
            currentWallClock: startReceivedAt.AddSeconds(1));

        Assert.Equal(200, index);
    }

    /// <summary>
    /// 早送りは選択した調査用速度に応じて interval を短くすることを確認する。
    /// </summary>
    [Theory]
    [InlineData(4, 400)]
    [InlineData(16, 100)]
    [InlineData(64, 25)]
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
    /// 64x 超の早送り interval が 30ms hard floor へ潰れないことを確認する。
    /// </summary>
    [Fact]
    public void GetInterval_ForFastForward128And256x_AreShorterThan64x()
    {
        var current = DateTimeOffset.Parse("2026-05-12T00:00:00.000+09:00");
        var next = DateTimeOffset.Parse("2026-05-12T00:00:01.600+09:00");

        var interval64x = DiagnosticsPlaybackState.GetInterval(
            DiagnosticsPlaybackMode.FastForward,
            current,
            next,
            speedMultiplier: 64);
        var interval128x = DiagnosticsPlaybackState.GetInterval(
            DiagnosticsPlaybackMode.FastForward,
            current,
            next,
            speedMultiplier: 128);
        var interval256x = DiagnosticsPlaybackState.GetInterval(
            DiagnosticsPlaybackMode.FastForward,
            current,
            next,
            speedMultiplier: 256);

        Assert.True(interval128x < interval64x);
        Assert.True(interval256x < interval128x);
        Assert.True(interval128x < TimeSpan.FromMilliseconds(30));
    }

    /// <summary>
    /// 早送りの interval は busy loop を避ける小さい timer floor で下限化することを確認する。
    /// </summary>
    [Fact]
    public void GetInterval_ForFastForward_UsesSmallTimerFloor()
    {
        var current = DateTimeOffset.Parse("2026-05-12T00:00:00.000+09:00");
        var next = DateTimeOffset.Parse("2026-05-12T00:00:00.040+09:00");

        var interval = DiagnosticsPlaybackState.GetInterval(
            DiagnosticsPlaybackMode.FastForward,
            current,
            next,
            speedMultiplier: 64);

        Assert.Equal(TimeSpan.FromMilliseconds(1), interval);
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

    private static string FindRepositoryFile(string relativePath)
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.");
    }

    private static string ExtractPlaybackControlsMarkup(string markup)
    {
        const string startMarker = "aria-label=\"Diagnostics playback controls\"";
        const string endMarker = "aria-label=\"Diagnostics timeline scrubber\"";
        var start = markup.IndexOf(startMarker, StringComparison.Ordinal);
        var end = markup.IndexOf(endMarker, StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return markup;
        }

        return markup[start..end];
    }

    private static string ExtractMethodBody(string code, string methodName)
    {
        var start = code.IndexOf(methodName, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var bodyStart = code.IndexOf('{', start);
        if (bodyStart < 0)
        {
            return string.Empty;
        }

        var depth = 0;
        for (var index = bodyStart; index < code.Length; index++)
        {
            if (code[index] == '{')
            {
                depth++;
            }
            else if (code[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return code[bodyStart..(index + 1)];
                }
            }
        }

        return code[bodyStart..];
    }
}
