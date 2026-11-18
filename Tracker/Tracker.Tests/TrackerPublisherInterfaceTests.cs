using System.Net;
using System.Net.NetworkInformation;
using Tracker.Core;

namespace Tracker.Tests;

public sealed class TrackerPublisherInterfaceTests
{
    [Fact]
    public void UnspecifiedInterfaceUsesSystemRoutingTable()
    {
        Assert.Equal(IPAddress.Any, UdpTrackerPacketPublisher.ResolveMulticastInterfaceAddress(null));
        Assert.Equal(IPAddress.Any, UdpTrackerPacketPublisher.ResolveMulticastInterfaceAddress(" "));
    }

    [Fact]
    public void UnknownInterfaceFailsBeforePublishing()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => UdpTrackerPacketPublisher.ResolveMulticastInterfaceAddress("duck-missing-interface"));

        Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PublisherAcceptsAConfiguredIpv4Interface()
    {
        var candidate = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .FirstOrDefault(networkInterface => networkInterface.GetIPProperties().UnicastAddresses
                .Any(address => address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address.Address)));

        Assert.NotNull(candidate);
        var interfaceAddress = UdpTrackerPacketPublisher.ResolveMulticastInterfaceAddress(candidate.Name);
        Assert.False(IPAddress.IsLoopback(interfaceAddress));

        using var publisher = new UdpTrackerPacketPublisher(new TrackerPublisherOptions
        {
            InterfaceName = candidate.Name,
            MulticastAddress = "224.5.23.2",
            Port = 11010,
        });
    }
}
