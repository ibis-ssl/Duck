using Microsoft.Extensions.Configuration;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

internal static class DebugHostHealthEndpoints
{
    internal static IResult GetComparisonHealthResult(
        ExternalTrackerSnapshotStore trackerSnapshots,
        VisionPacketStore visionPackets,
        IConfiguration configuration)
    {
        if (!string.Equals(configuration["Testing:Mode"], "comparison", StringComparison.Ordinal))
        {
            return Results.Ok(new { mode = "disabled", ready = true });
        }

        var snapshots = trackerSnapshots.GetSnapshot();
        var readiness = TrackerComparisonReadiness.Evaluate(
            snapshots,
            DateTimeOffset.UtcNow,
            TrackerComparisonReadiness.DefaultFreshnessWindow);
        var response = new
        {
            readiness.IsReady,
            readiness.MissingRoles,
            readiness.ConflictingRoles,
            VisionPacketsReceived = visionPackets.GetSnapshot().PacketCount,
        };
        return readiness.IsReady
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
