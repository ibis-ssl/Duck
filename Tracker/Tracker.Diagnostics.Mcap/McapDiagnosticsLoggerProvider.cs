using System.Text.Json;
using Fizzy.McapSharp;
using Microsoft.Extensions.Logging;

namespace Tracker.Diagnostics.Mcap;

/// <summary>
/// Foxglove の標準 Log schema で ILogger の診断記録を一つの MCAP に保存する provider。
/// </summary>
public sealed class McapDiagnosticsLoggerProvider : ILoggerProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object gate = new();
    private McapWriter? writer;
    private readonly string partialPath;
    private readonly string completedPath;
    private ushort channelId;
    private readonly TimeProvider timeProvider;
    private readonly bool isFileOutput;
    private ulong sequence;
    private Exception? writeFailure;
    private bool completionAttempted;
    private bool ownsPartialPath;

    /// <summary>
    /// 起動ごとの一時 MCAP を作成し、正常な Complete 後だけ最終拡張子へ確定する。
    /// </summary>
    public McapDiagnosticsLoggerProvider(string? directoryPath = null, TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
        var resolvedDirectory = string.IsNullOrWhiteSpace(directoryPath)
            ? Path.Combine(AppContext.BaseDirectory, "diagnostic-logs")
            : Path.IsPathRooted(directoryPath)
                ? directoryPath
                : Path.Combine(AppContext.BaseDirectory, directoryPath);
        var startedAt = this.timeProvider.GetUtcNow();
        var baseName = $"duck-diagnostics-{startedAt:yyyyMMddTHHmmssfffZ}-{Environment.ProcessId}-{Guid.NewGuid():N}";
        completedPath = Path.Combine(resolvedDirectory, $"{baseName}.mcap");
        partialPath = $"{completedPath}.partial";
        isFileOutput = true;
        InitializeFileWriter(resolvedDirectory);
    }

    /// <summary>所有権境界の試験で明示パスへ排他的に出力する writer 境界。</summary>
    internal McapDiagnosticsLoggerProvider(string partialPath, string completedPath)
    {
        this.partialPath = partialPath;
        this.completedPath = completedPath;
        this.timeProvider = TimeProvider.System;
        isFileOutput = true;
        InitializeFileWriter(directoryPath: null);
    }

    private void InitializeFileWriter(string? directoryPath)
    {
        FileStream? output = null;
        try
        {
            if (directoryPath is not null)
            {
                Directory.CreateDirectory(directoryPath);
            }

            output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            ownsPartialPath = true;
            var createdWriter = new McapWriter(output, new McapWriterOptions
            {
                UseChunks = false,
                EmitSummaryRecords = true,
            });
            output = null;
            writer = createdWriter;

            channelId = RegisterDiagnosticsChannel(createdWriter);
        }
        catch (Exception exception)
        {
            var streamFailure = TryDisposeStream(output);
            var disposeFailure = TryDisposeWriter();
            var cleanupFailure = TryDeletePartialPath();
            ReportSecondaryFailure("初期化中の stream 解放", streamFailure);
            ReportSecondaryFailure("初期化後始末", disposeFailure);
            ReportSecondaryFailure("初期化時の出力削除", cleanupFailure);
            ReportFailure("初期化", exception);
            throw;
        }
    }

    /// <summary>故障経路試験で書込エラーを注入する writer 境界。</summary>
    internal McapDiagnosticsLoggerProvider(
        string partialPath,
        string completedPath,
        Stream output,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.partialPath = partialPath;
        this.completedPath = completedPath;
        try
        {
            ownsPartialPath = true;
            var createdWriter = new McapWriter(output, new McapWriterOptions
            {
                UseChunks = false,
                EmitSummaryRecords = true,
            });
            writer = createdWriter;
            channelId = RegisterDiagnosticsChannel(createdWriter);
        }
        catch (Exception exception)
        {
            var disposeFailure = TryDisposeWriter();
            var cleanupFailure = TryDeletePartialPath();
            ReportSecondaryFailure("初期化後始末", disposeFailure);
            ReportSecondaryFailure("初期化時の出力削除", cleanupFailure);
            ReportFailure("初期化", exception);
            throw;
        }
    }

    /// <summary>
    /// 以降の log 呼び出しで共通する category logger を返す。
    /// </summary>
    public ILogger CreateLogger(string categoryName) => new McapDiagnosticsLogger(this, categoryName);

    /// <summary>
    /// MCAP の末尾を書き、永続化を要求して閉じてから最終ファイル名へ確定する。
    /// </summary>
    public void Complete() => Complete(hostSucceeded: true);

    /// <summary>
    /// MCAP を閉じる。ホスト正常終了の場合だけ完成名へ移し、失敗時は所有出力を削除する。
    /// </summary>
    public void Complete(bool hostSucceeded)
    {
        lock (gate)
        {
            if (completionAttempted)
            {
                return;
            }

            completionAttempted = true;
            if (!hostSucceeded)
            {
                var disposeFailure = TryDisposeWriter();
                var cleanupFailure = TryDeletePartialPath();
                if (disposeFailure is not null || cleanupFailure is not null)
                {
                    var failure = disposeFailure ?? cleanupFailure!;
                    ReportFailure("失敗したホストの後始末", failure);
                    throw new InvalidOperationException("失敗したホストの診断 MCAP 出力を片付けられませんでした。", failure);
                }

                return;
            }

            try
            {
                if (writeFailure is not null)
                {
                    throw new InvalidOperationException("診断 MCAP の書き込みに失敗しました。", writeFailure);
                }

                var currentWriter = writer ?? throw new ObjectDisposedException(nameof(McapDiagnosticsLoggerProvider));
                currentWriter.Complete();
                if (isFileOutput)
                {
                    currentWriter.FlushToDisk();
                }
                currentWriter.Dispose();
                writer = null;
                if (isFileOutput)
                {
                    File.Move(partialPath, completedPath);
                    ownsPartialPath = false;
                }
            }
            catch (Exception exception)
            {
                writeFailure ??= exception;
                var disposeFailure = TryDisposeWriter();
                var cleanupFailure = TryDeletePartialPath();
                ReportFailure("最終化", exception);
                ReportSecondaryFailure("最終化後始末", disposeFailure);
                ReportSecondaryFailure("最終化時の出力削除", cleanupFailure);
                throw;
            }
        }
    }

    /// <summary>
    /// 明示完了されていない出力を破棄し、完成名では公開しない。
    /// </summary>
    public void Dispose()
    {
        try
        {
            Complete(hostSucceeded: false);
        }
        catch (Exception exception)
        {
            ReportFailure("Dispose 時の後始末", exception);
        }
    }

    private void Log<TState>(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.None)
        {
            return;
        }

        lock (gate)
        {
            if (completionAttempted || writeFailure is not null)
            {
                return;
            }

            try
            {
                var timestamp = timeProvider.GetUtcNow();
                var unixNanoseconds = ToUnixNanoseconds(timestamp);
                var message = formatter(state, exception);
                if (exception is not null)
                {
                    message = string.IsNullOrEmpty(message)
                        ? exception.ToString()
                        : $"{message}{Environment.NewLine}{exception}";
                }

                var payload = JsonSerializer.SerializeToUtf8Bytes(new FoxgloveLogMessage(
                    new FoxgloveTime(checked((long)(unixNanoseconds / 1_000_000_000)), (int)(unixNanoseconds % 1_000_000_000)),
                    ToFoxgloveLevel(logLevel),
                    message,
                    categoryName,
                    string.Empty,
                    0), JsonOptions);
                var header = new McapMessageHeader(channelId, checked((uint)sequence++), unixNanoseconds, unixNanoseconds);
                (writer ?? throw new ObjectDisposedException(nameof(McapDiagnosticsLoggerProvider)))
                    .WriteToKnownChannel(in header, payload);
            }
            catch (Exception exceptionDuringWrite)
            {
                writeFailure = exceptionDuringWrite;
                Console.Error.WriteLine($"Duck 診断 MCAP への書き込みに失敗しました。未完了ファイル: {partialPath}; error: {exceptionDuringWrite}");
            }
        }
    }

    private Exception? TryDisposeWriter()
    {
        var currentWriter = writer;
        writer = null;
        if (currentWriter is null)
        {
            return null;
        }

        try
        {
            currentWriter.Dispose();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static Exception? TryDisposeStream(Stream? stream)
    {
        if (stream is null)
        {
            return null;
        }

        try
        {
            stream.Dispose();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private Exception? TryDeletePartialPath()
    {
        if (!ownsPartialPath)
        {
            return null;
        }

        try
        {
            File.Delete(partialPath);
            ownsPartialPath = false;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void ReportFailure(string stage, Exception exception) =>
        Console.Error.WriteLine($"Duck 診断 MCAP の{stage}に失敗しました: {exception}");

    private static void ReportSecondaryFailure(string stage, Exception? exception)
    {
        if (exception is not null)
        {
            Console.Error.WriteLine($"Duck 診断 MCAP の{stage}にも失敗しました: {exception}");
        }
    }

    private static byte[] LoadSchema()
    {
        using var stream = typeof(McapDiagnosticsLoggerProvider).Assembly
            .GetManifestResourceStream("Tracker.Diagnostics.Mcap.FoxgloveLog.schema.json")
            ?? throw new InvalidOperationException("Foxglove Log schema が assembly にありません。");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static ushort RegisterDiagnosticsChannel(McapWriter writer)
    {
        var schemaId = writer.RegisterSchema("foxglove.Log", "jsonschema", LoadSchema());
        return writer.RegisterChannel("/diagnostics", "json", schemaId, null);
    }

    private static ulong ToUnixNanoseconds(DateTimeOffset timestamp)
    {
        var ticks = timestamp.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks;
        if (ticks < 0)
        {
            throw new InvalidOperationException("MCAP のログ時刻に1970年より前の時刻は記録できません。");
        }

        return checked((ulong)ticks * 100);
    }

    private static int ToFoxgloveLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace or LogLevel.Debug => 1,
        LogLevel.Information => 2,
        LogLevel.Warning => 3,
        LogLevel.Error => 4,
        LogLevel.Critical => 5,
        _ => 0,
    };

    private sealed class McapDiagnosticsLogger(McapDiagnosticsLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            provider.Log(categoryName, logLevel, eventId, state, exception, formatter);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }

    private sealed record FoxgloveTime(long Sec, int Nsec);

    private sealed record FoxgloveLogMessage(
        FoxgloveTime Timestamp,
        int Level,
        string Message,
        string Name,
        string File,
        int Line);
}
