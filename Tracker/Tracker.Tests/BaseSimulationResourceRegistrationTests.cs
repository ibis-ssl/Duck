using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Duck.Testing.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Tracker.Tests;

public sealed class BaseSimulationResourceRegistrationTests
{
    [Fact]
    public async Task StandardCm4RestartGuardIsInstalledBeforeSelectedSimulatorStarts()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-base-restart-before-start-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);
        var restartCalls = 0;

        try
        {
            var resources = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346", "a52b6bd", "2020B", "None", "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a", "visibility_graph",
                    "base-restart-before-start-test", "crane", "yellow", Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: true, DuckRunId: "run-base-restart-before-start-test",
                    CraneTargetAddressSettings: null, CraneDiagnosticsPath: null,
                    DockerContextName: null, ExpectedEngineId: null, DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { },
                appHostAssetRoot: fixtureRoot);

            var simulator = resources.Simulator.Resource;
            simulator.Annotations.Add(new ResourceCommandAnnotation(
                KnownResourceCommands.RestartCommand,
                "Restart simulator",
                _ => ResourceCommandState.Enabled,
                _ =>
                {
                    Interlocked.Increment(ref restartCalls);
                    return Task.FromResult(new ExecuteCommandResult { Success = true });
                },
                "Restart the simulator",
                arguments: null,
                confirmationMessage: null,
                iconName: "ArrowCounterclockwise",
                iconVariant: IconVariant.Regular,
                isHighlighted: false));

            await using var application = builder.Build();
            var eventing = application.Services.GetRequiredService<IDistributedApplicationEventing>();
            await eventing.PublishAsync(
                new BeforeResourceStartedEvent(simulator, application.Services),
                EventDispatchBehavior.BlockingSequential,
                CancellationToken.None);

            var missingHelper = await Assert.ThrowsAsync<InvalidOperationException>(() => eventing.PublishAsync(
                new BeforeResourceStartedEvent(resources.Cm4Simulator!.Resource, application.Services),
                EventDispatchBehavior.BlockingSequential,
                CancellationToken.None));
            Assert.Contains("CM4 transport helper is missing", missingHelper.Message, StringComparison.Ordinal);

            var result = await application.Services.GetRequiredService<ResourceCommandService>()
                .ExecuteCommandAsync(simulator, KnownResourceCommands.RestartCommand, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(0, restartCalls);
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StandardCm4SimulatorRestartIsInterceptedAfterResourcesAreCreated()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-base-restart-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);
        var restartCalls = 0;

        try
        {
            var resources = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346", "a52b6bd", "2020B", "None", "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a", "visibility_graph",
                    "base-restart-guard-test", "crane", "yellow", Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: true, DuckRunId: "run-base-restart-guard-test",
                    CraneTargetAddressSettings: null, CraneDiagnosticsPath: null,
                    DockerContextName: null, ExpectedEngineId: null, DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { });

            var simulator = resources.Simulator.Resource;
            simulator.Annotations.Add(new ResourceCommandAnnotation(
                KnownResourceCommands.RestartCommand,
                "Restart simulator",
                _ => ResourceCommandState.Enabled,
                _ =>
                {
                    Interlocked.Increment(ref restartCalls);
                    return Task.FromResult(new ExecuteCommandResult { Success = true });
                },
                "Restart the simulator",
                arguments: null,
                confirmationMessage: null,
                iconName: "ArrowCounterclockwise",
                iconVariant: IconVariant.Regular,
                isHighlighted: false));
            var originalCommand = Assert.Single(
                simulator.Annotations.OfType<ResourceCommandAnnotation>(),
                command => command.Name == KnownResourceCommands.RestartCommand);

            await using var application = builder.Build();
            var model = application.Services.GetRequiredService<DistributedApplicationModel>();
            var eventing = application.Services.GetRequiredService<IDistributedApplicationEventing>();
            var createdEvent = new AfterResourcesCreatedEvent(application.Services, model);
            await eventing.PublishAsync(createdEvent, EventDispatchBehavior.BlockingSequential, CancellationToken.None);
            await eventing.PublishAsync(createdEvent, EventDispatchBehavior.BlockingSequential, CancellationToken.None);

            var guardedCommand = Assert.Single(
                simulator.Annotations.OfType<ResourceCommandAnnotation>(),
                command => command.Name == KnownResourceCommands.RestartCommand);
            Assert.NotSame(originalCommand, guardedCommand);
            Assert.Same(originalCommand.UpdateState, guardedCommand.UpdateState);
            Assert.Equal(originalCommand.DisplayName, guardedCommand.DisplayName);
            Assert.Equal(originalCommand.DisplayDescription, guardedCommand.DisplayDescription);
            Assert.Equal(originalCommand.IconName, guardedCommand.IconName);
            Assert.Equal(originalCommand.IconVariant, guardedCommand.IconVariant);
            Assert.Equal(originalCommand.Visibility, guardedCommand.Visibility);
            Assert.Null(guardedCommand.Progress);

            var result = await application.Services.GetRequiredService<ResourceCommandService>()
                .ExecuteCommandAsync(simulator, KnownResourceCommands.RestartCommand, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(0, restartCalls);
            Assert.Single(simulator.Annotations.OfType<ResourceCommandAnnotation>(),
                command => command.Name == KnownResourceCommands.RestartCommand);
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void StandardCraneHealthRegistrationUsesTheCraneServiceConsumer()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-base-health-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);

        try
        {
            _ = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346", "a52b6bd", "2020B", "None", "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a", "visibility_graph",
                    "base-health-test", "crane", "yellow", Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: true, DuckRunId: "run-base-health-test",
                    CraneTargetAddressSettings: null, CraneDiagnosticsPath: null,
                    DockerContextName: null, ExpectedEngineId: null, DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { });

            using var services = builder.Services.BuildServiceProvider();
            var registration = Assert.Single(
                services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
                item => item.Name == "dcp-crane-service-ready");
            var healthCheck = registration.Factory(services);

            var dcpHealthCheck = Assert.IsType<DcpBaseResourceReadinessHealthCheck>(healthCheck);
            Assert.NotNull(dcpHealthCheck.CraneReadinessProbe);
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StandardGameControllerHealthRequiresItsControlApiEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-base-gc-health-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);
        Uri? probedEndpoint = null;
        IResource? registeredGameController = null;
        var apiReachable = false;
        var endpointResolutions = 0;
        var apiCalls = 0;
        var cancelOnProbe = false;
        var delayPastBudget = false;
        CancellationTokenSource? callerCancellation = null;

        try
        {
            var resources = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346", "a52b6bd", "2020B", "None", "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a", "visibility_graph",
                    "base-gc-health-test", "crane", "yellow", Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: true, DuckRunId: "run-base-gc-health-test",
                    CraneTargetAddressSettings: null, CraneDiagnosticsPath: null,
                    DockerContextName: null, ExpectedEngineId: null, DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { },
                gameControllerControlEndpointResolver: (endpoint, _) =>
                {
                    endpointResolutions++;
                    Assert.Equal("game-controller-control", endpoint.EndpointName);
                    Assert.Same(registeredGameController, endpoint.Resource);
                    return Task.FromResult(new Uri("http://127.0.0.1:49123/", UriKind.Absolute));
                },
                gameControllerControlApiProbe: async (endpoint, _) =>
                {
                    apiCalls++;
                    probedEndpoint = endpoint;
                    if (cancelOnProbe)
                    {
                        callerCancellation!.Cancel();
                        return true;
                    }
                    if (delayPastBudget)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(2100), CancellationToken.None);
                        return true;
                    }
                    return apiReachable;
                });

            registeredGameController = resources.GameController.Resource;
            using var services = builder.Services.BuildServiceProvider();
            var registry = services.GetRequiredService<DcpBaseResourceReadinessRegistry>();
            var gameController = resources.GameController.Resource;
            var endpointAnnotation = Assert.Single(
                gameController.Annotations.OfType<EndpointAnnotation>(),
                endpoint => endpoint.Name == "game-controller-control");
            Assert.Null(endpointAnnotation.Port);
            Assert.Equal("127.0.0.1", endpointAnnotation.TargetHost);
            registry.ObserveCurrent(new("gc-runtime-key", "game-controller", new string('a', 64)), "Running");
            var candidate = Assert.Single(registry.GetCandidates(), item => ReferenceEquals(item.Resource, gameController));
            var registration = Assert.Single(
                services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
                item => item.Name == "dcp-game-controller-service-ready");
            var healthCheck = registration.Factory(services);

            var missingOwnership = await healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                CancellationToken.None);
            Assert.Equal(HealthStatus.Unhealthy, missingOwnership.Status);
            Assert.Equal(0, endpointResolutions);
            Assert.Equal(0, apiCalls);

            Assert.True(registry.TryMarkIdentityVerified(
                [new DcpBaseResourceOwnershipProof(
                    gameController, candidate.ResourceId, candidate.ContainerId!, candidate.Generation,
                    "expected-engine", "aspire-default-network")],
                sharedStandardDcpNetworkVerified: true));
            var result = await healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                CancellationToken.None);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("control API", result.Description, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("ws://127.0.0.1:49123/api/control", probedEndpoint?.AbsoluteUri);
            Assert.False(registry.Get(gameController)!.IsReady);

            apiReachable = true;
            var connected = await healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                CancellationToken.None);
            Assert.Equal(HealthStatus.Healthy, connected.Status);
            Assert.True(registry.Get(gameController)!.IsReady);

            apiReachable = false;
            var disconnected = await healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                CancellationToken.None);
            Assert.Equal(HealthStatus.Unhealthy, disconnected.Status);
            Assert.False(registry.Get(gameController)!.IsReady);

            callerCancellation = new CancellationTokenSource();
            cancelOnProbe = true;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                callerCancellation.Token));
            Assert.False(registry.Get(gameController)!.IsReady);

            cancelOnProbe = false;
            delayPastBudget = true;
            var lateSuccess = await healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                CancellationToken.None);
            Assert.Equal(HealthStatus.Unhealthy, lateSuccess.Status);
            Assert.False(registry.Get(gameController)!.IsReady);
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StandardGameControllerHealthOnlyReadiesTheCurrentOwnedGeneration()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-base-gc-current-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);
        DcpBaseResourceReadinessRegistry? registry = null;
        IResource? gameController = null;
        var apiCalls = 0;
        var cancelDuringResolve = false;
        var delayDuringResolve = false;
        var replaceCandidateDuringResolve = false;
        CancellationTokenSource? resolverCancellation = null;

        try
        {
            var resources = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346", "a52b6bd", "2020B", "None", "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a", "visibility_graph",
                    "base-gc-current-test", "crane", "yellow", Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: true, DuckRunId: "run-base-gc-current-test",
                    CraneTargetAddressSettings: null, CraneDiagnosticsPath: null,
                    DockerContextName: null, ExpectedEngineId: null, DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { },
                gameControllerControlEndpointResolver: async (endpoint, _) =>
                {
                    Assert.Equal("game-controller-control", endpoint.EndpointName);
                    Assert.Same(gameController, endpoint.Resource);
                    if (cancelDuringResolve)
                    {
                        resolverCancellation!.Cancel();
                    }
                    if (delayDuringResolve)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(2100), CancellationToken.None);
                    }
                    if (replaceCandidateDuringResolve)
                    {
                        registry!.ObserveUnavailable(gameController!, "gc-runtime-key", "Stopped");
                        registry.ObserveCurrent(new("gc-next-key", "game-controller", new string('b', 64)), "Running");
                    }
                    return new Uri("http://127.0.0.1:49124/", UriKind.Absolute);
                },
                gameControllerControlApiProbe: (_, _) =>
                {
                    apiCalls++;
                    return Task.FromResult(true);
                });

            using var services = builder.Services.BuildServiceProvider();
            registry = services.GetRequiredService<DcpBaseResourceReadinessRegistry>();
            gameController = resources.GameController.Resource;
            var endpointAnnotation = Assert.Single(
                gameController.Annotations.OfType<EndpointAnnotation>(),
                endpoint => endpoint.Name == "game-controller-control");
            Assert.Null(endpointAnnotation.Port);
            registry.ObserveCurrent(new("gc-runtime-key", "game-controller", new string('a', 64)), "Running");
            var candidate = Assert.Single(registry.GetCandidates(), item => ReferenceEquals(item.Resource, gameController));
            Assert.True(registry.TryMarkIdentityVerified(
                [new DcpBaseResourceOwnershipProof(
                    gameController, candidate.ResourceId, candidate.ContainerId!, candidate.Generation,
                    "expected-engine", "aspire-default-network")],
                sharedStandardDcpNetworkVerified: true));
            var registration = Assert.Single(
                services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
                item => item.Name == "dcp-game-controller-service-ready");

            var healthCheck = registration.Factory(services);
            using var resolverCancellationSource = new CancellationTokenSource();
            resolverCancellation = resolverCancellationSource;
            cancelDuringResolve = true;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                resolverCancellationSource.Token));
            Assert.Equal(0, apiCalls);
            Assert.False(registry.Get(gameController)!.IsReady);

            cancelDuringResolve = false;
            delayDuringResolve = true;
            var resolverTimedOut = await healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                CancellationToken.None);
            Assert.Equal(HealthStatus.Unhealthy, resolverTimedOut.Status);
            Assert.Equal(0, apiCalls);
            Assert.False(registry.Get(gameController)!.IsReady);

            delayDuringResolve = false;
            replaceCandidateDuringResolve = true;
            var result = await healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = registration },
                CancellationToken.None);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            var snapshot = Assert.IsType<DcpBaseResourceReadinessSnapshot>(registry.Get(gameController));
            Assert.Equal("gc-next-key", snapshot.ResourceId);
            Assert.Equal(new string('b', 64), snapshot.ContainerId);
            Assert.Equal(0, apiCalls);
            Assert.False(snapshot.IsReady);
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BaseRegistrationUsesDcpOwnedContainerResourcesForTheSelectedSimulationServices()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-base-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);

        try
        {
            var resources = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346",
                    "a52b6bd",
                    "2020B",
                    "None",
                    "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a",
                    "visibility_graph",
                    "base-test-" + Guid.NewGuid().ToString("N"),
                    "crane",
                    "yellow",
                    Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: true,
                    DuckRunId: "run-base-test",
                    CraneTargetAddressSettings: null,
                    CraneDiagnosticsPath: null,
                    DockerContextName: null,
                    ExpectedEngineId: null,
                    DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { });

            Assert.IsType<ContainerResource>(resources.Simulator.Resource);
            Assert.IsType<ContainerResource>(resources.GameController.Resource);
            Assert.IsType<ContainerResource>(resources.Cm4Simulator!.Resource);
            Assert.IsType<ContainerResource>(resources.Crane.Resource);
            var cm4HelperMount = Assert.Single(
                resources.Cm4Simulator.Resource.Annotations.OfType<ContainerMountAnnotation>());
            Assert.Equal(
                Path.Combine(AppContext.BaseDirectory, BaseSimulationResourceRegistration.Cm4TransportHelperAssetName),
                cm4HelperMount.Source);
            Assert.Equal(BaseSimulationResourceRegistration.Cm4TransportHelperContainerPath, cm4HelperMount.Target);
            Assert.True(cm4HelperMount.IsReadOnly);
            Assert.Contains(
                resources.Simulator.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
                command => command.Name == "reconfigure-cm4-transport");
            Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(DcpBaseResourceReadinessRegistry));
            Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(DcpBaseResourceReadinessEventConsumer));
            Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IHostedService));
            Assert.Contains(builder.Services, descriptor =>
                descriptor.ServiceType == typeof(DcpBaseResourceOwnershipEventConsumer));
            Assert.Contains(builder.Services, descriptor =>
                descriptor.ServiceType == typeof(IOwnedCm4SimulatorReconfigurationAdapter));
            Assert.Contains(builder.Services, descriptor =>
                descriptor.ServiceType == typeof(OwnedCm4SimulatorReconfigurationCoordinator));
            using (var serviceProvider = builder.Services.BuildServiceProvider())
            {
                Assert.NotNull(serviceProvider.GetRequiredService<OwnedCm4SimulatorReconfigurationCoordinator>());
            }
            var resourcesByRole = new Dictionary<string, ContainerResource>(StringComparer.Ordinal)
            {
                ["simulator"] = Assert.IsType<ContainerResource>(resources.Simulator.Resource),
                ["game-controller"] = Assert.IsType<ContainerResource>(resources.GameController.Resource),
                ["cm4-sim"] = Assert.IsType<ContainerResource>(resources.Cm4Simulator.Resource),
                ["crane"] = Assert.IsType<ContainerResource>(resources.Crane.Resource),
            };
            string? sharedRunLabel = null;
            foreach (var (role, resource) in resourcesByRole)
            {
                Assert.Contains(resource.Annotations.OfType<HealthCheckAnnotation>(),
                    annotation => annotation.Key == $"dcp-{role}-service-ready");
                Assert.Contains(resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>(),
                    annotation => annotation.Callback is not null);
                var runtimeArguments = await GetRuntimeArgumentsAsync(resource);
                Assert.DoesNotContain("--network", runtimeArguments);
                Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}={role}", runtimeArguments);
                var runLabel = Assert.Single(runtimeArguments, argument =>
                    argument.StartsWith($"{ContainerIdentityExecHandoff.RunIdLabel}=", StringComparison.Ordinal));
                sharedRunLabel ??= runLabel;
                Assert.Equal(sharedRunLabel, runLabel);
            }

            Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli", resourcesByRole["simulator"].Annotations.OfType<ContainerImageAnnotation>().Single().Image);
            Assert.Equal("a52b6bd", resourcesByRole["simulator"].Annotations.OfType<ContainerImageAnnotation>().Single().Tag);
            Assert.Equal(
                ["tini", "--", "./bin/simulator-cli", "-g", "2020B", "--realism", "None", "--ibis-port", "12346", "--ibis-team-color", "yellow"],
                await GetArgumentsAsync(resourcesByRole["simulator"]));
            Assert.Equal(
                ["-visionAddress", "224.5.23.2:10020", "-trackerAddress", "224.5.23.2:11010", "-publishAddress", "224.5.23.1:11003", "-address", ":8082"],
                await GetArgumentsAsync(resourcesByRole["game-controller"]));
            Assert.Contains("--robot-ids", await GetArgumentsAsync(resourcesByRole["cm4-sim"]));
            Assert.Contains("bash", await GetArgumentsAsync(resourcesByRole["crane"]));
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    private static async Task<string[]> GetRuntimeArgumentsAsync(ContainerResource resource)
    {
        var values = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            await annotation.Callback(new ContainerRuntimeArgsCallbackContext(values, CancellationToken.None));
        }

        return values.Select(value => Assert.IsType<string>(value)).ToArray();
    }

    private static async Task<string[]> GetArgumentsAsync(IResourceWithArgs resource)
    {
        var values = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await annotation.Callback(new CommandLineArgsCallbackContext(values, resource, CancellationToken.None));
        }

        return values.Select(value => value?.ToString() ?? string.Empty).ToArray();
    }

    [Fact]
    public void ComparisonRegistrationRetainsTheLegacyWrapperResources()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-comparison-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);

        try
        {
            var resources = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346",
                    "a52b6bd",
                    "2020B",
                    "None",
                    "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a",
                    "visibility_graph",
                    "comparison-test-" + Guid.NewGuid().ToString("N"),
                    "crane",
                    "yellow",
                    Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: false,
                    DuckRunId: "run-comparison-test",
                    CraneTargetAddressSettings: null,
                    CraneDiagnosticsPath: null,
                    DockerContextName: null,
                    ExpectedEngineId: null,
                    DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { });

            Assert.IsType<ExecutableResource>(resources.Simulator.Resource);
            Assert.IsType<ExecutableResource>(resources.GameController.Resource);
            Assert.IsType<ExecutableResource>(resources.Cm4Simulator!.Resource);
            Assert.IsType<ExecutableResource>(resources.Crane.Resource);
            Assert.Empty(resources.Simulator.Resource.Annotations.OfType<ResourceCommandAnnotation>());
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }
}
