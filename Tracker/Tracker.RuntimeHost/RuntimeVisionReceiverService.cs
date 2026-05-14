using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost の headless SSL-Vision UDP receiver。
/// </summary>
internal sealed class RuntimeVisionReceiverService : BackgroundService
{
    private readonly IOptions<RuntimeVisionReceiverOptions> options;
    private readonly RuntimeVisionPacketBuffer packetBuffer;
    private readonly ILogger<RuntimeVisionReceiverService> logger;

    /// <summary>
    /// RuntimeHost receiver options、latest packet buffer、logger を受け取って receiver を作成する。
    /// </summary>
    public RuntimeVisionReceiverService(
        IOptions<RuntimeVisionReceiverOptions> options,
        RuntimeVisionPacketBuffer packetBuffer,
        ILogger<RuntimeVisionReceiverService> logger)
    {
        this.options = options;
        this.packetBuffer = packetBuffer;
        this.logger = logger;
    }

    /// <summary>
    /// SSL-Vision UDP packet を受信し、latest packet buffer へ保存する。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = options.Value;
        var endpointDescription = $"{receiverOptions.MulticastAddress}:{receiverOptions.Port}";
        UdpClient udpClient;
        MulticastJoinResult joinResult;

        try
        {
            (udpClient, joinResult) = CreateUdpClient(receiverOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize RuntimeHost SSL-Vision receiver for {Endpoint}", endpointDescription);
            throw;
        }

        using (udpClient)
        {
            LogReceiverStarted(endpointDescription, joinResult);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await udpClient.ReceiveAsync(stoppingToken);
                    var receivedAt = DateTimeOffset.UtcNow;
                    var packet = SSL_WrapperPacket.Parser.ParseFrom(result.Buffer);
                    packetBuffer.StorePacket(packet, receivedAt);
                }
                catch (InvalidProtocolBufferException ex)
                {
                    logger.LogWarning(ex, "Failed to decode RuntimeHost SSL-Vision packet.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to receive RuntimeHost SSL-Vision packet.");
                }
            }
        }
    }

    private void LogReceiverStarted(string endpointDescription, MulticastJoinResult joinResult)
    {
        if (joinResult.FailedInterfaces.Count > 0)
        {
            logger.LogWarning(
                "RuntimeHost joined SSL-Vision multicast group on {JoinedInterfaces}; failed on {FailedInterfaces}",
                string.Join(", ", joinResult.JoinedInterfaces),
                string.Join(", ", joinResult.FailedInterfaces));
        }

        if (joinResult.JoinedInterfaces.Count > 0)
        {
            logger.LogInformation(
                "RuntimeHost receiving SSL-Vision packets from {Endpoint} via {JoinedInterfaces}",
                endpointDescription,
                string.Join(", ", joinResult.JoinedInterfaces));
        }
        else
        {
            logger.LogInformation("RuntimeHost receiving SSL-Vision packets from {Endpoint}", endpointDescription);
        }
    }

    private static (UdpClient Client, MulticastJoinResult JoinResult) CreateUdpClient(RuntimeVisionReceiverOptions options)
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

    /// <summary>
    /// 明示 interface address があればそれだけを、なければ multicast join 候補 IPv4 address を返す。
    /// </summary>
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
