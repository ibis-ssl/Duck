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
    UdpClient Client { get; }
    void SetExclusiveAddressUse(bool exclusive);
    void SetReuseAddress(bool reuse);
    void Bind(IPEndPoint endpoint);
    void JoinMulticastGroup(IPAddress group, IPAddress? interfaceAddress);
}

/// <summary>受信資源生成の先行テスト用境界。設定と生成の動作はまだ実装しない。</summary>
internal static class RefereeUdpReceiverFactory
{
    /// <summary>呼出し可能な最小scaffoldとして受信資源未生成を返す。</summary>
    internal static UdpRefereeDatagramReceiver Create(
        RefereeUdpReceiverOptions? options = null,
        Func<AddressFamily, IRefereeUdpSocketSetup>? createSocket = null) => null!;
}
