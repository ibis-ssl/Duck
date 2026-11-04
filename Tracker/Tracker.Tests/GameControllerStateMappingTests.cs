using System.Net.WebSockets;
using System.Text;
using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class GameControllerStateMappingTests
{
    /// <summary>公開APIの名前を内部指令と試合状態へ正規化し、API数値をUDP列挙値へ直接変換しない。</summary>
    [Theory]
    [InlineData("HALT", "HALT", "Halt", "Halt")]
    [InlineData("STOP", "STOP", "Stop", "Stop")]
    [InlineData("NORMAL_START", "RUNNING", "NormalStart", "Running")]
    [InlineData("KICKOFF", "KICKOFF", "Kickoff", "Kickoff")]
    [InlineData("NORMAL_START", "KICKOFF", "NormalStart", "Kickoff")]
    [InlineData("FORCE_START", "RUNNING", "ForceStart", "Running")]
    [InlineData("KICKOFF", "STOP", "Kickoff", "Stop")]
    public async Task NormalizesSupportedApiNames(
        string command, string phase, string expectedCommand, string expectedPhase)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            matchState = new { command = new { type = command, forTeam = "UNKNOWN" },
                gameState = new { type = phase, forTeam = "YELLOW" } },
        });
        using var socket = new OneOutputSocket(json);
        var observed = await CreateClient(socket).ReadMatchStateAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(observed);
        Assert.Equal(expectedCommand, observed.Command.ToString());
        Assert.Equal(expectedPhase, observed.Phase.ToString());
        Assert.Equal("UNKNOWN", observed.CommandForTeam);
        Assert.Equal("YELLOW", observed.PhaseForTeam);
        Assert.Equal(0, socket.SendCalls);
        Assert.Equal(1, socket.DisposeCalls);
    }

    [Fact]
    public async Task ReadsContinuationActionsAcrossSeparateCurrentOutputMessages()
    {
        using var socket = new OneOutputSocket(
            """{"matchState":{"command":{"type":"STOP","forTeam":"UNKNOWN"},"gameState":{"type":"STOP"},"stage":"NORMAL_FIRST_HALF","stageTimeLeft":"-1s","teamState":{"BLUE":{"goals":1},"YELLOW":{"goals":0}},"placementPos":{"x":0,"y":0},"nextCommand":{"type":"KICKOFF","forTeam":"YELLOW"}}}""",
            """{"gcState":{"continueActions":[{"type":"NEXT_COMMAND","forTeam":"YELLOW","state":"BLOCKED","continuationIssues":["Ball is 3.72 m (>1.00 m) away from placement pos"]}]}}""");

        var observed = await CreateClient(socket).ReadContinuationStateAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(observed);
        Assert.Equal("Stop", observed.Command);
        Assert.Equal("Stop", observed.Phase);
        Assert.Equal("NORMAL_FIRST_HALF", observed.Stage);
        Assert.Equal(TimeSpan.FromSeconds(-1), observed.StageTimeLeft);
        Assert.Equal("KICKOFF", observed.NextCommand);
        Assert.Equal("YELLOW", observed.NextCommandForTeam);
        Assert.Equal((uint)1, observed.BlueScore);
        Assert.Equal((uint)0, observed.YellowScore);
        Assert.Equal(0, observed.PlacementX);
        Assert.Equal(0, observed.PlacementY);
        Assert.Collection(observed.ContinueActions,
            action =>
            {
                Assert.Equal("NEXT_COMMAND", action.Type);
                Assert.Equal("YELLOW", action.ForTeam);
                Assert.Equal("BLOCKED", action.State);
                Assert.True(action.HasContinuationIssues);
                Assert.Equal("Ball is 3.72 m (>1.00 m) away from placement pos",
                    Assert.Single(action.ContinuationIssues!));
            });
    }

    [Theory]
    [InlineData("NORMAL_FIRST_HALF_PRE", Referee.Types.Stage.NormalFirstHalfPre)]
    [InlineData("NORMAL_FIRST_HALF", Referee.Types.Stage.NormalFirstHalf)]
    [InlineData("NORMAL_HALF_TIME", Referee.Types.Stage.NormalHalfTime)]
    [InlineData("NORMAL_SECOND_HALF_PRE", Referee.Types.Stage.NormalSecondHalfPre)]
    [InlineData("NORMAL_SECOND_HALF", Referee.Types.Stage.NormalSecondHalf)]
    [InlineData("POST_GAME", Referee.Types.Stage.PostGame)]
    public void ParsesWireStageNames(string wireName, Referee.Types.Stage expected)
    {
        Assert.True(GameControllerStateMapping.TryParseStage(wireName, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RejectsUnsupportedWireStageNames()
    {
        Assert.False(GameControllerStateMapping.TryParseStage("EXTRA_FIRST_HALF", out _));
        Assert.False(GameControllerStateMapping.TryParseStage(null, out _));
    }

    /// <summary>未取得、欠落、未知、未対応、型不正の状態を正常な指令へ補完しない。</summary>
    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"matchState":null}""")]
    [InlineData("""{"matchState":{}}""")]
    [InlineData("""{"matchState":{"command":null,"gameState":{"type":"HALT"}}}""")]
    [InlineData("""{"matchState":{"command":{"type":"HALT"},"gameState":null}}""")]
    [InlineData("""{"matchState":{"command":{},"gameState":{"type":"HALT"}}}""")]
    [InlineData("""{"matchState":{"command":{"type":"HALT"},"gameState":{}}}""")]
    [InlineData("""{"matchState":{"command":{"type":"UNKNOWN"},"gameState":{"type":"HALT"}}}""")]
    [InlineData("""{"matchState":{"command":{"type":"DIRECT"},"gameState":{"type":"FREE_KICK"}}}""")]
    [InlineData("""{"matchState":{"command":{"type":"HALT"},"gameState":{"type":"UNKNOWN"}}}""")]
    [InlineData("""{"matchState":{"command":{"type":999},"gameState":{"type":"HALT"}}}""")]
    [InlineData("""{"matchState":{"command":{"type":"HALT"},"gameState":{"type":true}}}""")]
    public async Task UnusableApiStateReturnsNoObservation(string json)
    {
        using var socket = new OneOutputSocket(json);
        var observed = await CreateClient(socket).ReadMatchStateAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(observed);
        Assert.Equal(0, socket.SendCalls);
        Assert.Equal(1, socket.DisposeCalls);
    }

    /// <summary>指令と試合状態の両方を明示対応し、別指令・矛盾した状態・未知値を一致としない。</summary>
    [Theory]
    [InlineData("Halt", "Halt", Referee.Types.Command.Halt, true)]
    [InlineData("Stop", "Stop", Referee.Types.Command.Stop, true)]
    [InlineData("NormalStart", "Running", Referee.Types.Command.NormalStart, true)]
    [InlineData("ForceStart", "Running", Referee.Types.Command.ForceStart, true)]
    [InlineData("Halt", "Halt", Referee.Types.Command.Stop, false)]
    [InlineData("Stop", "Stop", Referee.Types.Command.Halt, false)]
    [InlineData("NormalStart", "Running", Referee.Types.Command.ForceStart, false)]
    [InlineData("ForceStart", "Running", Referee.Types.Command.NormalStart, false)]
    [InlineData("Kickoff", "Stop", Referee.Types.Command.PrepareKickoffBlue, true)]
    [InlineData("Kickoff", "Stop", Referee.Types.Command.PrepareKickoffYellow, true)]
    [InlineData("Kickoff", "Kickoff", Referee.Types.Command.PrepareKickoffBlue, true)]
    [InlineData("Kickoff", "Kickoff", Referee.Types.Command.PrepareKickoffYellow, true)]
    [InlineData("NormalStart", "Kickoff", Referee.Types.Command.NormalStart, true)]
    [InlineData("Kickoff", "Running", Referee.Types.Command.PrepareKickoffBlue, false)]
    [InlineData("Kickoff", "Kickoff", Referee.Types.Command.NormalStart, false)]
    [InlineData("NormalStart", "Kickoff", Referee.Types.Command.ForceStart, false)]
    [InlineData("Halt", "Running", Referee.Types.Command.Halt, false)]
    [InlineData("NormalStart", "Stop", Referee.Types.Command.NormalStart, false)]
    [InlineData("Unknown", "Halt", Referee.Types.Command.Halt, false)]
    [InlineData("Halt", "Unknown", Referee.Types.Command.Halt, false)]
    [InlineData("Halt", "Halt", (Referee.Types.Command)999, false)]
    [InlineData(null, null, Referee.Types.Command.Halt, false)]
    public void MatchesOnlyExplicitCommandAndPhasePairs(
        string? command, string? phase, Referee.Types.Command udp, bool expected)
    {
        var api = command is null || phase is null ? null : new GameControllerMatchSnapshot(
            Enum.Parse<GameControllerCommandKind>(command), Enum.Parse<GameControllerMatchPhase>(phase));

        Assert.Equal(expected, GameControllerStateMapping.MatchesUdpCommand(api, udp));
    }

    private static GameControllerStateClient CreateClient(WebSocket socket) =>
        new(new Uri("ws://game-controller:8082/api/control"), (_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(socket);
        });

    /// <summary>一つの公開Outputの後はCloseを返し、状態欠落時も試験を有限に終端する。</summary>
    private sealed class OneOutputSocket(params string[] json) : WebSocket
    {
        private readonly byte[][] payloads = json.Select(Encoding.UTF8.GetBytes).ToArray();
        private int nextPayload;
        private bool disposed;
        public int SendCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => disposed ? WebSocketState.Closed : WebSocketState.Open;
        public override string? SubProtocol => null;

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nextPayload >= payloads.Length)
                return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            var payload = payloads[nextPayload++];
            payload.AsSpan().CopyTo(buffer.AsSpan());
            return Task.FromResult(new WebSocketReceiveResult(payload.Length, WebSocketMessageType.Text, true));
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            SendCalls++;
            throw new InvalidOperationException("観測専用fixtureで書込みを行わない。");
        }

        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DisposeCalls++;
        }
    }
}
