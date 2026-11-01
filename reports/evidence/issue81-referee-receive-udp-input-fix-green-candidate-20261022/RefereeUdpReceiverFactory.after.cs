using System.Net;
using System.Net.Sockets;

namespace Duck.Testing.RefereeDriver;

/// <summary>コンテナ内の審判情報受信に使う既定宛先と任意のIPv4参加IF。</summary>
internal sealed record RefereeUdpReceiverOptions(
    string MulticastAddress = UdpRefereeCommandSource.DefaultMulticastAddress,
    int Port = UdpRefereeCommandSource.DefaultPort,
    IPAddress? InterfaceAddress = null);

/// <summary>固定portを実bindせず、socket生成設定の順序を観測できる境界。</summary>
internal interface IRefereeUdpSocketSetup
{
    /// <summary>生成側へ所有を渡すsocket。</summary>
    UdpClient Client { get; }
    void SetExclusiveAddressUse(bool exclusive);
    void SetReuseAddress(bool reuse);
    void Bind(IPEndPoint endpoint);
    void JoinMulticastGroup(IPAddress group, IPAddress? interfaceAddress);
}

/// <summary>IPv4 multicastの受信設定を適用し、socketを所有するadapterを生成する。</summary>
internal static class RefereeUdpReceiverFactory
{
    /// <summary>共有portと参加IFを設定し、途中失敗時は取得した所有socketを破棄する。</summary>
    internal static UdpRefereeDatagramReceiver Create(
        RefereeUdpReceiverOptions? options = null,
        Func<AddressFamily, IRefereeUdpSocketSetup>? createSocket = null)
    {
        options ??= new();
        var group = IPAddress.Parse(options.MulticastAddress);
        if (group.AddressFamily != AddressFamily.InterNetwork ||
            group.GetAddressBytes()[0] is < 224 or > 239)
            throw new ArgumentException("審判情報の宛先はIPv4 multicastで指定する。", nameof(options));
        if (options.InterfaceAddress is not null &&
            options.InterfaceAddress.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("審判情報の参加IFはIPv4で指定する。", nameof(options));
        createSocket ??= family => new UdpSocketSetup(family);
        var setup = createSocket(AddressFamily.InterNetwork);
        var client = setup.Client;
        try
        {
            setup.SetExclusiveAddressUse(false);
            setup.SetReuseAddress(true);
            setup.Bind(new IPEndPoint(IPAddress.Any, options.Port));
            setup.JoinMulticastGroup(group, options.InterfaceAddress);
            return new UdpRefereeDatagramReceiver(client, ownsClient: true);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>OSのUDP socketへ設定を適用する。受信は単一pump側に任せる。</summary>
    internal sealed class UdpSocketSetup(AddressFamily family) : IRefereeUdpSocketSetup
    {
        public UdpClient Client { get; } = new(family);
        public void SetExclusiveAddressUse(bool exclusive) => Client.ExclusiveAddressUse = exclusive;
        public void SetReuseAddress(bool reuse) => Client.Client.SetSocketOption(
            SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, reuse);
        public void Bind(IPEndPoint endpoint) => Client.Client.Bind(endpoint);

        /// <summary>未指定なら既定IF、指定時はそのIPv4 IFでgroupへ参加する。</summary>
        public void JoinMulticastGroup(IPAddress group, IPAddress? interfaceAddress)
        {
            if (interfaceAddress is null)
                Client.JoinMulticastGroup(group);
            else
                Client.JoinMulticastGroup(group, interfaceAddress);
        }
    }
}
