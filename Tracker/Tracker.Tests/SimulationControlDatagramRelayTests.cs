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
}
