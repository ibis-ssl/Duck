using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tracker.Tests;

public sealed class ComposeSimulationStackTests
{
    [Fact]
    public async Task HostNetworkingCanPublishAndResolveOnlyTheBridgeIngressService()
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-mixed-network-stack", Path.Combine(Path.GetTempPath(), $"duck-mixed-{Guid.NewGuid():N}.yaml"), "docker",
            useHostNetworking: true);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "udp-relay", "udp-relay", "crane:locked", entrypoint: "perl");
        var ingress = stack.AddService(
            builder,
            "udp-ingress",
            "udp-ingress",
            "crane:locked",
            entrypoint: "perl",
            endpoints: [new ComposeServiceEndpoint("tracker-ingress", 39112, System.Net.Sockets.ProtocolType.Udp)],
            usesBridgeNetwork: true);
        stack.AddPublishedEndpoint(ingress, "tracker-ingress", 39112, System.Net.Sockets.ProtocolType.Udp);

        var manifest = stack.RenderYaml();

        Assert.Contains("udp-relay:", manifest);
        Assert.Contains("network_mode: \"service:cm4-sim\"", manifest);
        Assert.Contains("udp-ingress:", manifest);
        Assert.Contains("127.0.0.1::39112/udp", manifest);
        var ingressSection = manifest[manifest.IndexOf("  udp-ingress:", StringComparison.Ordinal)..];
        Assert.Contains("networks:", ingressSection);
        Assert.Contains("duck-simulation", ingressSection);
        Assert.DoesNotContain("network_mode: \"host\"", ingressSection);
        Assert.Contains("driver: bridge", manifest);
        Assert.Equal("udp-ingress", Assert.Single(stack.PortBindings).OwnerServiceName);

        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner();
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>());
        await owner.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal(49173, ingress.Annotations.OfType<EndpointAnnotation>().Single().AllocatedEndpoint?.Port);
            Assert.Contains(runner.Calls, call => call.SequenceEqual(["compose", "--project-name", "duck-mixed-network-stack", "--file", stack.ComposeFilePath, "port", "udp-ingress", "39112"]));
        }
        finally
        {
            await owner.StopAsync(CancellationToken.None);
            await owner.StoppedAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComposeOwnerProjectsEndpointAndStopsOnlyItsProject(bool useHostNetworking)
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-test-stack", Path.Combine(Path.GetTempPath(), $"duck-test-{Guid.NewGuid():N}.yaml"), "docker",
            useHostNetworking: useHostNetworking);
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
        var runner = new FakeComposeCommandRunner(rejectPortQueries: useHostNetworking);
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var owner = new ComposeSimulationStackOwner(stack, runner, notifications, resourceLogger, lifetime);
        var endpoint = Assert.Single(gameController.Annotations.OfType<EndpointAnnotation>());
        var endpointReference = new EndpointReference(gameController, endpoint);

        await owner.StartAsync(CancellationToken.None);

        Assert.Equal(useHostNetworking ? "8082" : "49173", await endpointReference.Property(EndpointProperty.Port).GetValueAsync());
        Assert.Equal("127.0.0.1", endpoint.AllocatedEndpoint?.Address);
        Assert.Equal(!useHostNetworking, runner.Calls.Any(call => call.Contains("port", StringComparer.Ordinal)));
        var manifest = await File.ReadAllTextAsync(stack.ComposeFilePath);
        Assert.Contains("network_mode: \"service:cm4-sim\"", manifest);
        Assert.Contains("simulator:locked", manifest);
        Assert.Contains("crane:locked", manifest);
        Assert.Contains("ssl-game-controller:locked", manifest);
        Assert.Equal(3, manifest.Split("network_mode: \"service:cm4-sim\"", StringSplitOptions.None).Length - 1);
        if (useHostNetworking)
        {
            Assert.Contains("network_mode: \"host\"", manifest);
            Assert.DoesNotContain("ports:", manifest);
            Assert.DoesNotContain("networks:", manifest);
        }
        Assert.DoesNotContain("networks:\n      - duck-simulation", manifest[(manifest.IndexOf("  simulator:", StringComparison.Ordinal))..]);
        Assert.Equal("cm4:locked", stack.Services.Single(service => service.ServiceName == "cm4-sim").Image);
        Assert.Equal("ssl-game-controller:locked", stack.Services.Single(service => service.ServiceName == "game-controller").Image);
        Assert.Contains(runner.Calls, call => call.Contains("exec", StringComparer.Ordinal) && call.Contains("crane", StringComparer.Ordinal));

        await owner.StopAsync(CancellationToken.None);
        await owner.StoppedAsync(CancellationToken.None);

        var up = Assert.Single(runner.Calls, call => call.Contains("up", StringComparer.Ordinal));
        Assert.Equal(new[] { "compose", "--project-name", "duck-test-stack", "--file", stack.ComposeFilePath, "up", "--pull", "never", "--detach", "--wait", "--wait-timeout", "120" }, up);
        var down = Assert.Single(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.Contains("--project-name", down);
        Assert.Contains("duck-test-stack", down);
        Assert.DoesNotContain("-v", down);
        Assert.False(File.Exists(stack.ComposeFilePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComposeOwnerReplacementRecreatesNamespaceFollowersWithoutPullingImages(bool useHostNetworking)
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-recreate-stack", Path.Combine(Path.GetTempPath(), $"duck-recreate-{Guid.NewGuid():N}.yaml"), "docker",
            useHostNetworking: useHostNetworking);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "simulator", "simulator", "simulator:locked");
        var gameController = stack.AddService(builder, "game-controller", "game-controller", "ssl-game-controller:locked",
            endpoints: [new ComposeServiceEndpoint("control", 8082, System.Net.Sockets.ProtocolType.Tcp)]);
        stack.AddPublishedEndpoint(gameController, "control", 8082, System.Net.Sockets.ProtocolType.Tcp);
        _ = stack.AddService(builder, "crane", "crane", "crane:locked");

        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner(rejectPortQueries: useHostNetworking);
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>(),
            pollInterval: TimeSpan.FromMilliseconds(10),
            ownerReplacementWaitTimeout: TimeSpan.FromSeconds(2));

        await owner.StartAsync(CancellationToken.None);
        try
        {
            runner.OwnerContainerId = null;
            await runner.OwnerUnavailableObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
            runner.OwnerContainerId = new string('b', 64);
            await runner.FollowersRecreated.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await runner.RecoveryStateObserved.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var endpoint = Assert.Single(gameController.Annotations.OfType<EndpointAnnotation>());
            Assert.Equal(useHostNetworking ? 8082 : 49173, endpoint.AllocatedEndpoint?.Port);
            Assert.Equal(!useHostNetworking, runner.Calls.Any(call => call.Contains("port", StringComparer.Ordinal)));
        }
        finally
        {
            await owner.StopAsync(CancellationToken.None);
            await owner.StoppedAsync(CancellationToken.None);
        }

        var recreate = Assert.Single(runner.Calls, call => call.Contains("--force-recreate", StringComparer.Ordinal));
        Assert.Contains("--pull", recreate);
        Assert.Contains("never", recreate);
        var noDependenciesIndex = Array.IndexOf(recreate, "--no-deps");
        Assert.True(noDependenciesIndex >= 0);
        Assert.Equal(
            ["simulator", "game-controller", "crane"],
            recreate[(noDependenciesIndex + 1)..]);
    }

    [Fact]
    public async Task ComposeStartupFailureRunsProjectScopedCleanup()
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-failed-stack", Path.Combine(Path.GetTempPath(), $"duck-failed-{Guid.NewGuid():N}.yaml"), "docker");
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner(failUp: true);
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.StartingAsync(CancellationToken.None));

        Assert.Contains("exit 17", error.Message, StringComparison.Ordinal);
        var up = Assert.Single(runner.Calls, call => call.Contains("up", StringComparer.Ordinal));
        Assert.Contains("--pull", up);
        Assert.Contains("never", up);
        var down = Assert.Single(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.Contains("duck-failed-stack", down);
        Assert.DoesNotContain("-v", down);
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

    private sealed class FakeComposeCommandRunner(bool failUp = false, bool rejectPortQueries = false) : IComposeCommandRunner
    {
        public List<string[]> Calls { get; } = [];
        public string? OwnerContainerId { get; set; } = new('a', 64);
        public TaskCompletionSource<bool> FollowersRecreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> RecoveryStateObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> OwnerUnavailableObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (arguments.Contains("--quiet", StringComparer.Ordinal))
            {
                if (OwnerContainerId is null)
                {
                    OwnerUnavailableObserved.TrySetResult(true);
                    return Task.FromResult(new ComposeCommandResult(0, "", ""));
                }
                return Task.FromResult(new ComposeCommandResult(0, OwnerContainerId, ""));
            }
            if (arguments.Contains("--services", StringComparer.Ordinal))
            {
                if (FollowersRecreated.Task.IsCompleted)
                    RecoveryStateObserved.TrySetResult(true);
                return Task.FromResult(new ComposeCommandResult(0, "cm4-sim\nsimulator\ngame-controller\ncrane", ""));
            }
            if (arguments.Contains("--force-recreate", StringComparer.Ordinal))
            {
                FollowersRecreated.TrySetResult(true);
            }
            if (arguments.Contains("port", StringComparer.Ordinal))
            {
                Assert.False(rejectPortQueries, "Host networking must not query published ports.");
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
