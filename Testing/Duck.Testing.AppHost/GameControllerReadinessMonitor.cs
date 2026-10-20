using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
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

/// <summary>Receives referee multicast updates and checks the Game Controller's control API.</summary>
public sealed class GameControllerReadinessMonitor : IGameControllerReadinessMonitor
{
    private static readonly IPAddress RefereeGroup = IPAddress.Parse("224.5.23.1");
    private const int RefereePort = 11003;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Socket socket;
    private readonly RefereeReadinessState state;
    private readonly string expectedYellowTeamName;
    private readonly Task receiveLoop;
    private readonly GameControllerTeamNameRetryPolicy teamNameConfigurationRetry = new();
    private DateTimeOffset? runtimeRecoveryStartedAt;
    private bool hasBeenHealthy;

    public GameControllerReadinessMonitor(int expectedInitialCommand, string expectedYellowTeamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedYellowTeamName);
        this.expectedYellowTeamName = expectedYellowTeamName;
        state = new RefereeReadinessState(expectedInitialCommand, expectedYellowTeamName: expectedYellowTeamName);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, RefereePort));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(RefereeGroup));
        receiveLoop = ReceiveAsync(cancellation.Token);
    }

    public RefereeReadinessState State => state;

    async Task<GameControllerRuntimeReadiness> IGameControllerReadinessMonitor.GetRuntimeReadinessAsync(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        if (await IsHealthyAsync(cancellationToken))
        {
            return GameControllerRuntimeReadiness.Healthy;
        }

        if (!hasBeenHealthy)
        {
            return GameControllerRuntimeReadiness.Recovering;
        }

        runtimeRecoveryStartedAt ??= checkedAt;
        return teamNameConfigurationRetry.IsRecoveryExpired(runtimeRecoveryStartedAt.Value, DateTimeOffset.UtcNow)
            ? GameControllerRuntimeReadiness.Failed
            : GameControllerRuntimeReadiness.Recovering;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        if (!state.IsCommandHealthy(DateTimeOffset.UtcNow))
        {
            return false;
        }

        using var webSocket = new ClientWebSocket();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            await webSocket.ConnectAsync(new Uri("ws://127.0.0.1:8082/api/control"), timeout.Token);
            if (!state.HasExpectedYellowTeamName)
            {
                if (teamNameConfigurationRetry.TryBeginSend(DateTimeOffset.UtcNow, state.HasExpectedYellowTeamName))
                {
                    try
                    {
                        await new Duck.Testing.RefereeDriver.GameControllerWebSocketControlClient()
                            .SendYellowTeamNameAsync(expectedYellowTeamName, timeout.Token);
                    }
                    finally
                    {
                        teamNameConfigurationRetry.RecordSendCompleted(DateTimeOffset.UtcNow);
                    }
                }

                return false;
            }

            var healthy = state.IsHealthy(DateTimeOffset.UtcNow) && webSocket.State == WebSocketState.Open;
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
                ObserveRefereePacket(state, referee, DateTimeOffset.UtcNow);
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

    public TimeSpan MaximumRecoveryDuration =>
        TimeSpan.FromTicks((requestTimeout.Ticks + retryInterval.Ticks) * maximumAttempts + retryInterval.Ticks);

    public bool TryBeginSend(DateTimeOffset now, bool expectedNameObserved)
    {
        lock (gate)
        {
            if (expectedNameObserved || attempts >= maximumAttempts ||
                (lastAttemptAt is { } previous && now - previous < retryInterval))
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

    public bool IsRecoveryExpired(DateTimeOffset recoveryStartedAt, DateTimeOffset now)
    {
        lock (gate)
        {
            if (now - recoveryStartedAt >= MaximumRecoveryDuration)
            {
                return true;
            }

            if (attempts < maximumAttempts)
            {
                return false;
            }

            var confirmationStartedAt = lastAttemptAt is { } lastAttempt && lastAttempt >= recoveryStartedAt
                ? lastAttempt
                : recoveryStartedAt;
            return now - confirmationStartedAt >= retryInterval;
        }
    }
}
