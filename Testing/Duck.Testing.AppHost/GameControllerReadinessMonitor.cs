using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Duck.Testing.AppHost;

internal enum GameControllerRuntimeReadiness
{
    Healthy,
    Recovering,
    Failed,
}

internal interface IGameControllerReadinessMonitor : IAsyncDisposable
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
    Task<GameControllerRuntimeReadiness> GetRuntimeReadinessAsync(CancellationToken cancellationToken);
}

internal interface IGameControllerRefereePacketTransport : IAsyncDisposable
{
    void Start(Action<global::Referee, DateTimeOffset> onPacket, TimeProvider timeProvider, CancellationToken cancellationToken);
}

/// <summary>Receives referee multicast updates and checks the Game Controller's control API.</summary>
public sealed class GameControllerReadinessMonitor : IGameControllerReadinessMonitor
{
    private static readonly IPAddress RefereeGroup = IPAddress.Parse("224.5.23.1");
    private const int RefereePort = 11003;
    private static readonly TimeSpan ControlApiTimeout = TimeSpan.FromSeconds(2);
    private readonly CancellationTokenSource cancellation = new();
    private readonly IGameControllerRefereePacketTransport packetTransport;
    private readonly Func<CancellationToken, Task<bool>>? controlApiProbe;
    private readonly GameControllerWebSocketControlClient controlClient;
    private readonly TimeProvider timeProvider;
    private readonly RefereeReadinessState state;
    private readonly string expectedYellowTeamName;
    private readonly GameControllerTeamNameRetryPolicy teamNameConfigurationRetry = new();
    private DateTimeOffset? runtimeRecoveryStartedAt;
    private bool hasBeenHealthy;

    public GameControllerReadinessMonitor(int expectedInitialCommand, string expectedYellowTeamName)
        : this(
            expectedInitialCommand,
            ValidateExpectedTeamName(expectedYellowTeamName),
            TimeProvider.System,
            new SocketGameControllerRefereePacketTransport(RefereeGroup, RefereePort),
            null,
            null)
    {
    }

    private static string ValidateExpectedTeamName(string expectedYellowTeamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedYellowTeamName);
        return expectedYellowTeamName;
    }

    internal GameControllerReadinessMonitor(
        int expectedInitialCommand,
        string expectedYellowTeamName,
        TimeProvider timeProvider,
        IGameControllerRefereePacketTransport packetTransport,
        IGameControllerWebSocketTransport? webSocketTransport,
        Func<CancellationToken, Task<bool>>? controlApiProbe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedYellowTeamName);
        this.expectedYellowTeamName = expectedYellowTeamName;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.packetTransport = packetTransport ?? throw new ArgumentNullException(nameof(packetTransport));
        this.controlApiProbe = controlApiProbe;
        controlClient = webSocketTransport is null
            ? new GameControllerWebSocketControlClient()
            : new GameControllerWebSocketControlClient(webSocketTransport);
        state = new RefereeReadinessState(expectedInitialCommand, expectedYellowTeamName: expectedYellowTeamName);
        packetTransport.Start(
            (referee, receivedAt) => ObserveRefereePacket(state, referee, receivedAt),
            timeProvider,
            cancellation.Token);
    }

    public RefereeReadinessState State => state;

    async Task<GameControllerRuntimeReadiness> IGameControllerReadinessMonitor.GetRuntimeReadinessAsync(CancellationToken cancellationToken)
    {
        var checkedAt = timeProvider.GetUtcNow();
        if (hasBeenHealthy)
        {
            runtimeRecoveryStartedAt ??= checkedAt;
        }

        if (runtimeRecoveryStartedAt is { } recoveryStart &&
            teamNameConfigurationRetry.IsRecoveryExpired(recoveryStart, checkedAt))
        {
            return GameControllerRuntimeReadiness.Failed;
        }

        if (await IsHealthyAsync(cancellationToken))
        {
            return runtimeRecoveryStartedAt is { } completedRecoveryStart &&
                   teamNameConfigurationRetry.IsRecoveryExpired(completedRecoveryStart, timeProvider.GetUtcNow())
                ? GameControllerRuntimeReadiness.Failed
                : GameControllerRuntimeReadiness.Healthy;
        }

        if (!hasBeenHealthy)
        {
            return GameControllerRuntimeReadiness.Recovering;
        }

        runtimeRecoveryStartedAt ??= checkedAt;
        return teamNameConfigurationRetry.IsRecoveryExpired(runtimeRecoveryStartedAt.Value, timeProvider.GetUtcNow())
            ? GameControllerRuntimeReadiness.Failed
            : GameControllerRuntimeReadiness.Recovering;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!state.IsCommandHealthy(now) ||
            runtimeRecoveryStartedAt is { } recoveryStart &&
            teamNameConfigurationRetry.IsRecoveryExpired(recoveryStart, now))
        {
            return false;
        }

        var controlTimeout = ControlApiTimeout;
        if (runtimeRecoveryStartedAt is { } startedAt)
        {
            var remainingRecovery = startedAt + teamNameConfigurationRetry.MaximumRecoveryDuration - now;
            if (remainingRecovery < controlTimeout)
            {
                controlTimeout = remainingRecovery;
            }

            if (controlTimeout <= TimeSpan.Zero)
            {
                return false;
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(controlTimeout);
        ClientWebSocket? webSocket = null;
        try
        {
            if (controlApiProbe is not null)
            {
                if (!await controlApiProbe(timeout.Token))
                {
                    return false;
                }
            }
            else
            {
                webSocket = new ClientWebSocket();
                await webSocket.ConnectAsync(new Uri("ws://127.0.0.1:8082/api/control"), timeout.Token);
            }

            if (!state.HasExpectedYellowTeamName)
            {
                if (teamNameConfigurationRetry.TryBeginSend(
                    timeProvider.GetUtcNow(),
                    state.HasExpectedYellowTeamName,
                    runtimeRecoveryStartedAt))
                {
                    try
                    {
                        await controlClient.SendYellowTeamNameAsync(expectedYellowTeamName, timeout.Token);
                    }
                    finally
                    {
                        teamNameConfigurationRetry.RecordSendCompleted(timeProvider.GetUtcNow());
                    }
                }

                return false;
            }

            var healthy = state.IsHealthy(timeProvider.GetUtcNow()) &&
                (controlApiProbe is not null || webSocket?.State == WebSocketState.Open);
            if (healthy)
            {
                hasBeenHealthy = true;
                runtimeRecoveryStartedAt = null;
            }

            return healthy;
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or InvalidOperationException)
        {
            return false;
        }
        finally
        {
            webSocket?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        await packetTransport.DisposeAsync();
        cancellation.Dispose();
    }

    internal static void ObserveRefereePacket(
        RefereeReadinessState readinessState,
        global::Referee referee,
        DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(readinessState);
        ArgumentNullException.ThrowIfNull(referee);

        if (referee.HasCommand)
        {
            readinessState.Observe(
                (int)referee.Command,
                receivedAt,
                referee.Yellow?.Name ?? string.Empty);
        }
        else
        {
            readinessState.Observe(-1, receivedAt);
        }
    }
}

internal sealed class SocketGameControllerRefereePacketTransport(
    IPAddress refereeGroup,
    int refereePort) : IGameControllerRefereePacketTransport
{
    private readonly Socket socket = CreateSocket(refereeGroup, refereePort);
    private Task? receiveLoop;

    public void Start(Action<global::Referee, DateTimeOffset> onPacket, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onPacket);
        receiveLoop = ReceiveAsync(onPacket, timeProvider, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        socket.Dispose();
        if (receiveLoop is null) return;
        try
        {
            await receiveLoop;
        }
        catch (Exception exception) when (exception is ObjectDisposedException or SocketException or OperationCanceledException)
        {
            // Socket disposal terminates the receive loop during wrapper shutdown.
        }
    }

    private async Task ReceiveAsync(
        Action<global::Referee, DateTimeOffset> onPacket,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[65_535];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote, cancellationToken);
                var referee = global::Referee.Parser.ParseFrom(buffer.AsSpan(0, result.ReceivedBytes));
                onPacket(referee, timeProvider.GetUtcNow());
            }
            catch (InvalidProtocolBufferException)
            {
                onPacket(new global::Referee(), timeProvider.GetUtcNow());
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }
        }
    }

    private static Socket CreateSocket(IPAddress group, int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(group));
        return socket;
    }
}

internal sealed class GameControllerTeamNameRetryPolicy(
    int maximumAttempts = 3,
    TimeSpan? retryInterval = null,
    TimeSpan? requestTimeout = null)
{
    private readonly object gate = new();
    private readonly TimeSpan retryInterval = retryInterval ?? TimeSpan.FromSeconds(1);
    private readonly TimeSpan requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(2);
    private DateTimeOffset? lastAttemptAt;
    private int attempts;

    public TimeSpan RequestTimeout => requestTimeout;

    public TimeSpan MaximumRecoveryDuration => TimeSpan.FromSeconds(10);

    public bool TryBeginSend(
        DateTimeOffset now,
        bool expectedNameObserved,
        DateTimeOffset? recoveryStartedAt = null)
    {
        lock (gate)
        {
            if (expectedNameObserved || attempts >= maximumAttempts ||
                (lastAttemptAt is { } previous && now - previous < retryInterval))
            {
                return false;
            }

            if (recoveryStartedAt is { } startedAt &&
                startedAt + MaximumRecoveryDuration - now < requestTimeout + retryInterval)
            {
                return false;
            }

            attempts++;
            lastAttemptAt = now;
            return true;
        }
    }

    public void RecordSendCompleted(DateTimeOffset completedAt)
    {
        lock (gate)
        {
            if (attempts > 0)
            {
                lastAttemptAt = completedAt;
            }
        }
    }

    public bool IsRecoveryExpired(DateTimeOffset recoveryStartedAt, DateTimeOffset now) =>
        now - recoveryStartedAt >= MaximumRecoveryDuration;
}
