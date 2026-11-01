using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveSubscriberRegressionTests
{
    /// <summary>購読側の取消は単一pumpを止めず、追加の受信処理も開始しない。</summary>
    [Fact]
    public async Task CallerCancellationEndsOnlyItsPendingSubscription()
    {
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-subscriber-cancel", "referee-driver", new string('b', 64), 1),
            new FixedReceiveClock());
        var receiver = new ControlledReceiver(CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray());
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var runCancellation = new CancellationTokenSource();
        using var subscriberCancellation = new CancellationTokenSource();
        var run = service.RunAsync(runCancellation.Token);
        Task<RefereeCommandSnapshot>? subscription = null;
        try
        {
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(3));
            subscription = service.WaitForChangeAsync(7, subscriberCancellation.Token).AsTask();
            Assert.False(subscription.IsCompleted);
            await subscriberCancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await subscription.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(run.IsCompleted);
            Assert.Equal(3, receiver.ReceiveCalls);
            Assert.Equal(1, receiver.MaxActiveReceives);
            Assert.Equal(2, Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).SuccessfullyDecodedCount);
        }
        finally
        {
            await runCancellation.CancelAsync();
            await subscriberCancellation.CancelAsync();
            var both = Task.WhenAll(run, (Task?)subscription ?? Task.CompletedTask);
            _ = await Record.ExceptionAsync(async () => await both.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(both.IsCompleted, "受信と購読のTaskをfixture終了前に回収する。");
        }
    }

    /// <summary>前counter未指定では保持値を返すが、期限を過ぎた保持値を再購読で延命しない。</summary>
    [Fact]
    public async Task NullPreviousCounterReturnsFreshCacheButPollingDoesNotExtendItsAge()
    {
        var clock = new ManualReceiveClock();
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-subscriber-age", "referee-driver", new string('c', 64), 1), clock);
        var receiver = new ControlledReceiver(CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray());
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var runCancellation = new CancellationTokenSource();
        using var subscriberCancellation = new CancellationTokenSource();
        var run = service.RunAsync(runCancellation.Token);
        Task<RefereeCommandSnapshot>? expired = null;
        try
        {
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(3));
            clock.Advance(ServiceReceiveDiagnostics.FreshnessWindow);
            var atBoundary = await service.WaitForChangeAsync(null, subscriberCancellation.Token)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((uint)7, atBoundary.CommandCounter);
            clock.Advance(TimeSpan.FromTicks(1));
            expired = service.WaitForChangeAsync(null, subscriberCancellation.Token).AsTask();
            Assert.False(expired.IsCompleted, "期限切れの保持commandを返さない。");
            var state = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.False(state.IsReady);
            Assert.Equal(2, state.SuccessfullyDecodedCount);
            Assert.Equal(ServiceReceiveDiagnostics.FreshnessWindow + TimeSpan.FromTicks(1), state.DecodeAgeUpperBound);
            Assert.Equal(3, receiver.ReceiveCalls);
            Assert.Equal(1, receiver.MaxActiveReceives);
        }
        finally
        {
            await runCancellation.CancelAsync();
            await subscriberCancellation.CancelAsync();
            var both = Task.WhenAll(run, (Task?)expired ?? Task.CompletedTask);
            _ = await Record.ExceptionAsync(async () => await both.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(both.IsCompleted, "受信と購読のTaskをfixture終了前に回収する。");
        }
    }

    /// <summary>Run終端後は前counter未指定でも旧commandを返さず、呼出し元が待機を取り消せる。</summary>
    [Fact]
    public async Task EndedRunDoesNotReturnCachedCommand()
    {
        var diagnostics = CreateDiagnostics("run-03a-subscriber-end");
        var receiver = new ControlledReceiver(CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray());
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var runCancellation = new CancellationTokenSource();
        using var subscriberCancellation = new CancellationTokenSource();
        var run = service.RunAsync(runCancellation.Token);
        Task<RefereeCommandSnapshot>? subscription = null;
        try
        {
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(3));
            await runCancellation.CancelAsync();
            _ = await Record.ExceptionAsync(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(run.IsCompleted);
            subscription = service.WaitForChangeAsync(null, subscriberCancellation.Token).AsTask();
            Assert.False(subscription.IsCompleted, "終了したRunの保持commandを返さない。");
            var state = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, state.SuccessfullyDecodedCount);
            Assert.False(state.IsReady);
        }
        finally
        {
            await runCancellation.CancelAsync();
            await subscriberCancellation.CancelAsync();
            var both = Task.WhenAll(run, (Task?)subscription ?? Task.CompletedTask);
            _ = await Record.ExceptionAsync(async () => await both.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(both.IsCompleted, "受信と購読のTaskをfixture終了前に回収する。");
        }
    }

    /// <summary>破損packetの失敗診断後は直前のcommandを返さず、同じpumpの正常受信で復帰する。</summary>
    [Fact]
    public async Task InvalidPacketDoesNotReturnCachedCommandAndNextValidPacketCanCompleteWaiter()
    {
        var diagnostics = CreateDiagnostics("run-03a-subscriber-invalid");
        var receiver = new ControlledReceiver(CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray());
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var runCancellation = new CancellationTokenSource();
        using var subscriberCancellation = new CancellationTokenSource();
        var run = service.RunAsync(runCancellation.Token);
        Task<RefereeCommandSnapshot>? subscription = null;
        try
        {
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(3));
            receiver.Supply(new byte[] { 0xff });
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(4));
            subscription = service.WaitForChangeAsync(null, subscriberCancellation.Token).AsTask();
            Assert.False(subscription.IsCompleted, "失敗前の保持commandを返さない。");
            var failed = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, failed.SuccessfullyDecodedCount);
            Assert.False(failed.IsReady);

            receiver.Supply(CreatePacket(8, global::Referee.Types.Command.Stop).ToByteArray());
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(5));
            var next = await subscription.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((uint)8, next.CommandCounter);
            Assert.Equal(global::Referee.Types.Command.Stop, next.Command);
            Assert.Equal(3, Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).SuccessfullyDecodedCount);
            Assert.Equal(1, receiver.MaxActiveReceives);
        }
        finally
        {
            await runCancellation.CancelAsync();
            await subscriberCancellation.CancelAsync();
            var both = Task.WhenAll(run, (Task?)subscription ?? Task.CompletedTask);
            _ = await Record.ExceptionAsync(async () => await both.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(both.IsCompleted, "受信と購読のTaskをfixture終了前に回収する。");
        }
    }

    /// <summary>再開で旧cacheを解除し、旧世代の遅い受信を購読成功へ変換しない。</summary>
    [Fact]
    public async Task RestartAndDelayedOldEpochDoNotReturnCachedCommand()
    {
        var diagnostics = CreateDiagnostics("run-03a-subscriber-epoch");
        var receiver = new ControlledReceiver(CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray());
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        using var subscriberCancellation = new CancellationTokenSource();
        var firstRun = service.RunAsync(firstCancellation.Token);
        Task? secondRun = null;
        Task<RefereeCommandSnapshot>? subscription = null;
        try
        {
            await WaitForBoundaryAsync(firstRun, receiver.AtReceiveCall(3));
            var firstEpoch = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).ReceiverEpoch;
            await firstCancellation.CancelAsync();
            _ = await Record.ExceptionAsync(async () => await firstRun.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(firstRun.IsCompleted);
            secondRun = service.RunAsync(secondCancellation.Token);
            await WaitForBoundaryAsync(secondRun, receiver.AtReceiveCall(4));
            subscription = service.WaitForChangeAsync(null, subscriberCancellation.Token).AsTask();
            Assert.False(subscription.IsCompleted, "再開時に旧cacheを返さない。");

            receiver.SupplyDelayedOldEpoch(CreatePacket(8, global::Referee.Types.Command.Stop).ToByteArray());
            await WaitForBoundaryAsync(secondRun, receiver.AtReceiveCall(5));
            Assert.False(subscription.IsCompleted, "旧世代の受信を購読成功へ変換しない。");
            var afterOld = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.True(afterOld.ReceiverEpoch > firstEpoch);
            Assert.Equal(2, afterOld.SuccessfullyDecodedCount);
            Assert.False(afterOld.IsReady);

            receiver.Supply(CreatePacket(9, global::Referee.Types.Command.Stop).ToByteArray());
            await WaitForBoundaryAsync(secondRun, receiver.AtReceiveCall(6));
            Assert.Equal((uint)9, (await subscription.WaitAsync(TimeSpan.FromSeconds(5))).CommandCounter);
            Assert.Equal(3, Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).SuccessfullyDecodedCount);
            Assert.Equal(1, receiver.MaxActiveReceives);
        }
        finally
        {
            await firstCancellation.CancelAsync();
            await secondCancellation.CancelAsync();
            await subscriberCancellation.CancelAsync();
            var all = Task.WhenAll(firstRun, secondRun ?? Task.CompletedTask, (Task?)subscription ?? Task.CompletedTask);
            _ = await Record.ExceptionAsync(async () => await all.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(all.IsCompleted, "両Runと購読のTaskをfixture終了前に回収する。");
        }
    }

    private static ServiceReceiveDiagnostics CreateDiagnostics(string runId) => new(
        new ResourceDiagnosticIdentity(runId, "referee-driver", new string('d', 64), 1),
        new FixedReceiveClock());

    private static async Task WaitForBoundaryAsync(Task run, Task boundary)
    {
        await Task.WhenAny(run, boundary).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(run.IsCompleted, "同じpumpが次の受信を待機する。");
    }

    private static global::Referee CreatePacket(uint counter, global::Referee.Types.Command command) => new()
    {
        PacketTimestamp = 1,
        Stage = global::Referee.Types.Stage.NormalFirstHalf,
        Command = command,
        CommandCounter = counter,
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
        private readonly object gate = new();
        private Func<ServiceReceiveDiagnostics.ReceiveAttempt>? oldCapture;
        private int receiveCalls;
        private int activeReceives;
        private int maxActiveReceives;

        public ControlledReceiver(ReadOnlyMemory<byte> initial)
        {
            Supply(initial);
            Supply(initial);
        }

        public int ReceiveCalls
        {
            get { lock (gate) return receiveCalls; }
        }

        public int MaxActiveReceives
        {
            get { lock (gate) return maxActiveReceives; }
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
            lock (gate)
            {
                oldCapture ??= captureReceive;
                activeReceives++;
                maxActiveReceives = Math.Max(maxActiveReceives, activeReceives);
                GetSignal(++receiveCalls).TrySetResult();
            }
            try
            {
                var payload = await packets.Reader.ReadAsync(cancellationToken);
                return new(payload.Payload, new IPEndPoint(IPAddress.Parse("192.0.2.70"), 11003),
                    payload.Attempt ?? captureReceive());
            }
            finally
            {
                lock (gate) activeReceives--;
            }
        }

        private TaskCompletionSource GetSignal(int number) =>
            calls.GetOrAdd(number, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

        private sealed record PendingDatagram(
            ReadOnlyMemory<byte> Payload,
            ServiceReceiveDiagnostics.ReceiveAttempt? Attempt);
    }

    private sealed class FixedReceiveClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    }
    private sealed class ManualReceiveClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref timestamp);
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(GetTimestamp());
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref timestamp, elapsed.Ticks);
    }

}
