using System.Net;
using Google.Protobuf;

namespace Tracker.Server.Vision;

public sealed class VisionPacketStore
{
    private readonly object gate = new();
    private SSL_WrapperPacket? latestPacket;
    private SSL_DetectionFrame? detection;
    private SSL_GeometryData? geometry;
    private long packetCount;
    private long errorCount;
    private string? remoteEndpoint;
    private DateTimeOffset? receivedAt;
    private string? lastError;

    public VisionPacketSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new VisionPacketSnapshot(
                latestPacket?.Clone(),
                detection?.Clone(),
                geometry?.Clone(),
                packetCount,
                errorCount,
                remoteEndpoint,
                receivedAt,
                lastError);
        }
    }

    public void StoreDatagram(byte[] payload, EndPoint remoteEndpoint, DateTimeOffset receivedAt)
    {
        try
        {
            StorePacket(SSL_WrapperPacket.Parser.ParseFrom(payload), remoteEndpoint, receivedAt);
        }
        catch (InvalidProtocolBufferException ex)
        {
            RecordDecodeError(ex);
        }
    }

    public void StorePacket(SSL_WrapperPacket packet, EndPoint remoteEndpoint, DateTimeOffset receivedAt)
    {
        lock (gate)
        {
            latestPacket = packet.Clone();

            if (packet.Detection is not null)
            {
                detection = packet.Detection.Clone();
            }

            if (packet.Geometry is not null)
            {
                geometry = packet.Geometry.Clone();
            }

            packetCount++;
            this.remoteEndpoint = remoteEndpoint.ToString();
            this.receivedAt = receivedAt;
            lastError = null;
        }
    }

    public void RecordDecodeError(Exception exception)
    {
        lock (gate)
        {
            errorCount++;
            lastError = exception.Message;
        }
    }
}
