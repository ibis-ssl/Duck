using System.Net;
using System.Net.Sockets;
using Google.Protobuf;

namespace Tracker.Server.Tracking;

public sealed class UdpTrackerPacketPublisher : ITrackerPacketPublisher, IDisposable
{
    private readonly bool publishUdp;
    private readonly UdpClient udpClient = new(AddressFamily.InterNetwork);
    private readonly IPEndPoint endpoint;

    public UdpTrackerPacketPublisher(TrackerPublisherOptions options)
    {
        publishUdp = options.PublishUdp;
        if (!IPAddress.TryParse(options.MulticastAddress, out var address))
        {
            throw new InvalidOperationException($"Invalid tracker multicast address '{options.MulticastAddress}'.");
        }

        endpoint = new IPEndPoint(address, options.Port);
    }

    public void Publish(TrackerWrapperPacket packet)
    {
        if (!publishUdp)
        {
            return;
        }

        var payload = packet.ToByteArray();
        udpClient.Send(payload, payload.Length, endpoint);
    }

    public void Dispose()
    {
        udpClient.Dispose();
    }
}
