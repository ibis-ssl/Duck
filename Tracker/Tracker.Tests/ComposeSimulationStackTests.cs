using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tracker.Tests;

public sealed class ComposeSimulationStackTests
{
    [Fact]
    public async Task ComposeOwnerPublishesDynamicEndpointAndStopsOnlyItsProject()
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-test-stack", Path.Combine(Path.GetTempPath(), $"duck-test-{Guid.NewGuid():N}.yaml"), "docker");
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "simulator", "simulator", "simulator:locked");
        _ = stack.AddService(builder, "crane", "crane", "crane:locked");
        stack.RequireCraneReadiness("crane", "Ibis");
        var gameController = stack.AddService(
            builder,
            "game-controller",
            "game-controller",
            "ssl-game-controller:locked",
            endpoints: [new ComposeServiceEndpoint("control", 8082, System.Net.Sockets.ProtocolType.Tcp)]);
        stack.AddPublishedEndpoint(gameController, "control", 8082, System.Net.Sockets.ProtocolType.Tcp);

        using var app = builder.Build();
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        var resourceLogger = app.Services.GetRequiredService<ResourceLoggerService>();
        var runner = new FakeComposeCommandRunner();
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var owner = new ComposeSimulationStackOwner(stack, runner, notifications, resourceLogger, lifetime);
        var endpoint = Assert.Single(gameController.Annotations.OfType<EndpointAnnotation>());
        var endpointReference = new EndpointReference(gameController, endpoint);

        await owner.StartAsync(CancellationToken.None);

        Assert.Equal("49173", await endpointReference.Property(EndpointProperty.Port).GetValueAsync());
        var manifest = await File.ReadAllTextAsync(stack.ComposeFilePath);
        Assert.Contains("network_mode: \"service:cm4-sim\"", manifest);
        Assert.Contains("simulator:locked", manifest);
        Assert.Contains("crane:locked", manifest);
        Assert.Contains("ssl-game-controller:locked", manifest);
        Assert.Equal(3, manifest.Split("network_mode: \"service:cm4-sim\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("networks:\n      - duck-simulation", manifest[(manifest.IndexOf("  simulator:", StringComparison.Ordinal))..]);
        Assert.Equal("cm4:locked", stack.Services.Single(service => service.ServiceName == "cm4-sim").Image);
        Assert.Equal("ssl-game-controller:locked", stack.Services.Single(service => service.ServiceName == "game-controller").Image);
        Assert.Contains(runner.Calls, call => call.Contains("exec", StringComparer.Ordinal) && call.Contains("crane", StringComparer.Ordinal));

        await owner.StopAsync(CancellationToken.None);
        await owner.StoppedAsync(CancellationToken.None);

        Assert.Contains(runner.Calls, call => call.Contains("up", StringComparer.Ordinal));
        var down = Assert.Single(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.Contains("--project-name", down);
        Assert.Contains("duck-test-stack", down);
        Assert.DoesNotContain("-v", down);
        Assert.False(File.Exists(stack.ComposeFilePath));
    }

    [Fact]
    public async Task ComposeStartupFailureRunsProjectScopedCleanup()
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-failed-stack", Path.Combine(Path.GetTempPath(), $"duck-failed-{Guid.NewGuid():N}.yaml"), "docker");
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "relay", "duck-udp-boundary-relay", "relay:locked");

        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner(failUp: true, throwOnInspect: true);
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.StartingAsync(CancellationToken.None));

        Assert.Contains("exit 17", error.Message, StringComparison.Ordinal);
        var down = Assert.Single(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.Contains("duck-failed-stack", down);
        Assert.DoesNotContain("-v", down);
        Assert.Contains(runner.Calls, call => call.Contains("inspect", StringComparer.Ordinal));
        Assert.Contains(runner.Calls, call => call.Contains("duck-udp-boundary-relay", StringComparer.Ordinal));
        var diagnostics = await File.ReadAllTextAsync(stack.RelayFailureDiagnosticsPath);
        Assert.Contains("relay-state-capture-IOException", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(stack.ComposeFilePath));
    }

    [Fact]
    public async Task RelayStartupFailureCapturesAllowlistedStateBeforeProjectCleanupWithoutLogs()
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-relay-diagnostic-stack", Path.Combine(Path.GetTempPath(), $"duck-relay-diagnostic-{Guid.NewGuid():N}.yaml"), "docker");
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "relay", "duck-udp-boundary-relay", "relay:locked");

        using var app = builder.Build();
        var relayContainerId = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var runner = new FakeComposeCommandRunner(
            failUp: true,
            relayContainerId: relayContainerId,
            relayStateJson: $$"""
                {"Status":"exited","Running":false,"ExitCode":139,"Error":"{{new string('x', 5000)}} token=hidden-value","OOMKilled":false,"FinishedAt":"2026-11-15T12:00:00Z"}
                """);
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.StartingAsync(CancellationToken.None));

        Assert.Contains("exit 17", error.Message, StringComparison.Ordinal);
        var diagnosticsJson = await File.ReadAllTextAsync(stack.RelayFailureDiagnosticsPath);
        using var diagnostics = System.Text.Json.JsonDocument.Parse(diagnosticsJson);
        var root = diagnostics.RootElement;
        Assert.Equal("duck-udp-boundary-relay", root.GetProperty("ServiceName").GetString());
        Assert.Equal(relayContainerId, root.GetProperty("ContainerId").GetString());
        var state = root.GetProperty("State");
        Assert.Equal(139, state.GetProperty("ExitCode").GetInt32());
        var stateError = state.GetProperty("Error").GetString();
        Assert.StartsWith("[truncated]\n", stateError, StringComparison.Ordinal);
        Assert.True(stateError!.Length <= 4096 + "[truncated]\n".Length);
        Assert.Contains("token=[REDACTED]", stateError, StringComparison.Ordinal);
        Assert.False(state.GetProperty("OOMKilled").GetBoolean());
        Assert.Equal("2026-11-15T12:00:00Z", state.GetProperty("FinishedAt").GetString());
        Assert.False(root.TryGetProperty("CombinedContainerLogs", out _));
        Assert.DoesNotContain("hidden-value", diagnosticsJson, StringComparison.Ordinal);

        var projectStatus = runner.Calls.FindIndex(call => call.Contains("ps", StringComparer.Ordinal));
        var inspect = runner.Calls.FindIndex(call => call.Contains("inspect", StringComparer.Ordinal));
        var cleanup = runner.Calls.FindIndex(call => call.Contains("down", StringComparer.Ordinal));
        Assert.True(projectStatus >= 0 && projectStatus < inspect && inspect < cleanup);
        Assert.Contains("--project-name", runner.Calls[projectStatus]);
        Assert.Contains("duck-relay-diagnostic-stack", runner.Calls[projectStatus]);
        Assert.Contains("duck-udp-boundary-relay", runner.Calls[projectStatus]);
        Assert.Equal(relayContainerId, runner.Calls[inspect][^1]);
        Assert.DoesNotContain(runner.Calls, call => call.Contains("logs", StringComparer.Ordinal));
        Assert.Contains("duck-relay-diagnostic-stack", runner.Calls[cleanup]);
        Assert.False(File.Exists(stack.ComposeFilePath));
    }

    [Fact]
    public async Task ComposeOwnerStartsBeforeDcpHostedServiceWaitsForComposeResources()
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-lifecycle-stack", Path.Combine(Path.GetTempPath(), $"duck-lifecycle-{Guid.NewGuid():N}.yaml"), "docker");
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);

        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner();
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(app.Services.GetRequiredService<ResourceNotificationService>());
                services.AddSingleton(app.Services.GetRequiredService<ResourceLoggerService>());
                services.AddSingleton<IHostedService>(new DcpStartupProbe(
                    () => runner.Calls.Any(call => call.Contains("up", StringComparer.Ordinal))));
                services.AddSingleton<IHostedService>(serviceProvider => new ComposeSimulationStackOwner(
                    stack,
                    runner,
                    serviceProvider.GetRequiredService<ResourceNotificationService>(),
                    serviceProvider.GetRequiredService<ResourceLoggerService>(),
                    serviceProvider.GetRequiredService<IHostApplicationLifetime>()));
            })
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Contains(runner.Calls, call => call.Contains("up", StringComparer.Ordinal));
        Assert.Contains(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.False(File.Exists(stack.ComposeFilePath));
    }

    private static IDistributedApplicationBuilder CreateBuilder() => DistributedApplication.CreateBuilder(
        new DistributedApplicationOptions
        {
            Args = [],
            AssemblyName = typeof(ComposeSimulationStackTests).Assembly.GetName().Name,
            ProjectDirectory = Projects.Duck_Testing_AppHost.ProjectPath,
            DisableDashboard = true,
        });

    private sealed class FakeComposeCommandRunner(
        bool failUp = false,
        string? relayContainerId = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        string? relayStateJson = null,
        bool throwOnInspect = false) : IComposeCommandRunner
    {
        public List<string[]> Calls { get; } = [];

        public Task<ComposeCommandResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            Calls.Add(arguments.ToArray());
            if (failUp && arguments.Contains("up", StringComparer.Ordinal))
            {
                return Task.FromResult(new ComposeCommandResult(17, "", "fixture startup failure"));
            }
            if (arguments.Contains("ps", StringComparer.Ordinal) && arguments.Contains("--quiet", StringComparer.Ordinal))
            {
                return Task.FromResult(new ComposeCommandResult(0, relayContainerId ?? "", ""));
            }
            if (arguments.Contains("inspect", StringComparer.Ordinal))
            {
                if (throwOnInspect)
                {
                    throw new IOException("secret token=hidden-value");
                }
                return Task.FromResult(new ComposeCommandResult(
                    0,
                    relayStateJson ?? "{\"Status\":\"exited\",\"Running\":false,\"ExitCode\":0,\"Error\":\"\",\"OOMKilled\":false,\"FinishedAt\":\"\"}",
                    ""));
            }
            if (arguments.Contains("port", StringComparer.Ordinal))
            {
                return Task.FromResult(new ComposeCommandResult(0, "127.0.0.1:49173", ""));
            }
            if (arguments.Contains("exec", StringComparer.Ordinal))
            {
                return Task.FromResult(new ComposeCommandResult(0, "DUCK_CRANE_ROS_SETUP_OK\n/session_controller\n", ""));
            }
            return Task.FromResult(new ComposeCommandResult(0, "", ""));
        }

        public IAsyncDisposable StartLogs(string executable, IReadOnlyList<string> arguments, Action<string> writeLine) =>
            new FakeLogFollower();
    }

    private sealed class DcpStartupProbe(Func<bool> composeStarted) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Assert.True(composeStarted(), "The Compose stack must be started before DCP waits on its resources.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeLogFollower : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
