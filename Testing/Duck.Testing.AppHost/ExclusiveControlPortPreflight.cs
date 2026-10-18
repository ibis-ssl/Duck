using System.Net;
using System.Net.Sockets;

namespace Duck.Testing.AppHost;

public sealed record ExclusiveControlPort(string Service, ProtocolType Protocol, int Port);

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
            new("game-controller control API", ProtocolType.Tcp, 8082),
        };
        if (useCm4Simulator)
        {
            ports.Add(new("cm4-sim Crane-command input", ProtocolType.Udp, 12345));
        }

        ports.Add(new("simulator Ibis-command input", ProtocolType.Udp, simulatorIbisPort));
        return ports;
    }

    /// <summary>
    /// Attempts to bind each configured exclusive endpoint, then releases it immediately.
    /// This reports current conflicts; another process can still claim a port after the probe.
    /// </summary>
    public static void EnsureAvailable(IEnumerable<ExclusiveControlPort> ports)
    {
        ArgumentNullException.ThrowIfNull(ports);

        foreach (var port in ports)
        {
            ValidatePort(port.Port, port.Service);
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, GetSocketType(port.Protocol), port.Protocol)
                {
                    ExclusiveAddressUse = true,
                };
                socket.Bind(new IPEndPoint(IPAddress.Any, port.Port));
                if (port.Protocol == ProtocolType.Tcp)
                {
                    socket.Listen(1);
                }
            }
            catch (SocketException exception)
            {
                var protocolName = port.Protocol.ToString().ToUpperInvariant();
                throw new InvalidOperationException(
                    $"Duck Aspire cannot start because {port.Service} requires {protocolName} port {port.Port}, which is already in use or unavailable.",
                    exception);
            }
        }
    }

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