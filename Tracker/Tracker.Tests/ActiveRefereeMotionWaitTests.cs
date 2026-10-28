using System.Threading.Channels;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Duck.Testing.RefereeDriver;
using Xunit.Abstractions;

namespace Tracker.Tests;

public sealed class ActiveRefereeMotionWaitTests
{
    private readonly ITestOutputHelper output;

    public ActiveRefereeMotionWaitTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task InjectedHaltStopsRecordedYellowMotionBeforeTimeoutAndPreservesDiagnosticHistory()
    {
        const uint initialCounter = 50;
        const uint haltCounter = 52;
        using var packetReceiver = new LoopbackUdpRefereePacketReceiver();
        Assert.Equal(IPAddress.Loopback, packetReceiver.Endpoint.Address);
        Assert.InRange(packetReceiver.Endpoint.Port, 1, 65535);
        using var parsedSource = new UdpRefereeCommandSource(packetReceiver);
        var snapshots = new ConcurrentQueue<RefereeCommandSnapshot>();
        using var recordedSource = new RecordingRefereeCommandSource(parsedSource, snapshots);
        await packetReceiver.PublishAsync(Referee.Types.Command.NormalStart, initialCounter);
        var active = await recordedSource.WaitForChangeAsync(null, CancellationToken.None);
        var resultWriter = new AspireAcceptanceResultWriter(snapshots);
        var artifactDirectory = Directory.CreateTempSubdirectory("issue101-acceptance-result-");
        var resultPath = Path.Combine(artifactDirectory.FullName, "acceptance-result.json");
        var motionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var motionCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var testCancellation = new CancellationTokenSource();
        const int motionTimeoutSeconds = 30;
        var wait = RefereeDriverFixture.WaitForMotionWhileActiveAsync(
            recordedSource,
            active,
            TimeSpan.FromSeconds(motionTimeoutSeconds),
            async cancellationToken =>
            {
                motionStarted.TrySetResult();
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
            testCancellation.Token);

        try
        {
            await motionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var injectedAt = Stopwatch.GetTimestamp();
            await packetReceiver.PublishAsync(Referee.Types.Command.ForceStart, initialCounter + 1);
            await packetReceiver.PublishAsync(Referee.Types.Command.Halt, haltCounter);

            var exception = await Assert.ThrowsAsync<RefereeStateLostException>(
                () => wait.WaitAsync(TimeSpan.FromSeconds(3)));
            var elapsed = Stopwatch.GetElapsedTime(injectedAt);
            output.WriteLine(
                $"loopback endpoint={packetReceiver.Endpoint}; Halt response={elapsed.TotalMilliseconds:F3} ms; configured motion timeout={motionTimeoutSeconds} s");
            try
            {
                throw AspireAcceptanceResultWriter.CreateMotionFailure(exception);
            }
            catch (InvalidOperationException acceptanceFailure)
            {
                resultWriter.RecordFailure(acceptanceFailure);
            }

            Assert.True(elapsed < TimeSpan.FromSeconds(motionTimeoutSeconds));
            Assert.True(motionCanceled.Task.IsCompleted);
            await resultWriter.WriteAsync(
                resultPath,
                stageReached: "active_robot_motion_wait",
                refereeRawDatagramCount: snapshots.Count,
                visionPacketsConsumed: null,
                trackerPacketsConsumed: null,
                comparisonMeasurement: null,
                receiverSockets: null,
                movement: null);

            using var result = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
            var resultRoot = result.RootElement;
            Assert.Equal("failed", resultRoot.GetProperty("status").GetString());
            Assert.Contains(
                "REFEREE_STATE_LOSS: active referee command was lost during Yellow motion; observed Halt at counter 52.",
                resultRoot.GetProperty("failure").GetString());
            var stateLoss = resultRoot.GetProperty("refereeStateLoss");
            Assert.Equal("REFEREE_STATE_LOSS", stateLoss.GetProperty("FailureReason").GetString());
            var lostSnapshot = stateLoss.GetProperty("Snapshot");
            Assert.Equal((int)Referee.Types.Command.Halt, lostSnapshot.GetProperty("Command").GetInt32());
            Assert.Equal(haltCounter, lostSnapshot.GetProperty("CommandCounter").GetUInt32());
            Assert.Equal(
                [
                    ("NormalStart", initialCounter),
                    ("ForceStart", initialCounter + 1),
                    ("Halt", haltCounter),
                ],
                resultRoot.GetProperty("refereeCommands").EnumerateArray()
                    .Select(command => (
                        command.GetProperty("command").GetString()!,
                        command.GetProperty("CommandCounter").GetUInt32()))
                    .ToArray());
        }
        finally
        {
            testCancellation.Cancel();
            try
            {
                await wait;
            }
            catch
            {
                // The expected referee-loss task faults; cancellation must still join it after assertion failures.
            }

            if (Directory.Exists(artifactDirectory.FullName))
            {
                artifactDirectory.Delete(recursive: true);
            }
        }

        packetReceiver.Dispose();
        Assert.True(packetReceiver.IsDisposed);
        output.WriteLine($"test-owned loopback UDP socket disposed={packetReceiver.IsDisposed}");
    }

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
