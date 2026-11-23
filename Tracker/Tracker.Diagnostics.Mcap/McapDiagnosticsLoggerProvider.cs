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
    private readonly McapWriter writer;
    private readonly string partialPath;
    private readonly string completedPath;
    private readonly ushort channelId;
    private readonly TimeProvider timeProvider;
    private readonly bool isFileOutput;
    private ulong sequence;
    private Exception? writeFailure;
    private bool completed;

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
        Directory.CreateDirectory(resolvedDirectory);

        var startedAt = this.timeProvider.GetUtcNow();
        var baseName = $"duck-diagnostics-{startedAt:yyyyMMddTHHmmssfffZ}-{Environment.ProcessId}-{Guid.NewGuid():N}";
        completedPath = Path.Combine(resolvedDirectory, $"{baseName}.mcap");
        partialPath = $"{completedPath}.partial";
        isFileOutput = true;
        writer = new McapWriter(partialPath, new McapWriterOptions
        {
            UseChunks = false,
            EmitSummaryRecords = true,
        });

        channelId = RegisterDiagnosticsChannel(writer);
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
        writer = new McapWriter(output, new McapWriterOptions
        {
            UseChunks = false,
            EmitSummaryRecords = true,
        }, leaveOpen: true);
        channelId = RegisterDiagnosticsChannel(writer);
    }

    /// <summary>
    /// 以降の log 呼び出しで共通する category logger を返す。
    /// </summary>
    public ILogger CreateLogger(string categoryName) => new McapDiagnosticsLogger(this, categoryName);

    /// <summary>
    /// MCAP の末尾を書き、永続化を要求して閉じてから最終ファイル名へ確定する。
    /// </summary>
    public void Complete()
    {
        lock (gate)
        {
            if (completed)
            {
                if (writeFailure is not null)
                {
                    throw new InvalidOperationException("診断 MCAP の記録に失敗しました。未完了ファイルを保持しています。", writeFailure);
                }

                return;
            }

            completed = true;
            try
            {
                if (writeFailure is not null)
                {
                    throw new InvalidOperationException("診断 MCAP の記録に失敗しました。未完了ファイルを保持しています。", writeFailure);
                }

                writer.Complete();
                if (isFileOutput)
                {
                    writer.FlushToDisk();
                }
                writer.Dispose();
                File.Move(partialPath, completedPath);
            }
            catch (Exception exception)
            {
                writeFailure ??= exception;
                TryDisposeWriter();
                Console.Error.WriteLine($"Duck 診断 MCAP を確定できませんでした。未完了ファイル: {partialPath}; error: {exception}");
                throw;
            }
        }
    }

    /// <summary>
    /// logger factory の破棄時にも明示完了を試み、失敗を標準エラーと終了値へ残す。
    /// </summary>
    public void Dispose()
    {
        try
        {
            Complete();
        }
        catch (Exception)
        {
            Environment.ExitCode = 1;
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
            if (completed || writeFailure is not null)
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
                writer.WriteToKnownChannel(in header, payload);
            }
            catch (Exception exceptionDuringWrite)
            {
                writeFailure = exceptionDuringWrite;
                Console.Error.WriteLine($"Duck 診断 MCAP への書き込みに失敗しました。未完了ファイル: {partialPath}; error: {exceptionDuringWrite}");
            }
        }
    }

    private void TryDisposeWriter()
    {
        try
        {
            writer.Dispose();
        }
        catch (Exception disposeFailure)
        {
            Console.Error.WriteLine($"Duck 診断 MCAP writer の解放にも失敗しました: {disposeFailure}");
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
