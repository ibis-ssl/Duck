using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace Duck.Testing.RefereeDriver.Hosting;

/// <summary>
/// Test-only HTTP bridge for simulator commands that must originate from the AppHost DCP network.
/// This runs as a separate sidecar process and does not add control behavior to RefereeDriver.
/// </summary>
internal static class SimulatorControlRelayApplication
{
    internal const string ListenUrl = "http://0.0.0.0:39111";

    internal static async Task RunAsync(CancellationToken cancellationToken)
    {
        var app = CreateApplication();
        await app.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static WebApplication CreateApplication(ISimulatorBallControlClient? simulator = null, string? listenUrl = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(listenUrl ?? ListenUrl);
        builder.Services.AddSingleton<ISimulatorBallControlClient>(simulator ?? new SimulatorBallControlClient());
        var app = builder.Build();
        var initialSetupSent = 0;
        InitialSetupObservationTask? initialSetupObservation = null;

        app.MapGet("/health/ready", () => Results.Ok(new { ready = true }));
        app.MapPost("/setup/initial-own-half", async (
            InitialOwnHalfSetupRequest request,
            ISimulatorBallControlClient simulator,
            IHostApplicationLifetime applicationLifetime,
            CancellationToken token) =>
        {
            if (Interlocked.CompareExchange(ref initialSetupSent, 1, 0) != 0)
                return Results.Conflict(new { error = "Initial simulator setup is one-shot." });

            var dispatch = await simulator.SendInitialOwnHalfSetupAsync(
                request.BlueTeamOnPositiveHalf, token, applicationLifetime.ApplicationStopping)
                .ConfigureAwait(false);
            var observationTask = simulator.CompleteInitialSetupObservationAsync();
            initialSetupObservation = new(dispatch.RequestId, observationTask);
            return Results.Accepted(value: dispatch);
        });
        app.MapGet("/setup/initial-own-half/observation/{requestId}", async (
            string requestId) =>
        {
            var observationTask = Volatile.Read(ref initialSetupObservation);
            if (observationTask is null || !string.Equals(observationTask.RequestId, requestId, StringComparison.Ordinal))
                return Results.Conflict(new { error = "Initial simulator setup has not been sent." });

            var observation = await observationTask.Observation.ConfigureAwait(false)
                ?? throw new InvalidOperationException("Simulator setup response observation was not created.");
            // GET is idempotent: after a lost response, callers can fetch the same
            // completed observation again without issuing another UDP setup command.
            return Results.Ok(observation with { RequestId = observationTask.RequestId });
        });
        app.MapPost("/setup/place-ball", async (
            BallPlacementRequest request,
            ISimulatorBallControlClient simulator,
            CancellationToken token) =>
        {
            if (!float.IsFinite(request.X) || !float.IsFinite(request.Y))
                return Results.BadRequest(new { error = "Ball placement coordinates must be finite." });

            await simulator.SendBallToPositionAsync(request.X, request.Y, token).ConfigureAwait(false);
            return Results.Ok(new { sent = true });
        });

        return app;
    }

    internal sealed record InitialOwnHalfSetupRequest(bool BlueTeamOnPositiveHalf);
    internal sealed record BallPlacementRequest(float X, float Y);
    private sealed record InitialSetupObservationTask(
        string RequestId,
        Task<SimulatorResponseObservation?> Observation);
}
