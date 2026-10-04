using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Google.Protobuf;

namespace Duck.Testing.AppHost;

/// <summary>Receives referee multicast updates and checks the Game Controller's control API.</summary>
public sealed class GameControllerReadinessMonitor : IAsyncDisposable
{
    private static readonly IPAddress RefereeGroup = IPAddress.Parse("224.5.23.1");
    private const int RefereePort = 11003;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Socket socket;
    private readonly RefereeReadinessState state;
    private readonly Task receiveLoop;

    public GameControllerReadinessMonitor(int expectedInitialCommand)
    {
        state = new RefereeReadinessState(expectedInitialCommand);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, RefereePort));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(RefereeGroup));
        receiveLoop = ReceiveAsync(cancellation.Token);
    }

    public RefereeReadinessState State => state;

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        if (!state.IsHealthy(DateTimeOffset.UtcNow))
        {
            return false;
        }

        using var webSocket = new ClientWebSocket();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            await webSocket.ConnectAsync(new Uri("ws://127.0.0.1:8082/api/control"), timeout.Token);
            return webSocket.State == WebSocketState.Open;
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        socket.Dispose();
        try
        {
            await receiveLoop;
        }
        catch (Exception exception) when (exception is ObjectDisposedException or SocketException or OperationCanceledException)
        {
            // Closing the socket terminates the receive loop during wrapper shutdown.
        }

        cancellation.Dispose();
    }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[65_535];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote, cancellationToken);
                var referee = global::Referee.Parser.ParseFrom(buffer.AsSpan(0, result.ReceivedBytes));
                if (referee.HasCommand)
                {
                    state.Observe((int)referee.Command, DateTimeOffset.UtcNow);
                }
                else
                {
                    state.Observe(-1, DateTimeOffset.UtcNow);
                }
            }
            catch (InvalidProtocolBufferException)
            {
                state.Observe(-1, DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }
}
