using System.Net;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using TrackerConnectionLib;

namespace Tracker.Tests;

public sealed class DebugHostHealthEndpointTests
{
    [Fact]
    public async Task ComparisonHealthReportsVisionPacketsInsteadOfTrackerSnapshots()
    {
        var trackerManager = new MultiTrackerManager<TrackerPacketAdapter>();
        using var trackerSnapshots = new ExternalTrackerSnapshotStore(trackerManager);
        var visionPackets = new VisionPacketStore();
        var receivedAt = DateTimeOffset.UtcNow;
        var detection = new SSL_DetectionFrame { FrameNumber = 77 };
        detection.RobotsYellow.Add(new SSL_DetectionRobot { RobotId = 4, X = 1250, Y = -300 });
        detection.RobotsBlue.Add(new SSL_DetectionRobot { RobotId = 2, X = -900, Y = 1600 });
        detection.Balls.Add(new SSL_DetectionBall { X = -1200, Y = 2990 });
        visionPackets.StorePacket(
            new SSL_WrapperPacket { Detection = detection },
            new IPEndPoint(IPAddress.Loopback, 10020),
            receivedAt);
        visionPackets.StorePacket(
            new SSL_WrapperPacket { Geometry = new SSL_GeometryData() },
            new IPEndPoint(IPAddress.Loopback, 10020),
            receivedAt.AddMilliseconds(1));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Testing:Mode"] = "comparison" })
            .Build();

        var result = DebugHostHealthEndpoints.GetComparisonHealthResult(
            trackerSnapshots,
            visionPackets,
            configuration);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var response = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        Assert.NotNull(response);
        Assert.Equal(2L, response.GetType().GetProperty("VisionPacketsReceived")?.GetValue(response));
        var latestVision = response.GetType().GetProperty("LatestVision")?.GetValue(response);
        Assert.NotNull(latestVision);
        Assert.Equal(2L, latestVision.GetType().GetProperty("PacketCount")?.GetValue(latestVision));
        Assert.Equal(receivedAt, latestVision.GetType().GetProperty("DetectionReceivedAt")?.GetValue(latestVision));
        Assert.Equal(77u, latestVision.GetType().GetProperty("FrameNumber")?.GetValue(latestVision));
        var yellowRobots = Assert.IsAssignableFrom<IEnumerable<object>>(
            latestVision.GetType().GetProperty("YellowRobots")?.GetValue(latestVision));
        var yellowRobot = Assert.Single(yellowRobots);
        Assert.Equal(4u, yellowRobot.GetType().GetProperty("RobotId")?.GetValue(yellowRobot));
        Assert.Equal(1250f, yellowRobot.GetType().GetProperty("X")?.GetValue(yellowRobot));
        Assert.Equal(-300f, yellowRobot.GetType().GetProperty("Y")?.GetValue(yellowRobot));
        var blueRobots = Assert.IsAssignableFrom<IEnumerable<object>>(
            latestVision.GetType().GetProperty("BlueRobots")?.GetValue(latestVision));
        var blueRobot = Assert.Single(blueRobots);
        Assert.Equal(2u, blueRobot.GetType().GetProperty("RobotId")?.GetValue(blueRobot));
        Assert.Equal(-900f, blueRobot.GetType().GetProperty("X")?.GetValue(blueRobot));
        Assert.Equal(1600f, blueRobot.GetType().GetProperty("Y")?.GetValue(blueRobot));
        var balls = Assert.IsAssignableFrom<IEnumerable<object>>(
            latestVision.GetType().GetProperty("Balls")?.GetValue(latestVision));
        var ball = Assert.Single(balls);
        Assert.Equal(-1200f, ball.GetType().GetProperty("X")?.GetValue(ball));
        Assert.Equal(2990f, ball.GetType().GetProperty("Y")?.GetValue(ball));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<TrackerComparisonSourceStatus>>(
            response.GetType().GetProperty("Sources")?.GetValue(response)));
    }

    [Fact]
    public void ComparisonHealthIncludesDifferencesFromTheThreeResolvedLiveTrackerSnapshots()
    {
        var manager = new MultiTrackerManager<TrackerPacketAdapter>("duck-id", "ibis");
        var receivedAt = DateTimeOffset.UtcNow;
        manager.ProcessPacket(new TrackerPacketAdapter(Packet("ibis", "duck-id", 1, 1)),
            new IPEndPoint(IPAddress.Loopback, 11010), receivedAt);
        manager.ProcessPacket(new TrackerPacketAdapter(Packet("TIGERs", "tigers-id", 2, 3)),
            new IPEndPoint(IPAddress.Loopback, 11010), receivedAt);
        manager.ProcessPacket(new TrackerPacketAdapter(Packet("ER-FORCE", "erforce-id", 3, 5)),
            new IPEndPoint(IPAddress.Loopback, 11010), receivedAt);
        using var trackerSnapshots = new ExternalTrackerSnapshotStore(manager);
        var visionPackets = new VisionPacketStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:Mode"] = "comparison",
                ["TrackerComparison:BallDistanceGateMeters"] = "5",
            })
            .Build();

        var result = DebugHostHealthEndpoints.GetComparisonHealthResult(
            trackerSnapshots,
            visionPackets,
            configuration);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var response = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        Assert.NotNull(response);
        var pairs = Assert.IsAssignableFrom<IEnumerable<TrackerDifferencePairResult>>(
            response.GetType().GetProperty("Pairs")?.GetValue(response)).ToArray();
        Assert.Equal(3, pairs.Length);
        var duckTigers = Assert.Single(pairs, pair => pair.ReferenceRole == "Duck" && pair.ComparisonRole == "TIGERs");
        Assert.Equal(1u, duckTigers.ReferenceFrameNumber);
        Assert.Equal(2u, duckTigers.ComparisonFrameNumber);
        Assert.Equal(2d, Assert.Single(duckTigers.Difference!.Robots).PositionDelta!.Value.X);
        Assert.Equal(2d, Assert.Single(duckTigers.Difference.Balls.Matches).PositionDelta.X);
    }

    private static TrackerWrapperPacket Packet(string sourceName, string uuid, uint frameNumber, float x)
    {
        var frame = new TrackedFrame { FrameNumber = frameNumber, Timestamp = frameNumber };
        frame.Robots.Add(new TrackedRobot
        {
            RobotId = new RobotId { Team = Team.Blue, Id = 3 },
            Pos = new Vector2 { X = x, Y = 0 },
            Orientation = 0,
        });
        frame.Balls.Add(new TrackedBall { Pos = new Vector3 { X = x, Y = 0, Z = 0 } });
        return new TrackerWrapperPacket { SourceName = sourceName, Uuid = uuid, TrackedFrame = frame };
    }
}
