using System.Net;
using System.Text.Json;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tracker.RuntimeHost;

/// <summary>自身の受信証跡を返す。ホストのポート公開と所有者の検証は別途行う。</summary>
public sealed class RuntimeVisionDiagnosticsHttpService(
    RuntimeVisionReceiverDiagnostics diagnostics,
    IConfiguration configuration,
    ILogger<RuntimeVisionDiagnosticsHttpService> logger) : BackgroundService
{
    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("Testing:ContainerMode") ||
        string.Equals(configuration["Testing:Mode"], "comparison", StringComparison.Ordinal);

    public static string ListenerPrefix(IConfiguration configuration) =>
        configuration.GetValue<bool>("Testing:ContainerMode")
            ? "http://+:39108/" : "http://127.0.0.1:39108/";

    public static (int StatusCode, RuntimeVisionHealthResponse Response) GetHealth(
        RuntimeVisionReceiverDiagnostics diagnostics, IConfiguration configuration)
    {
        var snapshot = diagnostics.Snapshot;
        // 既存の比較用 HTTP は件数を 200 で返す。この応答を厳密な準備完了の証拠にはしない。
        var statusReady = !configuration.GetValue<bool>("Testing:ContainerMode") || snapshot.IsReady;
        var count = configuration.GetValue<bool>("Testing:ContainerMode")
            ? snapshot.SuccessfullyDecodedCount : diagnostics.DecodedWrapperPacketsReceivedTotal;
        return (statusReady ? 200 : 503, new(count, snapshot.IsReady, snapshot));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(ListenerPrefix(configuration));
        listener.Start();
        logger.LogInformation("RuntimeHost receive diagnostics listening at {Prefix}", ListenerPrefix(configuration));
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(stoppingToken);
                try
                {
                    if (context.Request.HttpMethod != "GET" || context.Request.Url?.AbsolutePath != "/health/vision")
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                        continue;
                    }

                    var health = GetHealth(diagnostics, configuration);
                    context.Response.StatusCode = health.StatusCode;
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.OutputStream, health.Response,
                        cancellationToken: stoppingToken);
                    context.Response.Close();
                }
                catch (HttpListenerException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (IOException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { listener.Close(); }
    }
}

public sealed record RuntimeVisionHealthResponse(long VisionPacketsReceived, bool IsReady, ServiceReceiveSnapshot Diagnostics);
