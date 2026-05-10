using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using Tracker.Server.Tracking;

namespace Tracker.Server.Vision;

public sealed class VisionReceiverService(
    VisionReceiverRuntimeOptionsStore receiverOptionsStore,
    IOptions<TrackerOptions> trackerOptions,
    VisionPacketStore store,
    TrackerCoordinator trackerCoordinator,
    ILogger<VisionReceiverService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var snapshot = receiverOptionsStore.GetSnapshot();
            var receiverOptions = snapshot.Options;
            var endpointDescription = $"{receiverOptions.MulticastAddress}:{receiverOptions.Port}";
            UdpClient udpClient;
            MulticastJoinResult joinResult;

            try
            {
                (udpClient, joinResult) = CreateUdpClient(receiverOptions);
            }
            catch (Exception ex)
            {
                store.RecordDecodeError(ex);
                logger.LogError(ex, "Failed to initialize SSL-Vision receiver for {Endpoint}", endpointDescription);
                await WaitForConfigurationChangeAsync(snapshot.ChangeToken, stoppingToken);
                continue;
            }

            using (udpClient)
            using (var configurationScope = CancellationTokenSource.CreateLinkedTokenSource(
                       stoppingToken,
                       snapshot.ChangeToken))
            {
                if (joinResult.FailedInterfaces.Count > 0)
                {
                    logger.LogWarning(
                        "Joined SSL-Vision multicast group on {JoinedInterfaces}; failed on {FailedInterfaces}",
                        string.Join(", ", joinResult.JoinedInterfaces),
                        string.Join(", ", joinResult.FailedInterfaces));
                }

                if (joinResult.JoinedInterfaces.Count > 0)
                {
                    logger.LogInformation(
                        "Receiving SSL-Vision packets from {Endpoint} via {JoinedInterfaces}",
                        endpointDescription,
                        string.Join(", ", joinResult.JoinedInterfaces));
                }
                else
                {
                    logger.LogInformation("Receiving SSL-Vision packets from {Endpoint}", endpointDescription);
                }

                while (!configurationScope.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync(configurationScope.Token);
                        var receivedAt = DateTimeOffset.UtcNow;
                        SSL_WrapperPacket packet;
                        try
                        {
                            packet = SSL_WrapperPacket.Parser.ParseFrom(result.Buffer);
                        }
                        catch (InvalidProtocolBufferException ex)
                        {
                            store.RecordDecodeError(ex);
                            logger.LogWarning(ex, "Failed to receive or decode SSL-Vision packet");
                            continue;
                        }

                        store.StorePacket(packet, result.RemoteEndPoint, receivedAt);
                        if (trackerOptions.Value.Enabled)
                        {
                            trackerCoordinator.ProcessPacket(packet, receivedAt);
                        }
                    }
                    catch (OperationCanceledException) when (snapshot.ChangeToken.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                    {
                        logger.LogInformation(
                            "Reconfiguring SSL-Vision receiver to follow profile-specific settings");
                        break;
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
    }

    private static async Task WaitForConfigurationChangeAsync(
        CancellationToken changeToken,
        CancellationToken stoppingToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(changeToken, stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, linked.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static (UdpClient Client, MulticastJoinResult JoinResult) CreateUdpClient(VisionReceiverResolvedOptions options)
    {
        if (!IPAddress.TryParse(options.MulticastAddress, out var groupAddress))
        {
            throw new InvalidOperationException($"Invalid VisionReceiver multicast address '{options.MulticastAddress}'.");
        }

        var udpClient = new UdpClient(AddressFamily.InterNetwork);
        udpClient.ExclusiveAddressUse = false;
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, options.Port));
        var joinResult = MulticastJoinResult.None;

        if (IsMulticast(groupAddress))
        {
            joinResult = JoinMulticastGroup(udpClient, groupAddress, options.InterfaceAddress);
        }

        return (udpClient, joinResult);
    }

    private static bool IsMulticast(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] >= 224 && bytes[0] <= 239;
    }

    private static MulticastJoinResult JoinMulticastGroup(
        UdpClient udpClient,
        IPAddress groupAddress,
        string? configuredInterfaceAddress)
    {
        var candidateAddresses = ResolveMulticastJoinAddresses(
            configuredInterfaceAddress,
            DiscoverMulticastJoinAddresses());

        if (candidateAddresses.Count == 0)
        {
            throw new InvalidOperationException(
                $"No IPv4 interface is available to join SSL-Vision multicast group '{groupAddress}'. " +
                "Set VisionReceiver:InterfaceAddress to a specific local IPv4 address.");
        }

        var joinedInterfaces = new List<IPAddress>(candidateAddresses.Count);
        var failedInterfaces = new List<string>();
        SocketException? firstSocketException = null;

        foreach (var candidateAddress in candidateAddresses)
        {
            try
            {
                udpClient.JoinMulticastGroup(groupAddress, candidateAddress);
                joinedInterfaces.Add(candidateAddress);
            }
            catch (SocketException ex)
            {
                firstSocketException ??= ex;
                failedInterfaces.Add($"{candidateAddress} ({ex.SocketErrorCode})");
            }
        }

        if (joinedInterfaces.Count == 0)
        {
            throw new InvalidOperationException(
                $"Failed to join SSL-Vision multicast group '{groupAddress}' on any local IPv4 interface. " +
                $"Tried: {string.Join(", ", failedInterfaces)}",
                firstSocketException);
        }

        return new MulticastJoinResult(joinedInterfaces, failedInterfaces);
    }

    internal static IReadOnlyList<IPAddress> ResolveMulticastJoinAddresses(
        string? configuredInterfaceAddress,
        IEnumerable<IPAddress> discoveredAddresses)
    {
        if (!string.IsNullOrWhiteSpace(configuredInterfaceAddress))
        {
            if (!IPAddress.TryParse(configuredInterfaceAddress, out var interfaceAddress) ||
                interfaceAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new InvalidOperationException(
                    $"Invalid VisionReceiver interface address '{configuredInterfaceAddress}'.");
            }

            return [interfaceAddress];
        }

        return discoveredAddresses
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(address))
            .Distinct()
            .OrderBy(address => IPAddress.IsLoopback(address) ? 1 : 0)
            .ToArray();
    }

    private static IReadOnlyList<IPAddress> DiscoverMulticastJoinAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                (networkInterface.SupportsMulticast ||
                 networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback))
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(unicastAddress => unicastAddress.Address)
            .ToArray();
    }

    private sealed record MulticastJoinResult(
        IReadOnlyList<IPAddress> JoinedInterfaces,
        IReadOnlyList<string> FailedInterfaces)
    {
        public static MulticastJoinResult None { get; } =
            new(Array.Empty<IPAddress>(), Array.Empty<string>());
    }
}
