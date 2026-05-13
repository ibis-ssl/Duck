using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TrackerConnectionLib;

public sealed class UdpTrackerReceiver<TPacket> : IDisposable
    where TPacket : ITrackerPacket
{
    private readonly UdpClient _udpClient;
    private readonly ITrackerDeserializer<TPacket> _deserializer;

    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private int handlerErrorCount;

    public event Action<TPacket, IPEndPoint>? PacketReceived;

    /// <summary>
    /// PacketReceived handler から出た例外を receiver loop から隔離した回数。
    /// </summary>
    public int HandlerErrorCount => handlerErrorCount;

    public UdpTrackerReceiver(
        int port,
        ITrackerDeserializer<TPacket> deserializer)
        : this(port, null, deserializer, null)
    {
    }

    public UdpTrackerReceiver(
        int port,
        string? multicastAddress,
        ITrackerDeserializer<TPacket> deserializer,
        string? interfaceAddress = null)
    {
        _deserializer = deserializer;

        _udpClient = new UdpClient(AddressFamily.InterNetwork);

        _udpClient.Client.SetSocketOption(
            SocketOptionLevel.Socket,
            SocketOptionName.ReuseAddress,
            true);

        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));

        if (!string.IsNullOrWhiteSpace(multicastAddress))
        {
            JoinMulticastGroup(_udpClient, multicastAddress, interfaceAddress);
        }
    }

    public void Start()
    {
        if (_receiveTask is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync();
        _udpClient.Close();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                // UDP close による例外は停止処理として無視
            }
        }

        _cts.Dispose();
        _cts = null;
        _receiveTask = null;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;

            try
            {
                result = await _udpClient.ReceiveAsync(cancellationToken);
            }
            catch
            {
                break;
            }

            if (_deserializer.TryDeserialize(result.Buffer, out var packet) &&
                packet is not null)
            {
                DispatchPacket(packet, result.RemoteEndPoint);
            }
        }
    }

    private void DispatchPacket(TPacket packet, IPEndPoint remoteEndPoint)
    {
        var handlers = PacketReceived;
        if (handlers is null)
        {
            return;
        }

        foreach (Action<TPacket, IPEndPoint> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(packet, remoteEndPoint);
            }
            catch
            {
                Interlocked.Increment(ref handlerErrorCount);
            }
        }
    }

    private static void JoinMulticastGroup(
        UdpClient udpClient,
        string multicastAddress,
        string? interfaceAddress)
    {
        if (!IPAddress.TryParse(multicastAddress, out var groupAddress))
        {
            throw new InvalidOperationException($"Invalid tracker multicast address '{multicastAddress}'.");
        }

        if (!IsMulticast(groupAddress))
        {
            return;
        }

        var candidateAddresses = ResolveMulticastJoinAddresses(interfaceAddress);
        if (candidateAddresses.Count == 0)
        {
            throw new InvalidOperationException(
                $"No IPv4 interface is available to join tracker multicast group '{groupAddress}'.");
        }

        var failedInterfaces = new List<string>();
        SocketException? firstSocketException = null;
        var joinedCount = 0;

        foreach (var candidateAddress in candidateAddresses)
        {
            try
            {
                udpClient.JoinMulticastGroup(groupAddress, candidateAddress);
                joinedCount++;
            }
            catch (SocketException ex)
            {
                firstSocketException ??= ex;
                failedInterfaces.Add($"{candidateAddress} ({ex.SocketErrorCode})");
            }
        }

        if (joinedCount > 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Failed to join tracker multicast group '{groupAddress}' on any local IPv4 interface. " +
            $"Tried: {string.Join(", ", failedInterfaces)}",
            firstSocketException);
    }

    private static bool IsMulticast(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] >= 224 && bytes[0] <= 239;
    }

    private static IReadOnlyList<IPAddress> ResolveMulticastJoinAddresses(string? interfaceAddress)
    {
        if (!string.IsNullOrWhiteSpace(interfaceAddress))
        {
            if (!IPAddress.TryParse(interfaceAddress, out var parsedAddress) ||
                parsedAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new InvalidOperationException($"Invalid tracker receiver interface address '{interfaceAddress}'.");
            }

            return [parsedAddress];
        }

        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                (networkInterface.SupportsMulticast ||
                 networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback))
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(unicastAddress => unicastAddress.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(address))
            .Distinct()
            .OrderBy(address => IPAddress.IsLoopback(address) ? 1 : 0)
            .ToArray();
    }

    public void Dispose()
    {
        _ = StopAsync();
        _udpClient.Dispose();
    }
}
