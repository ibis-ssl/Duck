using System.Net;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveDiagnosticsTests
{
    /// <summary>同じcounterの正常packetも毎回数え、復号時間を含むageと受信ownerを保持する。</summary>
    [Fact]
    public async Task RepeatedCommandCounterCountsEachDecodedPacketAndPreservesReceiveClock()
    {
        var clock = new ReceiveTestClock();
        var identity = new ResourceDiagnosticIdentity("run-03a", "referee-driver", new string('a', 64), 1);
        var diagnostics = new ServiceReceiveDiagnostics(identity, clock);
        var endpoint = new IPEndPoint(IPAddress.Parse("192.0.2.10"), 11003);
        var packet = CreatePacket(7).ToByteArray();
        var receiver = new ScriptedReceiver(clock, endpoint, packet, packet);
        await using var service = new RefereeReceiveService(
            receiver,
            diagnostics,
            payload =>
            {
                clock.Advance(TimeSpan.FromSeconds(2));
                return global::Referee.Parser.ParseFrom(payload.Span);
            });
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        try
        {
            // 本実装では3回目の受信待機が、先の2packetの処理完了を示す。
            // no-op scaffoldは即時完了するため、timeoutではなく件数assertで不足を示す。
            await Task.WhenAny(run, receiver.WaitingForNextPacket).WaitAsync(TimeSpan.FromSeconds(5));
            await ObserveCompletedRunAsync(run);

            var snapshot = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, snapshot.SuccessfullyDecodedCount);
            Assert.Equal(0, snapshot.DecodeFailureCount);
            Assert.Equal(DiagnosticDecodeStatus.Succeeded, snapshot.DecodeStatus);
            Assert.Equal(identity, snapshot.Identity);
            Assert.Equal(endpoint.ToString(), snapshot.RemoteEndpoint);
            Assert.Null(snapshot.SourceUuid);
            Assert.Equal(clock.InitialUtc.AddSeconds(3), snapshot.LastReceivedAt);
            Assert.Equal(TimeSpan.FromSeconds(2), snapshot.DecodeAgeUpperBound);
            Assert.True(snapshot.IsReady);
            Assert.Equal(3, receiver.ReceiveCalls);
        }
        finally
        {
            await cancellation.CancelAsync();
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

    private static Task ObserveCompletedRunAsync(Task run) =>
        run.IsCompleted ? run : Task.CompletedTask;

    private static global::Referee CreatePacket(uint counter) => new()
    {
        PacketTimestamp = 1,
        Stage = global::Referee.Types.Stage.NormalFirstHalf,
        Command = global::Referee.Types.Command.Halt,
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

    private sealed class ScriptedReceiver(
        ReceiveTestClock clock,
        IPEndPoint endpoint,
        params ReadOnlyMemory<byte>[] payloads) : IRefereeDatagramReceiver
    {
        private readonly Queue<ReadOnlyMemory<byte>> packets = new(payloads);
        private readonly TaskCompletionSource waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitingForNextPacket => waiting.Task;
        public int ReceiveCalls { get; private set; }

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceiveCalls++;
            if (packets.Count > 0)
            {
                if (ReceiveCalls == 2)
                    clock.Advance(TimeSpan.FromSeconds(1));
                var payload = packets.Dequeue();
                return new(payload, endpoint, captureReceive());
            }

            waiting.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("取消される受信待機が予期せず完了した。");
        }
    }

    private sealed class ReceiveTestClock : TimeProvider
    {
        private long timestamp;
        public DateTimeOffset InitialUtc { get; } = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => InitialUtc + TimeSpan.FromTicks(timestamp);
        public void Advance(TimeSpan duration) => timestamp += duration.Ticks;
    }
}
