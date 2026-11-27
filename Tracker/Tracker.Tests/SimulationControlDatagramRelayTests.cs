using System.Collections.Concurrent;
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

    [Fact]
    public async Task LogsFirstBlueReceiveBeforeForwardingOnlyOnce()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var logger = new ReceiveLogger();
        var simulatorPort = ((IPEndPoint)simulator.Client.LocalEndPoint!).Port;
        await using var relay = new SimulationControlDatagramRelay(IPAddress.Loopback,
            controlListenPort: 0, blueListenPort: 0, controlSimulatorPort: simulatorPort,
            blueSimulatorPort: simulatorPort, logger: logger,
            sendBlueRequest: (socket, packet, destination, token) =>
            {
                logger.Messages.Enqueue("SEND_BLUE_REQUEST");
                return socket.SendAsync(packet, destination, token);
            });
        await relay.StartAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var command = new byte[] { 0x0a, 0x02, 0x08, 0x03 };

        await client.SendAsync(new global::SimulatorCommand().ToByteArray(),
            new IPEndPoint(IPAddress.Loopback, relay.ControlListenPort));
        await logger.ControlForwarded.Task.WaitAsync(timeout.Token);
        for (var i = 0; i < 2; i++)
        {
            await client.SendAsync(command, new IPEndPoint(IPAddress.Loopback, relay.BlueListenPort));
        }
        await logger.TwoBlueForwards.Task.WaitAsync(timeout.Token);
        var bluePort = relay.BlueListenPort;
        await relay.StopAsync(CancellationToken.None);

        // Socket ownership ends at StopAsync; the expected port is captured before stopping.
        var entries = logger.Messages.ToArray();
        var received = Assert.Single(entries, entry => entry.StartsWith("MATCH_BLUE_FIRST_RECEIVED ", StringComparison.Ordinal));
        var forwards = entries.Where(entry => entry.StartsWith("MATCH_BLUE_ROBOT_COMMAND ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, forwards.Length);
        Assert.True(Array.IndexOf(entries, received) < Array.IndexOf(entries, forwards[0]));
        var order = entries.Where(entry => entry == received || entry == "SEND_BLUE_REQUEST" || forwards.Contains(entry));
        Assert.Equal(new[] { received, "SEND_BLUE_REQUEST", forwards[0], "SEND_BLUE_REQUEST", forwards[1] }, order);
        Assert.Contains($"port={bluePort}", received);
        Assert.Contains($"client={client.Client.LocalEndPoint}", received);
        Assert.Contains($"bytes={command.Length}", received);
        var timeText = received.Split(' ').Single(part => part.StartsWith("receivedAtUtc=", StringComparison.Ordinal))["receivedAtUtc=".Length..];
        Assert.True(DateTimeOffset.TryParse(timeText, out var time));
        Assert.Equal(TimeSpan.Zero, time.Offset);
    }

    private sealed class ReceiveLogger : ILogger<SimulationControlDatagramRelay>
    {
        internal ConcurrentQueue<string> Messages { get; } = new();
        internal TaskCompletionSource ControlForwarded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource TwoBlueForwards { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int forwards;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Messages.Enqueue(message);
            if (message.StartsWith("MATCH_SIMULATION_CONTROL ", StringComparison.Ordinal))
                ControlForwarded.TrySetResult();
            if (message.StartsWith("MATCH_BLUE_ROBOT_COMMAND ", StringComparison.Ordinal) &&
                Interlocked.Increment(ref forwards) == 2)
                TwoBlueForwards.TrySetResult();
        }
    }
}
