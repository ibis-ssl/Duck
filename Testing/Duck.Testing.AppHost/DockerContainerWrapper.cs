using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Duck.Testing.AppHost;

/// <summary>Owns one exact Docker container for the lifetime of its Aspire executable resource.</summary>
public static class DockerContainerWrapper
{
    private static readonly TimeSpan ShutdownLimit = TimeSpan.FromSeconds(14);

    public static async Task<int> RunEntryPointAsync(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Docker wrapper expects one JSON launch-options argument.");
            return 2;
        }

        try
        {
            var options = JsonSerializer.Deserialize<DockerWrapperLaunchOptions>(args[0])
                ?? throw new InvalidOperationException("Docker wrapper options were empty.");
            return await RunAsync(options);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Docker wrapper failed: {exception.Message}");
            return 1;
        }
    }

    public static Task<int> RunAsync(DockerWrapperLaunchOptions options, CancellationToken shutdownToken = default) =>
        RunAsync(options, shutdownToken, StopChildProcessAsync, Console.Error.WriteLine);

    internal static Task<int> RunAsync(
        DockerWrapperLaunchOptions options,
        CancellationToken shutdownToken,
        Func<Process, TimeSpan, Task<bool>> stopChildProcessAsync) =>
        RunAsync(options, shutdownToken, stopChildProcessAsync, Console.Error.WriteLine);

    internal static async Task<int> RunAsync(
        DockerWrapperLaunchOptions options,
        CancellationToken shutdownToken,
        Func<Process, TimeSpan, Task<bool>> stopChildProcessAsync,
        Action<string> diagnosticWriter)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stopChildProcessAsync);
        ArgumentNullException.ThrowIfNull(diagnosticWriter);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        using var sigterm = OperatingSystem.IsWindows()
            ? null
            : PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                cancellation.Cancel();
            });

        var spec = new DockerContainerSpec(
            options.ResourceName,
            options.Image,
            options.ContainerArguments,
            options.Environment,
            options.StackId,
            options.RunId);
        using var health = StartHealthListener(options.HealthPort);
        using var ready = new ManualResetEventSlim(false);
        using var live = new ManualResetEventSlim(true);
        using var healthCancellation = new CancellationTokenSource();
        var healthTask = ServeHealthAsync(health, live, ready, healthCancellation.Token);
        await using var gameControllerReadiness = options.ReadinessProfile == "game-controller"
            ? new GameControllerReadinessMonitor(expectedInitialCommand: 0)
            : null;
        await using var visionReadiness = options.ReadinessProfile == "simulator"
            ? new VisionServiceReadiness()
            : null;
        var craneProbeDiagnostics = options.ReadinessProfile == "crane"
            ? new CraneProbeDiagnosticState()
            : null;
        Process? logProcess = null;
        string? containerId = null;
        var cleanupCompleted = true;

        try
        {
            var create = await RunDockerAsync(options.DockerExecutable, spec.CreateRunArguments(), cancellation.Token);
            if (create.ExitCode != 0)
            {
                throw new InvalidOperationException($"docker run failed: {create.StandardError.Trim()}");
            }

            containerId = create.StandardOutput.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(containerId))
            {
                throw new InvalidOperationException("docker run returned no container ID.");
            }

            await EnsureExactRunningOwnerAsync(spec, containerId, options.DockerExecutable, cancellation.Token);
            logProcess = StartLogFollower(options.DockerExecutable, containerId);
            await WaitForServiceReadinessAsync(
                spec, containerId, options, gameControllerReadiness, visionReadiness, craneProbeDiagnostics,
                diagnosticWriter, cancellation.Token);
            ready.Set();

            while (!cancellation.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellation.Token);
                await EnsureExactRunningOwnerAsync(spec, containerId, options.DockerExecutable, cancellation.Token);
                await EnsureExpectedProcessAsync(containerId, options, cancellation.Token);
                if (!await IsServiceReadyAsync(containerId, options, gameControllerReadiness, visionReadiness,
                    craneProbeDiagnostics, diagnosticWriter, cancellation.Token))
                {
                    throw new InvalidOperationException($"Service readiness became unhealthy for {spec.ResourceName}.");
                }

                if (logProcess.HasExited)
                {
                    throw new InvalidOperationException("Docker log follower exited unexpectedly.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Normal AppHost shutdown; cleanup runs below under its own bounded timeout.
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Wrapper resource {spec.ResourceName}: {exception.Message}");
            return 1;
        }
        finally
        {
            if (!cancellation.IsCancellationRequested && craneProbeDiagnostics is { AttemptCount: > 0 })
            {
                TryWriteCraneDiagnostic(diagnosticWriter, craneProbeDiagnostics.CreateFinalSummary());
            }

            var shutdownTimer = Stopwatch.StartNew();
            try
            {
                ready.Reset();
                live.Reset();
                Console.CancelKeyPress -= cancelHandler;
                healthCancellation.Cancel();
                health.Close();
                try
                {
                    await healthTask;
                }
                catch (HttpListenerException)
                {
                    // Closing the listener wakes its pending accept.
                }

                if (logProcess is not null)
                {
                    var reapTimeout = TimeSpan.FromMilliseconds(Math.Clamp(options.LogFollowerReapTimeoutMilliseconds, 0, 2000));
                    try
                    {
                        var logFollowerReaped = await stopChildProcessAsync(logProcess, reapTimeout);
                        if (!logFollowerReaped)
                        {
                            cleanupCompleted = false;
                        }
                    }
                    catch (Exception exception)
                    {
                        cleanupCompleted = false;
                        Console.Error.WriteLine($"Docker log follower could not be reaped: {exception.Message}");
                        try
                        {
                            await StopChildProcessAsync(logProcess, reapTimeout);
                        }
                        catch (Exception fallbackException)
                        {
                            Console.Error.WriteLine($"Docker log follower fallback reap failed: {fallbackException.Message}");
                        }
                        finally
                        {
                            logProcess.Dispose();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                cleanupCompleted = false;
                Console.Error.WriteLine($"Wrapper shutdown preparation failed for {spec.ResourceName}: {exception.Message}");
            }
            finally
            {
                var shutdownLimit = TimeSpan.FromSeconds(Math.Clamp(options.ShutdownTimeoutSeconds, 1, (int)ShutdownLimit.TotalSeconds));
                var remaining = shutdownLimit - shutdownTimer.Elapsed;
                using var cleanup = new CancellationTokenSource(remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(1));
                try
                {
                    await CleanupOwnedContainersAsync(spec, options.DockerExecutable, containerId, cleanup.Token);
                }
                catch (Exception exception)
                {
                    cleanupCompleted = false;
                    Console.Error.WriteLine($"Wrapper cleanup incomplete for {spec.ResourceName}: {exception.Message}");
                }
            }
        }

        return cleanupCompleted ? 0 : 1;
    }

    public static ProcessStartInfo CreateDockerStartInfo(string executable, IEnumerable<string> arguments) =>
        DockerCliProcess.CreateStartInfo(executable, arguments);

    private static HttpListener StartHealthListener(int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return listener;
    }

    private static async Task ServeHealthAsync(
        HttpListener listener,
        ManualResetEventSlim live,
        ManualResetEventSlim ready,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }

            var path = context.Request.Url?.AbsolutePath;
            var isHealthy = path switch
            {
                "/health/live" => live.IsSet,
                "/health/ready" => live.IsSet && ready.IsSet,
                _ => false,
            };
            context.Response.StatusCode = isHealthy ? (int)HttpStatusCode.OK : (int)HttpStatusCode.ServiceUnavailable;
            context.Response.Close();
        }
    }

    private static Process StartLogFollower(string executable, string containerId)
    {
        var process = Process.Start(DockerCliProcess.CreateStartInfo(executable, ["logs", "--follow", containerId]))
            ?? throw new InvalidOperationException("Could not start Docker log follower.");
        _ = ForwardOutputAsync(process.StandardOutput, Console.Out);
        _ = ForwardOutputAsync(process.StandardError, Console.Error);
        return process;
    }

    private static void TryWriteCraneDiagnostic(Action<string> diagnosticWriter, string jsonLine)
    {
        try
        {
            diagnosticWriter(jsonLine);
        }
        catch (Exception)
        {
            // Diagnostics must not prevent readiness retries or scoped cleanup.
        }
    }

    private static async Task ForwardOutputAsync(StreamReader source, TextWriter destination)
    {
        try
        {
            while (await source.ReadLineAsync() is { } line)
            {
                await destination.WriteLineAsync(line);
            }
        }
        catch (ObjectDisposedException)
        {
            // Process teardown closes redirected pipes.
        }
    }

    private static async Task EnsureExactRunningOwnerAsync(
        DockerContainerSpec spec,
        string containerId,
        string dockerExecutable,
        CancellationToken cancellationToken)
    {
        var inspect = await RunDockerAsync(
            dockerExecutable,
            DockerContainerSpec.CreateInspectArguments(containerId),
            cancellationToken);
        if (inspect.ExitCode != 0)
        {
            throw new InvalidOperationException($"Could not inspect owned container {containerId}: {inspect.StandardError.Trim()}");
        }

        using var json = JsonDocument.Parse(inspect.StandardOutput);
        if (!spec.IsExactOwner(json.RootElement, containerId))
        {
            throw new InvalidOperationException($"Container {containerId} did not match its exact owner, image, or host-network identity.");
        }

        if (!json.RootElement.GetProperty("State").GetProperty("Running").GetBoolean())
        {
            throw new InvalidOperationException($"Owned container {containerId} is no longer running.");
        }
    }

    private static async Task WaitForServiceReadinessAsync(
        DockerContainerSpec spec,
        string containerId,
        DockerWrapperLaunchOptions options,
        GameControllerReadinessMonitor? gameControllerReadiness,
        VisionServiceReadiness? visionReadiness,
        CraneProbeDiagnosticState? craneProbeDiagnostics,
        Action<string> diagnosticWriter,
        CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(options.StartupTimeoutSeconds))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureExactRunningOwnerAsync(spec, containerId, options.DockerExecutable, cancellationToken);
            var processReady = await EnsureExpectedProcessAsync(containerId, options, cancellationToken);
            var serviceReady = await IsServiceReadyAsync(
                containerId, options, gameControllerReadiness, visionReadiness, craneProbeDiagnostics,
                diagnosticWriter, cancellationToken);
            if (processReady && serviceReady)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException($"Service readiness timed out for {spec.ResourceName}.");
    }

    private static async Task<bool> EnsureExpectedProcessAsync(
        string containerId,
        DockerWrapperLaunchOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ExpectedProcessName))
        {
            return true;
        }

        var top = await RunDockerAsync(options.DockerExecutable, ["top", containerId], cancellationToken);
        if (top.ExitCode != 0)
        {
            throw new InvalidOperationException($"Could not inspect processes in owned container {containerId}: {top.StandardError.Trim()}");
        }

        return top.StandardOutput.Contains(options.ExpectedProcessName, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> IsServiceReadyAsync(
        string containerId,
        DockerWrapperLaunchOptions options,
        GameControllerReadinessMonitor? gameControllerReadiness,
        VisionServiceReadiness? visionReadiness,
        CraneProbeDiagnosticState? craneProbeDiagnostics,
        Action<string> diagnosticWriter,
        CancellationToken cancellationToken)
    {
        switch (options.ReadinessProfile)
        {
            case "game-controller":
                return gameControllerReadiness is not null &&
                    await gameControllerReadiness.IsHealthyAsync(cancellationToken);
            case "simulator":
                return visionReadiness?.IsReady == true;
            case "crane":
            {
                var probeTimer = Stopwatch.StartNew();
                var graph = await RunDockerAsync(
                    options.DockerExecutable,
                    DockerServiceReadiness.CreateCraneReadinessProbeArguments(containerId),
                    cancellationToken);
                if (craneProbeDiagnostics is null)
                {
                    return CraneProbeDiagnostics.Evaluate(graph.ExitCode, graph.StandardOutput, graph.StandardError,
                        probeTimer.Elapsed).Ready;
                }

                var outcome = CraneProbeDiagnostics.Evaluate(
                    graph.ExitCode, graph.StandardOutput, graph.StandardError, probeTimer.Elapsed);
                var progress = craneProbeDiagnostics.Record(outcome);
                if (progress is not null)
                {
                    TryWriteCraneDiagnostic(diagnosticWriter, CraneProbeDiagnostics.SerializeProgress(progress));
                }

                return outcome.Ready;
            }
            case "cm4-sim":
            {
                var listener = await RunDockerAsync(
                    options.DockerExecutable,
                    ["exec", containerId, "sh", "-c", DockerServiceReadiness.Cm4SimListenerScript],
                    cancellationToken);
                return listener.ExitCode == 0 && DockerServiceReadiness.Cm4SimOwnsListener(listener.StandardOutput);
            }
            default:
                return gameControllerReadiness is null ||
                    await gameControllerReadiness.IsHealthyAsync(cancellationToken);
        }
    }

    private static async Task CleanupOwnedContainersAsync(
        DockerContainerSpec spec,
        string dockerExecutable,
        string? knownContainerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(knownContainerId))
        {
            ids.Add(knownContainerId);
        }
        else
        {
            var discoveryTimer = Stopwatch.StartNew();
            do
            {
                var discovery = await RunDockerAsync(
                    dockerExecutable,
                    spec.CreateOwnerDiscoveryArguments(),
                    cancellationToken);
                if (discovery.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Could not discover interrupted container creation: {discovery.StandardError.Trim()}");
                }

                foreach (var id in discovery.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                {
                    ids.Add(id.Trim());
                }

                if (ids.Count > 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
            while (discoveryTimer.Elapsed < TimeSpan.FromSeconds(2));

            if (ids.Count == 0)
            {
                throw new InvalidOperationException("Could not prove whether a container was created before Docker CLI cancellation.");
            }
        }

        var inspectedOwnerFound = false;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inspect = await RunDockerAsync(
                dockerExecutable,
                DockerContainerSpec.CreateInspectArguments(id),
                cancellationToken);
            if (inspect.ExitCode != 0)
            {
                continue;
            }

            using var json = JsonDocument.Parse(inspect.StandardOutput);
            if (!spec.IsExactOwner(json.RootElement, id))
            {
                Console.Error.WriteLine($"Leaving container {id} untouched because exact owner inspection did not match.");
                continue;
            }
            inspectedOwnerFound = true;

            var running = json.RootElement.GetProperty("State").GetProperty("Running").GetBoolean();
            if (running)
            {
                var stop = await RunDockerAsync(dockerExecutable, ["stop", "--time", "3", id], cancellationToken);
                if (stop.ExitCode != 0)
                {
                    Console.Error.WriteLine($"docker stop failed for owned container {id}: {stop.StandardError.Trim()}");
                }
            }

            var remove = await RunDockerAsync(
                dockerExecutable,
                DockerContainerSpec.CreateRemoveArguments(id),
                cancellationToken);
            if (remove.ExitCode != 0)
            {
                throw new InvalidOperationException($"docker rm failed for owned container {id}: {remove.StandardError.Trim()}");
            }
        }

        if (ids.Count > 0 && !inspectedOwnerFound)
        {
            throw new InvalidOperationException("A candidate container was discovered, but its exact owner could not be verified; cleanup remains unresolved.");
        }
    }

    private static async Task<ProcessResult> RunDockerAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = DockerCliProcess.CreateStartInfo(executable, arguments) };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start Docker CLI '{executable}'.");
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw;
        }

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static async Task<bool> StopChildProcessAsync(Process process, TimeSpan wait)
    {
        if (process.HasExited)
        {
            await process.WaitForExitAsync();
            process.Dispose();
            return true;
        }

        process.Kill(entireProcessTree: true);
        using var timeout = new CancellationTokenSource(wait);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Docker log follower exceeded its reap deadline.");
            process.Dispose();
            return false;
        }

        process.Dispose();
        return true;
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
