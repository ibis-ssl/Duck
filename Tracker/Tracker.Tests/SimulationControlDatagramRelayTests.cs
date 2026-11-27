using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using Duck.Testing.RefereeDriver.Hosting;
using Google.Protobuf;
using Xunit;

namespace Tracker.Tests;

public sealed class SimulationControlDatagramRelayTests
{
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
