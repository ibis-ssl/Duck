using System.Net;
using System.Net.Sockets;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeUdpFactoryBoundaryTests
{
    /// <summary>不成立な宛先・port・IFを正常受信資源にせず、割当て前の拒否や例外型は固定しない。</summary>
    [Theory]
    [InlineData("127.0.0.1", 11003, null)]
    [InlineData("ff02::1", 11003, null)]
    [InlineData("not-an-ip", 11003, null)]
    [InlineData("224.5.23.1", -1, null)]
    [InlineData("224.5.23.1", 65536, null)]
    [InlineData("224.5.23.1", 11003, "::1")]
    public async Task InvalidOptionsDoNotProduceNormalReceiver(string group, int port, string? interfaceAddress)
    {
        using var socket = new UdpClient(AddressFamily.InterNetwork);
        var setup = new FaultingSetup(socket);
        var options = new RefereeUdpReceiverOptions(
            group, port, interfaceAddress is null ? null : IPAddress.Parse(interfaceAddress));
        UdpRefereeDatagramReceiver? receiver = null;
        try
        {
            var failure = Record.Exception(() => { receiver = RefereeUdpReceiverFactory.Create(options, _ => setup); });
            Assert.NotNull(failure);
            Assert.Null(receiver);
        }
        finally
        {
            if (receiver is not null)
                await receiver.DisposeAsync();
        }
    }

    /// <summary>socket取得後の各設定段階が失敗したら、正常資源を返さず所有socketを閉じる。</summary>
    [Theory]
    [InlineData("Exclusive")]
    [InlineData("Reuse")]
    [InlineData("Bind")]
    [InlineData("Join")]
    public async Task PartialSetupFailureClosesAcquiredOwnedSocket(string failedStage)
    {
        using var socket = new UdpClient(AddressFamily.InterNetwork);
        var handle = socket.Client.SafeHandle;
        var setup = new FaultingSetup(socket, failedStage);
        UdpRefereeDatagramReceiver? receiver = null;
        try
        {
            var failure = Record.Exception(() =>
            {
                receiver = RefereeUdpReceiverFactory.Create(createSocket: _ => setup);
            });
            Assert.NotNull(failure);
            Assert.Null(receiver);
            Assert.True(handle.IsClosed, "設定失敗時は取得済み所有socketを閉じる。");
        }
        finally
        {
            if (receiver is not null)
                await receiver.DisposeAsync();
        }
    }

    /// <summary>同じproduction設定を実socketへ適用し、動的portのloopback受信と所有停止を組み合わせる。</summary>
    [Fact]
    public async Task RealProductionSetupUsesDynamicPortAndFeedsSingleServicePump()
    {
        IRefereeUdpSocketSetup? setup = null;
        await using var receiver = RefereeUdpReceiverFactory.Create(
            new RefereeUdpReceiverOptions(Port: 0, InterfaceAddress: IPAddress.Loopback),
            family => setup = new RefereeUdpReceiverFactory.UdpSocketSetup(family));
        var diagnostics = new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-03a-real-udp-factory", "referee-driver", new string('b', 64), 1),
            new FixedReceiveClock());
        await using var service = new RefereeReceiveService(receiver, diagnostics, ownedReceiver: receiver);
        using var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var callerCancellation = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task? run = null;
        try
        {
            Assert.NotNull(setup);
            var client = setup!.Client;
            var handle = client.Client.SafeHandle;
            var bound = Assert.IsType<IPEndPoint>(client.Client.LocalEndPoint);
            Assert.Equal(AddressFamily.InterNetwork, client.Client.AddressFamily);
            Assert.Equal(IPAddress.Any, bound.Address);
            Assert.InRange(bound.Port, 1, 65535);
            Assert.False(client.ExclusiveAddressUse);
            Assert.Equal(1, (int)client.Client.GetSocketOption(
                SocketOptionLevel.Socket, SocketOptionName.ReuseAddress)!);

            run = service.RunAsync(callerCancellation.Token);
            var payload = CreatePacket().ToByteArray();
            var destination = new IPEndPoint(IPAddress.Loopback, bound.Port);
            await sender.SendAsync(payload, destination);
            await sender.SendAsync(payload, destination);
            var state = await WaitForDecodedCountAsync(diagnostics, run, 2, deadline.Token);
            Assert.Equal(2, state.SuccessfullyDecodedCount);
            Assert.True(state.IsReady);
            Assert.Equal(sender.Client.LocalEndPoint!.ToString(), state.RemoteEndpoint);
            Assert.Equal(TimeSpan.Zero, state.DecodeAgeUpperBound);
            var command = await service.WaitForChangeAsync(null, deadline.Token)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((uint)7, command.CommandCounter);
            Assert.Equal(global::Referee.Types.Command.Halt, command.Command);

            await service.DisposeAsync();
            Assert.True(run.IsCompleted);
            Assert.False(callerCancellation.IsCancellationRequested);
            Assert.False(Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).IsReady);
            Assert.True(handle.IsClosed);
        }
        finally
        {
            await callerCancellation.CancelAsync();
            if (run is not null)
            {
                _ = await Record.ExceptionAsync(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.True(run.IsCompleted, "実socketのRunをfixture終了前に回収する。");
            }
            await service.DisposeAsync();
        }
    }

    /// <summary>正常decodeの実測進捗を有限時間で待ち、pumpの途中終了を成功と扱わない。</summary>
    private static async Task<ServiceReceiveSnapshot> WaitForDecodedCountAsync(
        ServiceReceiveDiagnostics diagnostics, Task run, long count, CancellationToken cancellationToken)
    {
        while (true)
        {
            var state = Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId));
            if (state.SuccessfullyDecodedCount >= count)
                return state;
            Assert.False(run.IsCompleted, "正常packetの進捗前にpumpが終端しない。");
            await Task.Delay(10, cancellationToken);
        }
    }

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

    private sealed class FaultingSetup(UdpClient client, string? failedStage = null) : IRefereeUdpSocketSetup
    {
        public UdpClient Client => client;
        public void SetExclusiveAddressUse(bool exclusive) => MaybeFail("Exclusive");
        public void SetReuseAddress(bool reuse) => MaybeFail("Reuse");
        public void Bind(IPEndPoint endpoint) => MaybeFail("Bind");
        public void JoinMulticastGroup(IPAddress group, IPAddress? interfaceAddress) => MaybeFail("Join");

        private void MaybeFail(string stage)
        {
            if (stage == failedStage)
                throw new InvalidOperationException("fixtureが指定設定段階で失敗した。");
        }
    }

    private sealed class FixedReceiveClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    }
}
