using System.Threading.Channels;
using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class ActiveRefereeMotionWaitTests
{
    [Fact]
    public async Task ReturnsMovementAndCancelsTheRefereeMonitor()
    {
        var source = new ChannelRefereeCommandSource();
        var motion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = RefereeDriverFixture.WaitForMotionWhileActiveAsync(
            source,
            Active(10),
            TimeSpan.FromSeconds(1),
            _ => motion.Task,
            CancellationToken.None);

        await source.WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        motion.SetResult("100 mm");

        Assert.Equal("100 mm", await wait);
        Assert.True(source.CancellationObserved.Task.IsCompleted);
    }

    [Fact]
    public async Task StopsMotionAndReturnsTheLatestSnapshotWhenActiveIsLost()
    {
        var source = new ChannelRefereeCommandSource();
        var motionCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = RefereeDriverFixture.WaitForMotionWhileActiveAsync(
            source,
            Active(20),
            TimeSpan.FromSeconds(1),
            async cancellationToken =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return false;
                }
                catch (OperationCanceledException)
                {
                    motionCanceled.TrySetResult();
                    throw;
                }
            },
            CancellationToken.None);

        await source.WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var lost = new RefereeCommandSnapshot(Referee.Types.Command.Halt, 21, "crane");
        source.Publish(lost);

        var exception = await Assert.ThrowsAsync<RefereeStateLostException>(() => wait);

        Assert.Equal(lost, exception.StateLoss.Snapshot);
        Assert.Equal("REFEREE_STATE_LOSS", exception.StateLoss.FailureReason);
        Assert.Equal(
            "REFEREE_STATE_LOSS: active referee command was lost during Yellow motion; observed Halt at counter 21.",
            exception.StateLoss.ToFailureMessage());
        using var diagnostic = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(exception.StateLoss));
        Assert.Equal(
            "REFEREE_STATE_LOSS",
            diagnostic.RootElement.GetProperty("FailureReason").GetString());
        var snapshot = diagnostic.RootElement.GetProperty("Snapshot");
        Assert.Equal((int)Referee.Types.Command.Halt, snapshot.GetProperty("Command").GetInt32());
        Assert.Equal(21u, snapshot.GetProperty("CommandCounter").GetUInt32());
        Assert.True(motionCanceled.Task.IsCompleted);
    }

    [Fact]
    public async Task KeepsMonitoringWhenTheNextCommandIsStillActive()
    {
        var source = new ChannelRefereeCommandSource();
        var motion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = RefereeDriverFixture.WaitForMotionWhileActiveAsync(
            source,
            Active(30),
            TimeSpan.FromSeconds(1),
            _ => motion.Task,
            CancellationToken.None);

        await source.WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        source.Publish(Active(31, Referee.Types.Command.ForceStart));
        await source.SecondWaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        motion.SetResult("movement complete");

        Assert.Equal("movement complete", await wait);
        Assert.True(source.CancellationObserved.Task.IsCompleted);
    }

    [Fact]
    public async Task PrefersStateLossWhenMotionAndLossAreAlreadyComplete()
    {
        var lost = new RefereeCommandSnapshot(Referee.Types.Command.Stop, 35, "crane");
        var source = new ImmediateRefereeCommandSource(lost);

        var exception = await Assert.ThrowsAsync<RefereeStateLostException>(() =>
            RefereeDriverFixture.WaitForMotionWhileActiveAsync(
                source,
                Active(34),
                TimeSpan.FromSeconds(1),
                _ => Task.FromResult("movement complete"),
                CancellationToken.None));

        Assert.Equal(lost, exception.StateLoss.Snapshot);
    }

    [Fact]
    public async Task PreservesMotionTimeoutCancellationWithoutReportingStateLoss()
    {
        var source = new ChannelRefereeCommandSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            RefereeDriverFixture.WaitForMotionWhileActiveAsync(
                source,
                Active(40),
                TimeSpan.FromMilliseconds(50),
                async cancellationToken =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return false;
                },
                CancellationToken.None));

        Assert.True(source.CancellationObserved.Task.IsCompleted);
    }

    private static RefereeCommandSnapshot Active(
        uint counter,
        Referee.Types.Command command = Referee.Types.Command.NormalStart) => new(command, counter, "crane");

    private sealed class ChannelRefereeCommandSource : IRefereeCommandSource
    {
        private readonly Channel<RefereeCommandSnapshot> commands = Channel.CreateUnbounded<RefereeCommandSnapshot>();
        private int waitCount;

        public TaskCompletionSource WaitStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondWaitStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Publish(RefereeCommandSnapshot snapshot) => commands.Writer.TryWrite(snapshot);

        public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref waitCount);
            if (count == 1)
            {
                WaitStarted.TrySetResult();
            }
            else
            {
                SecondWaitStarted.TrySetResult();
            }

            while (true)
            {
                try
                {
                    var snapshot = await commands.Reader.ReadAsync(cancellationToken);
                    if (previousCommandCounter is null || snapshot.CommandCounter != previousCommandCounter)
                    {
                        return snapshot;
                    }
                }
                catch (OperationCanceledException)
                {
                    CancellationObserved.TrySetResult();
                    throw;
                }
            }
        }
    }

    private sealed class ImmediateRefereeCommandSource(RefereeCommandSnapshot snapshot)
        : IRefereeCommandSource
    {
        public ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(snapshot);
        }
    }
}
