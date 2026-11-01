using System.Net.WebSockets;
using System.Text;
using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class GameControllerStateClientTests
{
    /// <summary>初期の別Outputを成功にせず、内部URIでHALTのmatchStateまで読み、書込みは行わない。</summary>
    [Fact]
    public async Task ReadsMatchStateAfterInitialNonMatchOutputsWithoutWriting()
    {
        var endpoint = new Uri("ws://game-controller:8082/api/control");
        var socket = new RecordingWebSocket(
            """{"gcState":{}}""",
            """{"config":{}}""",
            """{"matchState":{"command":{"type":"HALT","forTeam":"UNKNOWN"},"gameState":{"type":"HALT"}}}""",
            """{"protocol":{"delta":false}}""");
        var connections = new List<Uri>();
        var client = new GameControllerStateClient(endpoint, (uri, token) =>
        {
            token.ThrowIfCancellationRequested();
            connections.Add(uri);
            return Task.FromResult<WebSocket>(socket);
        });

        try
        {
            var observed = await client.ReadMatchStateAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.NotNull(observed);
            Assert.Equal(GameControllerCommandKind.Halt, observed.Command);
            Assert.Equal(GameControllerMatchPhase.Halt, observed.Phase);
            Assert.Equal(endpoint, Assert.Single(connections));
            Assert.Equal(3, socket.ReceiveCalls);
            Assert.Equal(new[] { "gcState", "config", "matchState" }, socket.ReceivedOutputNames);
            Assert.Equal(1, socket.RemainingMessages);
            Assert.Equal(0, socket.SendCalls);
            Assert.Equal(1, socket.DisposeCalls);
        }
        finally
        {
            socket.Dispose();
        }
    }

    private sealed class RecordingWebSocket(params string[] messages) : WebSocket
    {
        private readonly Queue<byte[]> messages = new(messages.Select(Encoding.UTF8.GetBytes));
        private WebSocketState state = WebSocketState.Open;
        private bool disposed;

        public int ReceiveCalls { get; private set; }
        public int SendCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public int RemainingMessages => messages.Count;
        public List<string> ReceivedOutputNames { get; } = [];
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => state;
        public override string? SubProtocol => null;

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (messages.Count == 0)
                throw new InvalidOperationException("固定fixtureのOutputを最後まで読み終えた。");
            var payload = messages.Dequeue();
            if (payload.Length > buffer.Count)
                throw new InvalidOperationException("先行fixtureの正常Outputを収める領域が不足した。");
            payload.AsSpan().CopyTo(buffer.AsSpan());
            ReceiveCalls++;
            using var json = System.Text.Json.JsonDocument.Parse(payload);
            ReceivedOutputNames.Add(json.RootElement.EnumerateObject().First().Name);
            return Task.FromResult(new WebSocketReceiveResult(payload.Length, WebSocketMessageType.Text, true));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCalls++;
            return Task.CompletedTask;
        }

        public override void Abort() => state = WebSocketState.Aborted;

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken) =>
            CloseAsync(closeStatus, statusDescription, cancellationToken);

        public override void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                state = WebSocketState.Closed;
                DisposeCalls++;
            }
        }
    }
}
