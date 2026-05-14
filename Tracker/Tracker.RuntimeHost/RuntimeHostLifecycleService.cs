using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost scaffold の lifecycle boundary。現段階では options validation を host start path で確実に発火させる。
/// </summary>
internal sealed class RuntimeHostLifecycleService(IOptions<RuntimeHostOptions> options) : IHostedService
{
    /// <summary>
    /// host start 時に runtime options を解決し、validation 済み設定で起動できることを確認する。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = options.Value.OperationLoopIntervalMilliseconds;
        return Task.CompletedTask;
    }

    /// <summary>
    /// RuntimeHost scaffold の停止処理。RUNTIME-HOST-009 で operation loop を追加するまで停止対象は持たない。
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
