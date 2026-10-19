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
        visionPackets.StorePacket(
            new SSL_WrapperPacket { Detection = new SSL_DetectionFrame() },
            new IPEndPoint(IPAddress.Loopback, 10020),
            receivedAt);
        visionPackets.StorePacket(
            new SSL_WrapperPacket { Detection = new SSL_DetectionFrame() },
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
