using System.Net;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveReadinessBoundaryTests
{
    /// <summary>正常な復号と送信元endpointがあっても、明示owner欠落を準備完了にしない。</summary>
    [Fact]
    public async Task MissingOwnerRemainsNotReadyDespiteDecodedPacketsAndProducerEndpoint()
    {
        var clock = new ManualReceiveClock();
        var diagnostics = new ServiceReceiveDiagnostics(identity: null, timeProvider: clock);
        var receiver = new WaitingReceiver(CreatePacket().ToByteArray());
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        try
        {
            await WaitForReceiveBoundaryAsync(run, receiver.WaitingForThirdReceive);
            var snapshot = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, snapshot.SuccessfullyDecodedCount);
            Assert.Equal(DiagnosticDecodeStatus.Succeeded, snapshot.DecodeStatus);
            Assert.Equal(TimeSpan.Zero, snapshot.DecodeAgeUpperBound);
            Assert.Equal("192.0.2.60:11003", snapshot.RemoteEndpoint);
            Assert.Equal(snapshot.RemoteEndpoint, snapshot.SourceKey);
            Assert.Null(snapshot.Identity);
            Assert.False(snapshot.IsReady);
        }
        finally
        {
            await StopAsync(run, cancellation);
        }
    }

    /// <summary>pollで受信時刻を延長せず、期限ちょうどとその直後の準備状態を区別する。</summary>
    [Fact]
    public async Task PollingDoesNotExtendFreshnessAndExactBoundaryExpiresAfterOneTick()
    {
        var clock = new ManualReceiveClock();
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-freshness", "referee-driver", new string('f', 64), 1),
            clock);
        var receiver = new WaitingReceiver(CreatePacket().ToByteArray());
        await using var service = new RefereeReceiveService(receiver, diagnostics);
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        try
        {
            await WaitForReceiveBoundaryAsync(run, receiver.WaitingForThirdReceive);
            var initial = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, initial.SuccessfullyDecodedCount);
            Assert.True(initial.IsReady);
            Assert.NotNull(initial.LastReceivedAt);

            clock.Advance(TimeSpan.FromSeconds(2));
            for (var poll = 0; poll < 3; poll++)
            {
                var current = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
                Assert.True(current.IsReady);
                Assert.Equal(2, current.SuccessfullyDecodedCount);
                Assert.Equal(initial.LastReceivedAt, current.LastReceivedAt);
                Assert.Equal(TimeSpan.FromSeconds(2), current.DecodeAgeUpperBound);
            }

            clock.Advance(ServiceReceiveDiagnostics.FreshnessWindow - TimeSpan.FromSeconds(2));
            var boundary = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.True(boundary.IsReady);
            Assert.Equal(ServiceReceiveDiagnostics.FreshnessWindow, boundary.DecodeAgeUpperBound);
            Assert.Equal(initial.LastReceivedAt, boundary.LastReceivedAt);

            clock.Advance(TimeSpan.FromTicks(1));
            for (var poll = 0; poll < 3; poll++)
            {
                var expired = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
                Assert.False(expired.IsReady);
                Assert.Equal(2, expired.SuccessfullyDecodedCount);
                Assert.Equal(initial.LastReceivedAt, expired.LastReceivedAt);
                Assert.Equal(ServiceReceiveDiagnostics.FreshnessWindow + TimeSpan.FromTicks(1),
                    expired.DecodeAgeUpperBound);
            }
        }
        finally
        {
            await StopAsync(run, cancellation);
        }
    }

    private static async Task WaitForReceiveBoundaryAsync(Task run, Task boundary)
    {
        await Task.WhenAny(run, boundary).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(run.IsCompleted, "同じpumpが次の受信を待機する。");
    }

    /// <summary>すべてのfixtureで取消を発行し、受信Taskを終端まで回収する。</summary>
    private static async Task StopAsync(Task run, CancellationTokenSource cancellation)
    {
        await cancellation.CancelAsync();
        if (run.IsFaulted)
        {
            _ = run.Exception;
            return;
        }
        try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
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

        public WaitingReceiver(ReadOnlyMemory<byte> payload)
        {
            if (!packets.Writer.TryWrite(payload) || !packets.Writer.TryWrite(payload))
                throw new InvalidOperationException("fixtureへ正常packetを供給できなかった。");
        }

        public Task WaitingForThirdReceive => waiting.Task;

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref receiveCalls) == 3)
                waiting.TrySetResult();
            var payload = await packets.Reader.ReadAsync(cancellationToken);
            return new(payload, new IPEndPoint(IPAddress.Parse("192.0.2.60"), 11003), captureReceive());
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
