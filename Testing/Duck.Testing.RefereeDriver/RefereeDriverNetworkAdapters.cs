using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace Duck.Testing.RefereeDriver;

public interface IRefereePacketReceiver
{
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);
}

public sealed class UdpRefereeCommandSource : IRefereeCommandSource, IDisposable
{
    public const string DefaultMulticastAddress = "224.5.23.1";
    public const int DefaultPort = 11003;

    private readonly IRefereePacketReceiver receiver;
    private readonly IDisposable? ownedReceiver;

    public UdpRefereeCommandSource(
        string multicastAddress = DefaultMulticastAddress,
        int port = DefaultPort,
        IPAddress? interfaceAddress = null)
    {
        var udpReceiver = new UdpMulticastPacketReceiver(multicastAddress, port, interfaceAddress);
        receiver = udpReceiver;
        ownedReceiver = udpReceiver;
    }

    public UdpRefereeCommandSource(IRefereePacketReceiver receiver)
    {
        this.receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
    }

    public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
        uint? previousCommandCounter,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var packet = await receiver.ReceiveAsync(cancellationToken);
            var referee = Referee.Parser.ParseFrom(packet.ToArray());
            if (previousCommandCounter is null ||
                referee.CommandCounter > previousCommandCounter ||
                referee.Stage == Referee.Types.Stage.PostGame)
            {
                return new RefereeCommandSnapshot(
                    referee.Command,
                    referee.CommandCounter,
                    referee.Yellow?.Name ?? string.Empty,
                    referee.Stage,
                    referee.PacketTimestamp);
            }
        }
    }

    public void Dispose()
    {
        ownedReceiver?.Dispose();
    }

    private sealed class UdpMulticastPacketReceiver : IRefereePacketReceiver, IDisposable
    {
        private readonly UdpClient client;

        public UdpMulticastPacketReceiver(
            string multicastAddress,
            int port,
            IPAddress? interfaceAddress)
        {
            var group = IPAddress.Parse(multicastAddress);
            if (group.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException(
                    "Referee multicast address must be IPv4.",
                    nameof(multicastAddress));
            }

            client = new UdpClient(AddressFamily.InterNetwork)
            {
                ExclusiveAddressUse = false,
            };
            client.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, port));

            if (interfaceAddress is null)
            {
                client.JoinMulticastGroup(group);
            }
            else
            {
                client.JoinMulticastGroup(group, interfaceAddress);
            }
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
        {
            var datagram = await client.ReceiveAsync(cancellationToken);
            return datagram.Buffer;
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}

public interface IGameControllerWebSocketTransport
{
    Task SendJsonAsync(Uri endpoint, string json, CancellationToken cancellationToken);

    Task<GameControllerContinuationState?> ReadContinuationStateAsync(
        Uri endpoint, CancellationToken cancellationToken);

    Task<bool?> ReadAutoContinueAsync(Uri endpoint, CancellationToken cancellationToken) =>
        Task.FromResult<bool?>(null);

    Task<bool> SendConfigDeltaAndWaitForAutoContinueAsync(
        Uri endpoint,
        string json,
        bool expectedAutoContinue,
        CancellationToken cancellationToken) => Task.FromResult(false);
}

public static class GameControllerWebSocketTransportOperations
{
    private static readonly TimeSpan ConfigConfirmationTimeout = TimeSpan.FromSeconds(3);

    public static Task<GameControllerContinuationState?> ReadContinuationStateAsync(
        Func<Uri, CancellationToken, Task<WebSocket>> connect,
        Uri endpoint,
        CancellationToken cancellationToken) =>
        new GameControllerStateClient(endpoint, connect).ReadContinuationStateAsync(cancellationToken);

    public static async Task SendJsonAsync(
        Func<Uri, CancellationToken, Task<WebSocket>> connect,
        Uri endpoint,
        string json,
        CancellationToken cancellationToken)
    {
        using var socket = await connect(endpoint, cancellationToken).ConfigureAwait(false);
        _ = await ReceiveTextMessageAsync(socket, cancellationToken).ConfigureAwait(false);
        await SendTextMessageAsync(socket, json, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<bool?> ReadAutoContinueAsync(
        Func<Uri, CancellationToken, Task<WebSocket>> connect,
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateConfigTimeout(cancellationToken);
        try
        {
            using var socket = await connect(endpoint, timeout.Token).ConfigureAwait(false);
            while (true)
            {
                var message = await ReceiveTextMessageAsync(socket, timeout.Token).ConfigureAwait(false);
                if (TryReadAutoContinue(message, out var enabled))
                    return enabled;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Game Controller did not publish its autoContinue config within three seconds.");
        }
    }

    public static async Task<bool> SendConfigDeltaAndWaitForAutoContinueAsync(
        Func<Uri, CancellationToken, Task<WebSocket>> connect,
        Uri endpoint,
        string json,
        bool expectedAutoContinue,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateConfigTimeout(cancellationToken);
        try
        {
            using var socket = await connect(endpoint, timeout.Token).ConfigureAwait(false);
            _ = await ReceiveTextMessageAsync(socket, timeout.Token).ConfigureAwait(false);
            await SendTextMessageAsync(socket, json, timeout.Token).ConfigureAwait(false);
            while (true)
            {
                var message = await ReceiveTextMessageAsync(socket, timeout.Token).ConfigureAwait(false);
                if (TryReadAutoContinue(message, out var enabled) && enabled == expectedAutoContinue)
                    return true;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Game Controller did not confirm autoContinue={expectedAutoContinue} within three seconds.");
        }
    }

    public static bool TryReadAutoContinue(string outputJson, out bool enabled)
    {
        enabled = false;
        using var document = System.Text.Json.JsonDocument.Parse(outputJson);
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("config", out var config) ||
            config.ValueKind != System.Text.Json.JsonValueKind.Object)
            return false;

        if (!config.TryGetProperty("autoContinue", out var value) &&
            !config.TryGetProperty("auto_continue", out value))
            return false;

        if (value.ValueKind is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
            return false;
        enabled = value.GetBoolean();
        return true;
    }

    private static CancellationTokenSource CreateConfigTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConfigConfirmationTimeout);
        return timeout;
    }

    private static async Task SendTextMessageAsync(WebSocket socket, string json, CancellationToken cancellationToken)
    {
        await socket.SendAsync(Encoding.UTF8.GetBytes(json).AsMemory(), WebSocketMessageType.Text,
            endOfMessage: true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReceiveTextMessageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var message = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new InvalidOperationException("Game Controller closed the control WebSocket before Output.config was received.");
            if (result.MessageType != WebSocketMessageType.Text)
                throw new InvalidOperationException($"Game Controller control WebSocket returned {result.MessageType} instead of text Output.");

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                return Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
        }
    }
}

public sealed class GameControllerWebSocketControlClient : IGameControllerControlClient
{
    public static Uri DefaultEndpoint { get; } = new("ws://127.0.0.1:8082/api/control");

    private readonly IGameControllerWebSocketTransport transport;
    private readonly Uri endpoint;

    public GameControllerWebSocketControlClient(Uri? endpoint = null)
        : this(new ClientWebSocketJsonTransport(), endpoint)
    {
    }

    public GameControllerWebSocketControlClient(
        IGameControllerWebSocketTransport transport,
        Uri? endpoint = null)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.endpoint = endpoint ?? DefaultEndpoint;
    }

    public Task SendContinueActionAsync(
        GameControllerContinueAction action,
        CancellationToken cancellationToken)
    {
        return transport.SendJsonAsync(
            endpoint,
            CreateContinueActionMessage(action),
            cancellationToken);
    }

    public Task<bool?> ReadAutoContinueAsync(CancellationToken cancellationToken) =>
        transport.ReadAutoContinueAsync(endpoint, cancellationToken);

    public Task<bool> SetAutoContinueAsync(bool enabled, CancellationToken cancellationToken) =>
        transport.SendConfigDeltaAndWaitForAutoContinueAsync(
            endpoint,
            CreateAutoContinueConfigMessage(enabled),
            enabled,
            cancellationToken);

    public Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken) =>
        transport.ReadContinuationStateAsync(endpoint, cancellationToken);

    public Task SendYellowTeamNameAsync(string teamName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);
        return transport.SendJsonAsync(
            endpoint,
            CreateYellowTeamNameMessage(teamName),
            cancellationToken);
    }

    public static string CreateYellowTeamNameMessage(string teamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            change = new
            {
                update_team_state_change = new
                {
                    for_team = "YELLOW",
                    team_name = teamName,
                },
            },
        });
    }

    public static string CreateContinueActionMessage(GameControllerContinueAction action)
    {
        var actionName = action switch
        {
            GameControllerContinueAction.ResumeFromHalt => "RESUME_FROM_HALT",
            GameControllerContinueAction.NextCommand => "NEXT_COMMAND",
            GameControllerContinueAction.ForceStart => "FORCE_START",
            GameControllerContinueAction.NormalStart => "NORMAL_START",
            GameControllerContinueAction.StopGame => "STOP_GAME",
            GameControllerContinueAction.NextStage => "NEXT_STAGE",
            GameControllerContinueAction.EndGame => "END_GAME",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

        return $"{{\"continue_action\":{{\"type\":\"{actionName}\",\"for_team\":\"UNKNOWN\"}}}}";
    }

    public static string CreateAutoContinueConfigMessage(bool enabled) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            config_delta = new { auto_continue = enabled },
        });

    private sealed class ClientWebSocketJsonTransport : IGameControllerWebSocketTransport
    {
        public Task SendJsonAsync(
            Uri endpoint,
            string json,
            CancellationToken cancellationToken) =>
            GameControllerWebSocketTransportOperations.SendJsonAsync(ConnectAsync, endpoint, json, cancellationToken);

        public Task<bool?> ReadAutoContinueAsync(Uri endpoint, CancellationToken cancellationToken) =>
            GameControllerWebSocketTransportOperations.ReadAutoContinueAsync(ConnectAsync, endpoint, cancellationToken);

        public Task<GameControllerContinuationState?> ReadContinuationStateAsync(
            Uri endpoint, CancellationToken cancellationToken) =>
            GameControllerWebSocketTransportOperations.ReadContinuationStateAsync(
                ConnectAsync, endpoint, cancellationToken);

        public Task<bool> SendConfigDeltaAndWaitForAutoContinueAsync(
            Uri endpoint, string json, bool expectedAutoContinue, CancellationToken cancellationToken) =>
            GameControllerWebSocketTransportOperations.SendConfigDeltaAndWaitForAutoContinueAsync(
                ConnectAsync, endpoint, json, expectedAutoContinue, cancellationToken);

        private static async Task<WebSocket> ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            var socket = new ClientWebSocket();
            try
            {
                await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}
