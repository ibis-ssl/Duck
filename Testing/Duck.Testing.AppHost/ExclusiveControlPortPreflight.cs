using System.Net;
using System.Net.Sockets;

namespace Duck.Testing.AppHost;

public sealed record ExclusiveControlPort(
    string Service,
    ProtocolType Protocol,
    int Port,
    AddressFamily BindAddressFamily = AddressFamily.InterNetwork,
    bool DualMode = false);

/// <summary>
/// Provides a best-effort preflight check for control ports before Aspire resources are registered.
/// The probe releases its socket before startup and does not reserve the port or eliminate a later race.
/// </summary>
public static class ExclusiveControlPortPreflight
{
    public static IReadOnlyList<ExclusiveControlPort> CreateBaseConfigurationPorts(
        int simulatorIbisPort,
        bool useCm4Simulator = true)
    {
        ValidatePort(simulatorIbisPort, "simulator Ibis-command input");

        var ports = new List<ExclusiveControlPort>
        {
            new("game-controller control API", ProtocolType.Tcp, 8082, AddressFamily.InterNetworkV6, DualMode: true),
        };
        if (useCm4Simulator)
        {
            ports.Add(new("cm4-sim Crane-command input", ProtocolType.Udp, 12345));
        }

        ports.Add(new("simulator Ibis-command input", ProtocolType.Udp, simulatorIbisPort));
        ValidateDistinctEndpoints(ports);
        return ports;
    }

    /// <summary>
    /// Attempts to bind each configured exclusive endpoint, then releases it immediately.
    /// This reports current conflicts; another process can still claim a port after the probe.
    /// </summary>
    public static void EnsureAvailable(IEnumerable<ExclusiveControlPort> ports)
    {
        ArgumentNullException.ThrowIfNull(ports);

        var configuredPorts = ports.ToArray();
        foreach (var port in configuredPorts)
        {
            ValidatePort(port.Port, port.Service);
            _ = GetSocketType(port.Protocol);
        }

        ValidateDistinctEndpoints(configuredPorts);

        foreach (var port in configuredPorts)
        {
            try
            {
                Probe(port, port.BindAddressFamily, port.DualMode);
            }
            catch (SocketException exception) when (
                port.BindAddressFamily == AddressFamily.InterNetworkV6
                && port.DualMode
                && IsIpv6Unavailable(exception.SocketErrorCode))
            {
                // Go's unspecified TCP listener falls back to IPv4 when IPv6 is unavailable.
                try
                {
                    Probe(port, AddressFamily.InterNetwork, dualMode: false);
                }
                catch (SocketException fallbackException)
                {
                    throw CreateUnavailableException(port, fallbackException);
                }
            }
            catch (SocketException exception)
            {
                throw CreateUnavailableException(port, exception);
            }
        }
    }

    private static void Probe(ExclusiveControlPort port, AddressFamily family, bool dualMode)
    {
        using var socket = new Socket(family, GetSocketType(port.Protocol), port.Protocol)
        {
            ExclusiveAddressUse = true,
        };
        if (family == AddressFamily.InterNetworkV6)
        {
            socket.DualMode = dualMode;
        }

        var address = family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
        socket.Bind(new IPEndPoint(address, port.Port));
        if (port.Protocol == ProtocolType.Tcp)
        {
            socket.Listen(1);
        }
    }

    private static InvalidOperationException CreateUnavailableException(ExclusiveControlPort port, SocketException exception)
    {
        var protocolName = port.Protocol.ToString().ToUpperInvariant();
        return new InvalidOperationException(
            $"Duck Aspire cannot start because {port.Service} requires {protocolName} port {port.Port}, which is already in use or unavailable.",
            exception);
    }

    private static void ValidateDistinctEndpoints(IEnumerable<ExclusiveControlPort> ports)
    {
        var duplicate = ports
            .GroupBy(port => (port.Protocol, port.Port))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is null)
        {
            return;
        }

        var services = string.Join(" and ", duplicate.Select(port => port.Service));
        var protocolName = duplicate.Key.Protocol.ToString().ToUpperInvariant();
        throw new InvalidOperationException(
            $"Duck Aspire cannot start because {services} both require {protocolName} port {duplicate.Key.Port}.");
    }

    private static bool IsIpv6Unavailable(SocketError error) =>
        error is SocketError.AddressFamilyNotSupported or SocketError.ProtocolNotSupported or SocketError.AddressNotAvailable or SocketError.OperationNotSupported;

    private static SocketType GetSocketType(ProtocolType protocol) => protocol switch
    {
        ProtocolType.Tcp => SocketType.Stream,
        ProtocolType.Udp => SocketType.Dgram,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Only TCP and UDP control ports are supported."),
    };

    private static void ValidatePort(int port, string service)
    {
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException($"Duck Aspire cannot start because {service} has invalid port {port}; expected a port from 1 to 65535.");
        }
    }
}
