using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeDriverNetworkAdapterTests
{
    [Fact]
    public async Task UdpSourceSkipsPacketsWithNonAdvancingCommandCounter()
    {
        var receiver = new RecordingPacketReceiver(
            CreatePacket(Referee.Types.Command.Stop, 6),
            CreatePacket(Referee.Types.Command.ForceStart, 8));
        var source = new UdpRefereeCommandSource(receiver);

        var snapshot = await source.WaitForChangeAsync(7, CancellationToken.None);

        Assert.Equal(Referee.Types.Command.ForceStart, snapshot.Command);
        Assert.Equal((uint)8, snapshot.CommandCounter);
        Assert.Equal("yellow", snapshot.YellowTeamName);
        Assert.Equal(2, receiver.ReceiveCount);
    }

    [Theory]
    [InlineData(GameControllerContinueAction.ResumeFromHalt, "RESUME_FROM_HALT")]
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

    [Fact]
    public async Task ControlClientSetsYellowTeamNameUsingGameControllerUpdateTeamState()
    {
        var transport = new RecordingWebSocketTransport();
        var client = new GameControllerWebSocketControlClient(transport);

        await client.SendYellowTeamNameAsync("crane", CancellationToken.None);

        var sent = Assert.Single(transport.Messages);
        Assert.Equal(GameControllerWebSocketControlClient.DefaultEndpoint, sent.Endpoint);
        using var json = System.Text.Json.JsonDocument.Parse(sent.Json);
        var teamChange = json.RootElement
            .GetProperty("change")
            .GetProperty("update_team_state_change");
        Assert.Equal("YELLOW", teamChange.GetProperty("for_team").GetString());
        Assert.Equal("crane", teamChange.GetProperty("team_name").GetString());
    }

    [Fact]
    public async Task ControlClientReadsAutoContinueFromGameControllerOutputConfig()
    {
        var transport = new RecordingWebSocketTransport { AutoContinue = false };
        var client = new GameControllerWebSocketControlClient(transport);

        var enabled = await client.ReadAutoContinueAsync(CancellationToken.None);

        Assert.False(enabled);
        Assert.Equal(GameControllerWebSocketControlClient.DefaultEndpoint, transport.ReadEndpoint);
    }

    [Fact]
    public async Task ControlClientRestoresAutoContinueThroughConfigDeltaAndAwaitsConfirmation()
    {
        var transport = new RecordingWebSocketTransport { ConfirmedAutoContinue = true };
        var client = new GameControllerWebSocketControlClient(transport);

        var confirmed = await client.SetAutoContinueAsync(true, CancellationToken.None);

        Assert.True(confirmed);
        var sent = Assert.Single(transport.ConfigMessages);
        Assert.Equal(GameControllerWebSocketControlClient.DefaultEndpoint, sent.Endpoint);
        Assert.True(sent.ExpectedAutoContinue);
        using var json = System.Text.Json.JsonDocument.Parse(sent.Json);
        Assert.True(json.RootElement.GetProperty("config_delta").GetProperty("auto_continue").GetBoolean());
    }

    [Theory]
    [InlineData("{\"config\":{\"autoContinue\":false}}", false)]
    [InlineData("{\"config\":{\"auto_continue\":true}}", true)]
    public void OutputConfigParserReadsTheConfirmedAutoContinueValue(string json, bool expected)
    {
        Assert.True(GameControllerWebSocketTransportOperations.TryReadAutoContinue(json, out var actual));
        Assert.Equal(expected, actual);
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
        public List<(Uri Endpoint, string Json, bool ExpectedAutoContinue)> ConfigMessages { get; } = [];
        public Uri? ReadEndpoint { get; private set; }
        public bool? AutoContinue { get; init; }
        public bool ConfirmedAutoContinue { get; init; }

        public Task SendJsonAsync(Uri endpoint, string json, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add((endpoint, json));
            return Task.CompletedTask;
        }

        public Task<bool?> ReadAutoContinueAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadEndpoint = endpoint;
            return Task.FromResult(AutoContinue);
        }

        public Task<bool> SendConfigDeltaAndWaitForAutoContinueAsync(
            Uri endpoint,
            string json,
            bool expectedAutoContinue,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConfigMessages.Add((endpoint, json, expectedAutoContinue));
            return Task.FromResult(ConfirmedAutoContinue == expectedAutoContinue);
        }
    }
}
