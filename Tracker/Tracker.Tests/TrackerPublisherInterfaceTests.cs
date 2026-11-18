using System.Net;
using System.Net.NetworkInformation;
using Tracker.Core;

namespace Tracker.Tests;

/// <summary>TrackerPublisherInterfaceTests に関する試験をまとめます。</summary>
public sealed class TrackerPublisherInterfaceTests
{
    /// <summary>試験で interface が未指定または空白の場合、OS の routing table に委ねる wildcard address を使うことを確認します。</summary>
    [Fact]
    public void UnspecifiedInterfaceUsesSystemRoutingTable()
    {
        Assert.Equal(IPAddress.Any, UdpTrackerPacketPublisher.ResolveMulticastInterfaceAddress(null));
        Assert.Equal(IPAddress.Any, UdpTrackerPacketPublisher.ResolveMulticastInterfaceAddress(" "));
    }

    /// <summary>試験で 存在しない interface 名を publish 開始前に拒否することを確認します。</summary>
    [Fact]
    public void UnknownInterfaceFailsBeforePublishing()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => UdpTrackerPacketPublisher.ResolveMulticastInterfaceAddress("duck-missing-interface"));

        Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>試験で 稼働中の IPv4 interface を指定した publisher を作成できることを確認します。</summary>
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
