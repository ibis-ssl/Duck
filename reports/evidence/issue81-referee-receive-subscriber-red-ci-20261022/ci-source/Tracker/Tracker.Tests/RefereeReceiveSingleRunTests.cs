using System.Net;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveSingleRunTests
{
    /// <summary>同じ受信機へのRunが重なっても、受信処理を並列に増やさない。</summary>
    [Fact]
    public async Task OverlappingRunsNeverCreateConcurrentReceives()
    {
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-single", "referee-driver", new string('d', 64), 1),
            new FixedReceiveClock());
        var receiver = new MeasuringReceiver(CreatePacket().ToByteArray());
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var firstRun = service.RunAsync(firstCancellation.Token);
        Task? overlappingRun = null;
        Task? bothRuns = null;
        try
        {
            await Task.WhenAny(firstRun, receiver.WaitingForThirdReceive).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(firstRun.IsCompleted);
            var initial = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, initial.SuccessfullyDecodedCount);
            Assert.True(initial.IsReady);

            // 拒否か既存処理の共有かを固定せず、返されたTaskは必ず終端まで回収する。
            _ = Record.Exception(() =>
            {
                overlappingRun = service.RunAsync(secondCancellation.Token);
            });
            await firstCancellation.CancelAsync();
            await secondCancellation.CancelAsync();
            bothRuns = Task.WhenAll(firstRun, overlappingRun ?? Task.CompletedTask);
            await WaitForTerminalAsync(bothRuns);

            Assert.False(firstRun.IsFaulted);
            Assert.Equal(1, receiver.MaxActiveReceives);
        }
        finally
        {
            await firstCancellation.CancelAsync();
            await secondCancellation.CancelAsync();
            bothRuns ??= Task.WhenAll(firstRun, overlappingRun ?? Task.CompletedTask);
            await WaitForTerminalAsync(bothRuns);
        }
    }

    /// <summary>取消や二重起動の拒否の例外型を固定せず、両処理の終端を確認する。</summary>
    private static async Task WaitForTerminalAsync(Task task)
    {
        _ = await Record.ExceptionAsync(async () => await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(task.IsCompleted, "両RunのTaskはfixture終了前に終端する。");
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

    private sealed class MeasuringReceiver : IRefereeDatagramReceiver
    {
        private readonly Channel<ReadOnlyMemory<byte>> packets = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly TaskCompletionSource waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object gate = new();
        private int receiveCalls;
        private int activeReceives;
        private int maxActiveReceives;

        public MeasuringReceiver(ReadOnlyMemory<byte> packet)
        {
            if (!packets.Writer.TryWrite(packet) || !packets.Writer.TryWrite(packet))
                throw new InvalidOperationException("fixtureへ正常packetを供給できなかった。");
        }

        public Task WaitingForThirdReceive => waiting.Task;
        public int MaxActiveReceives
        {
            get { lock (gate) return maxActiveReceives; }
        }

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                activeReceives++;
                maxActiveReceives = Math.Max(maxActiveReceives, activeReceives);
                if (++receiveCalls == 3)
                    waiting.TrySetResult();
            }
            try
            {
                var payload = await packets.Reader.ReadAsync(cancellationToken);
                return new(payload, new IPEndPoint(IPAddress.Parse("192.0.2.40"), 11003), captureReceive());
            }
            finally
            {
                lock (gate) activeReceives--;
            }
        }
    }

    private sealed class FixedReceiveClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    }
}
