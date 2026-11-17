using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

public sealed class RuntimeVisionReceiverForwardingTests
{
    [Fact]
    public async Task SuccessfullyDecodedDatagramIsForwardedUnchanged()
    {
        var inputPort = GetAvailableUdpPort();
        using var destination = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var destinationPort = ((IPEndPoint)destination.Client.LocalEndPoint!).Port;
        var options = Options.Create(new RuntimeVisionReceiverOptions
        {
            MulticastAddress = "127.0.0.1",
            ListenAddress = "127.0.0.1",
            Port = inputPort,
            ForwardAddress = "127.0.0.1",
            ForwardPort = destinationPort,
        });
        var service = new RuntimeVisionReceiverService(
            options,
            new RuntimeVisionPacketBuffer(),
            new RuntimeVisionReceiverDiagnostics(),
            NullLogger<RuntimeVisionReceiverService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var payload = new SSL_WrapperPacket
            {
                Detection = new SSL_DetectionFrame { FrameNumber = 17 },
            }.ToByteArray();
            using var sender = new UdpClient(AddressFamily.InterNetwork);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await sender.SendAsync(payload.AsMemory(), new IPEndPoint(IPAddress.Loopback, inputPort), timeout.Token);
            var forwarded = await destination.ReceiveAsync(timeout.Token);

            Assert.Equal(payload, forwarded.Buffer);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }
    }

    private static int GetAvailableUdpPort()
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)client.Client.LocalEndPoint!).Port;
    }
}
