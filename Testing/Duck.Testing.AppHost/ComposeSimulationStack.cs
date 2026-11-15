using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.WebSockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duck.Testing.AppHost;

internal sealed class ComposeServiceResource(string name) :
    Resource(name), IResourceWithWaitSupport, IResourceWithEndpoints, IResourceWithEnvironment;

internal sealed record ComposeBindMount(string Source, string Target, bool ReadOnly);

internal sealed record ComposePortBinding(
    ComposeServiceResource EndpointResource,
    string EndpointName,
    string OwnerServiceName,
    string HostAddress,
    int TargetPort,
    System.Net.Sockets.ProtocolType Protocol,
    bool WaitForHttpReady = false,
    bool WaitForWebSocketReady = false);

internal sealed record ComposeServiceEndpoint(string Name, int TargetPort, System.Net.Sockets.ProtocolType Protocol);
internal sealed record ComposeCraneReadiness(string ServiceName, string ExpectedTeamName);

internal sealed class ComposeServiceDefinition(
    ComposeServiceResource resource,
    string serviceName,
    string image,
    IReadOnlyList<string> command,
    IReadOnlyDictionary<string, string> environment,
    IReadOnlyList<ComposeBindMount> mounts,
    bool sharesOwnerNamespace,
    IReadOnlyList<string> dependsOn,
    string? entrypoint)
{
    public ComposeServiceResource Resource { get; } = resource;
    public string ServiceName { get; } = serviceName;
    public string Image { get; } = image;
    public IReadOnlyList<string> Command { get; } = command;
    public IReadOnlyDictionary<string, string> Environment { get; } = environment;
    public IReadOnlyList<ComposeBindMount> Mounts { get; } = mounts;
    public bool SharesOwnerNamespace { get; } = sharesOwnerNamespace;
    public IReadOnlyList<string> DependsOn { get; } = dependsOn;
    public string? Entrypoint { get; } = entrypoint;
}

internal sealed class ComposeSimulationStack(
    string projectName,
    string composeFilePath,
    string dockerExecutable,
    string? dockerContextName = null)
{
    public const string OwnerServiceName = "cm4-sim";
    private readonly List<ComposeServiceDefinition> services = [];
    private readonly List<ComposePortBinding> portBindings = [];
    private ComposeCraneReadiness? craneReadiness;

    public string ProjectName { get; } = projectName;
    public string ComposeFilePath { get; } = composeFilePath;
    public string DockerExecutable { get; } = dockerExecutable;
    public string? DockerContextName { get; } = dockerContextName;
    public IReadOnlyList<ComposeServiceDefinition> Services => services;
    public IReadOnlyList<ComposePortBinding> PortBindings => portBindings;
    public ComposeCraneReadiness? CraneReadiness => craneReadiness;

    public void RequireCraneReadiness(string serviceName, string expectedTeamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTeamName);
        if (craneReadiness is not null)
        {
            throw new InvalidOperationException("Compose stack Crane readiness can only be configured once.");
        }
        craneReadiness = new ComposeCraneReadiness(serviceName, expectedTeamName);
    }

    public ComposeServiceResource AddService(
        IDistributedApplicationBuilder builder,
        string resourceName,
        string serviceName,
        string image,
        IEnumerable<string>? command = null,
        IReadOnlyDictionary<string, string>? environment = null,
        IEnumerable<ComposeBindMount>? mounts = null,
        IEnumerable<ComposeServiceEndpoint>? endpoints = null,
        bool isOwner = false,
        string? entrypoint = null,
        IEnumerable<string>? dependsOn = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidateServiceName(serviceName);
        var resource = new ComposeServiceResource(resourceName);
        var resourceBuilder = builder.AddResource(resource)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Docker Compose service",
                State = new ResourceStateSnapshot(KnownResourceStates.NotStarted, KnownResourceStateStyles.Info),
                Properties = [],
            });
        _ = resourceBuilder;
        foreach (var endpoint in endpoints ?? [])
        {
            resourceBuilder.WithEndpoint(
                port: null,
                targetPort: endpoint.TargetPort,
                name: endpoint.Name,
                isProxied: false,
                protocol: endpoint.Protocol);
            var endpointAnnotation = resource.Annotations.OfType<EndpointAnnotation>()
                .Single(annotation => annotation.Name == endpoint.Name);
            endpointAnnotation.Port = null;
            endpointAnnotation.TargetHost = "127.0.0.1";
        }
        foreach (var (key, value) in environment ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            resourceBuilder.WithEnvironment(key, value);
        }
        services.Add(new ComposeServiceDefinition(
            resource,
            serviceName,
            image,
            command?.ToArray() ?? [],
            environment?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal),
            mounts?.ToArray() ?? [],
            !isOwner,
            dependsOn?.ToArray() ?? (isOwner ? [] : [OwnerServiceName]),
            entrypoint));
        return resource;
    }

    public void AddPublishedEndpoint(
        ComposeServiceResource endpointResource,
        string endpointName,
        int targetPort,
        System.Net.Sockets.ProtocolType protocol,
        bool waitForHttpReady = false,
        bool waitForWebSocketReady = false)
    {
        ArgumentNullException.ThrowIfNull(endpointResource);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        if (targetPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(targetPort));
        }

        var annotation = endpointResource.Annotations.OfType<EndpointAnnotation>()
            .SingleOrDefault(endpoint => endpoint.Name == endpointName)
            ?? throw new InvalidOperationException($"Endpoint '{endpointName}' is not registered on '{endpointResource.Name}'.");
        if (annotation.TargetPort != targetPort || annotation.Protocol != protocol)
        {
            throw new InvalidOperationException($"Endpoint '{endpointName}' does not match Compose target {protocol}/{targetPort}.");
        }

        portBindings.Add(new ComposePortBinding(
            endpointResource,
            endpointName,
            OwnerServiceName,
            "127.0.0.1",
            targetPort,
            protocol,
            waitForHttpReady,
            waitForWebSocketReady));
    }

    public string RenderYaml()
    {
        if (services.Count == 0 || services.Count(service => !service.SharesOwnerNamespace) != 1 ||
            !services.Any(service => service.ServiceName == OwnerServiceName && !service.SharesOwnerNamespace))
        {
            throw new InvalidOperationException("The Compose stack requires exactly one cm4-sim namespace owner.");
        }

        var yaml = new StringBuilder("services:\n");
        foreach (var service in services.OrderBy(service => service.SharesOwnerNamespace).ThenBy(service => service.ServiceName, StringComparer.Ordinal))
        {
            yaml.Append("  ").Append(service.ServiceName).AppendLine(":");
            yaml.Append("    image: ").Append(Quote(service.Image)).Append('\n');
            if (service.Entrypoint is not null)
            {
                yaml.Append("    entrypoint: [").Append(Quote(service.Entrypoint)).AppendLine("]");
            }
            if (service.Command.Count > 0)
            {
                yaml.Append("    command: [")
                    .Append(string.Join(", ", service.Command.Select(Quote)))
                    .AppendLine("]");
            }
            if (service.Environment.Count > 0)
            {
                yaml.AppendLine("    environment:");
                foreach (var (key, value) in service.Environment.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    yaml.Append("      - ").Append(Quote($"{key}={value}")).Append('\n');
                }
            }
            yaml.AppendLine("    labels:")
                .Append("      duck.aspire.owner: ").Append(Quote(DockerContainerSpec.OwnerValue)).Append('\n')
                .Append("      duck.aspire.stack: ").Append(Quote(ProjectName)).Append('\n')
                .Append("      duck.aspire.resource: ").Append(Quote(service.Resource.Name)).Append('\n');
            if (service.SharesOwnerNamespace)
            {
                yaml.Append("    network_mode: ").Append(Quote($"service:{OwnerServiceName}")).Append('\n');
            }
            if (service.DependsOn.Count > 0)
            {
                yaml.AppendLine("    depends_on:");
                foreach (var dependency in service.DependsOn)
                {
                    yaml.Append("      - ").Append(Quote(dependency)).Append('\n');
                }
            }
            if (!service.SharesOwnerNamespace)
            {
                var published = portBindings.Where(binding => binding.OwnerServiceName == service.ServiceName).ToArray();
                if (published.Length > 0)
                {
                    yaml.AppendLine("    ports:");
                    foreach (var binding in published)
                    {
                        var protocol = binding.Protocol == System.Net.Sockets.ProtocolType.Udp ? "/udp" : "/tcp";
                        yaml.Append("      - ").Append(Quote($"{binding.HostAddress}::{binding.TargetPort}{protocol}")).Append('\n');
                    }
                }
                yaml.AppendLine("    networks:")
                    .AppendLine("      - duck-simulation");
            }
            if (service.Mounts.Count > 0)
            {
                yaml.AppendLine("    volumes:");
                foreach (var mount in service.Mounts)
                {
                    yaml.AppendLine("      - type: bind")
                        .Append("        source: ").Append(Quote(mount.Source)).Append('\n')
                        .Append("        target: ").Append(Quote(mount.Target)).Append('\n')
                        .Append("        read_only: ").Append(mount.ReadOnly ? "true" : "false").Append('\n')
                        .AppendLine("        bind:")
                        .AppendLine("          create_host_path: false");
                }
            }
        }

        yaml.AppendLine("networks:")
            .AppendLine("  duck-simulation:")
            .AppendLine("    driver: bridge");
        return yaml.ToString();
    }

    private static string Quote(string value) => JsonSerializer.Serialize(value.Replace("$", "$$", StringComparison.Ordinal));

    private static void ValidateServiceName(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        if (serviceName.Any(character => !(char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '-' or '_')))
        {
            throw new ArgumentException("Compose service names must use lowercase ASCII letters, digits, hyphen, and underscore.", nameof(serviceName));
        }
    }
}

internal static class ComposeSimulationStackRegistration
{
    public static ComposeSimulationStack Add(
        IDistributedApplicationBuilder builder,
        string dockerExecutable,
        string? dockerContextName,
        string stackId,
        string runId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(dockerExecutable);
        var projectHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{stackId}\0{runId}")))
            .ToLowerInvariant()[..20];
        var projectName = $"duck-{projectHash}";
        var composeFilePath = Path.Combine(Path.GetTempPath(), $"{projectName}.compose.yaml");
        var stack = new ComposeSimulationStack(projectName, composeFilePath, dockerExecutable, dockerContextName);
        builder.Services.AddSingleton(stack);
        builder.Services.AddSingleton<ComposeSimulationStackOwner>();
        builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<ComposeSimulationStackOwner>());
        return stack;
    }
}

internal sealed record ComposeCommandResult(int ExitCode, string StandardOutput, string StandardError);

internal interface IComposeCommandRunner
{
    Task<ComposeCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    IAsyncDisposable StartLogs(string executable, IReadOnlyList<string> arguments, Action<string> writeLine);
}

internal sealed class DockerComposeCommandRunner : IComposeCommandRunner
{
    public async Task<ComposeCommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = DockerCliProcess.CreateStartInfo(executable, arguments);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("docker compose could not be started.");
        }

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
        return new ComposeCommandResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    public IAsyncDisposable StartLogs(string executable, IReadOnlyList<string> arguments, Action<string> writeLine) =>
        new DockerComposeLogFollower(executable, arguments, writeLine);

    private sealed class DockerComposeLogFollower : IAsyncDisposable
    {
        private readonly Process process;
        private readonly Task stdout;
        private readonly Task stderr;

        public DockerComposeLogFollower(string executable, IReadOnlyList<string> arguments, Action<string> writeLine)
        {
            var startInfo = DockerCliProcess.CreateStartInfo(executable, arguments);
            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
            {
                throw new InvalidOperationException("docker compose logs could not be started.");
            }
            stdout = PumpAsync(process.StandardOutput, writeLine);
            stderr = PumpAsync(process.StandardError, writeLine);
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // It exited between the check and kill.
                }
            }
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            }
            finally
            {
                process.Dispose();
            }
        }

        private static async Task PumpAsync(StreamReader reader, Action<string> writeLine)
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                writeLine(line);
            }
        }
    }
}

internal sealed class ComposeSimulationStackOwner : IHostedLifecycleService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private readonly ComposeSimulationStack stack;
    private readonly IComposeCommandRunner runner;
    private readonly ResourceNotificationService notifications;
    private readonly ResourceLoggerService resourceLogger;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly CancellationTokenSource monitorCancellation = new();
    private IAsyncDisposable? logs;
    private Task? monitorTask;
    private readonly object startupLock = new();
    private Task? startupTask;
    private bool started;

    public ComposeSimulationStackOwner(
        ComposeSimulationStack stack,
        ResourceNotificationService notifications,
        ResourceLoggerService resourceLogger,
        IHostApplicationLifetime applicationLifetime)
        : this(stack, new DockerComposeCommandRunner(), notifications, resourceLogger, applicationLifetime)
    {
    }

    internal ComposeSimulationStackOwner(
        ComposeSimulationStack stack,
        IComposeCommandRunner runner,
        ResourceNotificationService notifications,
        ResourceLoggerService resourceLogger,
        IHostApplicationLifetime applicationLifetime)
    {
        this.stack = stack;
        this.runner = runner;
        this.notifications = notifications;
        this.resourceLogger = resourceLogger;
        this.applicationLifetime = applicationLifetime;
    }

    /// <summary>
    /// Starts the Compose stack before ordinary hosted services. Aspire's DCP hosted service can
    /// wait for a RuntimeHost resource whose dependencies include these Compose resources, so
    /// starting the stack from StartAsync would leave DCP waiting for a service that cannot start
    /// until DCP returns.
    /// </summary>
    public Task StartingAsync(CancellationToken cancellationToken) => StartExecutionAsync(cancellationToken);

    /// <summary>
    /// Keeps direct hosting/test callers compatible while avoiding a second start after
    /// StartingAsync has already created the stack.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) => StartExecutionAsync(cancellationToken);

    private Task StartExecutionAsync(CancellationToken cancellationToken)
    {
        lock (startupLock)
        {
            startupTask ??= StartExecutionCoreAsync(cancellationToken);
            return startupTask;
        }
    }

    private async Task StartExecutionCoreAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(stack.ComposeFilePath)!);
        await File.WriteAllTextAsync(stack.ComposeFilePath, stack.RenderYaml(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await RunRequiredAsync(ComposeArguments("up", "--detach", "--wait", "--wait-timeout", "120"), cancellationToken)
                .ConfigureAwait(false);
            logs = runner.StartLogs(stack.DockerExecutable, ComposeArguments("logs", "--follow", "--no-color", "--timestamps"), WriteLog);
            await ProjectPublishedPortsAsync(cancellationToken).ConfigureAwait(false);
            await WaitForPublishedHttpEndpointsAsync(cancellationToken).ConfigureAwait(false);
            await WaitForCraneReadinessAsync(cancellationToken).ConfigureAwait(false);
            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.Running, KnownResourceStateStyles.Success, null)
                    .ConfigureAwait(false);
            }
            started = true;
            monitorTask = MonitorAsync(monitorCancellation.Token);
        }
        catch (Exception exception)
        {
            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error, exception.Message)
                    .ConfigureAwait(false);
            }
            if (logs is not null)
            {
                await logs.DisposeAsync().ConfigureAwait(false);
                logs = null;
            }
            await TryCleanupAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        monitorCancellation.Cancel();
        if (monitorTask is not null)
        {
            try
            {
                await monitorTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The AppHost is stopping; project cleanup happens after all other hosted services stop.
            }
        }
        if (logs is not null)
        {
            await logs.DisposeAsync().ConfigureAwait(false);
            logs = null;
        }
    }

    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        if (started)
        {
            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.Stopping, KnownResourceStateStyles.Info, null)
                    .ConfigureAwait(false);
            }
            await CleanupProjectAsync().ConfigureAwait(false);
            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.Finished, KnownResourceStateStyles.Success, null)
                    .ConfigureAwait(false);
                resourceLogger.Complete(service.Resource);
            }
        }
        TryDeleteComposeFile();
    }

    private async Task ProjectPublishedPortsAsync(CancellationToken cancellationToken)
    {
        foreach (var binding in stack.PortBindings)
        {
            var output = await RunRequiredAsync(
                ComposeArguments("port", binding.OwnerServiceName, binding.TargetPort.ToString(CultureInfo.InvariantCulture)),
                cancellationToken).ConfigureAwait(false);
            var endpoint = binding.EndpointResource.Annotations.OfType<EndpointAnnotation>()
                .Single(annotation => annotation.Name == binding.EndpointName);
            var (address, port) = ParsePublishedEndpoint(output);
            ComposeEndpointProjection.SetAllocatedPort(endpoint, address, port);
        }
    }

    private async Task WaitForPublishedHttpEndpointsAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        foreach (var binding in stack.PortBindings.Where(binding => binding.WaitForHttpReady || binding.WaitForWebSocketReady))
        {
            var endpoint = binding.EndpointResource.Annotations.OfType<EndpointAnnotation>()
                .Single(annotation => annotation.Name == binding.EndpointName);
            var allocation = endpoint.AllocatedEndpoint
                ?? throw new InvalidOperationException($"Compose did not allocate endpoint '{binding.EndpointName}'.");
            var uri = binding.WaitForWebSocketReady
                ? DcpBaseGameControllerReadinessProbe.CreateControlApiUri(
                    new Uri($"http://{allocation.Address}:{allocation.Port}"))
                : new Uri($"http://{allocation.Address}:{allocation.Port}/health/ready");
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (binding.WaitForWebSocketReady)
                    {
                        using var socket = new ClientWebSocket();
                        await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
                        if (socket.State == WebSocketState.Open)
                            break;
                    }
                    else
                    {
                        using var response = await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                        if (response.IsSuccessStatusCode)
                            break;
                    }
                }
                catch (Exception exception) when (exception is HttpRequestException or WebSocketException or IOException)
                {
                    // Compose reported running; wait briefly for the relay's existing readiness endpoint.
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"Compose endpoint '{binding.EndpointName}' did not become ready.");
            }
        }
    }

    private async Task WaitForCraneReadinessAsync(CancellationToken cancellationToken)
    {
        if (stack.CraneReadiness is not { } readiness)
        {
            return;
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(360);
        var probe = CraneProbeDiagnostics.CreateCraneReadinessProbeArguments("compose-container-placeholder", readiness.ExpectedTeamName)
            .Skip(2)
            .ToArray();
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await runner.RunAsync(
                stack.DockerExecutable,
                ComposeArguments(["exec", "-T", readiness.ServiceName, .. probe]),
                cancellationToken).ConfigureAwait(false);
            var outcome = CraneProbeDiagnostics.Evaluate(
                result.ExitCode, result.StandardOutput, result.StandardError, TimeSpan.Zero);
            if (outcome.Ready)
            {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The Compose Crane service did not pass the existing ROS coordinator and team readiness probe.");
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                var result = await runner.RunAsync(
                    stack.DockerExecutable,
                    ComposeArguments("ps", "--all", "--services", "--status", "running"),
                    cancellationToken).ConfigureAwait(false);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"docker compose ps failed: {result.StandardError.Trim()}");
                }
                var running = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet(StringComparer.Ordinal);
                var missing = stack.Services.Select(service => service.ServiceName)
                    .Where(serviceName => !running.Contains(serviceName)).ToArray();
                if (missing.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Compose services stopped unexpectedly: {string.Join(", ", missing)}.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal AppHost shutdown.
        }
        catch (Exception exception)
        {
            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.RuntimeUnhealthy, KnownResourceStateStyles.Error, exception.Message)
                    .ConfigureAwait(false);
            }
            applicationLifetime.StopApplication();
        }
    }

    private async Task<string> RunRequiredAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(stack.DockerExecutable, arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker {string.Join(' ', arguments)} failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }
        return result.StandardOutput.Trim();
    }

    private string[] ComposeArguments(params string[] arguments) =>
        stack.DockerContextName is null
            ? ["compose", "--project-name", stack.ProjectName, "--file", stack.ComposeFilePath, .. arguments]
            : ["--context", stack.DockerContextName, "compose", "--project-name", stack.ProjectName, "--file", stack.ComposeFilePath, .. arguments];

    private async Task PublishStateAsync(
        ComposeServiceResource resource,
        string state,
        string style,
        string? detail)
    {
        await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = new ResourceStateSnapshot(state, style),
            StartTimeStamp = state == KnownResourceStates.Running ? DateTime.UtcNow : snapshot.StartTimeStamp,
            StopTimeStamp = state == KnownResourceStates.Finished || state == KnownResourceStates.FailedToStart
                ? DateTime.UtcNow
                : snapshot.StopTimeStamp,
            Properties = string.IsNullOrWhiteSpace(detail)
                ? snapshot.Properties
                : snapshot.Properties.Add(new ResourcePropertySnapshot("detail", detail)),
        }).ConfigureAwait(false);
    }

    private void WriteLog(string line)
    {
        var separator = line.IndexOf('|');
        var serviceName = separator > 0 ? line[..separator].Trim().Trim('[', ']') : "docker-compose";
        var service = stack.Services.FirstOrDefault(candidate => candidate.ServiceName == serviceName);
        resourceLogger.GetLogger(service?.Resource ?? stack.Services[0].Resource)
            .LogInformation("{ComposeLogLine}", line);
    }

    private static (string Address, int Port) ParsePublishedEndpoint(string value)
    {
        var endpointText = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
        if (endpointText is null || !IPEndPoint.TryParse(endpointText, out var endpoint))
        {
            throw new InvalidOperationException($"docker compose port returned an invalid endpoint: '{value}'.");
        }
        return (endpoint.Address.ToString(), endpoint.Port);
    }

    private async Task TryCleanupAsync()
    {
        try
        {
            await CleanupProjectAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            resourceLogger.GetLogger(stack.Services[0].Resource)
                .LogError(exception, "Compose project cleanup failed after startup error for {ProjectName}.", stack.ProjectName);
        }
        TryDeleteComposeFile();
    }

    private async Task CleanupProjectAsync()
    {
        using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await RunRequiredAsync(
            ComposeArguments("down", "--remove-orphans", "--timeout", "15"),
            cleanupBudget.Token).ConfigureAwait(false);
    }

    private void TryDeleteComposeFile()
    {
        try
        {
            if (File.Exists(stack.ComposeFilePath))
            {
                File.Delete(stack.ComposeFilePath);
            }
        }
        catch (IOException exception)
        {
            resourceLogger.GetLogger(stack.Services[0].Resource)
                .LogWarning(exception, "Could not remove the temporary Compose file {ComposeFilePath}.", stack.ComposeFilePath);
        }
    }
}
