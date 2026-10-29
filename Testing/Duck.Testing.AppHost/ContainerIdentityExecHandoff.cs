using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Hosting;
using Tracker.RuntimeHost;
using Microsoft.Extensions.Logging;

namespace Duck.Testing.AppHost;

internal interface IDockerCommandRunner
{
    Task<string> CaptureAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);

    Task<ContainerIdentityControlExecResult> RelayAsync(
        IReadOnlyList<string> arguments,
        ResourceDiagnosticIdentity identity,
        string roleName,
        string? expectedBootId,
        CancellationToken cancellationToken);
}

internal sealed record DockerStdioCommandResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed class DockerCliProcessRecoveryException(string message, Exception innerException)
    : IOException(message, innerException);

internal interface IDockerStdioCommandRunner : IDockerCommandRunner
{
    Task<DockerStdioCommandResult> CaptureBoundedAsync(
        IReadOnlyList<string> arguments,
        int maximumOutputCharacters,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task<DockerStdioCommandResult> ExecuteWithStdioAsync(
        IReadOnlyList<string> arguments,
        string standardInput,
        int maximumOutputCharacters,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal interface IDockerCliChildProcess : IDisposable
{
    TextWriter StandardInput { get; }
    TextReader StandardOutput { get; }
    TextReader StandardError { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task WaitForExitAsync(CancellationToken cancellationToken);
    void KillEntireProcessTree();
}

internal interface IDockerCliChildProcessFactory
{
    IDockerCliChildProcess Start(ProcessStartInfo startInfo);
}

internal sealed record ContainerIdentityHandoffState(long Generation, string BootId);

internal enum IdentityHandoffTraceStage
{
    EngineCheck,
    ContainerInspect,
    IdentityExec,
    ResourceNotification,
    CurrentResourceState,
    ResourceProjection,
    ServiceStarted,
    ServiceStopped,
}

internal enum IdentityHandoffTraceOutcome
{
    Started,
    Accepted,
    Rejected,
}

/// <summary>
/// 実行段階と結果だけを記録する任意の診断出力です。
/// </summary>
internal sealed class SafeIdentityHandoffTrace(string filePath)
{
    // Duck/RefereeDriverの監視インスタンス間でも共有ログへのAppendを直列化します。
    private static readonly object Sync = new();

    internal void Record(IdentityHandoffTraceStage stage, IdentityHandoffTraceOutcome outcome)
    {
        var entry = JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            stage = stage.ToString(),
            outcome = outcome.ToString(),
        });
        lock (Sync)
        {
            File.AppendAllText(filePath, entry + Environment.NewLine, new System.Text.UTF8Encoding(false));
        }
    }
}

internal static class ContainerIdentityExecHandoff
{
    internal const string RunIdLabel = "com.ibis-ssl.duck.run-id";
    internal const string RoleLabel = "com.ibis-ssl.duck.role";

    /// <summary>
    /// 明示した実行先、所有ラベル、Docker実行環境を照合してから制御接続を中継します。
    /// </summary>
    internal static async Task<ContainerIdentityHandoffState?> TryHandoffAsync(
        RegisteredContainerRuntimeTarget target,
        string runId,
        long generation,
        string? expectedBootId,
        string contextName,
        string expectedEngineId,
        IDockerCommandRunner runner,
        CancellationToken cancellationToken,
        SafeIdentityHandoffTrace? trace = null,
        string executableAssembly = "Tracker.RuntimeHost.dll")
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(runner);
        if (!IsFullContainerId(target.ContainerId) ||
            string.IsNullOrWhiteSpace(target.ResourceId) ||
            string.IsNullOrWhiteSpace(target.RoleName) ||
            string.IsNullOrWhiteSpace(runId) ||
            string.IsNullOrWhiteSpace(contextName) ||
            string.IsNullOrWhiteSpace(expectedEngineId) ||
            generation <= 0 ||
            (executableAssembly != "Tracker.RuntimeHost.dll" &&
             executableAssembly != "Duck.Testing.RefereeDriver.Host.dll"))
        {
            return null;
        }

        try
        {
            trace?.Record(IdentityHandoffTraceStage.EngineCheck, IdentityHandoffTraceOutcome.Started);
            var engineId = await runner.CaptureAsync(
                ["--context", contextName, "info", "--format", "{{.ID}}"],
                cancellationToken);
            if (!string.Equals(engineId.Trim(), expectedEngineId, StringComparison.Ordinal))
            {
                trace?.Record(IdentityHandoffTraceStage.EngineCheck, IdentityHandoffTraceOutcome.Rejected);
                return null;
            }
            trace?.Record(IdentityHandoffTraceStage.EngineCheck, IdentityHandoffTraceOutcome.Accepted);

            trace?.Record(IdentityHandoffTraceStage.ContainerInspect, IdentityHandoffTraceOutcome.Started);
            var inspectionJson = await runner.CaptureAsync(
                ["--context", contextName, "inspect", "--format", "{{json .}}", target.ContainerId],
                cancellationToken);
            if (!MatchesOwnedRunningContainer(inspectionJson, target, runId))
            {
                trace?.Record(IdentityHandoffTraceStage.ContainerInspect, IdentityHandoffTraceOutcome.Rejected);
                return null;
            }
            trace?.Record(IdentityHandoffTraceStage.ContainerInspect, IdentityHandoffTraceOutcome.Accepted);

            var arguments = new[]
            {
                "--context", contextName, "exec", "-i", target.ContainerId,
                "dotnet", executableAssembly, "--identity-control",
            };
            trace?.Record(IdentityHandoffTraceStage.IdentityExec, IdentityHandoffTraceOutcome.Started);
            var identity = new ResourceDiagnosticIdentity(runId, target.ResourceId, target.ContainerId, generation);
            var result = await runner.RelayAsync(arguments, identity, target.RoleName, expectedBootId, cancellationToken);
            if (result.RequiresNewGeneration && !string.IsNullOrWhiteSpace(result.BootId))
            {
                generation = checked(generation + 1);
                identity = new ResourceDiagnosticIdentity(runId, target.ResourceId, target.ContainerId, generation);
                result = await runner.RelayAsync(arguments, identity, target.RoleName, result.BootId, cancellationToken);
            }

            var accepted = result.Accepted && !string.IsNullOrWhiteSpace(result.BootId);
            trace?.Record(
                IdentityHandoffTraceStage.IdentityExec,
                accepted ? IdentityHandoffTraceOutcome.Accepted : IdentityHandoffTraceOutcome.Rejected);
            return accepted ? new ContainerIdentityHandoffState(generation, result.BootId!) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            Win32Exception or
            JsonException or
            InvalidOperationException or
            TimeoutException)
        {
            return null;
        }
    }

    private static bool MatchesOwnedRunningContainer(
        string inspectionJson,
        RegisteredContainerRuntimeTarget target,
        string runId)
    {
        using var document = JsonDocument.Parse(inspectionJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("Id", out var idElement) ||
            idElement.ValueKind != JsonValueKind.String ||
            !string.Equals(idElement.GetString(), target.ContainerId, StringComparison.Ordinal) ||
            !root.TryGetProperty("State", out var stateElement) ||
            !stateElement.TryGetProperty("Status", out var statusElement) ||
            statusElement.ValueKind != JsonValueKind.String ||
            !string.Equals(statusElement.GetString(), "running", StringComparison.Ordinal) ||
            !root.TryGetProperty("Config", out var configElement) ||
            !configElement.TryGetProperty("Labels", out var labelsElement) ||
            labelsElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return HasLabel(labelsElement, RunIdLabel, runId) &&
               HasLabel(labelsElement, RoleLabel, target.RoleName);
    }

    private static bool HasLabel(JsonElement labels, string name, string expectedValue) =>
        labels.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expectedValue, StringComparison.Ordinal);

    private static bool IsFullContainerId(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f') ||
                  (character >= 'A' && character <= 'F')))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Docker CLIを構造化引数で起動し、メタデータ取得またはUDS中継を行います。
/// </summary>
internal sealed class DockerCliCommandRunner : IDockerStdioCommandRunner
{
    private readonly string executablePath;
    private readonly IDockerCliChildProcessFactory childProcessFactory;
    private readonly object processLock = new();
    private IDockerCliChildProcess? unreapedChildProcess;
    private Exception? unreapedChildFailure;

    internal DockerCliCommandRunner(
        string executablePath,
        IDockerCliChildProcessFactory? childProcessFactory = null)
    {
        this.executablePath = executablePath;
        this.childProcessFactory = childProcessFactory ?? new DockerCliChildProcessFactory();
    }

    /// <summary>
    /// 指定したメタデータコマンドを実行し、標準出力だけを呼出し元へ返します。
    /// </summary>
    public async Task<string> CaptureAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = Start(arguments, redirectInput: false);
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await WaitForExitAsync(process, cancellationToken);
        var output = await outputTask;
        _ = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new IOException("Docker metadata command failed.");
        }

        return output;
    }

    public async Task<DockerStdioCommandResult> ExecuteWithStdioAsync(
        IReadOnlyList<string> arguments,
        string standardInput,
        int maximumOutputCharacters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardInput);
        if (maximumOutputCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputCharacters));
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        var timer = Stopwatch.StartNew();
        using var operationDeadline = new CancellationTokenSource(GetOperationWindow(timeout));
        using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationDeadline.Token);
        var process = StartChildProcess(CreateStartInfo(arguments, redirectInput: true));
        var outputTask = ReadBoundedAsync(process.StandardOutput, maximumOutputCharacters, operationSource.Token);
        var errorTask = ReadBoundedAsync(process.StandardError, maximumOutputCharacters, operationSource.Token);
        var waitTask = process.WaitForExitAsync(operationSource.Token);
        var recovered = false;
        var retainedForRecovery = false;
        try
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), operationSource.Token);
            await process.StandardInput.FlushAsync(operationSource.Token);
            process.StandardInput.Close();
            await AwaitAllProcessTasksAsync(waitTask, outputTask, errorTask, operationSource.Token).ConfigureAwait(false);
            var output = await outputTask;
            var error = await errorTask;
            recovered = true;
            return new(process.ExitCode, output, error);
        }
        catch (Exception exception)
        {
            try
            {
                await RecoverProcessAsync(process, waitTask, outputTask, errorTask, timer, timeout, exception)
                    .ConfigureAwait(false);
            }
            catch (DockerCliProcessRecoveryException recoveryException)
            {
                RetainUnreapedChild(process, recoveryException);
                retainedForRecovery = true;
                throw;
            }

            recovered = true;
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Docker stdio command was canceled after its child process exited.", exception, cancellationToken);
            }

            if (operationDeadline.IsCancellationRequested || exception is OperationCanceledException)
            {
                throw new TimeoutException("Docker stdio command exceeded its deadline.", exception);
            }

            throw;
        }
        finally
        {
            if (!retainedForRecovery && (recovered || process.HasExited)) process.Dispose();
        }
    }

    public async Task<DockerStdioCommandResult> CaptureBoundedAsync(
        IReadOnlyList<string> arguments,
        int maximumOutputCharacters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (maximumOutputCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputCharacters));
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        var timer = Stopwatch.StartNew();
        using var operationDeadline = new CancellationTokenSource(GetOperationWindow(timeout));
        using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationDeadline.Token);
        var process = StartChildProcess(CreateStartInfo(arguments, redirectInput: false));
        var outputTask = ReadBoundedAsync(process.StandardOutput, maximumOutputCharacters, operationSource.Token);
        var errorTask = ReadBoundedAsync(process.StandardError, maximumOutputCharacters, operationSource.Token);
        var waitTask = process.WaitForExitAsync(operationSource.Token);
        var recovered = false;
        var retainedForRecovery = false;
        try
        {
            await AwaitAllProcessTasksAsync(waitTask, outputTask, errorTask, operationSource.Token).ConfigureAwait(false);
            recovered = true;
            return new(process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
        }
        catch (Exception exception)
        {
            try
            {
                await RecoverProcessAsync(process, waitTask, outputTask, errorTask, timer, timeout, exception)
                    .ConfigureAwait(false);
            }
            catch (DockerCliProcessRecoveryException recoveryException)
            {
                RetainUnreapedChild(process, recoveryException);
                retainedForRecovery = true;
                throw;
            }

            recovered = true;
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Docker metadata command was canceled after its child process exited.", exception, cancellationToken);
            }

            if (operationDeadline.IsCancellationRequested || exception is OperationCanceledException)
            {
                throw new TimeoutException("Docker metadata command exceeded its deadline.", exception);
            }

            throw;
        }
        finally
        {
            if (!retainedForRecovery && (recovered || process.HasExited)) process.Dispose();
        }
    }

    private static async Task AwaitAllProcessTasksAsync(
        Task waitTask,
        Task<string> outputTask,
        Task<string> errorTask,
        CancellationToken cancellationToken)
    {
        var pending = new List<Task> { waitTask, outputTask, errorTask };
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completed = await Task.WhenAny(pending).ConfigureAwait(false);
            await completed.ConfigureAwait(false);
            pending.Remove(completed);
        }
    }

    private static TimeSpan GetOperationWindow(TimeSpan totalBudget)
    {
        var cleanupReserve = TimeSpan.FromMilliseconds(Math.Min(250, Math.Max(1, totalBudget.TotalMilliseconds / 4)));
        var operationWindow = totalBudget - cleanupReserve;
        return operationWindow > TimeSpan.Zero ? operationWindow : TimeSpan.FromTicks(1);
    }

    private static async Task RecoverProcessAsync(
        IDockerCliChildProcess process,
        Task waitTask,
        Task<string> outputTask,
        Task<string> errorTask,
        Stopwatch timer,
        TimeSpan timeout,
        Exception cause)
    {
        if (!process.HasExited)
        {
            try
            {
                process.KillEntireProcessTree();
            }
            catch (Exception killException)
            {
                cause = new AggregateException(cause, killException);
            }
        }

        var remaining = timeout - timer.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            if (!process.HasExited)
            {
                throw new DockerCliProcessRecoveryException("Docker CLI process exit was not confirmed before the original deadline.", cause);
            }
        }
        else if (!process.HasExited)
        {
            using var recoveryDeadline = new CancellationTokenSource(remaining);
            try
            {
                await process.WaitForExitAsync(recoveryDeadline.Token).ConfigureAwait(false);
            }
            catch (Exception recoveryException) when (recoveryException is OperationCanceledException or InvalidOperationException)
            {
                if (!process.HasExited)
                {
                    throw new DockerCliProcessRecoveryException("Docker CLI process exit was not confirmed before the original deadline.",
                        new AggregateException(cause, recoveryException));
                }
            }
        }

        await ObserveCompletedTaskAsync(waitTask).ConfigureAwait(false);
        await DrainTaskWithinDeadlineAsync(outputTask, timer, timeout).ConfigureAwait(false);
        await DrainTaskWithinDeadlineAsync(errorTask, timer, timeout).ConfigureAwait(false);
    }

    private static async Task ObserveCompletedTaskAsync(Task task)
    {
        if (!task.IsCompleted)
        {
            return;
        }

        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private static async Task DrainTaskWithinDeadlineAsync(Task task, Stopwatch timer, TimeSpan timeout)
    {
        if (!task.IsCompleted)
        {
            var remaining = timeout - timer.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new DockerCliProcessRecoveryException("Docker CLI output streams were not drained before the original deadline.",
                    new TimeoutException("No remaining process recovery budget."));
            }

            try
            {
                await task.WaitAsync(remaining).ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                throw new DockerCliProcessRecoveryException("Docker CLI output streams were not drained before the original deadline.", exception);
            }
            catch (OperationCanceledException) when (task.IsCanceled)
            {
            }
        }

        await ObserveCompletedTaskAsync(task).ConfigureAwait(false);
    }

    private static async Task<string> ReadBoundedAsync(
        TextReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder(Math.Min(maximumCharacters, 1024));
        while (true)
        {
            var buffer = new char[Math.Min(1024, maximumCharacters - output.Length + 1)];
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) return output.ToString();
            if (output.Length + count > maximumCharacters)
            {
                throw new IOException("Docker stdio output exceeded its size limit.");
            }

            output.Append(buffer, 0, count);
        }
    }

    /// <summary>
    /// 短命なコンテナ内プロセスとローカルUDSの間で制御データを中継します。
    /// </summary>
    public async Task<ContainerIdentityControlExecResult> RelayAsync(
        IReadOnlyList<string> arguments,
        ResourceDiagnosticIdentity identity,
        string roleName,
        string? expectedBootId,
        CancellationToken cancellationToken)
    {
        using var process = Start(arguments, redirectInput: true);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            var result = await ContainerIdentityControlExecSession.RunAsync(
                process.StandardOutput,
                process.StandardInput,
                identity,
                roleName,
                expectedBootId,
                cancellationToken,
                TimeSpan.FromSeconds(5));
            if (result.RequiresNewGeneration)
            {
                TryKill(process);
                await WaitForExitAsync(process, cancellationToken);
                _ = await errorTask;
                return result;
            }

            if (!result.Accepted) TryKill(process);
            await WaitForExitAsync(process, cancellationToken);
            _ = await errorTask;
            return process.ExitCode == 0 ? result : result with { Accepted = false, RequiresNewGeneration = false };
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private Process Start(IReadOnlyList<string> arguments, bool redirectInput)
    {
        lock (processLock)
        {
            ClearExitedUnreapedChild();
            return Process.Start(CreateStartInfo(arguments, redirectInput)) ?? throw new IOException("Docker process did not start.");
        }
    }

    private IDockerCliChildProcess StartChildProcess(ProcessStartInfo startInfo)
    {
        lock (processLock)
        {
            ClearExitedUnreapedChild();
            return childProcessFactory.Start(startInfo);
        }
    }

    private void RetainUnreapedChild(IDockerCliChildProcess process, Exception failure)
    {
        lock (processLock)
        {
            unreapedChildProcess = process;
            unreapedChildFailure = failure;
        }
    }

    private void ClearExitedUnreapedChild()
    {
        if (unreapedChildProcess is null) return;
        if (!unreapedChildProcess.HasExited)
        {
            throw new DockerCliProcessRecoveryException(
                "A previous Docker CLI process has not been confirmed exited; a new command was not started.",
                unreapedChildFailure ?? new IOException("The previous process exit is unknown."));
        }

        unreapedChildProcess.Dispose();
        unreapedChildProcess = null;
        unreapedChildFailure = null;
    }

    private ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments, bool redirectInput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardInput = redirectInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private static async Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed class DockerCliChildProcessFactory : IDockerCliChildProcessFactory
    {
        public IDockerCliChildProcess Start(ProcessStartInfo startInfo) =>
            new DockerCliChildProcess(Process.Start(startInfo) ?? throw new IOException("Docker process did not start."));
    }

    private sealed class DockerCliChildProcess(Process process) : IDockerCliChildProcess
    {
        public TextWriter StandardInput => process.StandardInput;
        public TextReader StandardOutput => process.StandardOutput;
        public TextReader StandardError => process.StandardError;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);

        public void KillEntireProcessTree()
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void Dispose() => process.Dispose();
    }
}

/// <summary>
/// 登録済みコンテナの通知と現在状態を照合し、明示選択時だけ識別情報を受け渡します。
/// </summary>
internal sealed class ContainerIdentityHandoffService(
    ResourceNotificationService notifications,
    IResource registeredResource,
    string expectedRole,
    string runId,
    string contextName,
    string expectedEngineId,
    IDockerCommandRunner runner,
    ILogger<ContainerIdentityHandoffService> logger,
    string? safeTracePath = null,
    string executableAssembly = "Tracker.RuntimeHost.dll") : BackgroundService, IHostedLifecycleService
{
    private readonly object startupLock = new();
    private Task? startupTask;

    private readonly SafeIdentityHandoffTrace? trace = string.IsNullOrWhiteSpace(safeTracePath)
        ? null
        : new SafeIdentityHandoffTrace(safeTracePath);

    internal bool IsSafeTraceEnabled => trace is not null;

    /// <summary>
    /// Aspireが資源を起動する前に通知監視を開始します。
    /// </summary>
    public Task StartingAsync(CancellationToken cancellationToken) => StartExecutionAsync(cancellationToken);

    /// <summary>
    /// lifecycle開始後の二重起動を避け、単独利用時は通常の開始を維持します。
    /// </summary>
    public override Task StartAsync(CancellationToken cancellationToken) => StartExecutionAsync(cancellationToken);

    /// <summary>
    /// 開始後のlifecycle通知を受け取ります。
    /// </summary>
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 基底サービスの停止処理へ進む前のlifecycle通知を受け取ります。
    /// </summary>
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 停止後のlifecycle通知を受け取ります。
    /// </summary>
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private Task StartExecutionAsync(CancellationToken cancellationToken)
    {
        lock (startupLock)
        {
            startupTask ??= base.StartAsync(cancellationToken);
            return startupTask;
        }
    }

    /// <summary>
    /// 資源イベントを監視し、現在も一致する所有コンテナだけへhandoffします。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        trace?.Record(IdentityHandoffTraceStage.ServiceStarted, IdentityHandoffTraceOutcome.Started);
        try
        {
            string? lastContainerId = null;
            string? lastBootId = null;
            long generation = 0;
            var restartObserved = false;
            await foreach (var observation in notifications.WatchAsync(stoppingToken))
            {
                if (!ReferenceEquals(observation.Resource, registeredResource)) continue;
                trace?.Record(IdentityHandoffTraceStage.ResourceNotification, IdentityHandoffTraceOutcome.Accepted);
                var candidateResourceId = observation.ResourceId;
                if (!notifications.TryGetCurrentState(candidateResourceId, out var currentState))
                {
                    trace?.Record(IdentityHandoffTraceStage.CurrentResourceState, IdentityHandoffTraceOutcome.Rejected);
                    continue;
                }
                trace?.Record(IdentityHandoffTraceStage.CurrentResourceState, IdentityHandoffTraceOutcome.Accepted);
                if (!RegisteredContainerEventProjection.HasStarted(currentState))
                {
                    trace?.Record(IdentityHandoffTraceStage.ResourceProjection, IdentityHandoffTraceOutcome.Rejected);
                    restartObserved = true;
                    continue;
                }

                if (!RegisteredContainerEventProjection.TryProject(
                        registeredResource, candidateResourceId, expectedRole, observation, currentState, out var target))
                {
                    trace?.Record(IdentityHandoffTraceStage.ResourceProjection, IdentityHandoffTraceOutcome.Rejected);
                    continue;
                }
                trace?.Record(IdentityHandoffTraceStage.ResourceProjection, IdentityHandoffTraceOutcome.Accepted);

                if (generation == 0 || restartObserved || !string.Equals(lastContainerId, target!.ContainerId, StringComparison.Ordinal))
                {
                    generation = checked(generation + 1);
                    lastBootId = null;
                    restartObserved = false;
                }

                var state = await ContainerIdentityExecHandoff.TryHandoffAsync(
                    target!, runId, generation, lastBootId, contextName, expectedEngineId, runner,
                    stoppingToken, trace, executableAssembly);
                if (state is not null)
                {
                    generation = state.Generation;
                    lastContainerId = target!.ContainerId;
                    lastBootId = state.BootId;
                }
                else
                {
                    logger.LogWarning("Duck runtime identity handoff did not complete for the current resource state.");
                }
            }
        }
        finally
        {
            trace?.Record(
                IdentityHandoffTraceStage.ServiceStopped,
                stoppingToken.IsCancellationRequested
                    ? IdentityHandoffTraceOutcome.Accepted
                    : IdentityHandoffTraceOutcome.Rejected);
        }
    }
}
