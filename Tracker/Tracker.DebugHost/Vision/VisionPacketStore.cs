using System.Net;
using Google.Protobuf;

namespace Tracker.DebugHost.Vision;

public sealed class VisionPacketStore
{
    private readonly object gate = new();
    private readonly Dictionary<uint, CameraState> cameras = [];
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
            var cameraSnapshots = cameras
                .OrderBy(entry => entry.Key)
                .Select(entry => entry.Value.ToSnapshot())
                .ToArray();

            return new VisionPacketSnapshot(
                latestPacket?.Clone(),
                detection?.Clone(),
                cameraSnapshots,
                BuildAggregateDetection(cameraSnapshots),
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
        var endpointText = remoteEndpoint.ToString();

        lock (gate)
        {
            latestPacket = packet.Clone();

            if (packet.Detection is not null)
            {
                detection = packet.Detection.Clone();
                cameras[packet.Detection.CameraId] = new CameraState(
                    latestPacket.Clone(),
                    detection.Clone(),
                    endpointText,
                    receivedAt);
            }

            if (packet.Geometry is not null)
            {
                geometry = packet.Geometry.Clone();
            }

            packetCount++;
            this.remoteEndpoint = endpointText;
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

    private static VisionAggregateDetectionSnapshot BuildAggregateDetection(
        IReadOnlyList<VisionCameraSnapshot> cameraSnapshots)
    {
        if (cameraSnapshots.Count == 0)
        {
            return VisionAggregateDetectionSnapshot.Empty;
        }

        return new VisionAggregateDetectionSnapshot(
            cameraSnapshots.Select(camera => camera.CameraId).ToArray(),
            cameraSnapshots
                .SelectMany(camera => camera.Detection.Balls)
                .Select(ball => ball.Clone())
                .ToArray(),
            cameraSnapshots
                .SelectMany(camera => camera.Detection.RobotsYellow)
                .Select(robot => robot.Clone())
                .ToArray(),
            cameraSnapshots
                .SelectMany(camera => camera.Detection.RobotsBlue)
                .Select(robot => robot.Clone())
                .ToArray());
    }

    private sealed record CameraState(
        SSL_WrapperPacket LatestPacket,
        SSL_DetectionFrame Detection,
        string? RemoteEndpoint,
        DateTimeOffset? ReceivedAt)
    {
        public VisionCameraSnapshot ToSnapshot()
        {
            return new VisionCameraSnapshot(
                Detection.CameraId,
                LatestPacket.Clone(),
                Detection.Clone(),
                RemoteEndpoint,
                ReceivedAt);
        }
    }
}
