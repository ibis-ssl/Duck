using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveRestartTests
{
    /// <summary>再開後は旧世代の遅いcallbackを採用せず、新しい受信の増分だけで準備を進める。</summary>
    [Fact]
    public async Task RestartRejectsDelayedOldEpochAndRequiresNewReceiveProgress()
    {
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-restart", "referee-driver", new string('e', 64), 1),
            new FixedReceiveClock());
        var payload = CreatePacket().ToByteArray();
        var receiver = new ControlledReceiver(payload);
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var firstRun = service.RunAsync(firstCancellation.Token);
        Task? secondRun = null;
        try
        {
            await WaitForBoundaryAsync(firstRun, receiver.AtReceiveCall(3));
            var first = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, first.SuccessfullyDecodedCount);
            Assert.True(first.IsReady);

            await firstCancellation.CancelAsync();
            await WaitForStoppedRunAsync(firstRun, firstCancellation.Token);
            secondRun = service.RunAsync(secondCancellation.Token);
            await WaitForBoundaryAsync(secondRun, receiver.AtReceiveCall(4));
            var restarted = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.True(restarted.ReceiverEpoch > first.ReceiverEpoch);
            Assert.Equal(2, restarted.SuccessfullyDecodedCount);
            Assert.False(restarted.IsReady);

            receiver.SupplyDelayedOldEpoch(payload);
            await WaitForBoundaryAsync(secondRun, receiver.AtReceiveCall(5));
            var afterOld = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(restarted.ReceiverEpoch, afterOld.ReceiverEpoch);
            Assert.Equal(2, afterOld.SuccessfullyDecodedCount);
            Assert.False(afterOld.IsReady);

            receiver.Supply(payload);
            await WaitForBoundaryAsync(secondRun, receiver.AtReceiveCall(6));
            var baseline = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(3, baseline.SuccessfullyDecodedCount);
            Assert.False(baseline.IsReady);

            receiver.Supply(payload);
            await WaitForBoundaryAsync(secondRun, receiver.AtReceiveCall(7));
            var progressed = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(4, progressed.SuccessfullyDecodedCount);
            Assert.True(progressed.IsReady);
        }
        finally
        {
            await firstCancellation.CancelAsync();
            await secondCancellation.CancelAsync();
            var both = Task.WhenAll(firstRun, secondRun ?? Task.CompletedTask);
            _ = await Record.ExceptionAsync(async () => await both.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(both.IsCompleted, "両RunのTaskはfixture終了前に終端する。");
        }
    }

    /// <summary>受信中に正しいownerを受け渡しても、旧受信試行を再利用せず新しい二件で準備を確立する。</summary>
    [Fact]
    public async Task IdentityHandoffDiscardsPendingOldEpochAndContinuesOneReceiver()
    {
        var diagnostics = new ServiceReceiveDiagnostics(identity: null, timeProvider: new FixedReceiveClock());
        var packet = CreatePacket().ToByteArray();
        var receiver = new ControlledReceiver(packet);
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        try
        {
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(3));
            var before = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, before.SuccessfullyDecodedCount);
            Assert.Null(before.Identity);
            Assert.False(before.IsReady);

            var identity = new ResourceDiagnosticIdentity("handoff-run", "opaque-resource", new string('a', 64), 1);
            service.ReplaceIdentity(identity);
            var rebound = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(identity, rebound.Identity);
            Assert.Equal(0, rebound.SuccessfullyDecodedCount);
            Assert.False(rebound.IsReady);

            receiver.SupplyDelayedOldEpoch(packet);
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(4));
            Assert.Equal(0, Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).SuccessfullyDecodedCount);

            receiver.Supply(packet);
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(5));
            var first = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(1, first.SuccessfullyDecodedCount);
            Assert.False(first.IsReady);

            receiver.Supply(packet);
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(6));
            var progressed = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, progressed.SuccessfullyDecodedCount);
            Assert.True(progressed.IsReady);
            Assert.Equal(identity, progressed.Identity);
            Assert.False(run.IsCompleted);
        }
        finally
        {
            await cancellation.CancelAsync();
            await WaitForStoppedRunAsync(run, cancellation.Token);
        }
    }

    /// <summary>取消の正常完了を待ち、想定外のfaultは失敗として伝える。</summary>
    private static async Task WaitForStoppedRunAsync(Task run, CancellationToken cancellationToken)
    {
        try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private static async Task WaitForBoundaryAsync(Task run, Task boundary)
    {
        await Task.WhenAny(run, boundary).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(run.IsCompleted, "同じpumpが次の受信を待機する。");
    }

    private static global::Referee CreatePacket() => new()
    {
        PacketTimestamp = 1,
        Stage = global::Referee.Types.Stage.NormalFirstHalf,
        Command = global::Referee.Types.Command.Halt,
        CommandCounter = 7,
        CommandTimestamp = 1,
        Yellow = CreateTeam("yellow"),
        Blue = CreateTeam("blue"),
    };

    private static global::Referee.Types.TeamInfo CreateTeam(string name) => new()
    {
        Name = name,
        Score = 0,
        RedCards = 0,
        YellowCards = 0,
        Timeouts = 0,
        TimeoutTime = 0,
        Goalkeeper = 0,
    };

    private sealed class ControlledReceiver : IRefereeDatagramReceiver
    {
        private readonly Channel<PendingDatagram> packets = Channel.CreateUnbounded<PendingDatagram>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> calls = new();
        private Func<ServiceReceiveDiagnostics.ReceiveAttempt>? oldCapture;
        private int receiveCalls;

        public ControlledReceiver(ReadOnlyMemory<byte> payload)
        {
            Supply(payload);
            Supply(payload);
        }

        public Task AtReceiveCall(int number) => GetSignal(number).Task;
        public void Supply(ReadOnlyMemory<byte> payload) => Enqueue(new(payload, null));

        public void SupplyDelayedOldEpoch(ReadOnlyMemory<byte> payload)
        {
            var capture = oldCapture ?? throw new InvalidOperationException("旧世代のcallbackが未採取。");
            Enqueue(new(payload, capture()));
        }

        private void Enqueue(PendingDatagram packet)
        {
            if (!packets.Writer.TryWrite(packet))
                throw new InvalidOperationException("fixtureへpacketを供給できなかった。");
        }

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref receiveCalls);
            if (number == 1)
                oldCapture = captureReceive;
            GetSignal(number).TrySetResult();
            var item = await packets.Reader.ReadAsync(cancellationToken);
            return new(item.Payload, new IPEndPoint(IPAddress.Parse("192.0.2.50"), 11003),
                item.Attempt ?? captureReceive());
        }

        private TaskCompletionSource GetSignal(int number) =>
            calls.GetOrAdd(number, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    private sealed record PendingDatagram(
        ReadOnlyMemory<byte> Payload,
        ServiceReceiveDiagnostics.ReceiveAttempt? Attempt);

    private sealed class FixedReceiveClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    }
}
