using System.Net;
using System.Net.Sockets;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class RefereeSenderTupleCaptureTests
{
    [Fact]
    public async Task RealUdpReceiveRetainsSourcePortAndDistinguishesSendersOnTheSameAddress()
    {
        // Ephemeral loopback sockets exercise the production receive adapter without touching
        // an existing stack or live 11003. RefereeReceiveUdpSettingsAndDisposalTests separately
        // pins the production multicast subscription to 224.5.23.1:11003.
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var firstSender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var secondSender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var receiver = new UdpRefereeDatagramReceiver(listener);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var destination = Assert.IsType<IPEndPoint>(listener.Client.LocalEndPoint);
        var firstEndpoint = Assert.IsType<IPEndPoint>(firstSender.Client.LocalEndPoint);
        var secondEndpoint = Assert.IsType<IPEndPoint>(secondSender.Client.LocalEndPoint);
        Assert.Equal(firstEndpoint.Address, secondEndpoint.Address);
        Assert.NotEqual(firstEndpoint.Port, secondEndpoint.Port);
        var diagnostics = new ServiceReceiveDiagnostics();
        var epoch = diagnostics.BeginEpoch(RefereeReceiveService.InputId);

        async Task<RefereeReceivedDatagram> SendAndReceiveAsync(UdpClient sender, byte value)
        {
            byte[] payload = [value];
            await sender.SendAsync(payload.AsMemory(), destination, deadline.Token);
            var received = await receiver.ReceiveAsync(() => diagnostics.BeginReceive(epoch), deadline.Token);
            Assert.Equal(payload, received.Payload.ToArray());
            return received;
        }

        var first = await SendAndReceiveAsync(firstSender, 1);
        var repeated = await SendAndReceiveAsync(firstSender, 2);
        var second = await SendAndReceiveAsync(secondSender, 3);

        Assert.Equal(firstEndpoint, first.RemoteEndpoint);
        Assert.Equal(first.RemoteEndpoint, repeated.RemoteEndpoint);
        Assert.Equal(secondEndpoint, second.RemoteEndpoint);
        Assert.NotEqual(first.RemoteEndpoint, second.RemoteEndpoint);
        Assert.Equal(2, new[] { first, repeated, second }
            .Select(packet => packet.RemoteEndpoint.ToString()).Distinct().Count());
    }
}
