using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tracker.RuntimeHost;

/// <summary>Exposes the RuntimeHost SSL-Vision receive counter for isolated Linux comparison acceptance.</summary>
public sealed class RuntimeVisionDiagnosticsHttpService(
    RuntimeVisionReceiverDiagnostics diagnostics,
    ILogger<RuntimeVisionDiagnosticsHttpService> logger) : BackgroundService
{
    private const int Port = 39108;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        listener.Start();
        logger.LogInformation("RuntimeHost receive-count endpoint listening on port {Port}", Port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(stoppingToken);
                try
                {
                    if (context.Request.Url?.AbsolutePath != "/health/vision")
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                        continue;
                    }

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(
                        context.Response.OutputStream,
                        new { VisionPacketsReceived = diagnostics.VisionPacketsReceivedTotal },
                        cancellationToken: stoppingToken);
                    context.Response.Close();
                }
                catch (HttpListenerException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (IOException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Close();
        }
    }
}
