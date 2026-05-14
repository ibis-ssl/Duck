using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost tracker operation loop を周期 tick ごとに実行する hosted service。
/// </summary>
internal sealed class RuntimeHostLifecycleService : BackgroundService
{
    private readonly IOptions<RuntimeHostOptions> options;
    private readonly IRuntimeHostTickSource tickSource;
    private readonly RuntimeHostOperationLoop operationLoop;
    private readonly ILogger<RuntimeHostLifecycleService> logger;

    /// <summary>
    /// RuntimeHost options、tick source、operation loop を受け取って hosted service を作成する。
    /// </summary>
    public RuntimeHostLifecycleService(
        IOptions<RuntimeHostOptions> options,
        IRuntimeHostTickSource tickSource,
        RuntimeHostOperationLoop operationLoop,
        ILogger<RuntimeHostLifecycleService> logger)
    {
        this.options = options;
        this.tickSource = tickSource;
        this.operationLoop = operationLoop;
        this.logger = logger;
    }

    /// <summary>
    /// options validation が起動時に走るように options を materialize し、operation loop を開始する。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = options.Value.OperationLoopIntervalMilliseconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await tickSource.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                _ = operationLoop.ProcessLatestPacket();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RuntimeHost tracker operation loop failed.");
            }
        }
    }
}
