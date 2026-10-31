using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Configuration;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

internal static class DebugHostHealthEndpoints
{
    internal static void ConfigureInternalHttp(WebApplicationBuilder builder)
    {
        if (builder.Configuration.GetValue<bool>("Testing:ContainerMode"))
            builder.WebHost.UseUrls("http://0.0.0.0:8080");
    }

    internal static void UseTransportMiddleware(WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("Testing:ContainerMode")) return;
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.UseHttpsRedirection();
    }

    internal static IResult GetVisionHealthResult(ServiceReceiveDiagnostics diagnostics, IConfiguration configuration, VisionPacketStore? rawPackets = null)
    {
        var snapshots = diagnostics.GetSnapshots("vision");
        var ready = snapshots.All(s => s.IsReady);
        var count = !configuration.GetValue<bool>("Testing:ContainerMode") && rawPackets is not null
            ? rawPackets.GetSnapshot().PacketCount : snapshots.Sum(s => s.SuccessfullyDecodedCount);
        var response = new { VisionPacketsReceived = count, IsReady = ready, Diagnostics = snapshots };
        var statusReady = !configuration.GetValue<bool>("Testing:ContainerMode") || ready;
        return statusReady ? Results.Ok(response) : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    internal static IResult GetComparisonHealthResult(
        ExternalTrackerSnapshotStore trackerSnapshots,
        VisionPacketStore visionPackets,
        IConfiguration configuration,
        ServiceReceiveDiagnostics? diagnostics = null)
    {
        if (!string.Equals(configuration["Testing:Mode"], "comparison", StringComparison.Ordinal))
            return Results.Ok(new { mode = "disabled", ready = true });

        var comparison = TrackerDifferenceSnapshotEvaluator.Evaluate(
            trackerSnapshots.GetSnapshot(),
            DateTimeOffset.UtcNow,
            TrackerComparisonReadiness.DefaultFreshnessWindow,
            configuration.GetValue<double>("TrackerComparison:BallDistanceGateMeters", 0.5));
        var readiness = comparison.Readiness;
        var evidence = diagnostics?.GetSnapshots("tracker");
        var strict = configuration.GetValue<bool>("Testing:ContainerMode");
        var ready = readiness.IsReady && (!strict || (evidence is not null && readiness.Sources.All(source =>
            evidence.Any(item => item.SourceKey == DiagnosticTrackerDeserializer.SourceKey(
                source.SourceName, source.SourceUuid, source.RemoteEndpoint) && item.IsReady))));
        var response = new
        {
            IsReady = ready,
            readiness.MissingRoles,
            readiness.ConflictingRoles,
            readiness.Sources,
            comparison.Pairs,
            VisionPacketsReceived = strict
                ? diagnostics?.GetSnapshots("vision").Sum(s => s.SuccessfullyDecodedCount) ?? 0
                : visionPackets.GetSnapshot().PacketCount,
            Diagnostics = evidence,
        };
        return ready ? Results.Ok(response) : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
