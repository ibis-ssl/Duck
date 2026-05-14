using System.Net;
using Tracker.DebugHost.Vision;

namespace Tracker.Tests;

public class VisionReceiverServiceTests
{
    /// <summary>
    /// 何を確認しているか: configured IPv4 interface address が multicast join address として単独使用されること。
    /// </summary>
    [Fact]
    public void ResolveMulticastJoinAddresses_WithConfiguredIpv4Address_ReturnsOnlyConfiguredAddress()
    {
        var addresses = VisionReceiverService.ResolveMulticastJoinAddresses(
            "192.168.10.4",
            [IPAddress.Loopback, IPAddress.Parse("10.0.0.5")]);

        var address = Assert.Single(addresses);
        Assert.Equal(IPAddress.Parse("192.168.10.4"), address);
    }

    /// <summary>
    /// 何を確認しているか: invalid configured interface address を multicast join 対象として拒否すること。
    /// </summary>
    [Fact]
    public void ResolveMulticastJoinAddresses_WithInvalidConfiguredAddress_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VisionReceiverService.ResolveMulticastJoinAddresses("::1", [IPAddress.Loopback]));

        Assert.Contains("Invalid VisionReceiver interface address", ex.Message);
    }

    /// <summary>
    /// 何を確認しているか: configured address がない場合に non-loopback を優先し重複を除外すること。
    /// </summary>
    [Fact]
    public void ResolveMulticastJoinAddresses_WithoutConfiguredAddress_PrefersNonLoopbackAndDeduplicates()
    {
        var addresses = VisionReceiverService.ResolveMulticastJoinAddresses(
            null,
            [
                IPAddress.Loopback,
                IPAddress.Parse("192.168.10.4"),
                IPAddress.Parse("10.0.0.8"),
                IPAddress.Parse("192.168.10.4"),
            ]);

        Assert.Equal(
            [IPAddress.Parse("192.168.10.4"), IPAddress.Parse("10.0.0.8"), IPAddress.Loopback],
            addresses);
    }
}
