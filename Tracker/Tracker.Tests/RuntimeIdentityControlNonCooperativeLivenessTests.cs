using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Duck.Testing.Diagnostics;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

public sealed class RuntimeIdentityControlNonCooperativeLivenessTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RelayReturnsWhenOutputCallBlocksSynchronously(bool blockWriteLine)
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-sync-output-{Guid.NewGuid():N}.sock");
        await using var server = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await server.StartAsync(CancellationToken.None);
        var prepare = JsonSerializer.Serialize(new
        {
            type = "prepare", runId, resourceId, containerId = new string('a', 64), generation = 1,
        });
        using var input = new StringReader(prepare + "\n");
        var output = new SynchronousBlockingTextWriter(blockWriteLine);
        var relayTask = Task.Run(() => RuntimeIdentityControlRelay.RunAsync(
            input, output, socketPath, CancellationToken.None, TimeSpan.FromMilliseconds(150)));
        await output.Entered.WaitAsync(TimeSpan.FromSeconds(2));
        var completedWithoutRelease = await Task.WhenAny(relayTask, Task.Delay(TimeSpan.FromSeconds(1))) == relayTask;
        output.Release();
        var result = await relayTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(completedWithoutRelease, blockWriteLine
            ? "A synchronous-blocking output write must not hold the relay past its deadline."
            : "A synchronous-blocking output flush must not hold the relay past its deadline.");
        Assert.NotEqual(130, result);
    }

    [Fact]
    public async Task RelayReturnsWhenFlushIgnoresCancellationToken()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-async-flush-{Guid.NewGuid():N}.sock");
        await using var server = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await server.StartAsync(CancellationToken.None);
        var prepare = JsonSerializer.Serialize(new
        {
            type = "prepare", runId, resourceId, containerId = new string('b', 64), generation = 1,
        });
        using var input = new StringReader(prepare + "\n");
        var output = new NonCooperativeFlushTextWriter();
        var relayTask = Task.Run(() => RuntimeIdentityControlRelay.RunAsync(
            input, output, socketPath, CancellationToken.None, TimeSpan.FromMilliseconds(150)));
        await output.Entered.WaitAsync(TimeSpan.FromSeconds(2));
        var completedWithoutRelease = await Task.WhenAny(relayTask, Task.Delay(TimeSpan.FromSeconds(1))) == relayTask;
        output.Release();
        var result = await relayTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(completedWithoutRelease, "A flush that ignores cancellation must not hold the relay past its deadline.");
        Assert.NotEqual(130, result);
    }

    [Fact]
    public async Task RelayReturnsWhenInputReadIgnoresCancellationToken()
    {
        var input = new NonCooperativeTextReader();
        var output = new StringWriter();
        var relayTask = Task.Run(() => RuntimeIdentityControlRelay.RunAsync(
            input, output, Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.sock"),
            CancellationToken.None, TimeSpan.FromMilliseconds(150)));
        await input.Entered.WaitAsync(TimeSpan.FromSeconds(2));
        var completedWithoutRelease = await Task.WhenAny(relayTask, Task.Delay(TimeSpan.FromSeconds(1))) == relayTask;
        input.Release();
        var result = await relayTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(completedWithoutRelease, "An input reader that ignores cancellation must not hold the relay past its deadline.");
        Assert.Equal(2, result);
    }

    private sealed class SynchronousBlockingTextWriter(bool blockWriteLine) : TextWriter
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Encoding Encoding => Encoding.UTF8;
        public Task Entered => entered.Task;
        public void Release() => release.TrySetResult();

        public override Task WriteLineAsync(string? value)
        {
            if (!blockWriteLine) return Task.CompletedTask;
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
            return Task.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (blockWriteLine) return Task.CompletedTask;
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class NonCooperativeFlushTextWriter : TextWriter
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Encoding Encoding => Encoding.UTF8;
        public Task Entered => entered.Task;
        public void Release() => release.TrySetResult();
        public override Task WriteLineAsync(string? value) => Task.CompletedTask;
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            return release.Task;
        }
    }

    private sealed class NonCooperativeTextReader : TextReader
    {
        private readonly TaskCompletionSource<int> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Entered => entered.Task;
        public void Release() => release.TrySetResult(0);
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            entered.TrySetResult();
            return new ValueTask<int>(release.Task);
        }
    }
}
