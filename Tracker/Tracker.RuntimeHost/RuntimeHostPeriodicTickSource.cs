using Microsoft.Extensions.Options;

namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost operation loop の周期待機を抽象化する。
/// </summary>
public interface IRuntimeHostTickSource
{
    /// <summary>
    /// 次の operation loop tick まで待機する。
    /// </summary>
    ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken);
}

/// <summary>
/// `RuntimeHost:OperationLoopIntervalMilliseconds` から作成した周期 timer。
/// </summary>
public sealed class RuntimeHostPeriodicTickSource : IRuntimeHostTickSource, IDisposable
{
    private readonly PeriodicTimer timer;

    /// <summary>
    /// RuntimeHost options の実行周期から tick source を作成する。
    /// </summary>
    public RuntimeHostPeriodicTickSource(IOptions<RuntimeHostOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Interval = TimeSpan.FromMilliseconds(options.Value.OperationLoopIntervalMilliseconds);
        timer = new PeriodicTimer(Interval);
    }

    /// <summary>
    /// operation loop の実行周期。
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// 次の周期 tick まで待機する。
    /// </summary>
    public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
    {
        return timer.WaitForNextTickAsync(cancellationToken);
    }

    /// <summary>
    /// 内部 timer を破棄する。
    /// </summary>
    public void Dispose()
    {
        timer.Dispose();
    }
}
