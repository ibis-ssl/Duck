using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeReceiveUdpSettingsAndDisposalTests
{
    /// <summary>IPv4生成、共有port設定、Any bind、既定または明示IFでの参加を順に依頼する。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("192.0.2.80")]
    public async Task FactoryUsesAcceptedIpv4MulticastSettingsInOrder(string? interfaceAddress)
    {
        using var socket = new UdpClient(AddressFamily.InterNetwork);
        var operations = new List<string>();
        var setup = new RecordingSetup(socket, operations);
        var explicitInterface = interfaceAddress is null ? null : IPAddress.Parse(interfaceAddress);
        var options = explicitInterface is null ? null : new RefereeUdpReceiverOptions(InterfaceAddress: explicitInterface);
        var receiver = RefereeUdpReceiverFactory.Create(options, family =>
        {
            operations.Add($"Create:{family}");
            return setup;
        });
        try
        {
            Assert.Equal(new[]
            {
                "Create:InterNetwork", "Exclusive:False", "Reuse:True", "Bind:0.0.0.0:11003",
                $"Join:224.5.23.1:{interfaceAddress ?? "default"}",
            }, operations);
            Assert.NotNull(receiver);
        }
        finally
        {
            if (receiver is not null)
                await receiver.DisposeAsync();
        }
    }

    /// <summary>未受信の取消は時刻を採取せず、借用socketの所有権を変えない。</summary>
    [Fact]
    public async Task CancellationBeforeAnyDatagramDoesNotCaptureReceiveTime()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var handle = socket.Client.SafeHandle;
        await using var receiver = new UdpRefereeDatagramReceiver(socket);
        var diagnostics = CreateDiagnostics();
        var epoch = diagnostics.BeginEpoch(RefereeReceiveService.InputId);
        using var cancellation = new CancellationTokenSource();
        var captures = 0;
        var pending = receiver.ReceiveAsync(
            () => { captures++; return diagnostics.BeginReceive(epoch); },
            cancellation.Token).AsTask();
        try
        {
            Assert.False(pending.IsCompleted);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, captures);
            Assert.False(handle.IsClosed);
        }
        finally
        {
            await cancellation.CancelAsync();
            _ = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(pending.IsCompleted, "未受信Taskをfixture終了前に回収する。");
        }
    }

    /// <summary>反復・並行破棄で、明示所有socketだけを閉じ、借用socketは残す。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdapterDisposesOnlyOwnedSocketAndRepeatedDisposeIsSafe(bool ownsClient)
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var handle = socket.Client.SafeHandle;
        var receiver = new UdpRefereeDatagramReceiver(socket, ownsClient);
        try
        {
            await Task.WhenAll(receiver.DisposeAsync().AsTask(), receiver.DisposeAsync().AsTask());
            await receiver.DisposeAsync();
            Assert.Equal(ownsClient, handle.IsClosed);
        }
        finally
        {
            await receiver.DisposeAsync();
        }
    }

    /// <summary>並行・反復Disposeは同じ所有破棄を待ち、呼出し元取消なしでRun終端を保証する。</summary>
    [Fact]
    public async Task ConcurrentAndRepeatedServiceDisposeShareOneOwnedCleanupAndAwaitRun()
    {
        var diagnostics = CreateDiagnostics();
        var receiver = new BlockingOwnedReceiver(CreatePacket().ToByteArray());
        var service = new RefereeReceiveService(receiver, diagnostics, ownedReceiver: receiver);
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        Task? first = null;
        Task? second = null;
        try
        {
            await Task.WhenAny(run, receiver.WaitingForThirdReceive).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            Assert.True(Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).IsReady);
            first = service.DisposeAsync().AsTask();
            await receiver.DisposeStarted.WaitAsync(TimeSpan.FromSeconds(5));
            second = service.DisposeAsync().AsTask();
            Assert.Equal(1, receiver.DisposeCalls);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);

            receiver.ReleaseDispose();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(run.IsCompleted);
            Assert.False(cancellation.IsCancellationRequested);
            var state = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            Assert.Equal(2, state.SuccessfullyDecodedCount);
            Assert.False(state.IsReady);
            await service.DisposeAsync();
            Assert.Equal(1, receiver.DisposeCalls);
        }
        finally
        {
            receiver.ReleaseDispose();
            await cancellation.CancelAsync();
            var all = Task.WhenAll(run, first ?? Task.CompletedTask, second ?? Task.CompletedTask);
            _ = await Record.ExceptionAsync(async () => await all.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(all.IsCompleted, "Runと両Disposeをfixture終了前に回収する。");
            await service.DisposeAsync();
        }
    }

    /// <summary>破棄済みserviceの再開は受信せずに拒否し、例外型や返却形を固定しない。</summary>
    [Fact]
    public async Task DisposedServiceRejectsRestartWithoutReceivingAgain()
    {
        var receiver = new CountingRejectingReceiver();
        var service = new RefereeReceiveService(receiver, CreateDiagnostics());
        using var cancellation = new CancellationTokenSource();
        Task? restart = null;
        try
        {
            await service.DisposeAsync();
            var rejection = await Record.ExceptionAsync(async () =>
            {
                restart = service.RunAsync(cancellation.Token);
                await restart.WaitAsync(TimeSpan.FromSeconds(5));
            });
            Assert.NotNull(rejection);
            Assert.IsNotType<TimeoutException>(rejection);
            Assert.Equal(0, receiver.ReceiveCalls);
        }
        finally
        {
            await cancellation.CancelAsync();
            if (restart is not null)
            {
                _ = await Record.ExceptionAsync(async () => await restart.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.True(restart.IsCompleted);
            }
            await service.DisposeAsync();
        }
    }

    private static ServiceReceiveDiagnostics CreateDiagnostics() => new(
        new ResourceDiagnosticIdentity("run-03a-udp-settings", "referee-driver", new string('a', 64), 1),
        new FixedReceiveClock());

    private static global::Referee CreatePacket() => new()
    {
        PacketTimestamp = 1, Stage = global::Referee.Types.Stage.NormalFirstHalf,
        Command = global::Referee.Types.Command.Halt, CommandCounter = 7, CommandTimestamp = 1,
        Yellow = CreateTeam("yellow"), Blue = CreateTeam("blue"),
    };

    private static global::Referee.Types.TeamInfo CreateTeam(string name) => new()
    {
        Name = name, Score = 0, RedCards = 0, YellowCards = 0,
        Timeouts = 0, TimeoutTime = 0, Goalkeeper = 0,
    };

    private sealed class RecordingSetup(UdpClient client, List<string> operations) : IRefereeUdpSocketSetup
    {
        public UdpClient Client => client;
        public void SetExclusiveAddressUse(bool exclusive) => operations.Add($"Exclusive:{exclusive}");
        public void SetReuseAddress(bool reuse) => operations.Add($"Reuse:{reuse}");
        public void Bind(IPEndPoint endpoint) => operations.Add($"Bind:{endpoint}");
        public void JoinMulticastGroup(IPAddress group, IPAddress? interfaceAddress) =>
            operations.Add($"Join:{group}:{interfaceAddress?.ToString() ?? "default"}");
    }

    private sealed class BlockingOwnedReceiver : IRefereeDatagramReceiver, IAsyncDisposable
    {
        private readonly Channel<ReadOnlyMemory<byte>> packets = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly TaskCompletionSource waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource disposeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource disposeRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int receives;
        private int disposeCalls;

        public BlockingOwnedReceiver(ReadOnlyMemory<byte> packet)
        {
            if (!packets.Writer.TryWrite(packet) || !packets.Writer.TryWrite(packet))
                throw new InvalidOperationException("fixtureへ正常packetを供給できなかった。");
        }

        public Task WaitingForThirdReceive => waiting.Task;
        public Task DisposeStarted => disposeStarted.Task;
        public int DisposeCalls => Volatile.Read(ref disposeCalls);
        public void ReleaseDispose() => disposeRelease.TrySetResult();

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref receives) == 3)
                waiting.TrySetResult();
            var packet = await packets.Reader.ReadAsync(cancellationToken);
            return new(packet, new IPEndPoint(IPAddress.Loopback, 11003), captureReceive());
        }

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref disposeCalls);
            disposeStarted.TrySetResult();
            await disposeRelease.Task;
        }
    }

    private sealed class CountingRejectingReceiver : IRefereeDatagramReceiver
    {
        private int receives;
        public int ReceiveCalls => Volatile.Read(ref receives);
        public ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref receives);
            return ValueTask.FromException<RefereeReceivedDatagram>(
                new InvalidOperationException("破棄後に受信へ進んだ。"));
        }
    }

    private sealed class FixedReceiveClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    }
}
