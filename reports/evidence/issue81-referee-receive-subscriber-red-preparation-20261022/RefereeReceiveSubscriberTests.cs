using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveSubscriberTests
{
    /// <summary>同counterの診断は更新しつつ購読を待機させ、新counterを単一pumpから通知する。</summary>
    [Fact]
    public async Task SubscriberWaitsThroughDuplicateCounterWithoutCompetingForReceives()
    {
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-subscriber", "referee-driver", new string('a', 64), 1),
            new FixedReceiveClock());
        var initial = CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray();
        var receiver = new ControlledReceiver(initial);
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var runCancellation = new CancellationTokenSource();
        using var subscriberCancellation = new CancellationTokenSource();
        var run = service.RunAsync(runCancellation.Token);
        Task<RefereeCommandSnapshot>? subscription = null;
        try
        {
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(3));
            var before = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, before.SuccessfullyDecodedCount);
            Assert.True(before.IsReady);

            IRefereeCommandSource commandSource = service;
            subscription = commandSource.WaitForChangeAsync(7, subscriberCancellation.Token).AsTask();
            receiver.Supply(initial);
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(4));
            Assert.Equal(1, receiver.MaxActiveReceives);
            Assert.Equal(4, receiver.ReceiveCalls);
            var duplicate = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(3, duplicate.SuccessfullyDecodedCount);
            Assert.True(duplicate.IsReady);
            Assert.False(subscription.IsCompleted, "同counterは診断へ数えても変更購読を完了させない。");

            receiver.Supply(CreatePacket(8, global::Referee.Types.Command.Stop).ToByteArray());
            await WaitForBoundaryAsync(run, receiver.AtReceiveCall(5));
            var changed = await subscription.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((uint)8, changed.CommandCounter);
            Assert.Equal(global::Referee.Types.Command.Stop, changed.Command);
            var after = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(4, after.SuccessfullyDecodedCount);
            Assert.True(after.IsReady);
            Assert.Equal(1, receiver.MaxActiveReceives);
            Assert.Equal(5, receiver.ReceiveCalls);
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
        private readonly Channel<ReadOnlyMemory<byte>> packets = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> calls = new();
        private readonly object gate = new();
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

        public void Supply(ReadOnlyMemory<byte> payload)
        {
            if (!packets.Writer.TryWrite(payload))
                throw new InvalidOperationException("fixtureへpacketを供給できなかった。");
        }

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                activeReceives++;
                maxActiveReceives = Math.Max(maxActiveReceives, activeReceives);
                GetSignal(++receiveCalls).TrySetResult();
            }
            try
            {
                var payload = await packets.Reader.ReadAsync(cancellationToken);
                return new(payload, new IPEndPoint(IPAddress.Parse("192.0.2.70"), 11003), captureReceive());
            }
            finally
            {
                lock (gate) activeReceives--;
            }
        }

        private TaskCompletionSource GetSignal(int number) =>
            calls.GetOrAdd(number, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    private sealed class FixedReceiveClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    }
}
