using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveCacheRaceTests
{
    /// <summary>次packetの診断だけが新しくても期限切れcommandを返さず、公開後のcommandを返す。</summary>
    [Fact]
    public async Task NextPacketDiagnosticsCannotRefreshExpiredCommandBeforeItsPublication()
    {
        var clock = new ManualReceiveClock();
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-cache-race", "referee-driver", new string('e', 64), 1), clock);
        var receiver = new ControlledReceiver();
        receiver.Supply(CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray());
        receiver.Supply(CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray());
        using var allowDiagnosticRead = new ManualResetEventSlim();
        using var allowPublication = new ManualResetEventSlim();
        var subscriberHasCommandGate = NewSignal();
        var nextPacketAccepted = NewSignal();
        var subscriberEnteredWait = NewSignal();
        var armed = 0;
        var stoppedSubscriber = 0;
        var stoppedPublication = 0;
        await using var service = new RefereeReceiveService(receiver, diagnostics)
        {
            BoundaryObserver = boundary =>
            {
                if (Volatile.Read(ref armed) == 0)
                    return;
                switch (boundary)
                {
                    case RefereeReceiveBoundary.BeforeCommandDiagnosticRead:
                        if (Interlocked.Exchange(ref stoppedSubscriber, 1) == 0)
                        {
                            subscriberHasCommandGate.TrySetResult();
                            WaitForRelease(allowDiagnosticRead);
                        }
                        break;
                    case RefereeReceiveBoundary.BeforeCommandPublish:
                        if (Interlocked.Exchange(ref stoppedPublication, 1) == 0)
                        {
                            nextPacketAccepted.TrySetResult();
                            WaitForRelease(allowPublication);
                        }
                        break;
                    case RefereeReceiveBoundary.BeforeCommandWait:
                        subscriberEnteredWait.TrySetResult();
                        break;
                }
            },
        };
        using var runCancellation = new CancellationTokenSource();
        using var subscriberCancellation = new CancellationTokenSource();
        var run = service.RunAsync(runCancellation.Token);
        Task<RefereeCommandSnapshot>? subscription = null;
        try
        {
            await receiver.AtReceiveCall(3).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            var first = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, first.SuccessfullyDecodedCount);
            Assert.True(first.IsReady);

            clock.Advance(TimeSpan.FromSeconds(6));
            Assert.False(Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).IsReady);
            Volatile.Write(ref armed, 1);
            subscription = Task.Run(async () =>
                await service.WaitForChangeAsync(null, subscriberCancellation.Token));
            await subscriberHasCommandGate.Task.WaitAsync(TimeSpan.FromSeconds(5));

            receiver.Supply(CreatePacket(8, global::Referee.Types.Command.Stop).ToByteArray());
            await nextPacketAccepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var accepted = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(first.SourceKey, accepted.SourceKey);
            Assert.Equal(first.ReceiverEpoch, accepted.ReceiverEpoch);
            Assert.Equal(3, accepted.SuccessfullyDecodedCount);
            Assert.True(accepted.IsReady);
            Assert.Equal(TimeSpan.Zero, accepted.DecodeAgeUpperBound);
            Assert.False(allowPublication.IsSet);

            allowDiagnosticRead.Set();
            await Task.WhenAny(subscription, subscriberEnteredWait.Task).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(subscription.IsCompleted,
                "次packetの診断受理だけでは、期限切れcommand Aを購読へ返してはならない。");
            Assert.True(subscriberEnteredWait.Task.IsCompleted);
            Assert.False(allowPublication.IsSet);

            allowPublication.Set();
            var published = await subscription.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((uint)8, published.CommandCounter);
            Assert.Equal(global::Referee.Types.Command.Stop, published.Command);
            Assert.Equal("yellow", published.YellowTeamName);
            await receiver.AtReceiveCall(4).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            Assert.Equal(3, Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).SuccessfullyDecodedCount);
            Assert.Equal(1, receiver.MaxActiveReceives);
        }
        finally
        {
            allowDiagnosticRead.Set();
            allowPublication.Set();
            await runCancellation.CancelAsync();
            await subscriberCancellation.CancelAsync();
            var all = Task.WhenAll(run, (Task?)subscription ?? Task.CompletedTask);
            _ = await Record.ExceptionAsync(async () => await all.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(all.IsCompleted, "受信と購読のTaskをfixture終了前に回収する。");
        }
    }

    /// <summary>競合順序の境界は合図で固定し、期限はfixtureの回収不能だけを検出する。</summary>
    private static void WaitForRelease(ManualResetEventSlim release)
    {
        if (!release.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("競合fixtureの解放合図が届かなかった。");
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

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
        private int receiveCalls;
        private int activeReceives;
        private int maxActiveReceives;

        public int MaxActiveReceives => Volatile.Read(ref maxActiveReceives);

        public Task AtReceiveCall(int number) => GetSignal(number).Task;

        public void Supply(ReadOnlyMemory<byte> packet)
        {
            if (!packets.Writer.TryWrite(packet))
                throw new InvalidOperationException("競合fixtureへpacketを供給できなかった。");
        }

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref activeReceives);
            Interlocked.Exchange(ref maxActiveReceives, Math.Max(active, MaxActiveReceives));
            GetSignal(Interlocked.Increment(ref receiveCalls)).TrySetResult();
            try
            {
                var packet = await packets.Reader.ReadAsync(cancellationToken);
                return new(packet, new IPEndPoint(IPAddress.Parse("192.0.2.70"), 11003), captureReceive());
            }
            finally
            {
                Interlocked.Decrement(ref activeReceives);
            }
        }

        private TaskCompletionSource GetSignal(int number) => calls.GetOrAdd(number, _ => NewSignal());
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
