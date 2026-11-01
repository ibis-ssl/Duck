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
    [InlineData("FORCE_START", "RUNNING", "ForceStart", "Running")]
    public async Task NormalizesSupportedApiNames(
        string command, string phase, string expectedCommand, string expectedPhase)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            matchState = new { command = new { type = command, forTeam = "UNKNOWN" },
                gameState = new { type = phase } },
        });
        using var socket = new OneOutputSocket(json);
        var observed = await CreateClient(socket).ReadMatchStateAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(observed);
        Assert.Equal(expectedCommand, observed.Command.ToString());
        Assert.Equal(expectedPhase, observed.Phase.ToString());
        Assert.Equal(0, socket.SendCalls);
        Assert.Equal(1, socket.DisposeCalls);
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
    private sealed class OneOutputSocket(string json) : WebSocket
    {
        private readonly byte[] payload = Encoding.UTF8.GetBytes(json);
        private bool delivered;
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
            if (delivered)
                return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            delivered = true;
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
