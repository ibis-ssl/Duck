using System.Net;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveCancellationTests
{
    /// <summary>受信後の取消で、時計の期限切れを待たずに準備を解除する。</summary>
    [Fact]
    public async Task CancellationAfterSuccessfulReceivesInvalidatesReadinessWithoutClockExpiry()
    {
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-cancel", "referee-driver", new string('c', 64), 1),
            new FixedReceiveClock());
        var receiver = new WaitingReceiver(CreatePacket().ToByteArray());
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        try
        {
            await Task.WhenAny(run, receiver.WaitingForThirdReceive).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted, "正常受信後もpumpが次のpacketを待つ。");
            var before = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, before.SuccessfullyDecodedCount);
            Assert.True(before.IsReady);

            await cancellation.CancelAsync();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // pumpの取消完了を待ち、例外の有無を準備判定の契約へ追加しない。
            }

            Assert.True(run.IsCompleted);
            var stopped = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.False(stopped.IsReady);
            Assert.Equal(2, stopped.SuccessfullyDecodedCount);
            Assert.Equal(3, receiver.ReceiveCalls);
        }
        finally
        {
            await cancellation.CancelAsync();
            if (run.IsFaulted)
            {
                _ = run.Exception;
            }
            else
            {
                try
                {
                    await run.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    // fixtureの受信待機を必ず終端させる。
                }
            }
        }
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

    private sealed class WaitingReceiver : IRefereeDatagramReceiver
    {
        private readonly Channel<ReadOnlyMemory<byte>> packets = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly TaskCompletionSource waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int receiveCalls;

        public WaitingReceiver(ReadOnlyMemory<byte> packet)
        {
            if (!packets.Writer.TryWrite(packet) || !packets.Writer.TryWrite(packet))
                throw new InvalidOperationException("fixtureへ正常packetを供給できなかった。");
        }

        public Task WaitingForThirdReceive => waiting.Task;
        public int ReceiveCalls => Volatile.Read(ref receiveCalls);

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref receiveCalls) == 3)
                waiting.TrySetResult();
            var payload = await packets.Reader.ReadAsync(cancellationToken);
            return new(payload, new IPEndPoint(IPAddress.Parse("192.0.2.30"), 11003), captureReceive());
        }
    }

    private sealed class FixedReceiveClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    }
}
