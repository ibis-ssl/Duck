using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeDriverNetworkAdapterTests
{
    [Fact]
    public async Task UdpSourceSkipsPacketsWithUnchangedCommandCounter()
    {
        var receiver = new RecordingPacketReceiver(
            CreatePacket(Referee.Types.Command.Stop, 7),
            CreatePacket(Referee.Types.Command.ForceStart, 8));
        var source = new UdpRefereeCommandSource(receiver);

        var snapshot = await source.WaitForChangeAsync(7, CancellationToken.None);

        Assert.Equal(Referee.Types.Command.ForceStart, snapshot.Command);
        Assert.Equal((uint)8, snapshot.CommandCounter);
        Assert.Equal(2, receiver.ReceiveCount);
    }

    [Theory]
    [InlineData(GameControllerContinueAction.NextCommand, "NEXT_COMMAND")]
    [InlineData(GameControllerContinueAction.ForceStart, "FORCE_START")]
    [InlineData(GameControllerContinueAction.NormalStart, "NORMAL_START")]
    public async Task ControlClientSendsCraneCompatibleJson(
        GameControllerContinueAction action,
        string expectedType)
    {
        var transport = new RecordingWebSocketTransport();
        var client = new GameControllerWebSocketControlClient(transport);

        await client.SendContinueActionAsync(action, CancellationToken.None);

        var sent = Assert.Single(transport.Messages);
        Assert.Equal(GameControllerWebSocketControlClient.DefaultEndpoint, sent.Endpoint);
        Assert.Equal(
            $"{{\"continue_action\":{{\"type\":\"{expectedType}\",\"for_team\":\"UNKNOWN\"}}}}",
            sent.Json);
    }

    private static ReadOnlyMemory<byte> CreatePacket(Referee.Types.Command command, uint commandCounter)
    {
        var message = new Referee
        {
            PacketTimestamp = 1,
            Stage = Referee.Types.Stage.NormalFirstHalf,
            Command = command,
            CommandCounter = commandCounter,
            CommandTimestamp = 1,
            Yellow = CreateTeam("yellow"),
            Blue = CreateTeam("blue"),
        };

        return message.ToByteArray();
    }

    private static Referee.Types.TeamInfo CreateTeam(string name)
    {
        return new Referee.Types.TeamInfo
        {
            Name = name,
            Score = 0,
            RedCards = 0,
            YellowCards = 0,
            Timeouts = 0,
            TimeoutTime = 0,
            Goalkeeper = 0,
        };
    }

    private sealed class RecordingPacketReceiver(params ReadOnlyMemory<byte>[] packets)
        : IRefereePacketReceiver
    {
        private readonly Queue<ReadOnlyMemory<byte>> packets = new(packets);

        public int ReceiveCount { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceiveCount++;
            return ValueTask.FromResult(packets.Dequeue());
        }
    }

    private sealed class RecordingWebSocketTransport : IGameControllerWebSocketTransport
    {
        public List<(Uri Endpoint, string Json)> Messages { get; } = [];

        public Task SendJsonAsync(Uri endpoint, string json, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add((endpoint, json));
            return Task.CompletedTask;
        }
    }
}
