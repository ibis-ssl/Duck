using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace Duck.Testing.RefereeDriver.Hosting;

/// <summary>AppHost のネットワークから Simulator 制御指令を送る試験専用 HTTP 中継を構成します。</summary>
internal static class SimulatorControlRelayApplication
{
    internal const string ListenUrl = "http://0.0.0.0:39111";

    /// <summary>中継用 Web アプリケーションを起動して停止まで待機します。</summary>
    /// <param name="cancellationToken">中継プロセスの停止を通知するトークンです。</param>
    /// <returns>Web アプリケーションの停止を表す非同期処理です。</returns>
    internal static async Task RunAsync(CancellationToken cancellationToken)
    {
        var app = CreateApplication();
        await app.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Simulator 制御指令を転送する HTTP エンドポイントを作成します。</summary>
    /// <param name="simulator">指令の送信と Simulator 応答の観測を行うクライアントです。</param>
    /// <param name="listenUrl">待受 URL です。省略時はコンテナーの全 IPv4 アドレスで待ち受けます。</param>
    /// <returns>構成済みの Web アプリケーションです。</returns>
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

    /// <summary>初期位置を決める際の青チーム側を表す要求です。</summary>
    /// <param name="BlueTeamOnPositiveHalf">青チームが正の半面にいる場合は <see langword="true"/> です。</param>
    internal sealed record InitialOwnHalfSetupRequest(bool BlueTeamOnPositiveHalf);

    /// <summary>ボールを置く位置を表す要求です。</summary>
    /// <param name="X">フィールド上の X 座標です。</param>
    /// <param name="Y">フィールド上の Y 座標です。</param>
    internal sealed record BallPlacementRequest(float X, float Y);
    private sealed record InitialSetupObservationTask(
        string RequestId,
        Task<SimulatorResponseObservation?> Observation);
}
