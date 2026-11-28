using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tracker.Core;

namespace Tracker.RuntimeHost;

/// <summary>設定済みの場合だけ、RuntimeHost の稼働境界に合わせて録画を開始・確定する。</summary>
internal sealed class RuntimeMatchRecordingHostedService(
    IOptions<RuntimeMatchRecordingOptions> options,
    TrackerRuntimeResolvedOptions resolvedOptions,
    RuntimeMatchRecordingSession session,
    TimeProvider timeProvider,
    RuntimeHostOperationLoop operationLoop,
    ILogger<RuntimeMatchRecordingHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var directoryPath = options.Value.DirectoryPath;
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            session.Start(directoryPath, resolvedOptions, timeProvider.GetUtcNow(), operationLoop.LastAppliedGeometry);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (session.IsRecording && operationLoop.ProcessLatestPacket())
            {
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not drain all admitted RuntimeHost packets before completing the match recording.");
        }

        session.Complete(timeProvider.GetUtcNow());
        return Task.CompletedTask;
    }
}
