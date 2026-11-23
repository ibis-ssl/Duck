using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;

namespace Tracker.Diagnostics.Mcap;

/// <summary>
/// ホストの logger factory 破棄と MCAP 診断記録の確定順序を制御する。
/// </summary>
public static class McapDiagnosticsHostRunner
{
    private const string HostingLoggerCategory = "Microsoft.Extensions.Hosting.Internal.Host";
    private const int BackgroundServiceFaultedEventId = 9;
    private const string BackgroundServiceFaultedEventName = "BackgroundServiceFaulted";

    /// <summary>
    /// ホストを実行し、その終了結果を保ってから診断 MCAP を確定する。
    /// </summary>
    /// <param name="loggerFactory">ホストが所有する logger factory。</param>
    /// <param name="diagnosticsProvider">ホストの診断 MCAP provider。</param>
    /// <param name="runHostAsync">ホストを実行して終了結果を返す処理。</param>
    /// <returns>ホスト停止と診断記録の確定が完了する非同期処理。</returns>
    public static async Task RunAsync(
        ILoggerFactory loggerFactory,
        McapDiagnosticsLoggerProvider diagnosticsProvider,
        Func<Task> runHostAsync)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(diagnosticsProvider);
        ArgumentNullException.ThrowIfNull(runHostAsync);

        var backgroundServiceFailure = new BackgroundServiceFailure();
        loggerFactory.AddProvider(new HostOwnedLoggerProvider(diagnosticsProvider, backgroundServiceFailure));

        Exception? hostFailure = null;
        try
        {
            await runHostAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            hostFailure = exception;
        }

        hostFailure ??= backgroundServiceFailure.Exception;

        Exception? recordingFailure = null;
        try
        {
            diagnosticsProvider.Complete(hostSucceeded: hostFailure is null);
        }
        catch (Exception exception)
        {
            recordingFailure = exception;
        }
        finally
        {
            diagnosticsProvider.Dispose();
        }

        if (hostFailure is not null)
        {
            if (recordingFailure is not null)
            {
                Console.Error.WriteLine($"ホストの停止と診断 MCAP の確定が失敗しました。ホスト側の一次例外を保持します: {recordingFailure}");
            }

            ExceptionDispatchInfo.Capture(hostFailure).Throw();
        }

        if (recordingFailure is not null)
        {
            ExceptionDispatchInfo.Capture(recordingFailure).Throw();
        }
    }

    private sealed class HostOwnedLoggerProvider(
        McapDiagnosticsLoggerProvider diagnosticsProvider,
        BackgroundServiceFailure backgroundServiceFailure) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new HostOwnedLogger(
            categoryName,
            diagnosticsProvider.CreateLogger(categoryName),
            backgroundServiceFailure);

        // Host.RunAsync が logger factory を破棄した時点では writer を閉じず、後続の Complete に所有権を残す。
        public void Dispose()
        {
        }
    }

    private sealed class HostOwnedLogger(
        string categoryName,
        ILogger innerLogger,
        BackgroundServiceFailure backgroundServiceFailure) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            innerLogger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => innerLogger.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            backgroundServiceFailure.Observe(categoryName, eventId, exception);
            innerLogger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    private sealed class BackgroundServiceFailure
    {
        private Exception? exception;

        public Exception? Exception => Volatile.Read(ref exception);

        public void Observe(string categoryName, EventId eventId, Exception? failure)
        {
            if (categoryName != HostingLoggerCategory ||
                eventId.Id != BackgroundServiceFaultedEventId ||
                eventId.Name != BackgroundServiceFaultedEventName)
            {
                return;
            }

            var capturedFailure = failure ?? new InvalidOperationException(
                "ホストが BackgroundService の障害イベントを例外なしで通知しました。");
            Interlocked.CompareExchange(ref exception, capturedFailure, null);
        }
    }
}
