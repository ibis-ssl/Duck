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
        var comparison = TrackerDifferenceSnapshotEvaluator.Evaluate(
            snapshots,
            DateTimeOffset.UtcNow,
            TrackerComparisonReadiness.DefaultFreshnessWindow,
            configuration.GetValue<double>("TrackerComparison:BallDistanceGateMeters", 0.5));
        var readiness = comparison.Readiness;
        var response = new
        {
            readiness.IsReady,
            readiness.MissingRoles,
            readiness.ConflictingRoles,
            readiness.Sources,
            comparison.Pairs,
            VisionPacketsReceived = visionPackets.GetSnapshot().PacketCount,
        };
        return readiness.IsReady
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
