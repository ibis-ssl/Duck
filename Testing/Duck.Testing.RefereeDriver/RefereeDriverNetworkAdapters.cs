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
            if (previousCommandCounter is null || referee.CommandCounter != previousCommandCounter)
            {
                return new RefereeCommandSnapshot(referee.Command, referee.CommandCounter);
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

    public static string CreateContinueActionMessage(GameControllerContinueAction action)
    {
        var actionName = action switch
        {
            GameControllerContinueAction.NextCommand => "NEXT_COMMAND",
            GameControllerContinueAction.ForceStart => "FORCE_START",
            GameControllerContinueAction.NormalStart => "NORMAL_START",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

        return $"{{\"continue_action\":{{\"type\":\"{actionName}\",\"for_team\":\"UNKNOWN\"}}}}";
    }

    private sealed class ClientWebSocketJsonTransport : IGameControllerWebSocketTransport
    {
        public async Task SendJsonAsync(
            Uri endpoint,
            string json,
            CancellationToken cancellationToken)
        {
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(endpoint, cancellationToken);
            await ReceiveInitialOutputAsync(socket, cancellationToken);

            var payload = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(
                payload.AsMemory(),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }

        private static async Task ReceiveInitialOutputAsync(
            ClientWebSocket socket,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new InvalidOperationException(
                        "Game Controller closed the control WebSocket before the initial Output was received.");
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    throw new InvalidOperationException(
                        $"Game Controller control WebSocket returned {result.MessageType} instead of text Output.");
                }

                if (result.EndOfMessage)
                {
                    return;
                }
            }
        }
    }
}
