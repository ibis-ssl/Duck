using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Duck.Testing.Diagnostics;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

public sealed class RuntimeIdentityControlOperationLifetimeTests
{
    /// <summary>期限後に遅れて失敗した処理の例外が未観測にならない。</summary>
    [Fact]
    public async Task DeadlineObservesFaultThatArrivesAfterTimeout()
    {
        const string marker = "runtime-identity-late-fault-observation";
        var observed = new TaskCompletionSource<AggregateException>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, args) =>
        {
            if (args.Exception.ToString().Contains(marker, StringComparison.Ordinal))
                observed.TrySetResult(args.Exception);
        };
        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            var operation = await CreateLateFaultAfterDeadline(marker);
            for (var attempt = 0; attempt < 12 && !observed.Task.IsCompleted; attempt++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                await Task.Delay(25);
            }

            Assert.False(observed.Task.IsCompleted, "A timed-out operation's later fault must be observed by the deadline helper.");
            Assert.False(operation.TryGetTarget(out _), "The completed operation should not be retained indefinitely.");
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    /// <summary>期限切れ時に所有するUnixソケットを閉じ、後続の制御接続を受け付ける。</summary>
    [Fact]
    public async Task RelayTimeoutClosesOwnedUnixSocketForNextClient()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-lifetime-{Guid.NewGuid():N}.sock");
        await using var server = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await server.StartAsync(CancellationToken.None);

        var identity = JsonSerializer.Serialize(new
        {
            type = "prepare", runId, resourceId, containerId = new string('c', 64), generation = 1,
        });
        using var input = new StringReader(identity + "\n");
        var blockedOutput = new NeverCompletingTextWriter();
        var firstRelay = RuntimeIdentityControlRelay.RunAsync(
            input, blockedOutput, socketPath, CancellationToken.None, TimeSpan.FromMilliseconds(150));
        await blockedOutput.Entered.WaitAsync(TimeSpan.FromSeconds(2));
        var firstExit = await firstRelay.WaitAsync(TimeSpan.FromSeconds(2));

        var commit = JsonSerializer.Serialize(new
        {
            type = "commit", expectedBootId = server.BootId, runId, resourceId,
            containerId = new string('c', 64), generation = 1,
        });
        using var nextInput = new StringReader(identity + "\n" + commit + "\n");
        using var nextOutput = new StringWriter();
        var nextExit = await RuntimeIdentityControlRelay.RunAsync(
            nextInput, nextOutput, socketPath, CancellationToken.None, TimeSpan.FromSeconds(2));

        Assert.Equal(2, firstExit);
        Assert.Equal(0, nextExit);
        Assert.False(string.IsNullOrWhiteSpace(nextOutput.ToString()));
    }

    /// <summary>標準入力が終端しない場合も短命control processが期限で終了する。</summary>
    [Fact]
    public async Task IdentityControlChildProcessExitsOnIdleStandardInput()
    {
        var runtimeHostAssembly = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../..",
            "Tracker",
            "Tracker.RuntimeHost",
            "bin",
            "Debug",
            "net10.0",
            "Tracker.RuntimeHost.dll"));
        Assert.True(File.Exists(runtimeHostAssembly), $"Expected existing build output at {runtimeHostAssembly}.");
        Assert.True(File.Exists(Path.ChangeExtension(runtimeHostAssembly, ".runtimeconfig.json")));
        Assert.True(File.Exists(Path.ChangeExtension(runtimeHostAssembly, ".deps.json")));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(runtimeHostAssembly);
        process.StartInfo.ArgumentList.Add("--identity-control");
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(35));
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2));
            var standardOutput = await stdout;
            var standardError = await stderr;
            Assert.True(process.ExitCode == 2,
                $"Expected the idle relay deadline exit code 2, got {process.ExitCode}. stdout: {standardOutput} stderr: {standardError}");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    /// <summary>制御接続のタイムアウト後に遅延faultを発生させる試験taskを作る。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference<Task>> CreateLateFaultAfterDeadline(string marker)
    {
        TaskCompletionSource? completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? operationTask = completion.Task;
        var weakOperation = new WeakReference<Task>(operationTask);
        await Assert.ThrowsAsync<TimeoutException>(() => RuntimeIdentityControlRelay.RunWithDeadlineAsync(
            _ => operationTask, CancellationToken.None, TimeSpan.FromMilliseconds(50)));
        completion.SetException(new IOException(marker));
        completion = null;
        operationTask = null;
        await Task.Delay(50);
        return weakOperation;
    }

    private sealed class NeverCompletingTextWriter : TextWriter
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Encoding Encoding => Encoding.UTF8;
        public Task Entered => entered.Task;

        public override Task WriteLineAsync(string? value)
        {
            entered.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan);
        }
    }
}
