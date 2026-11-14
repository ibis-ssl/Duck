using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveFailureTests
{
    /// <summary>不正入力は正常件数へ混ぜず準備を解除し、同じpumpの次の正常入力で復帰する。</summary>
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown-command")]
    [InlineData("missing-command")]
    [InlineData("missing-counter")]
    public async Task InvalidPacketInvalidatesReadinessWithoutStoppingPumpAndValidPacketRecovers(string invalidKind)
    {
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-failure", "referee-driver", new string('b', 64), 1),
            new FixedReceiveClock());
        var valid = CreatePacket().ToByteArray();
        var receiver = new ControlledReceiver(valid, valid);
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        try
        {
            await ReachBoundaryAsync(run, receiver.AtReceiveCall(3));
            Assert.False(run.IsCompleted, "正常入力の後も同じ受信pumpが待機する。");
            var initial = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, initial.SuccessfullyDecodedCount);
            Assert.Equal(0, initial.DecodeFailureCount);
            Assert.True(initial.IsReady);

            receiver.Supply(CreateInvalidPacket(invalidKind));
            await ReachBoundaryAsync(run, receiver.AtReceiveCall(4));
            Assert.False(run.IsCompleted, "破損や意味の欠落があっても受信pumpを停止しない。");
            var failed = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, failed.SuccessfullyDecodedCount);
            Assert.Equal(1, failed.DecodeFailureCount);
            Assert.Equal(DiagnosticDecodeStatus.Failed, failed.DecodeStatus);
            Assert.False(failed.IsReady);

            receiver.Supply(valid);
            await ReachBoundaryAsync(run, receiver.AtReceiveCall(5));
            Assert.False(run.IsCompleted, "次の正常入力も同じpumpで処理して待機する。");
            var recovered = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(3, recovered.SuccessfullyDecodedCount);
            Assert.Equal(1, recovered.DecodeFailureCount);
            Assert.Equal(DiagnosticDecodeStatus.Succeeded, recovered.DecodeStatus);
            Assert.True(recovered.IsReady);
        }
        finally
        {
            await cancellation.CancelAsync();
            if (run.IsFaulted)
            {
                // pump停止は上のassertで検出し、後始末の例外で元の失敗を上書きしない。
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
                    // 受信待機の取消はfixtureの正常な後始末として扱う。
                }
            }
        }
    }

    private static async Task ReachBoundaryAsync(Task run, Task boundary) =>
        await Task.WhenAny(run, boundary).WaitAsync(TimeSpan.FromSeconds(5));

    private static ReadOnlyMemory<byte> CreateInvalidPacket(string kind) => kind switch
    {
        "malformed" => new byte[] { 0x80 },
        "unknown-command" => CreatePacket(command: (global::Referee.Types.Command)999).ToByteArray(),
        "missing-command" => CreatePacket(includeCommand: false).ToByteArray(),
        "missing-counter" => CreatePacket(includeCounter: false).ToByteArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static global::Referee CreatePacket(
        bool includeCommand = true,
        bool includeCounter = true,
        global::Referee.Types.Command command = global::Referee.Types.Command.Halt)
    {
        var packet = new global::Referee
        {
            PacketTimestamp = 1,
            Stage = global::Referee.Types.Stage.NormalFirstHalf,
            CommandTimestamp = 1,
            Yellow = CreateTeam("yellow"),
            Blue = CreateTeam("blue"),
        };
        if (includeCommand)
            packet.Command = command;
        if (includeCounter)
            packet.CommandCounter = 7;
        return packet;
    }

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

        public ControlledReceiver(params ReadOnlyMemory<byte>[] initialPackets)
        {
            foreach (var packet in initialPackets)
                Supply(packet);
        }

        public void Supply(ReadOnlyMemory<byte> payload)
        {
            if (!packets.Writer.TryWrite(payload))
                throw new InvalidOperationException("fixtureへpacketを供給できなかった。");
        }

        public Task AtReceiveCall(int number) => GetSignal(number).Task;

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            GetSignal(Interlocked.Increment(ref receiveCalls)).TrySetResult();
            var payload = await packets.Reader.ReadAsync(cancellationToken);
            return new(payload, new IPEndPoint(IPAddress.Parse("192.0.2.20"), 11003), captureReceive());
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
