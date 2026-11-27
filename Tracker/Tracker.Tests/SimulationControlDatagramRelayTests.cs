using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using Duck.Testing.RefereeDriver.Hosting;
using Google.Protobuf;
using Xunit;

namespace Tracker.Tests;

public sealed class SimulationControlDatagramRelayTests
{
    private sealed class IngressLogger : ILogger<SimulationControlDatagramRelay>
    {
        internal ConcurrentQueue<JsonElement> Observations { get; } = new();
        internal TaskCompletionSource First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Final { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool ThrowOnIngress { get; init; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            const string prefix = "MATCH_BLUE_INGRESS_INTERVAL ";
            var message = formatter(state, exception);
            if (!message.StartsWith(prefix, StringComparison.Ordinal)) return;
            using var json = JsonDocument.Parse(message[prefix.Length..]);
            Observations.Enqueue(json.RootElement.Clone());
            First.TrySetResult();
            if (json.RootElement.GetProperty("Final").GetBoolean()) Final.TrySetResult();
            if (ThrowOnIngress) throw new InvalidOperationException("diagnostic logger failure");
        }
    }

    private static byte[] BlueMovementPacket() => new global::RobotControl
    {
        RobotCommands =
        {
            new global::RobotCommand
            {
                Id = 3,
                MoveCommand = new global::RobotMoveCommand
                {
                    LocalVelocity = new global::MoveLocalVelocity { Forward = 1.25f, Left = -0.5f, Angular = 0.25f },
                },
            },
            new global::RobotCommand
            {
                Id = 8,
                MoveCommand = new global::RobotMoveCommand
                {
                    GlobalVelocity = new global::MoveGlobalVelocity { X = -2f, Y = 3f, Angular = 0.5f },
                },
            },
        },
    }.ToByteArray();

    [Fact]
    public async Task EnabledIngressReportsIdleIntervalWithoutReceiving()
    {
        var logger = new IngressLogger();
        await using var relay = new SimulationControlDatagramRelay(IPAddress.Loopback,
            controlListenPort: 0, blueListenPort: 0, logger: logger,
            blueIngressDiagnosticEnabled: true, blueIngressDiagnosticInterval: TimeSpan.FromMilliseconds(50));
        await relay.StartAsync(CancellationToken.None);
        await logger.First.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var observation = logger.Observations.First();
        Assert.Equal(0, observation.GetProperty("Received").GetInt64());
        Assert.Equal(0, observation.GetProperty("Forwarded").GetInt64());
        Assert.Equal(0, observation.GetProperty("ForwardFailed").GetInt64());
        Assert.False(observation.GetProperty("Robot3HasMove").GetBoolean());
        Assert.False(observation.GetProperty("Robot8HasMove").GetBoolean());
        var start = observation.GetProperty("StartUtc").GetDateTimeOffset();
        var end = observation.GetProperty("EndUtc").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, start.Offset);
        Assert.Equal(TimeSpan.Zero, end.Offset);
        Assert.True(end > start);
    }

    [Fact]
    public async Task EnabledIngressReportsFailedSendAndPreservesSocketFailureEvenIfDiagnosticLoggerThrows()
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var logger = new IngressLogger { ThrowOnIngress = true };
        // UDP port zero is an invalid send destination; use the real SendAsync path.
        var relay = new SimulationControlDatagramRelay(IPAddress.Loopback,
            controlListenPort: 0, blueListenPort: 0, blueSimulatorPort: 0,
            logger: logger, blueIngressDiagnosticEnabled: true,
            blueIngressDiagnosticInterval: TimeSpan.FromMinutes(1));
        await relay.StartAsync(CancellationToken.None);
        await client.SendAsync(BlueMovementPacket(), new IPEndPoint(IPAddress.Loopback, relay.BlueListenPort));
        await logger.First.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var observation = Assert.Single(logger.Observations);
        Assert.Equal(1, observation.GetProperty("Received").GetInt64());
        Assert.Equal(0, observation.GetProperty("Forwarded").GetInt64());
        Assert.Equal(1, observation.GetProperty("ForwardFailed").GetInt64());
        Assert.Equal(0, observation.GetProperty("PendingAtEnd").GetInt32());
        Assert.True(observation.GetProperty("Robot3HasMove").GetBoolean());
        Assert.True(observation.GetProperty("Robot8HasMove").GetBoolean());
        var original = await Assert.ThrowsAsync<SocketException>(() => relay.StopAsync(CancellationToken.None));
        Assert.Same(original, await Assert.ThrowsAsync<SocketException>(() => relay.DisposeAsync().AsTask()));
    }

    [Fact]
    public async Task EnabledIngressReportsSuccessfulSendAndRepresentativeMovesWithoutChangingBytes()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var logger = new IngressLogger();
        await using var relay = new SimulationControlDatagramRelay(IPAddress.Loopback,
            controlListenPort: 0, blueListenPort: 0, blueSimulatorPort: ((IPEndPoint)simulator.Client.LocalEndPoint!).Port,
            logger: logger, blueIngressDiagnosticEnabled: true,
            blueIngressDiagnosticInterval: TimeSpan.FromMinutes(1));
        await relay.StartAsync(CancellationToken.None);
        var bytes = BlueMovementPacket();
        await client.SendAsync(bytes, new IPEndPoint(IPAddress.Loopback, relay.BlueListenPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var forwarded = await simulator.ReceiveAsync(timeout.Token);
        Assert.Equal(bytes, forwarded.Buffer);
        // Round trip ensures the relay has reached the successful send before stopping.
        await simulator.SendAsync(new byte[] { 0x12, 0x02, 0x08, 0x03 }, forwarded.RemoteEndPoint);
        await client.ReceiveAsync(timeout.Token);
        await relay.StopAsync(CancellationToken.None);
        var observation = Assert.Single(logger.Observations);
        Assert.Equal(1, observation.GetProperty("Received").GetInt64());
        Assert.Equal(1, observation.GetProperty("Forwarded").GetInt64());
        Assert.Equal(0, observation.GetProperty("ForwardFailed").GetInt64());
        Assert.True(observation.GetProperty("Robot3HasMove").GetBoolean());
        Assert.True(observation.GetProperty("Robot8HasMove").GetBoolean());
        Assert.Equal(1.25f, observation.GetProperty("Robot3Move").GetProperty("Forward").GetSingle());
        Assert.Equal(-2f, observation.GetProperty("Robot8Move").GetProperty("X").GetSingle());
    }

    [Fact]
    public async Task IngressDiagnosticIsOffByDefaultEvenWhenBluePacketIsForwarded()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var logger = new IngressLogger();
        await using var relay = new SimulationControlDatagramRelay(IPAddress.Loopback,
            controlListenPort: 0, blueListenPort: 0, blueSimulatorPort: ((IPEndPoint)simulator.Client.LocalEndPoint!).Port,
            logger: logger, blueIngressDiagnosticInterval: TimeSpan.FromMilliseconds(10));
        await relay.StartAsync(CancellationToken.None);
        var bytes = BlueMovementPacket();
        await client.SendAsync(bytes, new IPEndPoint(IPAddress.Loopback, relay.BlueListenPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Equal(bytes, (await simulator.ReceiveAsync(timeout.Token)).Buffer);
        await relay.StopAsync(CancellationToken.None);
        Assert.Empty(logger.Observations);
    }

    [Fact]
    public async Task IngressIntervalLimitDoesNotStopForwarding()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var logger = new IngressLogger();
        await using var relay = new SimulationControlDatagramRelay(IPAddress.Loopback,
            controlListenPort: 0, blueListenPort: 0, blueSimulatorPort: ((IPEndPoint)simulator.Client.LocalEndPoint!).Port,
            logger: logger, blueIngressDiagnosticEnabled: true,
            blueIngressDiagnosticInterval: TimeSpan.FromMilliseconds(1));
        await relay.StartAsync(CancellationToken.None);
        await logger.Final.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(360, logger.Observations.Count);
        var bytes = BlueMovementPacket();
        await client.SendAsync(bytes, new IPEndPoint(IPAddress.Loopback, relay.BlueListenPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Equal(bytes, (await simulator.ReceiveAsync(timeout.Token)).Buffer);
        await relay.StopAsync(CancellationToken.None);
        Assert.Equal(360, logger.Observations.Count);
    }

    [Fact]
    public async Task HostedStopThenServiceProviderDisposalOfBothRegistrationsIsSafe()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => new SimulationControlDatagramRelay(IPAddress.Loopback,
            controlListenPort: 0, blueListenPort: 0));
        services.AddHostedService(provider => provider.GetRequiredService<SimulationControlDatagramRelay>());
        var provider = services.BuildServiceProvider();
        var relay = provider.GetRequiredService<SimulationControlDatagramRelay>();
        var hosted = Assert.Single(provider.GetServices<IHostedService>());
        Assert.Same(relay, hosted);
        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();
        await relay.StopAsync(CancellationToken.None);
        await relay.DisposeAsync();
    }

    [Fact]
    public async Task RepeatedStopAndDisposePreserveUnexpectedForwardingFailure()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var logger = new FailingBlueLogger();
        var relay = new SimulationControlDatagramRelay(IPAddress.Loopback,
            controlListenPort: 0, blueListenPort: 0,
            blueSimulatorPort: ((IPEndPoint)simulator.Client.LocalEndPoint!).Port, logger: logger);
        await relay.StartAsync(CancellationToken.None);
        await client.SendAsync(new byte[] { 0x0a, 0x02, 0x08, 0x03 },
            new IPEndPoint(IPAddress.Loopback, relay.BlueListenPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await logger.Called.Task.WaitAsync(timeout.Token);
        var firstStop = relay.StopAsync(CancellationToken.None);
        var disposal = relay.DisposeAsync().AsTask();
        Assert.Same(logger.Expected, await Assert.ThrowsAsync<InvalidOperationException>(() => firstStop));
        Assert.Same(logger.Expected, await Assert.ThrowsAsync<InvalidOperationException>(() => disposal));
        Assert.Same(logger.Expected, await Assert.ThrowsAsync<InvalidOperationException>(() => relay.StopAsync(CancellationToken.None)));
    }

    private sealed class FailingBlueLogger : ILogger<SimulationControlDatagramRelay>
    {
        internal TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal InvalidOperationException Expected { get; } = new("unexpected forwarding diagnostic failure");
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception).StartsWith("MATCH_BLUE_ROBOT_COMMAND ", StringComparison.Ordinal))
            {
                Called.TrySetResult();
                throw Expected;
            }
        }
    }

    [Fact]
    public async Task BlueCommandAndSimulatorResponseAreForwardedByteForByte()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var relay = new SimulationControlDatagramRelay(
            IPAddress.Loopback,
            controlListenPort: 0,
            blueListenPort: 0,
            blueSimulatorPort: ((IPEndPoint)simulator.Client.LocalEndPoint!).Port);
        await relay.StartAsync(CancellationToken.None);

        var command = new byte[] { 0x0a, 0x02, 0x08, 0x03 };
        await client.SendAsync(command, new IPEndPoint(IPAddress.Loopback, relay.BlueListenPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var forwarded = await simulator.ReceiveAsync(timeout.Token);
        Assert.Equal(command, forwarded.Buffer);

        var response = new byte[] { 0x12, 0x02, 0x08, 0x03 };
        await simulator.SendAsync(response, forwarded.RemoteEndPoint);
        var returned = await client.ReceiveAsync(timeout.Token);
        Assert.Equal(response, returned.Buffer);
        Assert.Equal(relay.BlueListenPort, returned.RemoteEndPoint.Port);
    }

    [Fact]
    public async Task SimulatorControlPacketIsForwardedByteForByte()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var relay = new SimulationControlDatagramRelay(
            IPAddress.Loopback,
            controlListenPort: 0,
            blueListenPort: 0,
            controlSimulatorPort: ((IPEndPoint)simulator.Client.LocalEndPoint!).Port);
        await relay.StartAsync(CancellationToken.None);

        var command = new global::SimulatorCommand
        {
            Control = new global::SimulatorControl
            {
                TeleportBall = new global::TeleportBall { X = 0, Y = 0 },
            },
        }.ToByteArray();
        await client.SendAsync(command, new IPEndPoint(IPAddress.Loopback, relay.ControlListenPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var forwarded = await simulator.ReceiveAsync(timeout.Token);
        Assert.Equal(command, forwarded.Buffer);

        var response = new global::SimulatorResponse().ToByteArray();
        await simulator.SendAsync(response, forwarded.RemoteEndPoint);
        var returned = await client.ReceiveAsync(timeout.Token);
        Assert.Equal(response, returned.Buffer);
        Assert.Equal(relay.ControlListenPort, returned.RemoteEndPoint.Port);
    }
}
