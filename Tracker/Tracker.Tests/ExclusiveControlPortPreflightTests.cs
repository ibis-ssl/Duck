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
            new ExclusiveControlPort("game-controller control API", ProtocolType.Tcp, 8082, AddressFamily.InterNetworkV6, DualMode: true),
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
    public void BaseConfigurationRejectsTheSameUdpPortForCm4AndSimulator()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExclusiveControlPortPreflight.CreateBaseConfigurationPorts(12345));

        Assert.Contains("cm4-sim Crane-command input", exception.Message, StringComparison.Ordinal);
        Assert.Contains("simulator Ibis-command input", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UDP", exception.Message, StringComparison.Ordinal);
        Assert.Contains("12345", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureAvailableRejectsDuplicateEndpointsBeforeBinding()
    {
        using var occupiedSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        occupiedSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)occupiedSocket.LocalEndPoint!).Port;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExclusiveControlPortPreflight.EnsureAvailable(
            [
                new ExclusiveControlPort("first UDP service", ProtocolType.Udp, port),
                new ExclusiveControlPort("second UDP service", ProtocolType.Udp, port),
            ]));

        Assert.Contains("first UDP service", exception.Message, StringComparison.Ordinal);
        Assert.Contains("second UDP service", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UDP", exception.Message, StringComparison.Ordinal);
        Assert.Contains(port.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
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

    [Fact]
    public void EnsureAvailable_ReportsOccupiedIpv6DualStackTcpEndpoint()
    {
        if (!Socket.OSSupportsIPv6)
        {
            throw Xunit.Sdk.SkipException.ForSkip("IPv6 is not supported by this operating system.");
        }

        using var occupiedListener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
        {
            DualMode = false,
        };
        try
        {
            occupiedListener.Bind(new IPEndPoint(IPAddress.IPv6Loopback, 0));
            occupiedListener.Listen(1);
        }
        catch (SocketException socketException) when (IsIpv6Unavailable(socketException.SocketErrorCode))
        {
            throw Xunit.Sdk.SkipException.ForSkip($"IPv6 loopback is unavailable: {socketException.SocketErrorCode}.");
        }

        var port = ((IPEndPoint)occupiedListener.LocalEndPoint!).Port;
        var controlApi = ExclusiveControlPortPreflight.CreateBaseConfigurationPorts(12346)
            .Single(endpoint => endpoint.Service == "game-controller control API") with { Port = port };
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExclusiveControlPortPreflight.EnsureAvailable([controlApi]));

        Assert.Contains("game-controller control API", exception.Message, StringComparison.Ordinal);
        Assert.Contains("TCP", exception.Message, StringComparison.Ordinal);
        Assert.Contains(port.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.IsType<SocketException>(exception.InnerException);
    }

    [Fact]
    public void EnsureAvailable_DoesNotTreatIpv6OnlyUdpEndpointAsOccupiedForIpv4Service()
    {
        if (!Socket.OSSupportsIPv6)
        {
            throw Xunit.Sdk.SkipException.ForSkip("IPv6 is not supported by this operating system.");
        }

        using var occupiedSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
        {
            DualMode = false,
        };
        try
        {
            occupiedSocket.Bind(new IPEndPoint(IPAddress.IPv6Loopback, 0));
        }
        catch (SocketException socketException) when (IsIpv6Unavailable(socketException.SocketErrorCode))
        {
            throw Xunit.Sdk.SkipException.ForSkip($"IPv6 loopback is unavailable: {socketException.SocketErrorCode}.");
        }

        var port = ((IPEndPoint)occupiedSocket.LocalEndPoint!).Port;

        var exception = Record.Exception(() => ExclusiveControlPortPreflight.EnsureAvailable(
            [new ExclusiveControlPort("IPv4-only UDP service", ProtocolType.Udp, port)]));

        Assert.Null(exception);
    }

    private static bool IsIpv6Unavailable(SocketError error) =>
        error is SocketError.AddressFamilyNotSupported or SocketError.ProtocolNotSupported or SocketError.AddressNotAvailable or SocketError.OperationNotSupported;
}
