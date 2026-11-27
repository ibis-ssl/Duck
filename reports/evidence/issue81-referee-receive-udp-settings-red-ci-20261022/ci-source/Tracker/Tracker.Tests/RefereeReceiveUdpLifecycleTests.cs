using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveUdpLifecycleTests
{
    /// <summary>実datagramのpayloadと送信元を保ち、復号前の時計を後段へ渡す。</summary>
    [Fact]
    public async Task UdpAdapterReturnsActualDatagramEndpointAndReceiveStamp()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var receiver = new UdpRefereeDatagramReceiver(socket);
        var clock = new ManualReceiveClock();
        var diagnostics = CreateDiagnostics(clock);
        var epoch = diagnostics.BeginEpoch(RefereeReceiveService.InputId);
        var payload = new byte[] { 0x22, 0x33, 0x44 };
        var destination = Assert.IsType<IPEndPoint>(socket.Client.LocalEndPoint);
        await sender.SendAsync(payload, destination);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var captures = 0;
        var datagram = await receiver.ReceiveAsync(
            () => { captures++; return diagnostics.BeginReceive(epoch); },
            cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(payload, datagram.Payload.ToArray());
        Assert.Equal(sender.Client.LocalEndPoint, datagram.RemoteEndpoint);
        Assert.Equal(1, captures);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.True(diagnostics.RecordSuccess(datagram.ReceiveAttempt, "sender"));
        var state = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
        Assert.Equal(TimeSpan.FromSeconds(3), state.DecodeAgeUpperBound);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero), state.LastReceivedAt);
    }

    /// <summary>呼出し元取消なしでDisposeがRunと明示所有受信資源を終端し、同じ時計で準備を解除する。</summary>
    [Fact]
    public async Task DisposeStopsRunAndDisposesExplicitlyOwnedReceiverWithoutCallerCancellation()
    {
        var diagnostics = CreateDiagnostics(new ManualReceiveClock());
        var receiver = new OwnedReceiver(CreatePacket().ToByteArray());
        var service = new RefereeReceiveService(receiver, diagnostics, ownedReceiver: receiver);
        using var runCancellation = new CancellationTokenSource();
        var run = service.RunAsync(runCancellation.Token);
        try
        {
            await Task.WhenAny(run, receiver.WaitingForThirdReceive).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            var before = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, before.SuccessfullyDecodedCount);
            Assert.True(before.IsReady);

            await service.DisposeAsync();
            Assert.Equal(1, receiver.DisposeCalls);
            Assert.True(run.IsCompleted, "Dispose完了時に所有Runも終端する。");
            Assert.False(runCancellation.IsCancellationRequested);
            var after = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, after.SuccessfullyDecodedCount);
            Assert.False(after.IsReady, "時間経過やcaller取消に依存せず準備を解除する。");
        }
        finally
        {
            await runCancellation.CancelAsync();
            _ = await Record.ExceptionAsync(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(run.IsCompleted, "fixture終了前にRunを回収する。");
            await service.DisposeAsync();
            await receiver.DisposeAsync();
        }
    }

    private static ServiceReceiveDiagnostics CreateDiagnostics(TimeProvider clock) => new(
        new ResourceDiagnosticIdentity("run-03a-udp-dispose", "referee-driver", new string('f', 64), 1), clock);

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
        Name = name, Score = 0, RedCards = 0, YellowCards = 0,
        Timeouts = 0, TimeoutTime = 0, Goalkeeper = 0,
    };

    private sealed class OwnedReceiver : IRefereeDatagramReceiver, IAsyncDisposable
    {
        private readonly Channel<ReadOnlyMemory<byte>> packets = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly TaskCompletionSource waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int receives;
        private int disposeCalls;

        public OwnedReceiver(ReadOnlyMemory<byte> payload)
        {
            if (!packets.Writer.TryWrite(payload) || !packets.Writer.TryWrite(payload))
                throw new InvalidOperationException("fixtureへ正常packetを供給できなかった。");
        }

        public Task WaitingForThirdReceive => waiting.Task;
        public int DisposeCalls => Volatile.Read(ref disposeCalls);

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref receives) == 3)
                waiting.TrySetResult();
            var payload = await packets.Reader.ReadAsync(cancellationToken);
            return new(payload, new IPEndPoint(IPAddress.Loopback, 11003), captureReceive());
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref disposeCalls);
            return ValueTask.CompletedTask;
        }
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
