using System.Net;
using Google.Protobuf;
using Tracker.Server.Vision;

namespace Tracker.Tests;

public class VisionPacketStoreTests
{
    [Fact]
    public void StoreDatagram_WithDetectionOnlyPacket_UpdatesDetectionSnapshot()
    {
        var store = new VisionPacketStore();
        var packet = new SSL_WrapperPacket
        {
            Detection = new SSL_DetectionFrame
            {
                FrameNumber = 42,
                CameraId = 3,
                Balls =
                {
                    new SSL_DetectionBall
                    {
                        Confidence = 0.9f,
                        X = 120,
                        Y = -45,
                    },
                },
            },
        };

        store.StoreDatagram(packet.ToByteArray(), new IPEndPoint(IPAddress.Loopback, 10006), DateTimeOffset.UtcNow);

        var snapshot = store.GetSnapshot();

        Assert.Equal(1, snapshot.PacketCount);
        Assert.Equal(0, snapshot.ErrorCount);
        Assert.NotNull(snapshot.LatestPacket);
        Assert.NotNull(snapshot.Detection);
        Assert.Null(snapshot.Geometry);
        Assert.Equal((uint)42, snapshot.Detection.FrameNumber);
        Assert.Single(snapshot.Detection.Balls);
    }

    [Fact]
    public void StoreDatagram_WithGeometryOnlyPacket_UpdatesGeometrySnapshot()
    {
        var store = new VisionPacketStore();
        var packet = new SSL_WrapperPacket
        {
            Geometry = new SSL_GeometryData
            {
                Field = new SSL_GeometryFieldSize
                {
                    FieldLength = 9000,
                    FieldWidth = 6000,
                    FieldLines =
                    {
                        new SSL_FieldLineSegment
                        {
                            Name = "HalfwayLine",
                            P1 = new Vector2f { X = 0, Y = -3000 },
                            P2 = new Vector2f { X = 0, Y = 3000 },
                        },
                    },
                },
            },
        };

        store.StoreDatagram(packet.ToByteArray(), new IPEndPoint(IPAddress.Loopback, 10006), DateTimeOffset.UtcNow);

        var snapshot = store.GetSnapshot();

        Assert.Equal(1, snapshot.PacketCount);
        Assert.Equal(0, snapshot.ErrorCount);
        Assert.NotNull(snapshot.LatestPacket);
        Assert.Null(snapshot.Detection);
        Assert.NotNull(snapshot.Geometry);
        Assert.Equal(9000, snapshot.Geometry.Field.FieldLength);
        Assert.Single(snapshot.Geometry.Field.FieldLines);
    }

    [Fact]
    public void StoreDatagram_WithInvalidPayload_IncrementsErrorCount()
    {
        var store = new VisionPacketStore();

        store.StoreDatagram([0xFF, 0xFF, 0xFF], new IPEndPoint(IPAddress.Loopback, 10006), DateTimeOffset.UtcNow);

        var snapshot = store.GetSnapshot();

        Assert.Equal(0, snapshot.PacketCount);
        Assert.Equal(1, snapshot.ErrorCount);
        Assert.NotNull(snapshot.LastError);
        Assert.Null(snapshot.LatestPacket);
    }
}
