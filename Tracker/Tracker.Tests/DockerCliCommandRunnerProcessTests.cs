using System.Diagnostics;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class DockerCliCommandRunnerProcessTests
{
    [Fact]
    public async Task ForegroundSessionKeepsItsOwnedCliProcessUntilTheRemoteCommandExits()
    {
        var child = new ControlledChildProcess();
        var factory = new ControlledChildProcessFactory(child);
        var runner = new DockerCliCommandRunner("controlled-docker", factory);
        var arguments = new[]
        {
            "--context", "desktop-linux", "exec", new string('a', 64),
            "/opt/duck/cm4-transport-control", "--server",
            "--run-id", "owned-run", "--engine-id", "owned-engine",
            "--cm4-resource-id", "opaque-cm4-key", "--cm4-container-id", new string('a', 64),
            "--cm4-generation", "4", "--helper-launch-id", "owned-run", "--cm4-ip", "172.28.0.4",
        };

        var session = runner.StartForegroundSession(arguments);
        await child.StandardOutputReader.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(session.HasExited);
        Assert.False(session.Completion.IsCompleted);
        Assert.Equal(arguments, factory.LastArguments);
        Assert.Equal(0, child.KillCalls);

        child.CompleteExit();
        Assert.Equal(0, await session.Completion.WaitAsync(TimeSpan.FromSeconds(1)));
        await session.DisposeAsync();
        Assert.Equal(0, child.KillCalls);
    }

    [Fact]
    public async Task CancellationKillsAndWaitsForTheOwnedCliProcess()
    {
        var child = new ControlledChildProcess(blockStandardOutput: true);
        var runner = CreateRunner(child);
        using var cancellation = new CancellationTokenSource();

        var operation = runner.ExecuteWithStdioAsync(
            ["exec", "-i", "owned-container-id"],
            "request\n",
            64,
            TimeSpan.FromSeconds(2),
            cancellation.Token);
        await child.StandardOutputReader.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        Assert.Equal(1, child.KillCalls);
        Assert.Equal(1, child.WaitCallsAfterKill);
        Assert.True(child.HasExited);
    }

    [Fact]
    public async Task TimeoutKillsAndWaitsForTheOwnedCliProcess()
    {
        var child = new ControlledChildProcess(blockStandardOutput: true);
        var runner = CreateRunner(child);

        await Assert.ThrowsAsync<TimeoutException>(() => runner.ExecuteWithStdioAsync(
            ["exec", "-i", "owned-container-id"],
            "request\n",
            64,
            TimeSpan.FromMilliseconds(120),
            CancellationToken.None));

        Assert.Equal(1, child.KillCalls);
        Assert.Equal(1, child.WaitCallsAfterKill);
        Assert.True(child.HasExited);
    }

    [Fact]
    public async Task OutputOverflowKillsAndWaitsForTheOwnedCliProcess()
    {
        var child = new ControlledChildProcess(standardOutput: new string('x', 100));
        var runner = CreateRunner(child);

        await Assert.ThrowsAsync<IOException>(() => runner.ExecuteWithStdioAsync(
            ["exec", "-i", "owned-container-id"],
            "request\n",
            16,
            TimeSpan.FromSeconds(1),
            CancellationToken.None));

        Assert.Equal(1, child.KillCalls);
        Assert.Equal(1, child.WaitCallsAfterKill);
        Assert.True(child.HasExited);
    }

    [Fact]
    public async Task FailureToReapIsReportedWithoutRestartingTheDeadline()
    {
        var child = new ControlledChildProcess(
            standardOutput: new string('x', 100),
            exitWhenKilled: false);
        var runner = CreateRunner(child);

        await Assert.ThrowsAsync<DockerCliProcessRecoveryException>(() => runner.ExecuteWithStdioAsync(
            ["exec", "-i", "owned-container-id"],
            "request\n",
            16,
            TimeSpan.FromMilliseconds(120),
            CancellationToken.None));

        Assert.Equal(1, child.KillCalls);
        Assert.True(child.WaitCallsAfterKill > 0);
        Assert.False(child.HasExited);
    }

    /// <summary>元の期限を使い切ったkill後に、新しい回収待ち期限を開始しないことを確認します。</summary>
    [Fact]
    public async Task KillThatConsumesTheOriginalDeadlineDoesNotStartAReapWait()
    {
        var child = new ControlledChildProcess(
            standardOutput: new string('x', 100),
            exitWhenKilled: false,
            killDelay: TimeSpan.FromMilliseconds(150));
        var runner = CreateRunner(child);

        await Assert.ThrowsAsync<DockerCliProcessRecoveryException>(() => runner.ExecuteWithStdioAsync(
            ["exec", "-i", "owned-container-id"],
            "request\n",
            16,
            TimeSpan.FromMilliseconds(120),
            CancellationToken.None));

        Assert.Equal(1, child.KillCalls);
        Assert.Equal(0, child.WaitCallsAfterKill);
        Assert.False(child.HasExited);
    }

    [Fact]
    public async Task UnreapedOwnedCliProcessBlocksTheNextCommand()
    {
        var child = new ControlledChildProcess(
            standardOutput: new string('x', 100),
            exitWhenKilled: false);
        var factory = new ControlledChildProcessFactory(
            child,
            new ControlledChildProcess(standardOutput: "metadata", alreadyExited: true));
        var runner = new DockerCliCommandRunner("controlled-docker", factory);

        await Assert.ThrowsAsync<DockerCliProcessRecoveryException>(() => runner.ExecuteWithStdioAsync(
            ["exec", "-i", "owned-container-id"],
            "request\n",
            16,
            TimeSpan.FromMilliseconds(120),
            CancellationToken.None));

        await Assert.ThrowsAsync<DockerCliProcessRecoveryException>(() => runner.CaptureBoundedAsync(
            ["inspect", "owned-container-id"],
            16,
            TimeSpan.FromSeconds(1),
            CancellationToken.None));

        Assert.Equal(1, factory.StartCalls);
        Assert.False(child.HasExited);
        Assert.Equal(1, child.KillCalls);

        child.CompleteExit();
        var nextResult = await runner.CaptureBoundedAsync(
            ["inspect", "owned-container-id"],
            16,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal(0, nextResult.ExitCode);
        Assert.Equal("metadata", nextResult.StandardOutput);
        Assert.Equal(2, factory.StartCalls);
    }

    [Fact]
    public async Task ConcurrentRecoveryPreventsStartingAnotherChildUntilReaped()
    {
        var firstChild = new ControlledChildProcess(
            standardOutput: new string('x', 100),
            exitWhenKilled: false);
        var secondChild = new ControlledChildProcess(
            standardOutput: new string('y', 100),
            exitWhenKilled: false);
        var nextChild = new ControlledChildProcess(standardOutput: "metadata", alreadyExited: true);
        var factory = new RoutingControlledChildProcessFactory(new Dictionary<string, ControlledChildProcess>
        {
            ["first-container"] = firstChild,
            ["second-container"] = secondChild,
            ["next-container"] = nextChild,
        });
        var runner = new DockerCliCommandRunner("controlled-docker", factory);

        var first = runner.CaptureBoundedAsync(
            ["inspect", "first-container"],
            16,
            TimeSpan.FromMilliseconds(450),
            CancellationToken.None);
        await firstChild.KillObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var second = runner.CaptureBoundedAsync(
            ["inspect", "second-container"],
            16,
            TimeSpan.FromMilliseconds(160),
            CancellationToken.None);
        await Assert.ThrowsAsync<TimeoutException>(() => second);
        await Assert.ThrowsAsync<DockerCliProcessRecoveryException>(() => first);

        Assert.Equal(1, factory.StartCalls);
        Assert.False(firstChild.HasExited);
        Assert.Equal(0, secondChild.KillCalls);

        var blockedWhileFirstUnreaped = await Assert.ThrowsAsync<DockerCliProcessRecoveryException>(() =>
            runner.CaptureBoundedAsync(
                ["inspect", "next-container"],
                16,
                TimeSpan.FromSeconds(1),
                CancellationToken.None));

        Assert.Contains("previous Docker CLI process", blockedWhileFirstUnreaped.Message, StringComparison.Ordinal);
        Assert.Equal(1, factory.StartCalls);

        firstChild.CompleteExit();
        var nextResult = await runner.CaptureBoundedAsync(
            ["inspect", "next-container"],
            16,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal(0, nextResult.ExitCode);
        Assert.Equal("metadata", nextResult.StandardOutput);
        Assert.Equal(2, factory.StartCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataCaptureStopsReadingWhenEitherOutputExceedsItsBound(bool overflowStandardError)
    {
        var child = new ControlledChildProcess(
            standardOutput: overflowStandardError ? "ok" : new string('x', 100),
            standardError: overflowStandardError ? new string('e', 100) : string.Empty,
            alreadyExited: true);
        var runner = CreateRunner(child);

        await Assert.ThrowsAsync<IOException>(() => runner.CaptureBoundedAsync(
            ["inspect", "owned-container-id"],
            16,
            TimeSpan.FromSeconds(1),
            CancellationToken.None));

        var overflowingReader = overflowStandardError ? child.StandardErrorReader : child.StandardOutputReader;
        Assert.InRange(overflowingReader.CharactersRead, 1, 17);
    }

    private static DockerCliCommandRunner CreateRunner(ControlledChildProcess child) =>
        new("controlled-docker", new ControlledChildProcessFactory(child));

    private sealed class ControlledChildProcessFactory : IDockerCliChildProcessFactory
    {
        private readonly Queue<ControlledChildProcess> children;

        public ControlledChildProcessFactory(ControlledChildProcess child, params ControlledChildProcess[] additionalChildren)
        {
            children = new Queue<ControlledChildProcess>([child, .. additionalChildren]);
        }

        public int StartCalls { get; private set; }
        public string[] LastArguments { get; private set; } = [];

        public IDockerCliChildProcess Start(ProcessStartInfo startInfo)
        {
            StartCalls++;
            Assert.Equal("controlled-docker", startInfo.FileName);
            Assert.False(startInfo.UseShellExecute);
            LastArguments = startInfo.ArgumentList.ToArray();
            return children.Dequeue();
        }
    }

    private sealed class RoutingControlledChildProcessFactory(
        IReadOnlyDictionary<string, ControlledChildProcess> children) : IDockerCliChildProcessFactory
    {
        public int StartCalls { get; private set; }

        public IDockerCliChildProcess Start(ProcessStartInfo startInfo)
        {
            StartCalls++;
            Assert.Equal("controlled-docker", startInfo.FileName);
            Assert.False(startInfo.UseShellExecute);
            return children[startInfo.ArgumentList.Last()];
        }
    }

    private sealed class ControlledChildProcess(
        string standardOutput = "",
        string standardError = "",
        bool blockStandardOutput = false,
        bool exitWhenKilled = true,
        bool alreadyExited = false,
        TimeSpan? killDelay = null) : IDockerCliChildProcess
    {
        private readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ControlledTextReader StandardOutputReader { get; } = new(standardOutput, blockStandardOutput);
        public ControlledTextReader StandardErrorReader { get; } = new(standardError, blockStandardOutput: false);
        public TextWriter StandardInput { get; } = new StringWriter();
        public TextReader StandardOutput => StandardOutputReader;
        public TextReader StandardError => StandardErrorReader;
        public bool HasExited { get; private set; } = alreadyExited;
        public int ExitCode => 0;
        public int KillCalls { get; private set; }
        public int WaitCallsAfterKill { get; private set; }
        public TaskCompletionSource KillObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            if (HasExited) return Task.CompletedTask;
            if (KillCalls > 0)
            {
                WaitCallsAfterKill++;
                return ConfirmExitAfterKillAsync(cancellationToken);
            }

            return exited.Task.WaitAsync(cancellationToken);
        }

        public void KillEntireProcessTree()
        {
            KillCalls++;
            KillObserved.TrySetResult();
            if (killDelay is { } delay) System.Threading.Thread.Sleep(delay);
            if (!exitWhenKilled) return;
            exited.TrySetResult();
        }

        public void CompleteExit()
        {
            HasExited = true;
            exited.TrySetResult();
        }

        private async Task ConfirmExitAfterKillAsync(CancellationToken cancellationToken)
        {
            await exited.Task.WaitAsync(cancellationToken);
            HasExited = true;
        }

        public void Dispose() => StandardInput.Dispose();
    }

    private sealed class ControlledTextReader(string value, bool blockStandardOutput) : TextReader
    {
        private int offset;

        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CharactersRead { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            if (blockStandardOutput)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (offset >= value.Length) return 0;
            var count = Math.Min(buffer.Length, value.Length - offset);
            value.AsMemory(offset, count).CopyTo(buffer);
            offset += count;
            CharactersRead += count;
            return count;
        }

        public override Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            var available = Math.Min(count, value.Length - offset);
            if (available <= 0) return Task.FromResult(0);
            value.CopyTo(offset, buffer, index, available);
            offset += available;
            CharactersRead += available;
            return Task.FromResult(available);
        }
    }
}
