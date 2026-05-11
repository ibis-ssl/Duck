using System.Net;
using System.Net.Sockets;
using Google.Protobuf;

namespace Tracker.Server.Tracking;

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
    public UdpTrackerPacketPublisher(TrackerPublisherOptions options)
    {
        ApplyConfiguration(options);
    }

    /// <summary>
    /// multicast address と port を検証し、以後の publish に使う宛先と有効状態を更新する。
    /// </summary>
    public void ApplyConfiguration(TrackerPublisherOptions options)
    {
        if (!IPAddress.TryParse(options.MulticastAddress, out var address))
        {
            throw new InvalidOperationException($"Invalid tracker multicast address '{options.MulticastAddress}'.");
        }

        lock (gate)
        {
            publishUdp = options.PublishUdp;
            endpoint = new IPEndPoint(address, options.Port);
        }
    }

    /// <summary>
    /// publish が有効な場合だけ tracker wrapper packet を protobuf binary として送信する。
    /// </summary>
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
