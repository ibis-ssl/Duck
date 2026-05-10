using System.Net;
using System.Net.Sockets;
using Google.Protobuf;

namespace Tracker.Server.Tracking;

public sealed class UdpTrackerPacketPublisher : ITrackerPacketPublisher, IDisposable
{
    private readonly UdpClient udpClient = new(AddressFamily.InterNetwork);
    private readonly object gate = new();
    private bool publishUdp;
    private IPEndPoint endpoint = new(IPAddress.Any, 0);

    public UdpTrackerPacketPublisher(TrackerPublisherOptions options)
    {
        ApplyConfiguration(options);
    }

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

    public void Dispose()
    {
        udpClient.Dispose();
    }
}
