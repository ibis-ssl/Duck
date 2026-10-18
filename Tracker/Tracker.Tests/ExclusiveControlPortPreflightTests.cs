using System.Net;
using System.Net.Sockets;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class ExclusiveControlPortPreflightTests
{
    [Fact]
    public void BaseConfigurationChecksOnlyItsExclusiveControlEndpoints()
    {
        var ports = ExclusiveControlPortPreflight.CreateBaseConfigurationPorts(12346);
        Assert.Equal(
        [
            new ExclusiveControlPort("game-controller control API", ProtocolType.Tcp, 8082),
            new ExclusiveControlPort("cm4-sim Crane-command input", ProtocolType.Udp, 12345),
            new ExclusiveControlPort("simulator Ibis-command input", ProtocolType.Udp, 12346),
        ],
        ports);
        Assert.DoesNotContain(ports, port => port.Port is 10020 or 11010);
    }

    [Fact]
    public void BaseConfigurationUsesConfiguredSimulatorIbisPort()
    {
        var ports = ExclusiveControlPortPreflight.CreateBaseConfigurationPorts(22346);
        Assert.Contains(new ExclusiveControlPort("simulator Ibis-command input", ProtocolType.Udp, 22346), ports);
    }

    [Fact]
    public void NonVisibilityConfigurationOmitsUnusedCm4InputPort()
    {
        var ports = ExclusiveControlPortPreflight.CreateBaseConfigurationPorts(22346, useCm4Simulator: false);
        Assert.DoesNotContain(ports, port => port.Port == 12345);
        Assert.Contains(ports, port => port.Port == 22346 && port.Protocol == ProtocolType.Udp);
    }

    [Fact]
    public void EnsureAvailable_ReportsOccupiedUdpEndpoint()
    {
        using var occupiedSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        occupiedSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)occupiedSocket.LocalEndPoint!).Port;
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExclusiveControlPortPreflight.EnsureAvailable([new ExclusiveControlPort("test UDP listener", ProtocolType.Udp, port)]));
        Assert.Contains("UDP", exception.Message, StringComparison.Ordinal);
        Assert.Contains(port.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("test UDP listener", exception.Message, StringComparison.Ordinal);
        Assert.IsType<SocketException>(exception.InnerException);
    }

    [Fact]
    public void EnsureAvailable_ReportsOccupiedTcpEndpoint()
    {
        using var occupiedListener = new TcpListener(IPAddress.Loopback, 0);
        occupiedListener.Start();
        var port = ((IPEndPoint)occupiedListener.LocalEndpoint).Port;
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExclusiveControlPortPreflight.EnsureAvailable([new ExclusiveControlPort("test TCP control API", ProtocolType.Tcp, port)]));
        Assert.Contains("TCP", exception.Message, StringComparison.Ordinal);
        Assert.Contains(port.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("test TCP control API", exception.Message, StringComparison.Ordinal);
        Assert.IsType<SocketException>(exception.InnerException);
    }
}