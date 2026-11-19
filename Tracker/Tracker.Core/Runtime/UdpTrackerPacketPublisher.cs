using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Google.Protobuf;

namespace Tracker.Core;

/// <summary>Tracker wrapper パケットを設定された IPv4 UDP 宛先へ送信する publisher です。</summary>
public sealed class UdpTrackerPacketPublisher : ITrackerPacketPublisher, IDisposable
{
    private readonly UdpClient udpClient = new(AddressFamily.InterNetwork);
    private readonly object gate = new();
    private bool publishUdp;
    private IPEndPoint endpoint = new(IPAddress.Any, 0);

    /// <summary>初期送信設定を適用して UDP publisher を作成します。</summary>
    /// <param name="options">送信先と UDP 送信の有効状態を示す設定です。</param>
    /// <exception cref="InvalidOperationException">マルチキャストアドレスまたは指定インターフェイスを解決できません。</exception>
    public UdpTrackerPacketPublisher(TrackerPublisherOptions options)
    {
        ApplyConfiguration(options);
    }

    /// <summary>送信先のアドレスとポートを検証し、publisher の送信設定を更新します。</summary>
    /// <param name="options">更新後の送信先と UDP 送信の有効状態です。</param>
    /// <exception cref="InvalidOperationException">アドレスまたは指定インターフェイスを解決できません。</exception>
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

    /// <summary>指定されたネットワークインターフェイスの IPv4 アドレスを送信元に選択します。</summary>
    /// <param name="interfaceName">送信元に使うインターフェイス名です。未指定の場合は OS の既定経路を使います。</param>
    /// <returns>選択した送信元 IPv4 アドレスです。</returns>
    /// <exception cref="InvalidOperationException">指定インターフェイスが見つからないか、利用可能な IPv4 アドレスがありません。</exception>
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

    /// <summary>送信が有効な場合に Tracker wrapper パケットを protobuf バイナリとして送信します。</summary>
    /// <param name="packet">送信する Tracker wrapper パケットです。</param>
    /// <exception cref="SocketException">UDP ソケットでの送信に失敗しました。</exception>
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

    /// <summary>内部 UDP ソケットを破棄します。</summary>
    public void Dispose()
    {
        udpClient.Dispose();
    }
}
