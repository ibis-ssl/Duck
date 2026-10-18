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
    }
}
