using System.Net;
using Google.Protobuf;
using Tracker.Server.Vision;

namespace Tracker.Tests;

public class VisionPacketStoreTests
{
    /// <summary>
    /// 何を確認しているか: detection のみの packet が detection snapshot を更新すること。
    /// </summary>
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

    /// <summary>
    /// 何を確認しているか: geometry のみの packet が geometry snapshot を更新すること。
    /// </summary>
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

    /// <summary>
    /// 何を確認しているか: invalid payload を packet として保存せず error count を増やすこと。
    /// </summary>
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

    /// <summary>
    /// 何を確認しているか: 複数 camera frame の latest state と aggregate view を保持すること。
    /// </summary>
    [Fact]
    public void StorePacket_WithMultipleCameraFrames_PreservesPerCameraLatestStateAndAggregateView()
    {
        var store = new VisionPacketStore();

        store.StorePacket(
            new SSL_WrapperPacket
            {
                Detection = new SSL_DetectionFrame
                {
                    FrameNumber = 10,
                    CameraId = 1,
                    Balls =
                    {
                        new SSL_DetectionBall
                        {
                            Confidence = 0.9f,
                            X = 100,
                            Y = 200,
                            PixelX = 300,
                            PixelY = 400,
                        },
                    },
                    RobotsYellow =
                    {
                        new SSL_DetectionRobot
                        {
                            Confidence = 0.8f,
                            RobotId = 2,
                            X = 500,
                            Y = 600,
                            PixelX = 700,
                            PixelY = 800,
                        },
                    },
                },
            },
            new IPEndPoint(IPAddress.Loopback, 10006),
            DateTimeOffset.UtcNow);

        store.StorePacket(
            new SSL_WrapperPacket
            {
                Detection = new SSL_DetectionFrame
                {
                    FrameNumber = 11,
                    CameraId = 2,
                    RobotsBlue =
                    {
                        new SSL_DetectionRobot
                        {
                            Confidence = 0.7f,
                            RobotId = 5,
                            X = -500,
                            Y = -600,
                            PixelX = 100,
                            PixelY = 200,
                        },
                    },
                },
            },
            new IPEndPoint(IPAddress.Loopback, 10007),
            DateTimeOffset.UtcNow);

        store.StorePacket(
            new SSL_WrapperPacket
            {
                Detection = new SSL_DetectionFrame
                {
                    FrameNumber = 12,
                    CameraId = 1,
                    Balls =
                    {
                        new SSL_DetectionBall
                        {
                            Confidence = 0.95f,
                            X = 150,
                            Y = 250,
                            PixelX = 350,
                            PixelY = 450,
                        },
                    },
                    RobotsYellow =
                    {
                        new SSL_DetectionRobot
                        {
                            Confidence = 0.85f,
                            RobotId = 3,
                            X = 550,
                            Y = 650,
                            PixelX = 750,
                            PixelY = 850,
                        },
                    },
                },
            },
            new IPEndPoint(IPAddress.Loopback, 10006),
            DateTimeOffset.UtcNow);

        var snapshot = store.GetSnapshot();

        Assert.Equal(2, snapshot.Cameras.Count);

        var camera1 = Assert.Single(snapshot.Cameras, camera => camera.CameraId == 1);
        var camera2 = Assert.Single(snapshot.Cameras, camera => camera.CameraId == 2);

        Assert.Equal((uint)12, camera1.Detection.FrameNumber);
        Assert.Single(camera1.Detection.RobotsYellow);
        Assert.Equal((uint)3, camera1.Detection.RobotsYellow[0].RobotId);

        Assert.Equal((uint)11, camera2.Detection.FrameNumber);
        Assert.Single(camera2.Detection.RobotsBlue);
        Assert.Equal((uint)5, camera2.Detection.RobotsBlue[0].RobotId);

        Assert.Equal([1u, 2u], snapshot.AggregateDetection.CameraIds);
        Assert.Single(snapshot.AggregateDetection.Balls);
        Assert.Single(snapshot.AggregateDetection.RobotsYellow);
        Assert.Single(snapshot.AggregateDetection.RobotsBlue);
        Assert.Equal((uint)3, snapshot.AggregateDetection.RobotsYellow[0].RobotId);
        Assert.Equal((uint)5, snapshot.AggregateDetection.RobotsBlue[0].RobotId);
    }
}
