using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Google.Protobuf;

namespace Tracker.Core;

/// <summary>
/// tracker wrapper packet を UDP multicast/unicast 宛先へ送信する publisher。
/// </summary>
public sealed class UdpTrackerPacketPublisher : ITrackerPacketPublisher, IDisposable
{
    private readonly UdpClient udpClient = new(AddressFamily.InterNetwork);
    private readonly object gate = new();
    private bool publishUdp;
    private IPEndPoint endpoint = new(IPAddress.Any, 0);

    /// <summary>
    /// 初期 publish 設定を適用して UDP publisher を作成する。
    /// </summary>
    /// <param name="options">送信先と publish の有効状態。</param>
    /// <exception cref="InvalidOperationException">マルチキャストアドレスまたは指定インターフェイスを解決できない場合。</exception>
    public UdpTrackerPacketPublisher(TrackerPublisherOptions options)
    {
        ApplyConfiguration(options);
    }

    /// <summary>
    /// multicast address と port を検証し、以後の publish に使う宛先と有効状態を更新する。
    /// </summary>
    /// <param name="options">適用する送信設定。</param>
    /// <exception cref="InvalidOperationException">アドレスまたは指定インターフェイスを解決できない場合。</exception>
    public void ApplyConfiguration(TrackerPublisherOptions options)
    {
        if (!IPAddress.TryParse(options.MulticastAddress, out var address))
        {
            throw new InvalidOperationException($"Invalid tracker multicast address '{options.MulticastAddress}'.");
        }

        lock (gate)
        {
            var interfaceAddress = ResolveMulticastInterfaceAddress(options.InterfaceName);
            udpClient.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastInterface,
                interfaceAddress.GetAddressBytes());
            publishUdp = options.PublishUdp;
            endpoint = new IPEndPoint(address, options.Port);
        }
    }

    internal static IPAddress ResolveMulticastInterfaceAddress(string? interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            return IPAddress.Any;
        }

        var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .SingleOrDefault(candidate => string.Equals(candidate.Name, interfaceName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Tracker multicast interface '{interfaceName}' was not found.");
        var address = networkInterface.GetIPProperties().UnicastAddresses
            .Select(candidate => candidate.Address)
            .FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(candidate));
        return address ?? throw new InvalidOperationException(
            $"Tracker multicast interface '{interfaceName}' has no non-loopback IPv4 address.");
    }

    /// <summary>
    /// publish が有効な場合だけ tracker wrapper packet を protobuf binary として送信する。
    /// </summary>
    /// <param name="packet">送信する Tracker wrapper packet。</param>
    /// <exception cref="SocketException">UDP socket による送信に失敗した場合。</exception>
    public void Publish(TrackerWrapperPacket packet)
    {
        lock (gate)
        {
            if (!publishUdp)
            {
                return;
            }

            var payload = packet.ToByteArray();
            udpClient.Send(payload, payload.Length, endpoint);
        }
    }

    /// <summary>
    /// 内部 UDP socket を破棄する。
    /// </summary>
    public void Dispose()
    {
        udpClient.Dispose();
    }
}
