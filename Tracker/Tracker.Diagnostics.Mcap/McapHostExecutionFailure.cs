namespace Tracker.Diagnostics.Mcap;

/// <summary>対象の常駐処理からホストへ、起動単位で最初の実行障害を伝える。</summary>
public sealed class McapHostExecutionFailure
{
    private Exception? firstFailure;

    internal Exception? FirstFailure => Volatile.Read(ref firstFailure);

    /// <summary>常駐処理を実行し、未処理例外を保存してからホストへ再送出する。</summary>
    /// <param name="runAsync">対象の常駐処理。</param>
    /// <param name="stoppingToken">ホストの停止を伝えるトークン。</param>
    /// <returns>対象の常駐処理の結果。</returns>
    public async Task RunServiceAsync(Func<CancellationToken, Task> runAsync, CancellationToken stoppingToken)
    {
        try
        {
            await runAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            Interlocked.CompareExchange(ref firstFailure, exception, null);
            throw;
        }
    }
}
