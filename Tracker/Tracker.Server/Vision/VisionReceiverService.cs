using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Tracker.Server.Vision;

public sealed class VisionReceiverService(
    IOptions<VisionReceiverOptions> options,
    VisionPacketStore store,
    ILogger<VisionReceiverService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = options.Value;
        var endpointDescription = $"{receiverOptions.MulticastAddress}:{receiverOptions.Port}";
        UdpClient udpClient;

        try
        {
            udpClient = CreateUdpClient(receiverOptions);
        }
        catch (Exception ex)
        {
            store.RecordDecodeError(ex);
            logger.LogError(ex, "Failed to initialize SSL-Vision receiver for {Endpoint}", endpointDescription);
            return;
        }

        using (udpClient)
        {
            logger.LogInformation("Receiving SSL-Vision packets from {Endpoint}", endpointDescription);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await udpClient.ReceiveAsync(stoppingToken);
                    store.StoreDatagram(result.Buffer, result.RemoteEndPoint, DateTimeOffset.UtcNow);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    store.RecordDecodeError(ex);
                    logger.LogWarning(ex, "Failed to receive or decode SSL-Vision packet");
                }
            }
        }
    }

    private static UdpClient CreateUdpClient(VisionReceiverOptions options)
    {
        if (!IPAddress.TryParse(options.MulticastAddress, out var groupAddress))
        {
            throw new InvalidOperationException($"Invalid VisionReceiver multicast address '{options.MulticastAddress}'.");
        }

        var udpClient = new UdpClient(AddressFamily.InterNetwork);
        udpClient.ExclusiveAddressUse = false;
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, options.Port));

        if (IsMulticast(groupAddress))
        {
            if (!string.IsNullOrWhiteSpace(options.InterfaceAddress))
            {
                if (!IPAddress.TryParse(options.InterfaceAddress, out var interfaceAddress))
                {
                    throw new InvalidOperationException($"Invalid VisionReceiver interface address '{options.InterfaceAddress}'.");
                }

                udpClient.JoinMulticastGroup(groupAddress, interfaceAddress);
            }
            else
            {
                udpClient.JoinMulticastGroup(groupAddress);
            }
        }

        return udpClient;
    }

    private static bool IsMulticast(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] >= 224 && bytes[0] <= 239;
    }
}
