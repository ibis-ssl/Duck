using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Duck.Testing.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Xml.Linq;

namespace Tracker.Tests;

public sealed class AppHostApplicationModelTests
{
    [Fact]
    public async Task StandardBaseRegistersAnOwnedCm4TransportServerSessionLifecycle()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");

        var cm4 = Assert.IsType<ContainerResource>(Find(appHost, "cm4-sim"));
        Assert.Contains(cm4.Annotations.OfType<ContainerImageAnnotation>(), annotation =>
            annotation.Image == "ghcr.io/ibis-ssl/orion-cm4-sim");
        Assert.Contains(appHost.Services, descriptor =>
            descriptor.ServiceType.Name == "OwnedCm4TransportServerSessionManager");
    }

    [Fact]
    public async Task StandardBaseSharesTheCm4TargetControllerWithItsOwnedSessionLifecycle()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");

        Assert.Single(appHost.Services, descriptor =>
            descriptor.ServiceType == typeof(IOwnedCm4TransportTargetController));
    }

    [Fact]
    public void StackOwnershipLeaseRejectsSecondOwnerAndAllowsReuseWithExistingMarker()
    {
        var lockPath = Path.Combine(Path.GetTempPath(), $"duck-aspire-stack-{Guid.NewGuid():N}.lock");
        File.WriteAllText(lockPath, "stale-owner=previous-run");

        using (StackOwnershipLease.Acquire(lockPath))
        {
            var exception = Assert.Throws<InvalidOperationException>(() => StackOwnershipLease.Acquire(lockPath));
            Assert.Contains("already owns", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        using var nextOwner = StackOwnershipLease.Acquire(lockPath);
    }

    [Fact]
    public async Task BaseModelRegistersDcpContainersAndFailClosedReadinessChecksForBaseServices()
    {
        using var appHost = await CreateAppHostAsync();

        Assert.IsType<ContainerResource>(Assert.Single(appHost.Resources, resource => resource.Name == "duck"));
        string? sharedRunId = null;
        string? sharedStackId = null;
        foreach (var name in new[] { "simulator", "game-controller", "crane", "cm4-sim" })
        {
            var resource = Assert.IsType<ContainerResource>(Assert.Single(appHost.Resources, item => item.Name == name));
            Assert.Contains(resource.Annotations.OfType<HealthCheckAnnotation>(), annotation => annotation.Key == $"dcp-{name}-service-ready");
            Assert.DoesNotContain(resource.Annotations.OfType<EndpointAnnotation>(), endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);

            var runtimeArgs = await GetContainerRuntimeArgsAsync(resource);
            var stackLabel = Assert.Single(runtimeArgs.Where(argument => argument.StartsWith("duck.aspire.stack=", StringComparison.Ordinal)));
            sharedStackId ??= stackLabel["duck.aspire.stack=".Length..];
            Assert.Equal(sharedStackId, stackLabel["duck.aspire.stack=".Length..]);
            var runLabel = Assert.Single(runtimeArgs.Where(argument => argument.StartsWith("com.ibis-ssl.duck.run-id=", StringComparison.Ordinal)));
            sharedRunId ??= runLabel["com.ibis-ssl.duck.run-id=".Length..];
            Assert.Equal(sharedRunId, runLabel["com.ibis-ssl.duck.run-id=".Length..]);
            Assert.Contains($"com.ibis-ssl.duck.role={name}", runtimeArgs);
            Assert.Contains($"duck.aspire.run={sharedRunId}", runtimeArgs);
            var resourceLabel = Assert.Single(runtimeArgs.Where(argument => argument.StartsWith("duck.aspire.resource=", StringComparison.Ordinal)));
            Assert.Equal($"duck.aspire.resource={name}", resourceLabel);
            Assert.Contains("duck.aspire.owner=duck-apphost", runtimeArgs);
            Assert.DoesNotContain("--network", runtimeArgs);
        }

        Assert.NotNull(sharedRunId);
        Assert.NotNull(sharedStackId);
        var duckArgs = await GetContainerRuntimeArgsAsync(Assert.IsType<ContainerResource>(Find(appHost, "duck")));
        Assert.Contains($"duck.aspire.stack={sharedStackId}", duckArgs);
        Assert.Contains("duck.aspire.resource=duck", duckArgs);
        Assert.Contains($"duck.aspire.run={sharedRunId}", duckArgs);

        var gameController = Assert.IsType<ContainerResource>(Find(appHost, "game-controller"));
        var controlEndpoint = Assert.Single(gameController.Annotations.OfType<EndpointAnnotation>(), endpoint => endpoint.Name == "game-controller-control");
        Assert.Equal(System.Net.Sockets.ProtocolType.Tcp, controlEndpoint.Protocol);
        Assert.Null(controlEndpoint.Port);
        Assert.Equal("127.0.0.1", controlEndpoint.TargetHost);
    }

    [Fact]
    public async Task StandardDuckRegistrationLabelsAreAcceptedByTheOwnershipConsumer()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:IdentityControl:DesktopBridgeEnabled=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var runtimeArgs = await GetContainerRuntimeArgsAsync(duck);
        var stackLabel = Assert.Single(runtimeArgs.Where(argument => argument.StartsWith("duck.aspire.stack=", StringComparison.Ordinal)));
        var resourceLabel = Assert.Single(runtimeArgs.Where(argument => argument.StartsWith("duck.aspire.resource=", StringComparison.Ordinal)));
        var runLabel = Assert.Single(runtimeArgs.Where(argument => argument.StartsWith("duck.aspire.run=", StringComparison.Ordinal)));
        var identityRunLabel = Assert.Single(runtimeArgs.Where(argument => argument.StartsWith(ContainerIdentityExecHandoff.RunIdLabel + "=", StringComparison.Ordinal)));
        var identityRoleLabel = Assert.Single(runtimeArgs.Where(argument => argument.StartsWith(ContainerIdentityExecHandoff.RoleLabel + "=", StringComparison.Ordinal)));
        var stackId = stackLabel["duck.aspire.stack=".Length..];
        var runId = runLabel["duck.aspire.run=".Length..];
        Assert.Equal(runId, identityRunLabel[(ContainerIdentityExecHandoff.RunIdLabel.Length + 1)..]);
        var duckLabels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ContainerIdentityExecHandoff.RunIdLabel] = identityRunLabel[(ContainerIdentityExecHandoff.RunIdLabel.Length + 1)..],
            [ContainerIdentityExecHandoff.RoleLabel] = identityRoleLabel[(ContainerIdentityExecHandoff.RoleLabel.Length + 1)..],
            [DockerContainerSpec.StackLabel] = stackId,
            [DockerContainerSpec.ResourceLabel] = resourceLabel["duck.aspire.resource=".Length..],
            [DockerContainerSpec.RunLabel] = runId,
        };
        var fixture = new DcpBaseResourceOwnershipEventConsumerTests.OwnershipFixture(
            includeDuck: true,
            runId: runId,
            stackId: stackId,
            duckLabels: duckLabels);

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, result.Disposition);
        var duckCandidate = Assert.Single(fixture.Registry.GetCandidates(), candidate => candidate.ResourceName == "duck");
        Assert.True(duckCandidate.OwnershipVerified);
        Assert.False(fixture.Registry.Get(duckCandidate.Resource)!.IsReady);
    }

    [Fact]
    public async Task DisabledIdentityHandoffResolvesTheRegisteredSimulatorHealthCheckAsUnhealthy()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:IdentityControl:DesktopBridgeEnabled=false",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");
        using var serviceProvider = appHost.Services.BuildServiceProvider();
        var healthOptions = serviceProvider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var registration = Assert.Single(
            healthOptions.Registrations,
            item => item.Name == "dcp-simulator-service-ready");
        Assert.DoesNotContain(healthOptions.Registrations, item => item.Name == "dcp-duck-service-ready");

        var healthCheck = registration.Factory(serviceProvider);
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext { Registration = registration },
            CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("current owned container identity", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BaseCraneDiagnosticsPathDoesNotLeakIntoContainerArgumentsOrEnvironment()
    {
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"crane-diagnostics-{Guid.NewGuid():N}.json");
        using var appHost = await CreateAppHostAsync("--Testing:Crane:DiagnosticsPath=" + diagnosticsPath);

        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var runtimeArguments = await GetContainerRuntimeArgsAsync(crane);
        var environment = await GetResourceEnvironmentAsync(crane);

        Assert.DoesNotContain(runtimeArguments, argument => argument.Contains(diagnosticsPath, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(environment.Values, value => value?.ToString()?.Contains(diagnosticsPath, StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ExplicitVisionSimulationModeUsesPinnedSimulatorAndDuckContainersOnly()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:VisionSimulationOnly=true",
            "--Testing:IdentityControl:DesktopBridgeEnabled=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=5bc5ab8a-7340-47ba-8b7c-9f645375a54c");

        Assert.Equal(2, appHost.Resources.Count());
        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));

        var image = Assert.Single(simulator.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli", image.Image);
        // ダイジェスト指定時は、可変タグをモデルに残さない。
        Assert.Null(image.Tag);
        Assert.Equal("f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79", image.SHA256);

        Assert.Single(simulator.Annotations.OfType<CommandLineArgsCallbackAnnotation>());
        Assert.Equal(
            ["--", "./bin/simulator-cli", "-g", "2020B", "--realism", "None", "--ibis-port", "12346", "--ibis-team-color", "yellow"],
            await GetArgumentsAsync(simulator));

        Assert.DoesNotContain(
            simulator.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
        Assert.DoesNotContain(
            duck.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
        var simulatorEnvironment = await GetResourceEnvironmentAsync(simulator);
        Assert.Empty(simulatorEnvironment);

        var duckEnvironment = await GetResourceEnvironmentAsync(duck);
        Assert.Equal("sim", duckEnvironment["Tracker__ActiveProfileName"].ToString());
        Assert.Equal("true", duckEnvironment["Testing__ContainerMode"].ToString());
        Assert.Equal("true", duckEnvironment["Testing__IdentityControl__Enabled"].ToString());
        Assert.Equal("duck", duckEnvironment["Testing__IdentityControl__ExpectedRole"].ToString());
        Assert.Equal("224.5.23.2", duckEnvironment["VisionReceiver__MulticastAddress"].ToString());
        Assert.Equal("10020", duckEnvironment["VisionReceiver__Port"].ToString());

        var healthEndpoint = Assert.Single(
            duck.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Name == "vision-health");
        Assert.Equal(39108, healthEndpoint.TargetPort);
        Assert.Null(healthEndpoint.Port);
        Assert.Equal("127.0.0.1", healthEndpoint.TargetHost);
        Assert.Single(duck.Annotations.OfType<HealthCheckAnnotation>());
        AssertWaits(duck, (simulator, WaitType.WaitUntilStarted));
    }

    [Fact]
    public async Task GameControllerVisionModeUsesSharedDefaultStackOwnershipLease()
    {
        var defaultLockPath = Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock");

        using (StackOwnershipLease.Acquire(defaultLockPath))
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
                    ["--Testing:GameControllerVisionOnly=true"]);
            });

            Assert.Contains("already owns", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        using var nextOwner = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            ["--Testing:GameControllerVisionOnly=true"]);
        Assert.Equal(4, nextOwner.Resources.Count());
    }

    [Fact]
    public async Task ExplicitGameControllerVisionModeUsesPinnedStandardResourcesAndLoopbackUiOnly()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:GameControllerVisionOnly=true",
            "--RefereeDriver:Enabled=true",
            "--Testing:IdentityControl:DesktopBridgeEnabled=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=5bc5ab8a-7340-47ba-8b7c-9f645375a54c");

        Assert.Equal(4, appHost.Resources.Count());
        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));

        var gameController = Assert.IsType<ContainerResource>(Find(appHost, "game-controller"));
        var refereeDriver = Assert.IsType<ContainerResource>(Find(appHost, "referee-driver"));

        var gameControllerImage = Assert.Single(gameController.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("robocupssl/ssl-game-controller", gameControllerImage.Image);
        Assert.Null(gameControllerImage.Tag);
        Assert.Equal("d62d772de063c2951f600a0b7898803f97822f4f46a412a731e42f7d82a99dc7", gameControllerImage.SHA256);
        Assert.Equal(
            ["-visionAddress", "224.5.23.2:10020", "-trackerAddress", "224.5.23.2:11010",
             "-publishAddress", "224.5.23.1:11003", "-address", ":8082"],
            await GetArgumentsAsync(gameController));

        var gcUi = Assert.Single(
            gameController.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Tcp &&
                        endpoint.Name == "game-controller-ui");
        Assert.Equal(8082, gcUi.TargetPort);
        Assert.Null(gcUi.Port);
        Assert.Equal("127.0.0.1", gcUi.TargetHost);
        Assert.False(gcUi.IsProxied);

        foreach (var resource in new IResource[] { simulator, duck, gameController, refereeDriver })
        {
            Assert.DoesNotContain(
                resource.Annotations.OfType<EndpointAnnotation>(),
                endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
        }

        AssertLinuxDistribution(
            refereeDriver,
            "Testing/Duck.Testing.RefereeDriver.Host",
            "Duck.Testing.RefereeDriver.Host.dll",
            "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca");
        var refereeEnvironment = await GetResourceEnvironmentAsync(refereeDriver);
        Assert.Equal("true", refereeEnvironment["RefereeDriver__Enabled"].ToString());
        Assert.Equal("ws://game-controller:8082/api/control", refereeEnvironment["RefereeDriver__GameControllerUri"].ToString());
        Assert.Contains(
            $"{DockerContainerSpec.OwnerLabel}={DockerContainerSpec.OwnerValue}",
            await GetContainerRuntimeArgsAsync(refereeDriver));
        Assert.Empty(gameController.Annotations.OfType<ResourceCommandAnnotation>());
        Assert.False(refereeEnvironment.ContainsKey("RefereeDriver__AutoDrive"));

        AssertWaits(duck, (simulator, WaitType.WaitUntilStarted));
        AssertWaits(gameController, (duck, WaitType.WaitUntilStarted));
        AssertWaits(refereeDriver, (gameController, WaitType.WaitUntilStarted));
    }

    [Fact]
    public async Task OwnedReconfigurationOptInAddsGameControllerResourceCommand()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:GameControllerVisionOnly=true",
            "--Testing:OwnedReconfiguration:Enabled=true",
            "--Testing:IdentityControl:DesktopBridgeEnabled=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");

        var gameController = Assert.IsType<ContainerResource>(Find(appHost, "game-controller"));
        Assert.Single(
            gameController.Annotations.OfType<ResourceCommandAnnotation>(),
            command => command.Name == "reconfigure-game-controller-endpoint");
        var referee = Assert.IsType<ContainerResource>(Find(appHost, "referee-driver"));
        var environment = await GetResourceEnvironmentAsync(referee);
        Assert.Equal("ws://game-controller:8082/api/control", environment["RefereeDriver__GameControllerUri"].ToString());
    }

    [Theory]
    [InlineData("--Testing:RuntimeHostOnly=true")]
    [InlineData("--Testing:VisionSimulationOnly=true")]
    [InlineData("--Testing:DebugHost:ReplayOnly=true")]
    [InlineData("--Testing:Mode=comparison")]
    public async Task OwnedReconfigurationRejectsCompetingEarlyAppHostModes(string competingMode)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
                [
                    "--Testing:GameControllerVisionOnly=true",
                    "--Testing:OwnedReconfiguration:Enabled=true",
                    "--Testing:IdentityControl:DesktopBridgeEnabled=true",
                    "--Testing:IdentityControl:DockerContext=desktop-linux",
                    "--Testing:IdentityControl:ExpectedEngineId=expected-engine",
                    competingMode,
                    CreateIsolatedOwnershipLockArgument(),
                ]));

        Assert.Contains("OwnedReconfiguration", exception.Message, StringComparison.Ordinal);
        Assert.Contains("cannot be combined", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// ブリッジが未指定または明示的に無効なら、従来の健康待ちと識別情報サービス未登録を維持します。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GameControllerVisionModeWithoutDesktopBridgeKeepsHealthWaitAndNoHandoffService(
        bool explicitlyDisabled)
    {
        string[] arguments = explicitlyDisabled
            ? ["--Testing:GameControllerVisionOnly=true", "--Testing:IdentityControl:DesktopBridgeEnabled=false"]
            : ["--Testing:GameControllerVisionOnly=true"];
        using var appHost = await CreateAppHostAsync(arguments);
        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));

        var gameController = Assert.IsType<ContainerResource>(Find(appHost, "game-controller"));
        var referee = Assert.IsType<ContainerResource>(Find(appHost, "referee-driver"));

        Assert.Equal(4, appHost.Resources.Count());
        AssertWaits(duck, (simulator, WaitType.WaitUntilStarted));
        AssertWaits(gameController, (duck, WaitType.WaitUntilHealthy));
        AssertWaits(referee, (gameController, WaitType.WaitUntilStarted));

        var refereeEnvironment = await GetResourceEnvironmentAsync(referee);
        Assert.False(refereeEnvironment.ContainsKey("Testing__IdentityControl__Enabled"));
        Assert.False(refereeEnvironment.ContainsKey("Testing__IdentityControl__ExpectedRole"));
        Assert.False(refereeEnvironment.ContainsKey("Testing__Diagnostics__RunId"));
        foreach (var resource in new[] { duck, referee,
            Assert.IsType<ContainerResource>(Find(appHost, "simulator")), gameController })
        {
            var runtimeArguments = await GetContainerRuntimeArgsAsync(resource);
            Assert.DoesNotContain(runtimeArguments, argument =>
                argument.StartsWith(ContainerIdentityExecHandoff.RunIdLabel + "=", StringComparison.Ordinal) ||
                argument.StartsWith(ContainerIdentityExecHandoff.RoleLabel + "=", StringComparison.Ordinal));
        }

        await using var services = appHost.Services.BuildServiceProvider();
        Assert.Empty(services.GetServices<IHostedService>().OfType<ContainerIdentityHandoffService>());
    }

    /// <summary>四資源構成でもDriver自身の実所有識別情報を渡し、Duckと別のwatcherで扱う。</summary>
    [Fact]
    public async Task DesktopBridgeLabelsAllFourOwnedResourcesForShutdownEvidence()
    {
        const string runId = "model-owned-run";
        using var appHost = await CreateAppHostAsync(
            "--Testing:GameControllerVisionOnly=true",
            "--Testing:Diagnostics:RunId=" + runId,
            "--Testing:IdentityControl:DesktopBridgeEnabled=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=5bc5ab8a-7340-47ba-8b7c-9f645375a54c");
        foreach (var name in new[] { "simulator", "duck", "game-controller", "referee-driver" })
        {
            var resource = Assert.IsType<ContainerResource>(Find(appHost, name));
            var args = await GetContainerRuntimeArgsAsync(resource);
            Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}={runId}", args);
            Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}={name}", args);
        }
        await using var services = appHost.Services.BuildServiceProvider();
        Assert.Equal(2, services.GetServices<IHostedService>()
            .OfType<ContainerIdentityHandoffService>().Count());
    }

    [Fact]
    public async Task GameControllerVisionModeConfiguresRefereeDriverIdentityHandoff()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:GameControllerVisionOnly=true",
            "--RefereeDriver:Enabled=true",
            "--Testing:IdentityControl:DesktopBridgeEnabled=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=5bc5ab8a-7340-47ba-8b7c-9f645375a54c");

        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var referee = Assert.IsType<ContainerResource>(Find(appHost, "referee-driver"));
        var duckEnvironment = await GetResourceEnvironmentAsync(duck);
        var environment = await GetResourceEnvironmentAsync(referee);
        var runId = Assert.IsType<string>(duckEnvironment["Testing__Diagnostics__RunId"].ToString());
        Assert.Equal(runId, environment["Testing__Diagnostics__RunId"].ToString());
        Assert.Equal("true", environment["Testing__IdentityControl__Enabled"].ToString());
        Assert.Equal("referee-driver", environment["Testing__IdentityControl__ExpectedRole"].ToString());
        Assert.False(environment.ContainsKey("Testing__Diagnostics__ResourceId"));
        Assert.False(environment.ContainsKey("Testing__Diagnostics__ContainerId"));
        Assert.False(environment.ContainsKey("Testing__Diagnostics__Generation"));

        var args = await GetContainerRuntimeArgsAsync(referee);
        Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}={runId}", args);
        Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}=referee-driver", args);
        await using var services = appHost.Services.BuildServiceProvider();
        Assert.Equal(2, services.GetServices<IHostedService>()
            .OfType<ContainerIdentityHandoffService>().Count());
    }

    [Fact]
    public async Task ExplicitGameControllerVisionModeUsesPinnedSimulatorRepositoryAndEntrypoint()
    {
        using var appHost = await CreateAppHostAsync("--Testing:GameControllerVisionOnly=true");

        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        var image = Assert.Single(simulator.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli", image.Image);
        Assert.Null(image.Tag);
        Assert.Equal("f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79", image.SHA256);
        Assert.Equal(
            ["--", "./bin/simulator-cli", "-g", "2020B", "--realism", "None", "--ibis-port", "12346", "--ibis-team-color", "yellow"],
            await GetArgumentsAsync(simulator));
    }

    [Fact]
    public async Task ExplicitGameControllerVisionModeUsesDuckRoleForIdentityProjection()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:GameControllerVisionOnly=true",
            "--Testing:IdentityControl:DesktopBridgeEnabled=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=5bc5ab8a-7340-47ba-8b7c-9f645375a54c");

        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var environment = await GetResourceEnvironmentAsync(duck);
        Assert.Equal("duck", environment["Testing__IdentityControl__ExpectedRole"].ToString());
        var runId = Assert.IsType<string>(environment["Testing__Diagnostics__RunId"].ToString());
        var runtimeArguments = await GetContainerRuntimeArgsAsync(duck);
        Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}={runId}", runtimeArguments);
        Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}=duck", runtimeArguments);

        const string resourceId = "opaque-duck-event-key";
        var fullContainerId = new string('a', 64);
        var running = new ResourceEvent(
            duck,
            resourceId,
            new CustomResourceSnapshot
            {
                ResourceType = "container",
                State = new ResourceStateSnapshot(KnownResourceStates.Running, "fixture"),
                Properties = [new ResourcePropertySnapshot("container.id", fullContainerId)],
            });
        Assert.True(RegisteredContainerEventProjection.TryProject(
            duck, resourceId, "duck", running, running, out var target));
        Assert.Equal(resourceId, target!.ResourceId);
        Assert.Equal("duck", target.RoleName);
        Assert.Equal(fullContainerId, target.ContainerId);
    }

    [Fact]
    public async Task BaseModeUsesPinnedRuntimeContainerFromItsLinuxPublishOutputWithoutPublishingUdp()
    {
        using var appHost = await CreateAppHostAsync();

        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        AssertLinuxDistribution(
            duck,
            "Tracker/Tracker.RuntimeHost",
            "Tracker.RuntimeHost.dll",
            "mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3");
        Assert.DoesNotContain(
            duck.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
        Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "referee-driver");
    }

    [Fact]
    public async Task BaseModeCanAddDebugHostVisionObserverWithoutPublishedUdp()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:DebugHost:VisionObserverEnabled=true",
            "--Testing:IdentityControl:DesktopBridgeEnabled=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");

        var debugHost = Assert.IsType<ContainerResource>(Find(appHost, "debug-host"));
        Assert.Contains(appHost.Resources, resource => resource.Name == "simulator");
        Assert.Contains(appHost.Resources, resource => resource.Name == "duck");
        Assert.DoesNotContain(appHost.Resources, resource => resource.Name is "tracker-tigers" or "tracker-erforce");
        AssertLinuxDistribution(
            debugHost,
            "Tracker/Tracker.DebugHost",
            "Tracker.DebugHost.dll",
            "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca");

        var http = Assert.Single(
            debugHost.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Tcp && endpoint.TargetPort == 8080);
        Assert.Null(http.Port);
        Assert.Equal("127.0.0.1", http.TargetHost);
        Assert.DoesNotContain(
            debugHost.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);

        var environment = await GetResourceEnvironmentAsync(debugHost);
        Assert.Equal("224.5.23.2", environment["VisionReceiver__MulticastAddress"].ToString());
        Assert.Equal("10020", environment["VisionReceiver__Port"].ToString());
        Assert.Equal("false", environment["Tracker__Enabled"].ToString());
        Assert.Equal("false", environment["Tracker__PublishUdp"].ToString());
        Assert.Equal("false", environment["Tracker__Receive__Enabled"].ToString());
        Assert.Equal("true", environment["Testing__IdentityControl__Enabled"].ToString());
        Assert.Equal("debug-host", environment["Testing__IdentityControl__ExpectedRole"].ToString());
        var duckArgs = await GetContainerRuntimeArgsAsync(Assert.IsType<ContainerResource>(Find(appHost, "duck")));
        var debugHostArgs = await GetContainerRuntimeArgsAsync(debugHost);
        var duckStack = Assert.Single(duckArgs, argument => argument.StartsWith("duck.aspire.stack=", StringComparison.Ordinal));
        var duckRun = Assert.Single(duckArgs, argument => argument.StartsWith("duck.aspire.run=", StringComparison.Ordinal));
        Assert.Contains(duckStack, debugHostArgs);
        Assert.Contains("duck.aspire.resource=debug-host", debugHostArgs);
        Assert.Contains(duckRun, debugHostArgs);
        Assert.Equal("true", environment["VisionReceiver__PacketCapture__Enabled"].ToString());
        Assert.Equal("true", environment["VisionReceiver__PacketCapture__FlushEachPacket"].ToString());
        var captureMount = Assert.Single(debugHost.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Contains(Path.Combine("Duck", "Aspire"), captureMount.Source, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("captures", captureMount.Source, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/data/captures", captureMount.Target);
        Assert.False(captureMount.IsReadOnly);
    }
    [Fact]
    public async Task ComparisonModeUsesPinnedDebugHostPublishOutputAndLoopbackDynamicTcp()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison");

        var debugHost = Assert.IsType<ContainerResource>(Find(appHost, "debug-host"));
        AssertLinuxDistribution(
            debugHost,
            "Tracker/Tracker.DebugHost",
            "Tracker.DebugHost.dll",
            "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca");
        Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "simulator-control-relay");
        var http = Assert.Single(
            debugHost.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Tcp && endpoint.TargetPort == 8080);

        Assert.Null(http.Port);
        Assert.Equal("127.0.0.1", http.TargetHost);
        Assert.DoesNotContain(
            debugHost.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
        var captureMount = Assert.Single(debugHost.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Contains(Path.Combine("Duck", "Aspire"), captureMount.Source, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("captures", captureMount.Source, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/data/captures", captureMount.Target);
        Assert.False(captureMount.IsReadOnly);
        var environment = await GetResourceEnvironmentAsync(debugHost);
        Assert.Equal("/data/captures", environment["VisionReceiver__PacketCapture__DirectoryPath"].ToString());
        Assert.Equal("false", environment["VisionReceiver__PacketCapture__Enabled"].ToString());
        Assert.Contains(
            $"{DockerContainerSpec.OwnerLabel}={DockerContainerSpec.OwnerValue}",
            await GetContainerRuntimeArgsAsync(debugHost));
    }

    [Fact]
    public async Task ComparisonModeConfiguresDebugHostIdentityHandoffForStrictReadiness()
    {
        const string runId = "comparison-debug-host-owned-run";
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=comparison",
            "--Testing:Diagnostics:RunId=" + runId,
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");

        var debugHost = Assert.IsType<ContainerResource>(Find(appHost, "debug-host"));
        var environment = await GetResourceEnvironmentAsync(debugHost);
        Assert.Equal("true", environment["Testing__IdentityControl__Enabled"].ToString());
        Assert.Equal("debug-host", environment["Testing__IdentityControl__ExpectedRole"].ToString());
        Assert.Equal(runId, environment["Testing__Diagnostics__RunId"].ToString());
        var runtimeArgs = await GetContainerRuntimeArgsAsync(debugHost);
        Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}={runId}", runtimeArgs);
        Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}=debug-host", runtimeArgs);

        await using var services = appHost.Services.BuildServiceProvider();
        Assert.Equal(2, services.GetServices<IHostedService>()
            .OfType<ContainerIdentityHandoffService>().Count());
    }

    [Fact]
    public async Task ComparisonModeConfiguresRefereeIdentityHandoffWhenDcpEngineIsKnown()
    {
        const string runId = "comparison-referee-owned-run";
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=comparison",
            "--Testing:Diagnostics:RunId=" + runId,
            "--RefereeDriver:Enabled=true",
            "--Testing:ContainerMode=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");

        var refereeDriver = Assert.IsType<ContainerResource>(Find(appHost, "referee-driver"));
        var environment = await GetResourceEnvironmentAsync(refereeDriver);
        Assert.Equal("true", environment["Testing__IdentityControl__Enabled"].ToString());
        Assert.Equal("referee-driver", environment["Testing__IdentityControl__ExpectedRole"].ToString());
        Assert.Equal(runId, environment["Testing__Diagnostics__RunId"].ToString());

        var runtimeArgs = await GetContainerRuntimeArgsAsync(refereeDriver);
        Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}={runId}", runtimeArgs);
        Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}=referee-driver", runtimeArgs);

        await using var services = appHost.Services.BuildServiceProvider();
        Assert.Equal(3, services.GetServices<IHostedService>()
            .OfType<ContainerIdentityHandoffService>().Count());
    }

    [Fact]
    public async Task DebugHostReplayOnlyUsesExistingDirectoryReadOnlyWithoutStartingNetworkResources()
    {
        var replayDirectory = Path.Combine(Path.GetTempPath(), "duck-debug-host-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(replayDirectory);
        var sentinelPath = Path.Combine(replayDirectory, "capture.metadata.json");
        var sentinelBytes = System.Text.Encoding.UTF8.GetBytes("{\"saved\":true}");
        await File.WriteAllBytesAsync(sentinelPath, sentinelBytes);

        try
        {
            using var appHost = await CreateAppHostAsync(
                "--Testing:DebugHost:ReplayOnly=true",
                "--Testing:DebugHost:ReplayDirectoryPath=" + replayDirectory);

            var debugHost = Assert.IsType<ContainerResource>(Assert.Single(appHost.Resources));
            Assert.Equal("debug-host", debugHost.Name);
            AssertLinuxDistribution(
                debugHost,
                "Tracker/Tracker.DebugHost",
                "Tracker.DebugHost.dll",
                "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca");

            var replayMount = Assert.Single(debugHost.Annotations.OfType<ContainerMountAnnotation>());
            Assert.Equal(Path.GetFullPath(replayDirectory), replayMount.Source);
            Assert.Equal("/data/replay", replayMount.Target);
            Assert.True(replayMount.IsReadOnly);

            var environment = await GetResourceEnvironmentAsync(debugHost);
            Assert.Equal("true", environment["Testing__DebugHost__ReplayOnly"].ToString());
            Assert.Equal("/data/replay", environment["VisionReceiver__PacketCapture__DirectoryPath"].ToString());
            Assert.Equal("false", environment["VisionReceiver__PacketCapture__Enabled"].ToString());
            Assert.Equal("false", environment["Tracker__Enabled"].ToString());
            Assert.Equal("false", environment["Tracker__PublishUdp"].ToString());
            Assert.Equal("false", environment["Tracker__Receive__Enabled"].ToString());
            Assert.DoesNotContain(
                debugHost.Annotations.OfType<EndpointAnnotation>(),
                endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
            Assert.Equal(sentinelBytes, await File.ReadAllBytesAsync(sentinelPath));
        }
        finally
        {
            Directory.Delete(replayDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DebugHostReplayOnlyRejectsAnotherSelectedAppHostMode()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
                [
                    "--Testing:DebugHost:ReplayOnly=true",
                    "--Testing:VisionSimulationOnly=true",
                    CreateIsolatedOwnershipLockArgument(),
                ]));

        Assert.Contains("cannot be combined", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--Testing:RuntimeHostOnly=true")]
    [InlineData("--Testing:VisionSimulationOnly=true")]
    [InlineData("--Testing:GameControllerVisionOnly=true")]
    [InlineData("--Testing:OwnedReconfiguration:Enabled=true")]
    [InlineData("--Testing:DebugHost:ReplayOnly=true")]
    [InlineData("--Testing:Mode=comparison")]
    public async Task NamespaceProbeGenerationRejectsAnotherSelectedAppHostMode(string otherMode)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
                [
                    "--Testing:NamespaceProbe:GenerateModel=true",
                    otherMode,
                    CreateIsolatedOwnershipLockArgument(),
                ]));

        Assert.Contains("cannot be combined", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--Testing:NamespaceProbe:GenerateModel=enabled-ish")]
    [InlineData("--Testing:NamespaceProbe:GenerateModel=")]
    [InlineData("--Testing:NamespaceProbe:GenerateModel=   ")]
    public async Task NamespaceProbeGenerationRejectsInvalidSelectionValue(string selection)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
                [
                    selection,
                    CreateIsolatedOwnershipLockArgument(),
                ]));

        Assert.Contains("true or false", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NamespaceProbeGenerationProjectsTwoOwnedStandardContainersWithoutHostPaths()
    {
        var publishDirectory = Path.Combine(Path.GetTempPath(), $"duck-namespace-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(publishDirectory);
        try
        {
            foreach (var fileName in new[]
                     {
                         "Duck.Testing.RefereeDriver.Host.dll",
                         "Duck.Testing.RefereeDriver.Host.deps.json",
                         "Duck.Testing.RefereeDriver.Host.runtimeconfig.json",
                         "Duck.Testing.RefereeDriver.dll",
                         "Duck.Testing.Diagnostics.dll",
                         "Google.Protobuf.dll",
                         "SslProto.dll",
                     })
            {
                await File.WriteAllTextAsync(Path.Combine(publishDirectory, fileName), "fixture");
            }

            var builder = DistributedApplication.CreateBuilder([]);
            var model = await NamespaceProbeModelFactory.AddResourcesAndProjectAsync(
                builder, "model-run_01", publishDirectory);

            Assert.Equal("AppHost resources only; DCP networks are not included", model.ModelStage);
            Assert.Equal(2, model.Resources.Count);
            var owner = Assert.Single(model.Resources, resource => resource.ResourceName == NamespaceProbeModelFactory.OwnerResourceName);
            var follower = Assert.Single(model.Resources, resource => resource.ResourceName == NamespaceProbeModelFactory.FollowerResourceName);
            foreach (var resource in model.Resources)
            {
                Assert.Equal(nameof(ContainerResource), resource.ResourceType);
                Assert.Equal(NamespaceProbeModelFactory.AspNetImage, resource.Image);
                Assert.Equal(NamespaceProbeModelFactory.AspNetImageSha256, resource.ImageSha256);
                Assert.Equal("Never", resource.ImagePullPolicy);
                Assert.Contains("dotnet", resource.Arguments);
                Assert.Contains("/app/Duck.Testing.RefereeDriver.Host.dll", resource.Arguments);
                Assert.Contains("--RefereeDriver:Enabled=false", resource.Arguments);
                Assert.Single(resource.Mounts);
                Assert.Equal("/app", resource.Mounts[0].Target);
                Assert.True(resource.Mounts[0].IsReadOnly);
            }

            Assert.Contains("--urls=http://0.0.0.0:39110", owner.Arguments);
            Assert.Contains("--label", owner.RuntimeArguments);
            Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}=model-run_01", owner.RuntimeArguments);
            Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}={NamespaceProbeModelFactory.OwnerRole}", owner.RuntimeArguments);
            var ownerEndpoint = Assert.Single(owner.Endpoints);
            Assert.Null(ownerEndpoint.HostPort);
            Assert.Equal(NamespaceProbeModelFactory.OwnerPort, ownerEndpoint.TargetPort);
            Assert.Equal("127.0.0.1", ownerEndpoint.TargetHost);

            Assert.Contains("--urls=http://0.0.0.0:39111", follower.Arguments);
            Assert.Contains("--network", follower.RuntimeArguments);
            Assert.Contains($"container:{owner.ContainerName}", follower.RuntimeArguments);
            Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}=model-run_01", follower.RuntimeArguments);
            Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}={NamespaceProbeModelFactory.FollowerRole}", follower.RuntimeArguments);
            Assert.Empty(follower.Endpoints);
            Assert.Contains(NamespaceProbeModelFactory.OwnerResourceName, follower.WaitForStart);

            var json = NamespaceProbeModelFactory.Serialize(model);
            Assert.DoesNotContain(publishDirectory, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NetworkSettings", json, StringComparison.Ordinal);

        }
        finally
        {
            Directory.Delete(publishDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BridgeProbeUsesDefaultNetworkWithoutNamespaceArgs()
    {
        var publishDirectory = Path.Combine(Path.GetTempPath(), $"duck-bridge-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(publishDirectory);
        try
        {
            foreach (var fileName in new[]
                     {
                         "Duck.Testing.RefereeDriver.Host.dll",
                         "Duck.Testing.RefereeDriver.Host.deps.json",
                         "Duck.Testing.RefereeDriver.Host.runtimeconfig.json",
                         "Duck.Testing.RefereeDriver.dll",
                         "Duck.Testing.Diagnostics.dll",
                         "Google.Protobuf.dll",
                         "SslProto.dll",
                     })
            {
                await File.WriteAllTextAsync(Path.Combine(publishDirectory, fileName), "fixture");
            }

            var builder = DistributedApplication.CreateBuilder([]);
            var model = await NamespaceProbeModelFactory.AddResourcesAndProjectAsync(
                builder,
                "bridge-run_01",
                publishDirectory,
                NamespaceProbeModelFactory.NetworkMode.DefaultBridge);
            var owner = Assert.Single(model.Resources,
                resource => resource.ResourceName == NamespaceProbeModelFactory.OwnerResourceName);
            var follower = Assert.Single(model.Resources,
                resource => resource.ResourceName == NamespaceProbeModelFactory.FollowerResourceName);

            Assert.Equal(2, model.Resources.Count);
            Assert.Equal(nameof(ContainerResource), owner.ResourceType);
            Assert.Equal(nameof(ContainerResource), follower.ResourceType);
            Assert.Equal(NamespaceProbeModelFactory.AspNetImageSha256, owner.ImageSha256);
            Assert.Equal(NamespaceProbeModelFactory.AspNetImageSha256, follower.ImageSha256);
            Assert.Equal(NamespaceProbeModelFactory.AspNetImage, owner.Image);
            Assert.Equal(NamespaceProbeModelFactory.AspNetImage, follower.Image);
            Assert.Equal("Never", owner.ImagePullPolicy);
            Assert.Equal("Never", follower.ImagePullPolicy);
            Assert.Single(owner.Mounts);
            Assert.True(owner.Mounts[0].IsReadOnly);
            Assert.Single(follower.Mounts);
            Assert.True(follower.Mounts[0].IsReadOnly);
            Assert.Null(owner.Endpoints.Single().HostPort);
            Assert.Equal("127.0.0.1", owner.Endpoints.Single().TargetHost);
            Assert.Equal(NamespaceProbeModelFactory.OwnerPort, owner.Endpoints.Single().TargetPort);
            Assert.Empty(follower.Endpoints);
            Assert.DoesNotContain("--network", follower.RuntimeArguments);
            Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}=bridge-run_01", owner.RuntimeArguments);
            Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}=bridge-run_01", follower.RuntimeArguments);
            Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}={NamespaceProbeModelFactory.OwnerRole}", owner.RuntimeArguments);
            Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}={NamespaceProbeModelFactory.FollowerRole}", follower.RuntimeArguments);
            Assert.Contains(NamespaceProbeModelFactory.OwnerResourceName, follower.WaitForStart);
        }
        finally
        {
            Directory.Delete(publishDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task NamespaceProbeGenerationFalseLeavesExistingResourceSelectionUnchanged()
    {
        using var defaultAppHost = await CreateAppHostAsync();
        using var disabledAppHost = await CreateAppHostAsync("--Testing:NamespaceProbe:GenerateModel=false");

        Assert.Equal(
            defaultAppHost.Resources.Select(resource => (resource.Name, resource.GetType())).OrderBy(resource => resource.Name),
            disabledAppHost.Resources.Select(resource => (resource.Name, resource.GetType())).OrderBy(resource => resource.Name));
    }

    [Fact]
    public async Task NamespaceProbeGenerationRejectsMissingExistingPublishOutputBeforeAddingResources()
    {
        var builder = DistributedApplication.CreateBuilder([]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NamespaceProbeModelFactory.AddResourcesAndProjectAsync(
                builder,
                "model-run_02",
                Path.Combine(Path.GetTempPath(), $"missing-namespace-probe-{Guid.NewGuid():N}")));

        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(builder.Resources);
    }

    [Fact]
    public async Task ExplicitRefereeDriverSelectionUsesPinnedPublishOutputAndInternalGameControllerAddress()
    {
        using var defaultAppHost = await CreateAppHostAsync();
        Assert.DoesNotContain(defaultAppHost.Resources, resource => resource.Name == "referee-driver");

        using var selectedDefaultInterfaceAppHost = await CreateAppHostAsync(
            "--RefereeDriver:Enabled=true",
            "--Testing:ContainerMode=true");
        var defaultInterfaceDriver = Assert.IsType<ContainerResource>(Find(selectedDefaultInterfaceAppHost, "referee-driver"));
        var defaultInterfaceEnvironment = await GetResourceEnvironmentAsync(defaultInterfaceDriver);
        Assert.DoesNotContain("RefereeDriver__InterfaceAddress", defaultInterfaceEnvironment.Keys);

        using var selectedAppHost = await CreateAppHostAsync(
            "--RefereeDriver:Enabled=true",
            "--Testing:RefereeDriver:InterfaceAddress=192.0.2.10",
            "--Testing:ContainerMode=true");
        var refereeDriver = Assert.IsType<ContainerResource>(Find(selectedAppHost, "referee-driver"));
        AssertLinuxDistribution(
            refereeDriver,
            "Testing/Duck.Testing.RefereeDriver.Host",
            "Duck.Testing.RefereeDriver.Host.dll",
            "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca");
        var environment = await GetResourceEnvironmentAsync(refereeDriver);

        Assert.Equal("true", environment["RefereeDriver__Enabled"].ToString());
        Assert.Equal("ws://game-controller:8082/api/control", environment["RefereeDriver__GameControllerUri"].ToString());
        Assert.Equal("192.0.2.10", environment["RefereeDriver__InterfaceAddress"].ToString());
        Assert.Equal("true", environment["Testing__ContainerMode"].ToString());
        Assert.DoesNotContain(
            refereeDriver.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
    }

    [Fact]
    public async Task ComparisonModeAddsPinnedExternalTrackersAndObserverWithoutAnotherRefereeProducer()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=comparison",
            "--RefereeDriver:Enabled=true",
            "--Testing:ContainerMode=true");

        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        var gameController = Assert.IsType<ContainerResource>(Find(appHost, "game-controller"));
        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var cm4 = Assert.IsType<ContainerResource>(Find(appHost, "cm4-sim"));
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var tigers = Assert.IsType<ContainerResource>(Find(appHost, "tracker-tigers"));
        var erForce = Assert.IsType<ContainerResource>(Find(appHost, "tracker-erforce"));
        var debugHost = Assert.IsType<ContainerResource>(Find(appHost, "debug-host"));
        var refereeDriver = Assert.IsType<ContainerResource>(Find(appHost, "referee-driver"));
        var simulatorControlRelay = Assert.IsType<ContainerResource>(Find(appHost, "simulator-control-relay"));
        var refereeHttp = Assert.Single(
            refereeDriver.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Name == "http");
        Assert.Equal(39110, refereeHttp.TargetPort);
        Assert.Null(refereeHttp.Port);
        Assert.Equal("127.0.0.1", refereeHttp.TargetHost);
        Assert.False(refereeHttp.IsProxied);
        var resources = new[] { simulator, gameController, crane, cm4, duck, tigers, erForce, debugHost, refereeDriver, simulatorControlRelay };

        string? sharedStackId = null;
        string? sharedRunId = null;
        foreach (var resource in resources)
        {
            var runtimeArgs = await GetContainerRuntimeArgsAsync(resource);
            Assert.DoesNotContain("--network", runtimeArgs);
            var stackLabel = Assert.Single(runtimeArgs, argument => argument.StartsWith("duck.aspire.stack=", StringComparison.Ordinal));
            var runLabel = Assert.Single(runtimeArgs, argument => argument.StartsWith("duck.aspire.run=", StringComparison.Ordinal));
            Assert.Equal(sharedStackId ??= stackLabel["duck.aspire.stack=".Length..], stackLabel["duck.aspire.stack=".Length..]);
            Assert.Equal(sharedRunId ??= runLabel["duck.aspire.run=".Length..], runLabel["duck.aspire.run=".Length..]);
            Assert.Contains($"duck.aspire.resource={resource.Name}", runtimeArgs);
            Assert.DoesNotContain(resource.Annotations.OfType<EndpointAnnotation>(), endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
        }

        Assert.NotNull(sharedStackId);
        Assert.NotNull(sharedRunId);
        AssertLinuxDistribution(
            simulatorControlRelay,
            "Testing/Duck.Testing.RefereeDriver.Host",
            "Duck.Testing.RefereeDriver.Host.dll",
            "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca");
        Assert.Equal(["--simulator-control-relay"], await GetArgumentsAsync(simulatorControlRelay));
        var simulatorRelayEndpoint = Assert.Single(
            simulatorControlRelay.Annotations.OfType<EndpointAnnotation>(), endpoint => endpoint.Name == "http");
        Assert.Equal(System.Net.Sockets.ProtocolType.Tcp, simulatorRelayEndpoint.Protocol);
        Assert.Equal(39111, simulatorRelayEndpoint.TargetPort);
        Assert.Null(simulatorRelayEndpoint.Port);
        Assert.Equal("127.0.0.1", simulatorRelayEndpoint.TargetHost);
        Assert.DoesNotContain(simulatorControlRelay.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
        Assert.Contains(simulatorControlRelay.Annotations.OfType<HealthCheckAnnotation>(),
            annotation => annotation.Key.Contains("/health/ready", StringComparison.Ordinal));
        AssertWaits(simulatorControlRelay, (simulator, WaitType.WaitUntilStarted));
        Assert.Equal(
            ["tini", "--", "./bin/simulator-cli", "-g", "2020B", "--realism", "None", "--ibis-port", "12346", "--ibis-team-color", "yellow"],
            await GetArgumentsAsync(simulator, callbackLimit: 1));
        Assert.Equal(
            ["-visionAddress", "224.5.23.2:10020", "-trackerAddress", "224.5.23.2:11010", "-publishAddress", "224.5.23.1:11003", "-address", ":8082"],
            await GetArgumentsAsync(gameController));
        Assert.Contains(crane.Annotations.OfType<HealthCheckAnnotation>(), annotation => annotation.Key == "dcp-crane-service-ready");
        Assert.Contains(cm4.Annotations.OfType<HealthCheckAnnotation>(), annotation => annotation.Key == "dcp-cm4-sim-service-ready");

        Assert.Equal(
            ["--headless", "--visionAddress", "224.5.23.2:10020", "--refereeAddress", "224.5.23.1:11003", "--moduli", "simulation_protocol_comparison"],
            await GetArgumentsAsync(tigers));
        var protocolMount = Assert.Single(tigers.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal("/Sumatra/config/moduli/simulation_protocol_comparison.xml", protocolMount.Target);
        Assert.True(protocolMount.IsReadOnly);
        var protocol = XDocument.Load(protocolMount.Source);
        var refereeProperties = protocol.Descendants("module")
            .Single(module => (string?)module.Attribute("id") == "edu.tigers.sumatra.referee.AReferee")
            .Element("properties");
        Assert.Equal("NETWORK", (string?)refereeProperties?.Element("source"));
        Assert.Equal("11003", (string?)refereeProperties?.Element("port"));
        Assert.Equal("false", (string?)refereeProperties?.Element("gameController"));
        Assert.Equal("false", (string?)refereeProperties?.Element("publishRefereeMessages"));

        var cameraProperties = protocol.Descendants("module")
            .Single(module => (string?)module.Attribute("id") == "edu.tigers.sumatra.cam.ACam")
            .Element("properties");
        Assert.Equal("10020", (string?)cameraProperties?.Element("port"));

        var trackerProperties = protocol.Descendants("module")
            .Single(module => (string?)module.Attribute("id") == "edu.tigers.sumatra.wp.exporter.VisionTrackerSender")
            .Element("properties");
        Assert.Equal("224.5.23.2", (string?)trackerProperties?.Element("address"));
        Assert.Equal("11010", (string?)trackerProperties?.Element("port"));
        Assert.Equal(
            ["--vision-port", "10020", "--tracker-port", "11010", "--gc-port", "11003"],
            await GetArgumentsAsync(erForce));

        AssertWaits(tigers, (simulator, WaitType.WaitUntilHealthy), (gameController, WaitType.WaitUntilHealthy));
        AssertWaits(erForce, (simulator, WaitType.WaitUntilHealthy), (gameController, WaitType.WaitUntilHealthy));
        AssertWaits(refereeDriver, (gameController, WaitType.WaitUntilHealthy));

        var debugHostEnvironment = await GetResourceEnvironmentAsync(debugHost);
        Assert.Equal("224.5.23.2", debugHostEnvironment["VisionReceiver__MulticastAddress"]);
        Assert.Equal("10020", debugHostEnvironment["VisionReceiver__Port"]);
        Assert.Equal("false", debugHostEnvironment["Tracker__Enabled"]);
        Assert.Equal("false", debugHostEnvironment["Tracker__PublishUdp"]);
        Assert.Equal("true", debugHostEnvironment["Tracker__Receive__Enabled"]);
        Assert.Equal("224.5.23.2", debugHostEnvironment["Tracker__Receive__MulticastAddress"]);
        Assert.Equal("11010", debugHostEnvironment["Tracker__Receive__Port"]);
        Assert.Equal("debug-host-observer", debugHostEnvironment["Tracker__Uuid"]);
        Assert.Equal("debug-host-observer", debugHostEnvironment["Tracker__SourceName"]);
        Assert.Equal("comparison", debugHostEnvironment["Testing__Mode"]);
        Assert.Equal("true", debugHostEnvironment["Testing__ContainerMode"]);
        Assert.Single(debugHost.Annotations.OfType<HealthCheckAnnotation>());
        var debugHttp = Assert.Single(
            debugHost.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Name == "http");
        Assert.Null(debugHttp.Port);
        Assert.Equal(8080, debugHttp.TargetPort);
        Assert.Equal("127.0.0.1", debugHttp.TargetHost);
        Assert.False(debugHttp.IsProxied);
        AssertWaits(debugHost,
            (simulator, WaitType.WaitUntilStarted),
            (duck, WaitType.WaitUntilStarted),
            (tigers, WaitType.WaitUntilStarted),
            (erForce, WaitType.WaitUntilStarted));

        var refereeProducers = new List<string>();
        foreach (var resource in resources)
        {
            var resourceArgs = await GetArgumentsAsync(resource, callbackLimit: resource.Name == "simulator" ? 1 : null);
            for (var index = 0; index + 1 < resourceArgs.Length; index++)
            {
                if (resourceArgs[index] == "-publishAddress" && resourceArgs[index + 1] == "224.5.23.1:11003")
                    refereeProducers.Add(resource.Name);
            }
        }

        Assert.Equal(["game-controller"], refereeProducers);
    }

    [Fact]
    public void MatchFixturesKeepTigersConfigurationAndInitialFriendlyStopState()
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "match");
        var protocol = XDocument.Load(Path.Combine(fixtureDirectory, "simulation_protocol_tigers.xml"));
        var refereeProperties = protocol.Descendants("module")
            .Single(module => (string?)module.Attribute("id") == "edu.tigers.sumatra.referee.AReferee")
            .Element("properties");
        Assert.Equal("NETWORK", (string?)refereeProperties?.Element("source"));
        Assert.Equal("11003", (string?)refereeProperties?.Element("port"));
        Assert.Equal("false", (string?)refereeProperties?.Element("gameController"));
        var matchConfig = File.ReadAllText(Path.Combine(fixtureDirectory, "ssl-game-controller-match.yaml"));
        Assert.Contains("state-store-file: /tmp/state-store.json.stream", matchConfig, StringComparison.Ordinal);
        Assert.Contains("half-duration: 45s", matchConfig, StringComparison.Ordinal);
        Assert.Contains("half-duration: 30s", matchConfig, StringComparison.Ordinal);

        using var state = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(fixtureDirectory, "state-store-initial.json.stream")));
        var matchState = state.RootElement.GetProperty("state");
        Assert.Equal("STOP", matchState.GetProperty("command").GetProperty("type").GetString());
        Assert.Equal("STOP", matchState.GetProperty("gameState").GetProperty("type").GetString());
        var teams = matchState.GetProperty("teamState");
        Assert.Equal("TIGERs Mannheim", teams.GetProperty("BLUE").GetProperty("name").GetString());
        Assert.Equal("ibis", teams.GetProperty("YELLOW").GetProperty("name").GetString());
        Assert.Equal("FRIENDLY", matchState.GetProperty("matchType").GetString());
    }

    [Fact]
    public async Task BaseModeDoesNotAddComparisonOnlyResources()
    {
        using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            [CreateIsolatedOwnershipLockArgument()]);

        Assert.DoesNotContain(appHost.Resources, resource => resource.Name is "tracker-tigers" or "tracker-erforce" or "debug-host");
    }

    [Fact]
    public async Task MatchModePinsSimulatorGameControllerAndFixtureConfiguration()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=match", "--Testing:Diagnostics:RunId=issue46-model-test");

        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        var simulatorImage = Assert.Single(simulator.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli", simulatorImage.Image);
        Assert.Null(simulatorImage.Tag);
        Assert.Equal("9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554", simulatorImage.SHA256);
        Assert.Equal(
            ["tini", "--", "./bin/simulator-cli", "-g", "2020B", "--realism", "None", "--ibis-port", "12346", "--ibis-team-color", "yellow"],
            await GetArgumentsAsync(simulator, callbackLimit: 1));

        var gameController = Assert.IsType<ContainerResource>(Find(appHost, "game-controller"));
        var gameControllerImage = Assert.Single(gameController.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("robocupssl/ssl-game-controller", gameControllerImage.Image);
        Assert.Null(gameControllerImage.Tag);
        Assert.Equal("2c0a03394c37541b6d4df8ec5f97207c70b6ddbe553e04bc4c049ffc58b02ef4", gameControllerImage.SHA256);
        Assert.Equal(
            ["-visionAddress", "224.5.23.2:10020", "-trackerAddress", "224.5.23.2:11010", "-publishAddress", "224.5.23.1:11003", "-address", ":8082"],
            await GetArgumentsAsync(gameController));

        var controlEndpoint = Assert.Single(
            gameController.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Name == "game-controller-control");
        Assert.Null(controlEndpoint.Port);
        Assert.Equal(8082, controlEndpoint.TargetPort);
        Assert.Equal("127.0.0.1", controlEndpoint.TargetHost);
        Assert.False(controlEndpoint.IsProxied);
        Assert.DoesNotContain(
            gameController.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);

        var mounts = gameController.Annotations.OfType<ContainerMountAnnotation>().ToArray();
        Assert.Equal(3, mounts.Length);
        var matchConfig = Assert.Single(mounts, mount => mount.Target == "/config/ssl-game-controller.yaml");
        Assert.NotNull(matchConfig.Source);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "match", "ssl-game-controller-match.yaml")),
            Path.GetFullPath(matchConfig.Source!));
        Assert.True(matchConfig.IsReadOnly);
        var initialState = Assert.Single(mounts, mount => mount.Target == "/tmp/state-store.json.stream");
        Assert.NotNull(initialState.Source);
        var stateStoreKey = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("issue46-model-test")))
            .ToLowerInvariant();
        var expectedStateStorePath = Path.Combine(
            Path.GetTempPath(), "duck-aspire-match-state", $"{stateStoreKey}.json.stream");
        Assert.Equal(Path.GetFullPath(expectedStateStorePath), Path.GetFullPath(initialState.Source!));
        Assert.Equal(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "match", "state-store-initial.json.stream")),
            File.ReadAllText(initialState.Source!));
        Assert.False(initialState.IsReadOnly);
        var engineConfig = Assert.Single(mounts, mount => mount.Target == "/tmp/match-engine.json");
        var expectedEngineConfigPath = Path.Combine(
            Path.GetTempPath(), "duck-aspire-match-state", $"{stateStoreKey}.engine.json");
        Assert.Equal(Path.GetFullPath(expectedEngineConfigPath), Path.GetFullPath(engineConfig.Source!));
        Assert.Equal("{\"autoContinue\":false}", File.ReadAllText(engineConfig.Source!).Trim());
        Assert.False(engineConfig.IsReadOnly);
        Assert.Contains(
            "config-filename: /tmp/match-engine.json",
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "match", "ssl-game-controller-match.yaml")),
            StringComparison.Ordinal);

        var producerResources = new List<string>();
        foreach (var resource in new IResourceWithArgs[]
        {
            simulator,
            gameController,
            Assert.IsType<ContainerResource>(Find(appHost, "crane")),
            Assert.IsType<ContainerResource>(Find(appHost, "duck")),
        })
        {
            var arguments = await GetArgumentsAsync(resource, callbackLimit: resource.Name == "simulator" ? 1 : null);
            for (var index = 0; index + 1 < arguments.Length; index++)
            {
                if (arguments[index] == "-publishAddress" && arguments[index + 1] == "224.5.23.1:11003")
                {
                    producerResources.Add(resource.Name);
                }
            }
        }

        Assert.Equal(["game-controller"], producerResources);
    }

    [Fact]
    public async Task MatchModelCanComposeWithoutAgentButMissingAgentFailsItsPreflight()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=match");

        Assert.Contains(appHost.Resources, resource => resource.Name == "autoref-tigers");
        var autoRef = Assert.IsType<ContainerResource>(Find(appHost, "autoref-tigers"));
        var configuredAgentPath = Path.Combine(AppContext.BaseDirectory, "autoref-game-log-agent.jar");
        if (File.Exists(configuredAgentPath))
        {
            await GetResourceEnvironmentAsync(autoRef);
        }
        else
        {
            var callbackFailure = await Assert.ThrowsAsync<FileNotFoundException>(
                () => GetResourceEnvironmentAsync(autoRef));
            Assert.Contains("Match mode requires the AutoRef native game-log activation agent.", callbackFailure.Message);
            Assert.Equal(configuredAgentPath, callbackFailure.FileName);
        }

        var missingAgentPath = Path.Combine(Path.GetTempPath(), $"missing-autoref-agent-{Guid.NewGuid():N}.jar");
        var exception = Assert.Throws<FileNotFoundException>(
            () => AutoRefGameLogAgent.RequireAvailable(missingAgentPath));
        Assert.Contains("Match mode requires the AutoRef native game-log activation agent.", exception.Message);
        Assert.Equal(missingAgentPath, exception.FileName);
    }

    [Fact]
    public async Task MatchModeRoutesCraneThroughCm4WithoutSimulatorPacketAdapter()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=match");

        foreach (var name in new[] { "simulator", "game-controller", "crane", "duck" })
        {
            Assert.IsType<ContainerResource>(Find(appHost, name));
        }

        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var environment = await GetResourceEnvironmentAsync(duck);
        Assert.Equal("match", environment["Testing__Mode"].ToString());

        Assert.DoesNotContain(appHost.Resources, resource => resource.Name is "tracker-tigers" or "tracker-erforce");

        var simulator = Find(appHost, "simulator");
        var gameController = Find(appHost, "game-controller");
        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var craneImage = Assert.Single(crane.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/ibis-ssl/crane", craneImage.Image);
        Assert.Null(craneImage.Tag);
        Assert.Equal("40435ed83e7a6c4f79a029fe576f1e3c771b38f5606377b4450668ef5dd4392b", craneImage.SHA256);
        var craneArguments = await GetArgumentsAsync(crane);
        Assert.Contains("team:=ibis", craneArguments[^1], StringComparison.Ordinal);
        var craneEnvironment = await GetResourceEnvironmentAsync(crane);
        Assert.DoesNotContain("PLANNER", craneEnvironment.Keys);
        Assert.DoesNotContain("planner:=", craneArguments[^1], StringComparison.Ordinal);
        Assert.Equal("12345", craneEnvironment["CRANE_TARGET_PORT"].ToString());
        Assert.Equal("cm4-sim", craneEnvironment[CraneTargetAddressSettings.TargetAddressEnvironmentKey].ToString());
        Assert.Equal("cm4-sim", craneEnvironment[CraneTargetAddressSettings.ExpectedResourceEnvironmentKey].ToString());
        Assert.DoesNotContain("FEEDBACK_SIM_MODE", craneEnvironment.Keys);
        Assert.Equal("true", craneEnvironment["DUCK_CRANE_IBIS_PACKET_ADAPTER"].ToString());
        Assert.DoesNotContain("DUCK_CRANE_SIMULATOR_PACKET_ADAPTER", craneEnvironment.Keys);
        Assert.Contains(
            crane.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Target == CraneTargetAddressSettings.ScriptMountTarget && mount.IsReadOnly);
        Assert.Contains(CraneTargetAddressSettings.ScriptMountTarget, craneArguments[^1], StringComparison.Ordinal);
        Assert.Contains("sim:=true", craneArguments[^1], StringComparison.Ordinal);
        AssertArgumentValue(
            await GetArgumentsAsync(Assert.IsType<ContainerResource>(simulator), callbackLimit: 1),
            "--ibis-port",
            "12346");
        var cm4Simulator = Assert.IsType<ContainerResource>(Find(appHost, "cm4-sim"));
        var cm4Arguments = await GetArgumentsAsync(cm4Simulator);
        AssertArgumentValue(cm4Arguments, "--in-port", "12345");
        AssertArgumentValue(cm4Arguments, "--out-port", "12346");
        var duckEnvironment = await GetResourceEnvironmentAsync(duck);
        Assert.Equal("false", duckEnvironment["Tracker__PublishUdp"].ToString());
        Assert.DoesNotContain(
            duck.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp && endpoint.TargetPort == 11010);

        AssertSingleWait(duck, simulator, WaitType.WaitUntilStarted);
        AssertWaits(
            crane,
            (duck, WaitType.WaitUntilStarted),
            (gameController, WaitType.WaitUntilHealthy),
            (cm4Simulator, WaitType.WaitUntilStarted));

        foreach (var name in new[] { "simulator", "game-controller", "crane", "duck", "tigers-blue", "autoref-tigers", "ssl-log-recorder", "match-controller" })
        {
            var resource = Assert.IsType<ContainerResource>(Find(appHost, name));
            Assert.Contains(
                $"{DockerContainerSpec.OwnerLabel}={DockerContainerSpec.OwnerValue}",
                await GetContainerRuntimeArgsAsync(resource));
        }
    }

    [Fact]
    public async Task MatchModeAddsCm4WhenSelectedPlannerRequiresIt()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=match", "--Testing:Crane:Planner=visibility_graph");

        Assert.Contains(appHost.Resources, resource => resource.Name == "cm4-sim");
        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        Assert.Equal("cm4-sim", (await GetResourceEnvironmentAsync(crane))[
            CraneTargetAddressSettings.ExpectedResourceEnvironmentKey].ToString());
    }

    [Fact]
    public async Task MatchModeComposesLifecycleControllerAfterRequiredMatchResources()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=match",
            "--Testing:RefereeDriver:InterfaceAddress=192.0.2.10",
            "--Testing:Diagnostics:RunId=issue49-model-test");

        var controller = Assert.IsType<ContainerResource>(Find(appHost, "match-controller"));
        var environment = await GetResourceEnvironmentAsync(controller);
        Assert.Equal("true", environment["RefereeDriver__Enabled"].ToString());
        Assert.Equal("192.0.2.10", environment["RefereeDriver__InterfaceAddress"].ToString());
        Assert.Equal("true", environment["MatchController__Enabled"].ToString());
        Assert.Equal("true", environment["MatchController__SimulationControlRelayEnabled"].ToString());
        Assert.Equal("simulator", environment["MatchController__SimulatorHost"].ToString());
        Assert.Equal("http://duck:39108/health/vision", environment["MatchController__VisionHealthUri"].ToString());
        Assert.Equal("/match-results/match-result.json", environment["MatchController__ResultPath"].ToString());
        Assert.Equal("/match-results/participant-status.json", environment["MatchController__ParticipantStatusPath"].ToString());
        Assert.Contains("tigers-blue", environment["MatchController__ParticipantResources"].ToString());

        var resultMount = Assert.Single(controller.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Target == "/match-results");
        Assert.False(resultMount.IsReadOnly);

        var tigers = Assert.IsType<ContainerResource>(Find(appHost, "tigers-blue"));
        var sumatraUser = Assert.Single(tigers.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Target == "/Sumatra/config/user.xml");
        Assert.NotNull(sumatraUser.Source);
        Assert.Contains("<staticHostname>match-controller</staticHostname>",
            await File.ReadAllTextAsync(sumatraUser.Source!), StringComparison.Ordinal);

        AssertWaits(
            controller,
            (Find(appHost, "simulator"), WaitType.WaitUntilHealthy),
            (Find(appHost, "game-controller"), WaitType.WaitUntilHealthy),
            (Find(appHost, "crane"), WaitType.WaitUntilHealthy),
            (Find(appHost, "duck"), WaitType.WaitUntilHealthy),
            (Find(appHost, "tigers-blue"), WaitType.WaitUntilStarted),
            (Find(appHost, "autoref-tigers"), WaitType.WaitUntilStarted),
            (Find(appHost, "ssl-log-recorder"), WaitType.WaitUntilStarted));

        var simulatorImage = Assert.Single(Find(appHost, "simulator").Annotations.OfType<ContainerImageAnnotation>());
        Assert.Null(simulatorImage.Tag);
        Assert.Equal("9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554", simulatorImage.SHA256);
        var gameControllerImage = Assert.Single(Find(appHost, "game-controller").Annotations.OfType<ContainerImageAnnotation>());
        Assert.Null(gameControllerImage.Tag);
        Assert.Equal("2c0a03394c37541b6d4df8ec5f97207c70b6ddbe553e04bc4c049ffc58b02ef4", gameControllerImage.SHA256);
        var craneImage = Assert.Single(Find(appHost, "crane").Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/ibis-ssl/crane", craneImage.Image);
        Assert.Null(craneImage.Tag);
        Assert.Equal("40435ed83e7a6c4f79a029fe576f1e3c771b38f5606377b4450668ef5dd4392b", craneImage.SHA256);
    }

    [Fact]
    public async Task MatchReadinessOnlyModeRegistersParticipantsWithoutControlWorker()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=match",
            "--Testing:MatchReadinessOnly=true",
            "--Testing:Diagnostics:RunId=issue114-readiness-only-model-test");

        Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "match-controller");
        Assert.Contains(appHost.Resources, resource => resource.Name == "tigers-blue");
        Assert.Contains(appHost.Resources, resource => resource.Name == "autoref-tigers");
        Assert.Contains(appHost.Resources, resource => resource.Name == "ssl-log-recorder");
        Assert.Contains(appHost.Resources, resource => resource.Name == "simulator");
        Assert.Contains(appHost.Resources, resource => resource.Name == "game-controller");
        Assert.Contains(appHost.Resources, resource => resource.Name == "crane");
        Assert.Contains(appHost.Resources, resource => resource.Name == "duck");
    }

    [Fact]
    public async Task MatchModeHandsOffTheControllerContainerIdentityWhenDesktopBridgeIsConfigured()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=match",
            "--Testing:Diagnostics:RunId=issue49-identity-model-test",
            "--Testing:IdentityControl:DesktopBridgeEnabled=true",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");

        var controller = Assert.IsType<ContainerResource>(Find(appHost, "match-controller"));
        var environment = await GetResourceEnvironmentAsync(controller);
        Assert.Equal("true", environment["Testing__IdentityControl__Enabled"].ToString());
        Assert.Equal("match-controller", environment["Testing__IdentityControl__ExpectedRole"].ToString());
        Assert.Equal("issue49-identity-model-test", environment["Testing__Diagnostics__RunId"].ToString());
    }

    [Fact]
    public async Task MatchModeRegistersTigersAutoRefAndLogRecorderOnSharedDcpBridge()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=match");

        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var duckEnvironment = await GetResourceEnvironmentAsync(duck);
        Assert.Equal("true", duckEnvironment["Tracker__Receive__Enabled"].ToString());
        Assert.Equal("224.5.23.2", duckEnvironment["Tracker__Receive__MulticastAddress"].ToString());
        Assert.Equal("11010", duckEnvironment["Tracker__Receive__Port"].ToString());

        var tigers = Assert.IsType<ContainerResource>(Find(appHost, "tigers-blue"));
        var tigersImage = Assert.Single(tigers.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("tigersmannheim/sumatra", tigersImage.Image);
        Assert.Null(tigersImage.Tag);
        Assert.Equal("0c05451f26aeb21bffde4c3d6858b6f441bd6bfaf8286f0ecd799241cb1fbc6c", tigersImage.SHA256);
        Assert.Equal(
            ["--headless", "--aiBlue", "--visionAddress", "224.5.23.2:10020", "--refereeAddress", "224.5.23.1:11003", "--matchStats", "--moduli", "simulation_protocol"],
            await GetArgumentsAsync(tigers));
        var sumatraMounts = tigers.Annotations.OfType<ContainerMountAnnotation>().ToArray();
        Assert.Equal(2, sumatraMounts.Length);
        var sumatraConfig = Assert.Single(sumatraMounts, mount => mount.Target == "/Sumatra/config/moduli/simulation_protocol.xml");
        Assert.Equal(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "match", "simulation_protocol_tigers.xml")),
            Path.GetFullPath(sumatraConfig.Source!));
        Assert.True(sumatraConfig.IsReadOnly);
        var sumatraUserConfig = Assert.Single(sumatraMounts, mount => mount.Target == "/Sumatra/config/user.xml");
        var expectedUserConfigPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "match", "sumatra-user.xml"));
        Assert.Equal(expectedUserConfigPath, Path.GetFullPath(sumatraUserConfig.Source!));
        Assert.True(File.Exists(expectedUserConfigPath));
        Assert.True(sumatraUserConfig.IsReadOnly);
        var sumatraUserConfigXml = XDocument.Load(expectedUserConfigPath);
        Assert.Equal("edu.tigers.sumatra.simulation.SimulationControlModule", (string?)sumatraUserConfigXml.Root?.Attribute("base"));
        Assert.Equal("true", sumatraUserConfigXml.Root?.Element("controlBlueTeam")?.Value);
        Assert.Equal("false", sumatraUserConfigXml.Root?.Element("controlYellowTeam")?.Value);

        var autoRef = Assert.IsType<ContainerResource>(Find(appHost, "autoref-tigers"));
        var autoRefImage = Assert.Single(autoRef.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("tigersmannheim/auto-referee", autoRefImage.Image);
        Assert.Null(autoRefImage.Tag);
        Assert.Equal("96dd8417c1fcf4025ee0a20c8210954068440991989587b68ecdea7d27db6b3d", autoRefImage.SHA256);
        Assert.Equal(
            ["--active", "--headless", "--visionAddress", "224.5.23.2:10020", "--refereeAddress", "224.5.23.1:11003", "--trackerAddress", "224.5.23.2:11010"],
            await GetArgumentsAsync(autoRef));
        var autoRefConfigPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "match", "autoreferee.xml"));
        Assert.True(File.Exists(autoRefConfigPath));
        var autoRefConfigMount = Assert.Single(
            autoRef.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Target == "/config/moduli/autoreferee.xml");
        Assert.Equal(autoRefConfigPath, Path.GetFullPath(autoRefConfigMount.Source!));
        Assert.True(autoRefConfigMount.IsReadOnly);
        var autoRefGameLogMount = Assert.Single(
            autoRef.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Target == "/data/gamelog");
        Assert.False(autoRefGameLogMount.IsReadOnly);

        var autoRefModules = XDocument.Load(autoRefConfigPath).Root?.Elements("module").ToArray();
        Assert.NotNull(autoRefModules);
        Assert.Contains(autoRefModules, module =>
            (string?)module.Attribute("id") == "edu.tigers.sumatra.gamelog.GameLogRecorder");
        Assert.Contains(autoRefModules, module =>
            (string?)module.Attribute("id") == "edu.tigers.autoreferee.module.AutoRefModule");
        Assert.Equal(
            "edu.tigers.autoreferee.AutoRefRecordManager",
            (string?)autoRefModules.Single(module =>
                (string?)module.Attribute("id") == "edu.tigers.sumatra.persistence.RecordManager")
                .Element("implementation"));

        var recorder = Assert.IsType<ContainerResource>(Find(appHost, "ssl-log-recorder"));
        var recorderImage = Assert.Single(recorder.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("robocupssl/ssl-log-recorder", recorderImage.Image);
        Assert.Null(recorderImage.Tag);
        Assert.Equal("d7ee410736512340602baa88f03762bfc3c5b590a3f91097e5b7e0bb45b7207b", recorderImage.SHA256);
        Assert.Equal(
            ["-referee-address", "224.5.23.1:11003", "-vision-address", "224.5.23.2:10020", "-vision-tracker-address", "224.5.23.2:11010", "-vision-legacy-enabled=false"],
            await GetArgumentsAsync(recorder));

        foreach (var resource in new[] { tigers, autoRef, recorder })
        {
            Assert.DoesNotContain(
                resource.Annotations.OfType<EndpointAnnotation>(),
                endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
            Assert.DoesNotContain("--network", await GetContainerRuntimeArgsAsync(resource));
        }

        using var imageLock = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "Testing", "Duck.Testing.AppHost", "linux-image-lock.json")));
        var lockedImages = imageLock.RootElement.GetProperty("images");
        Assert.Equal("roboticserlangen/autoref:2025.1.0", lockedImages.GetProperty("autoref").GetProperty("reference").GetString());
        Assert.Equal("sha256:de4f06a9bcfad524b3bae2015f92bb3fc3a209295eb668a34d5a0b329616bf68",
            lockedImages.GetProperty("autoref").GetProperty("digest").GetString());
        Assert.Equal("tigersmannheim/sumatra:latest", lockedImages.GetProperty("sumatra").GetProperty("reference").GetString());
        Assert.Equal("sha256:9ad3f65410f1c33b5b00400390dd3fb85c35ba6d76500fa68b4d51b4c3cd90c1",
            lockedImages.GetProperty("sumatra").GetProperty("digest").GetString());
        Assert.Equal(
            "sha256:" + tigersImage.SHA256,
            lockedImages.GetProperty("sumatra").GetProperty("platforms").EnumerateArray()
                .Single(platform => platform.GetProperty("architecture").GetString() == "amd64")
                .GetProperty("digest").GetString());
        Assert.Equal("robocupssl/ssl-log-recorder:latest", lockedImages.GetProperty("ssl-log-recorder").GetProperty("reference").GetString());
        Assert.Equal("sha256:907f2cd8d81cc09a5bf53a9801c2ed3af4d8eca5c9ee401d0badc35c30c75046",
            lockedImages.GetProperty("ssl-log-recorder").GetProperty("digest").GetString());
        Assert.Equal(
            "sha256:" + recorderImage.SHA256,
            lockedImages.GetProperty("ssl-log-recorder").GetProperty("platforms").EnumerateArray()
                .Single(platform => platform.GetProperty("architecture").GetString() == "amd64")
                .GetProperty("digest").GetString());
        var recorderRuntimeArgs = await GetContainerRuntimeArgsAsync(recorder);
        Assert.Equal(["--stop-signal", "SIGINT", "--stop-timeout", "30"], recorderRuntimeArgs.TakeLast(4));
        Assert.Equal("tigersmannheim/auto-referee:latest", lockedImages.GetProperty("tigers-auto-referee").GetProperty("reference").GetString());
        Assert.Equal("sha256:e3571a2c10218522483d25261426681dd75a734f66c0c85877fec61481a158a3",
            lockedImages.GetProperty("tigers-auto-referee").GetProperty("digest").GetString());
        Assert.Equal(
            "sha256:" + autoRefImage.SHA256,
            lockedImages.GetProperty("tigers-auto-referee").GetProperty("platforms").EnumerateArray()
                .Single(platform => platform.GetProperty("architecture").GetString() == "amd64")
                .GetProperty("digest").GetString());
        Assert.Equal("sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79",
            lockedImages.GetProperty("framework-simulator").GetProperty("digest").GetString());
        Assert.Equal("sha256:f407603f592d46c5d1e17b1505e3869e1e05de645933a69eba93833da132c98b",
            lockedImages.GetProperty("game-controller").GetProperty("digest").GetString());
        Assert.Equal("sha256:ac3f6d0e59968add4392771bba91bd265910a4d853944452eb2a805e78a363e4",
            lockedImages.GetProperty("crane").GetProperty("digest").GetString());
    }
    [Theory]
    [InlineData("comparision")]
    public async Task UnsupportedModeFailsDuringAppHostCreation(string mode)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
                [$"--Testing:Mode={mode}", CreateIsolatedOwnershipLockArgument()]);
        });

        Assert.Contains("Unsupported Testing:Mode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComparisonModelKeepsPinnedSimulatorCraneAndCm4ArgumentsOnDcpContainers()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison");
        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        var simulatorImage = Assert.Single(simulator.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli", simulatorImage.Image);
        Assert.Equal("a52b6bd", simulatorImage.Tag);
        Assert.Equal(
            ["tini", "--", "./bin/simulator-cli", "-g", "2020B", "--realism", "None", "--ibis-port", "12346", "--ibis-team-color", "yellow"],
            await GetArgumentsAsync(simulator, callbackLimit: 1));

        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var craneImage = Assert.Single(crane.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/ibis-ssl/crane", craneImage.Image);
        Assert.Equal("scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d", craneImage.Tag);
        var craneArgs = await GetArgumentsAsync(crane);
        Assert.Contains("team:=crane", craneArgs[^1]);
        Assert.Contains("team_color:=YELLOW", craneArgs[^1]);
        var craneEnvironment = await GetResourceEnvironmentAsync(crane);
        Assert.Equal("visibility_graph", craneEnvironment["PLANNER"].ToString());
        Assert.Equal("12345", craneEnvironment["CRANE_TARGET_PORT"].ToString());
        Assert.DoesNotContain("FEEDBACK_SIM_MODE", craneEnvironment.Keys);
        Assert.Equal("cm4-sim", craneEnvironment["DUCK_CRANE_EXPECTED_RESOURCE"].ToString());
        var craneOverlayMount = Assert.Single(
            crane.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Target == CraneTargetAddressSettings.ScriptMountTarget);
        Assert.EndsWith("crane-launch-overlay", craneOverlayMount.Source, StringComparison.Ordinal);
        Assert.True(craneOverlayMount.IsReadOnly);
        Assert.True(File.Exists(craneOverlayMount.Source));
        Assert.Contains(CraneTargetAddressSettings.ScriptMountTarget, craneArgs[^1], StringComparison.Ordinal);

        var cm4Simulator = Assert.IsType<ContainerResource>(Find(appHost, "cm4-sim"));
        var cm4Image = Assert.Single(cm4Simulator.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/ibis-ssl/orion-cm4-sim", cm4Image.Image);
        Assert.Equal("d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a", cm4Image.Tag);
        var cm4Args = await GetArgumentsAsync(cm4Simulator);
        AssertArgumentValue(cm4Args, "--in-port", "12345");
        AssertArgumentValue(cm4Args, "--out-port", "12346");
        Assert.DoesNotContain("--network", await GetContainerRuntimeArgsAsync(cm4Simulator));
    }

    [Fact]
    public async Task CraneLaunchUsesCraneTeamNameAndSeparateYellowColor()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison");
        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var arguments = await GetArgumentsAsync(crane);
        var duckEnvironment = await GetResourceEnvironmentAsync(duck);

        Assert.Contains("team:=crane", arguments[^1]);
        Assert.Contains("team_color:=YELLOW", arguments[^1]);
        Assert.DoesNotContain("Tracker__PublishUdp", duckEnvironment.Keys);
    }

    [Fact]
    public async Task ExplicitCraneTargetAddressUsesTheOwnedOverlayConsumer()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison", "--Testing:Crane:TargetAddress=cm4-sim");
        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var craneEnvironment = await GetResourceEnvironmentAsync(crane);

        Assert.Equal("cm4-sim", craneEnvironment["DUCK_CRANE_TARGET_ADDRESS"].ToString());
        Assert.Equal("cm4-sim", craneEnvironment["DUCK_CRANE_EXPECTED_RESOURCE"].ToString());
        Assert.True(
            (await GetArgumentsAsync(crane))[^1].Contains("/duck/crane-launch-overlay", StringComparison.Ordinal));
        var overlayMount = Assert.Single(crane.Annotations.OfType<ContainerMountAnnotation>(), mount => mount.Target == "/duck/crane-launch-overlay");
        Assert.True(overlayMount.IsReadOnly);
        Assert.True(File.Exists(overlayMount.Source));
    }

    [Theory]
    [InlineData("simulator")]
    [InlineData("outside.example")]
    [InlineData("::1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("127.0.0.2")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CraneTargetAddressRejectsValuesOutsideTheSelectedResourceOrSafeIpv4(string address)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var appHost = await CreateAppHostAsync("--Testing:Crane:TargetAddress=" + address);
        });

        Assert.Contains("Testing:Crane:TargetAddress", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CraneSimulatorPlannerAcceptsOnlyItsSelectedResourceTarget()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=comparison",
            "--Testing:Crane:Planner=rvo2",
            "--Testing:Crane:TargetAddress=simulator");

        Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "cm4-sim");
        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var craneEnvironment = await GetResourceEnvironmentAsync(crane);
        Assert.Equal("simulator", craneEnvironment["DUCK_CRANE_EXPECTED_RESOURCE"].ToString());
        Assert.Equal("simulator", craneEnvironment["DUCK_CRANE_TARGET_ADDRESS"].ToString());
    }

    [Fact]
    public async Task CraneRejectsHostLoopbackAddressInDcpMode()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison", "--Testing:Crane:TargetAddress=127.0.0.1");
        });

        Assert.Contains("Testing:Crane:TargetAddress", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomCraneTeamNameIsSharedByCraneAndGameController()
    {
        const string teamName = "custom_crane_team";
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison", "--Testing:Crane:TeamName=" + teamName);

        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var craneArgs = await GetArgumentsAsync(crane);
        var gameController = Assert.IsType<ContainerResource>(Find(appHost, "game-controller"));
        Assert.NotEmpty(await GetArgumentsAsync(gameController));

        Assert.Contains($"team:={teamName}", craneArgs[^1]);
    }

    [Fact]
    public async Task AppHostWritesResolvedCliTeamSettingsToAnOwnedManifest()
    {
        var stackId = "manifest-" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "duck-crane-model-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "fixture.json");
        try
        {
            using var appHost = await CreateAppHostAsync(
                "--Testing:Crane:TeamName=custom_from_cli",
                "--Testing:Crane:FixtureManifestPath=" + path,
                "--Testing:StackOwnership:StackId=" + stackId);
            var settings = CraneFixtureManifest.ReadForStack(path, stackId);
            Assert.Equal("custom_from_cli", settings.TeamName);
            Assert.Equal(CraneFixtureSettings.YellowTeamColor, settings.TeamColor);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("crane team")]
    [InlineData("crane;touch_tmp")]
    [InlineData("crane$(id)")]
    public async Task CraneTeamNameRejectsWhitespaceAndShellSyntax(string teamName)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var appHost = await CreateAppHostAsync("--Testing:Crane:TeamName=" + teamName);
        });

        Assert.Contains("Testing:Crane:TeamName", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GameControllerIsTheOnlyRefereeProducerOnDcpResources()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison");
        var gameController = Assert.IsType<ContainerResource>(Find(appHost, "game-controller"));
        Assert.Equal(
            ["-visionAddress", "224.5.23.2:10020", "-trackerAddress", "224.5.23.2:11010", "-publishAddress", "224.5.23.1:11003", "-address", ":8082"],
            await GetArgumentsAsync(gameController));

        var producers = new List<string>();
        foreach (var resource in appHost.Resources.OfType<ContainerResource>()
                     .Where(resource => resource.Name is "simulator" or "game-controller" or "crane" or "cm4-sim" or "tracker-tigers" or "tracker-erforce"))
        {
            var resourceArgs = await GetArgumentsAsync(resource, callbackLimit: resource.Name == "simulator" ? 1 : null);
            if (PublishesRefereeEndpoint(resourceArgs))
            {
                producers.Add(resource.Name);
            }
        }

        Assert.Equal(["game-controller"], producers);
    }

    [Fact]
    public async Task ComparisonDcpDependenciesUseServiceReadinessAndStartOrdering()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison");
        var simulator = Find(appHost, "simulator");
        var cm4 = Find(appHost, "cm4-sim");
        var duck = Find(appHost, "duck");
        var gameController = Find(appHost, "game-controller");
        var crane = Find(appHost, "crane");

        AssertSingleWait(duck, simulator, WaitType.WaitUntilStarted);
        AssertWaits(crane, (duck, WaitType.WaitUntilStarted), (gameController, WaitType.WaitUntilHealthy), (cm4, WaitType.WaitUntilStarted));
        AssertSingleWait(simulator, cm4, WaitType.WaitUntilStarted);
    }

    [Fact]
    public async Task ConfigOverridesAndNonVisibilityPlannerChangeDcpResourceConfigurationAndDependencies()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=comparison",
            "--Testing:Simulator:ImageTag=custom-tag",
            "--Testing:Simulator:Geometry=custom-geometry",
            "--Testing:Simulator:Realism=Custom",
            "--Testing:Simulator:IbisPort=22346",
            "--Testing:Simulator:IbisTeamColor=blue",
            "--Testing:Crane:Planner=rvo2");

        Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "cm4-sim");
        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        var simulatorImage = Assert.Single(simulator.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli", simulatorImage.Image);
        Assert.Equal("custom-tag", simulatorImage.Tag);
        var simulatorArgs = await GetArgumentsAsync(simulator, callbackLimit: 1);
        Assert.Contains("custom-geometry", simulatorArgs);
        Assert.Contains("Custom", simulatorArgs);
        Assert.Contains("22346", simulatorArgs);
        Assert.Contains("blue", simulatorArgs);

        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var craneEnvironment = await GetResourceEnvironmentAsync(crane);
        Assert.Equal("rvo2", craneEnvironment["PLANNER"].ToString());
        Assert.Equal("22346", craneEnvironment["CRANE_TARGET_PORT"].ToString());
        Assert.Equal("true", craneEnvironment["FEEDBACK_SIM_MODE"].ToString());
        Assert.Equal("simulator", craneEnvironment[CraneTargetAddressSettings.TargetAddressEnvironmentKey].ToString());
        Assert.Equal("simulator", craneEnvironment[CraneTargetAddressSettings.ExpectedResourceEnvironmentKey].ToString());
        Assert.Contains(
            crane.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Target == CraneTargetAddressSettings.ScriptMountTarget && mount.IsReadOnly);
        Assert.Contains(
            CraneTargetAddressSettings.ScriptMountTarget,
            (await GetArgumentsAsync(crane))[^1],
            StringComparison.Ordinal);
        AssertWaits(crane, (Find(appHost, "duck"), WaitType.WaitUntilStarted), (Find(appHost, "game-controller"), WaitType.WaitUntilHealthy));
    }

    [Fact]
    public async Task ComparisonDcpDoesNotRejectAnOccupiedHostSimulatorUdpPort()
    {
        using var occupiedPort = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));
        var port = ((System.Net.IPEndPoint)occupiedPort.Client.LocalEndPoint!).Port;
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=comparison",
            "--Testing:Simulator:IbisPort=" + port);

        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        AssertArgumentValue(await GetArgumentsAsync(simulator, callbackLimit: 1), "--ibis-port", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ComparisonDuckUsesTheSimVisionProfileAndStartsWithSimulator()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison");
        var simulator = Find(appHost, "simulator");
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var executionConfiguration = await ExecutionConfigurationBuilder.Create(duck)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new(DistributedApplicationOperation.Run),
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                CancellationToken.None);
        var environment = executionConfiguration.EnvironmentVariables.ToDictionary();
        var wait = Assert.Single(duck.Annotations.OfType<WaitAnnotation>());

        Assert.Equal("sim", environment["Tracker__ActiveProfileName"]);
        Assert.Equal("224.5.23.2", environment["VisionReceiver__MulticastAddress"]);
        Assert.Equal("10020", environment["VisionReceiver__Port"]);
        Assert.Same(simulator, wait.Resource);
        Assert.Equal(WaitType.WaitUntilStarted, wait.WaitType);
    }

    [Fact]
    public async Task StandardBaseDuckStartsAfterSimulatorStartsBeforeVisionReadiness()
    {
        using var appHost = await CreateAppHostAsync();
        var simulator = Find(appHost, "simulator");
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));

        AssertWaits(duck, (simulator, WaitType.WaitUntilStarted));
    }

    [Fact]
    public async Task StandardBaseCraneStartsAfterCm4StartsWithoutWaitingForCm4Readiness()
    {
        using var appHost = await CreateAppHostAsync();
        var crane = Find(appHost, "crane");
        var duck = Find(appHost, "duck");
        var gameController = Find(appHost, "game-controller");
        var cm4 = Find(appHost, "cm4-sim");

        AssertWaits(
            crane,
            (duck, WaitType.WaitUntilStarted),
            (gameController, WaitType.WaitUntilHealthy),
            (cm4, WaitType.WaitUntilStarted));
        Assert.Contains(
            Assert.IsType<ContainerResource>(cm4).Annotations.OfType<HealthCheckAnnotation>(),
            annotation => annotation.Key == "dcp-cm4-sim-service-ready");
    }

    [Fact]
    public async Task StandardDcpDuckUsesExistingIdentityHandoffWhenContextIsConfigured()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=comparison",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var environment = await GetResourceEnvironmentAsync(duck);
        var runtimeArguments = await GetContainerRuntimeArgsAsync(duck);

        Assert.Equal("true", environment["Testing__IdentityControl__Enabled"]?.ToString());
        Assert.Equal("duck", environment["Testing__IdentityControl__ExpectedRole"]?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(environment["Testing__Diagnostics__RunId"]?.ToString()));
        Assert.Contains(runtimeArguments, argument => argument.StartsWith("com.ibis-ssl.duck.run-id=", StringComparison.Ordinal));
        Assert.Contains("com.ibis-ssl.duck.role=duck", runtimeArguments);
    }

    [Fact]
    public async Task StandardDcpDuckRespectsExplicitlyDisabledIdentityHandoff()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=comparison",
            "--Testing:IdentityControl:DesktopBridgeEnabled=false",
            "--Testing:IdentityControl:DockerContext=desktop-linux",
            "--Testing:IdentityControl:ExpectedEngineId=expected-engine");
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var environment = await GetResourceEnvironmentAsync(duck);
        var runtimeArguments = await GetContainerRuntimeArgsAsync(duck);

        Assert.DoesNotContain("Testing__IdentityControl__Enabled", environment.Keys);
        Assert.DoesNotContain(runtimeArguments, argument => argument == "com.ibis-ssl.duck.role=duck");
    }

    [Fact]
    public async Task StandardBaseDuckPublishesDecodedVisionReadinessWithoutWaitingOnItsOwnHealth()
    {
        using var appHost = await CreateAppHostAsync();
        var simulator = Find(appHost, "simulator");
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var environment = await GetResourceEnvironmentAsync(duck);
        var endpoint = Assert.Single(
            duck.Annotations.OfType<EndpointAnnotation>(),
            annotation => annotation.Name == "vision-health");
        var healthCheck = Assert.Single(duck.Annotations.OfType<HealthCheckAnnotation>());
        var wait = Assert.Single(duck.Annotations.OfType<WaitAnnotation>());

        Assert.Equal("true", environment["Testing__ContainerMode"].ToString());
        Assert.Equal("224.5.23.2", environment["VisionReceiver__MulticastAddress"].ToString());
        Assert.Equal("10020", environment["VisionReceiver__Port"].ToString());
        Assert.Equal(System.Net.Sockets.ProtocolType.Tcp, endpoint.Protocol);
        Assert.Equal(39108, endpoint.TargetPort);
        Assert.Null(endpoint.Port);
        Assert.Equal("127.0.0.1", endpoint.TargetHost);
        Assert.False(endpoint.IsProxied);
        Assert.NotNull(healthCheck);
        Assert.DoesNotContain(
            duck.Annotations.OfType<EndpointAnnotation>(),
            annotation => annotation.Protocol == System.Net.Sockets.ProtocolType.Udp);
        Assert.Same(simulator, wait.Resource);
        Assert.Equal(WaitType.WaitUntilStarted, wait.WaitType);
    }

    [Fact]
    public async Task ComparisonDuckHealthEndpointDoesNotRequireItsContainerTargetPortOnTheHost()
    {
        using var occupiedHostPort = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 39108);
        occupiedHostPort.Start();

        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison");
        var duck = Assert.IsType<ContainerResource>(Find(appHost, "duck"));
        var endpoint = Assert.Single(
            duck.Annotations.OfType<EndpointAnnotation>(),
            annotation => annotation.Name == "vision-health");

        Assert.Equal(39108, endpoint.TargetPort);
        Assert.Null(endpoint.Port);
    }

    [Theory]
    [InlineData("visibility_graph")]
    [InlineData("rvo2")]
    public async Task SimulatorIbisPortOverridePropagatesToCranePath(string planner)
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Mode=comparison",
            "--Testing:Simulator:IbisPort=22346",
            "--Testing:Crane:Planner=" + planner);

        var simulator = Assert.IsType<ContainerResource>(Find(appHost, "simulator"));
        AssertArgumentValue(await GetArgumentsAsync(simulator, callbackLimit: 1), "--ibis-port", "22346");

        var crane = Assert.IsType<ContainerResource>(Find(appHost, "crane"));
        var craneEnvironment = await GetResourceEnvironmentAsync(crane);
        Assert.Equal(planner, craneEnvironment["PLANNER"].ToString());
        if (planner == "visibility_graph")
        {
            var cm4Simulator = Assert.IsType<ContainerResource>(Find(appHost, "cm4-sim"));
            AssertArgumentValue(await GetArgumentsAsync(cm4Simulator), "--out-port", "22346");
            Assert.Equal("12345", craneEnvironment["CRANE_TARGET_PORT"].ToString());
        }
        else
        {
            Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "cm4-sim");
            Assert.Equal("22346", craneEnvironment["CRANE_TARGET_PORT"].ToString());
        }
    }

    [Fact]
    public async Task DcpCraneDiagnosticsPathIsNotInjectedIntoContainers()
    {
        var path = Path.Combine(Path.GetTempPath(), "duck-crane-model-" + Guid.NewGuid().ToString("N"), "crane-probe.jsonl");
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison", "--Testing:Crane:DiagnosticsPath=" + path);
        foreach (var name in new[] { "simulator", "game-controller", "crane", "cm4-sim" })
        {
            var resource = Assert.IsType<ContainerResource>(Find(appHost, name));
            var environment = await GetResourceEnvironmentAsync(resource);
            var runtimeArgs = await GetContainerRuntimeArgsAsync(resource);
            Assert.DoesNotContain(environment, entry => entry.Key.Contains("DiagnosticsPath", StringComparison.Ordinal));
            Assert.DoesNotContain(runtimeArgs, argument => argument.Contains(path, StringComparison.Ordinal));
        }
    }

    private static async Task<IDistributedApplicationTestingBuilder> CreateAppHostAsync(params string[] additionalArgs)
    {
        var captureRoot = Path.Combine(Path.GetTempPath(), "duck-debug-host-apphost-model-" + Guid.NewGuid().ToString("N"));
        var expectedEngineId = additionalArgs
            .LastOrDefault(argument => argument.StartsWith("--Testing:IdentityControl:ExpectedEngineId=", StringComparison.Ordinal))?
            .Split('=', 2)[1] ?? "model-test-engine";
        var fakeDockerExecutable = CreateFakeDockerExecutable(expectedEngineId);
        IDistributedApplicationTestingBuilder appHost;
        try
        {
            appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
                [
                    "--Testing:IdentityControl:DockerContext=desktop-linux",
                    "--Testing:IdentityControl:ExpectedEngineId=" + expectedEngineId,
                    "--Testing:IdentityControl:DockerExecutable=" + fakeDockerExecutable,
                    .. additionalArgs,
                    "--Testing:IdentityControl:DockerExecutable=" + fakeDockerExecutable,
                    "--Testing:DebugHost:CaptureRootPath=" + captureRoot,
                    CreateIsolatedOwnershipLockArgument(),
                ]);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(fakeDockerExecutable)!, recursive: true);
        }
        if (Directory.Exists(captureRoot))
        {
            Directory.Delete(captureRoot, recursive: true);
        }

        return appHost;
    }

    internal static string CreateFakeDockerExecutable(string engineId)
    {
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repositoryRoot is not null &&
            !File.Exists(Path.Combine(repositoryRoot.FullName, "Testing", "Duck.Testing.AppHost", "Duck.Testing.AppHost.csproj")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }
        Assert.NotNull(repositoryRoot);

        var configuration = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.Name;
        var fakeProjectOutput = Path.Combine(
            repositoryRoot!.FullName, "Testing", "Duck.Testing.FakeDockerCli", "bin", configuration, "net10.0");
        var executableName = OperatingSystem.IsWindows() ? "Duck.Testing.FakeDockerCli.exe" : "Duck.Testing.FakeDockerCli";
        var sourceExecutable = Path.Combine(fakeProjectOutput, executableName);
        Assert.True(File.Exists(sourceExecutable), $"Fake Docker CLI was not built: {sourceExecutable}");

        var directory = Path.Combine(Path.GetTempPath(), "duck-fake-docker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var suffix in new[] { executableName, "Duck.Testing.FakeDockerCli.dll", "Duck.Testing.FakeDockerCli.deps.json", "Duck.Testing.FakeDockerCli.runtimeconfig.json" })
        {
            var source = Path.Combine(fakeProjectOutput, suffix);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(directory, suffix));
        }
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(Path.Combine(directory, executableName), File.GetUnixFileMode(sourceExecutable));
        File.WriteAllText(Path.Combine(directory, "engine-id.txt"), engineId);
        return Path.Combine(directory, executableName);
    }

    private static IResource Find(IDistributedApplicationTestingBuilder appHost, string name) =>
        Assert.Single(appHost.Resources, resource => resource.Name == name);

    private static void AssertLinuxDistribution(
        ContainerResource resource,
        string projectPath,
        string entryAssembly,
        string expectedFrom)
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectDirectory = Path.Combine(repositoryRoot, projectPath);
        var build = Assert.Single(resource.Annotations.OfType<DockerfileBuildAnnotation>());

        Assert.Equal(
            Path.GetFullPath(Path.Combine(projectDirectory, "bin", "Release", "net10.0", "publish")),
            Path.GetFullPath(build.ContextPath));
        Assert.Equal(Path.GetFullPath(Path.Combine(projectDirectory, "Dockerfile")), Path.GetFullPath(build.DockerfilePath));
        Assert.Contains(
            File.ReadAllLines(build.DockerfilePath),
            line => string.Equals(line.Trim(), "FROM " + expectedFrom, StringComparison.Ordinal));
        Assert.Contains(
            File.ReadAllLines(build.DockerfilePath),
            line => string.Equals(line.Trim(), "ENTRYPOINT [\"dotnet\", \"" + entryAssembly + "\"]", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Testing", "Duck.Testing.AppHost", "Duck.Testing.AppHost.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Duck repository root could not be located from the test output directory.");
    }

    private static async Task<DockerWrapperLaunchOptions> GetLaunchOptionsAsync(ExecutableResource resource)
    {
        var launch = Assert.Single(resource.Annotations.OfType<DockerWrapperLaunchOptionsAnnotation>()).Options;
        var configuration = await ExecutionConfigurationBuilder.Create(resource)
            .WithArgumentsConfig()
            .BuildAsync(new(DistributedApplicationOperation.Run), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, CancellationToken.None);
        var arguments = configuration.Arguments.Select(value => value.Value?.ToString() ?? string.Empty).ToArray();
        var marker = Array.IndexOf(arguments, "--docker-wrapper");
        Assert.True(marker >= 0 && marker + 1 < arguments.Length, $"Wrapper argv was missing from {resource.Name}: {string.Join(" | ", arguments)}");
        var passedOptions = System.Text.Json.JsonSerializer.Deserialize<DockerWrapperLaunchOptions>(arguments[marker + 1]);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(launch), System.Text.Json.JsonSerializer.Serialize(passedOptions));
        return launch;
    }

    private static DockerContainerSpec ToSpec(DockerWrapperLaunchOptions launch) =>
        new(launch.ResourceName, launch.Image, launch.ContainerArguments, launch.Environment, launch.StackId, launch.RunId, launch.Mounts);

    private static void AssertArgumentValue(IReadOnlyList<string> arguments, string name, string expectedValue)
    {
        var index = Array.IndexOf(arguments.ToArray(), name);
        Assert.True(index >= 0 && index + 1 < arguments.Count, $"Missing value for {name}: {string.Join(" | ", arguments)}");
        Assert.Equal(expectedValue, arguments[index + 1]);
    }

    private static async Task<string[]> GetArgumentsAsync(IResourceWithArgs resource, int? callbackLimit = null)
    {
        var args = new List<object>();
        var callbacks = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>();
        if (callbackLimit is not null)
        {
            callbacks = callbacks.Take(callbackLimit.Value);
        }

        foreach (var annotation in callbacks)
        {
            await annotation.Callback(new CommandLineArgsCallbackContext(args, resource, CancellationToken.None));
        }

        return args.Select(value => value?.ToString() ?? string.Empty).ToArray();
    }

    private static bool PublishesRefereeEndpoint(IReadOnlyList<string> args)
    {
        for (var index = 0; index + 1 < args.Count; index++)
        {
            if (args[index] == "-publishAddress" && args[index + 1] == "224.5.23.1:11003")
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<IReadOnlyList<string>> GetContainerRuntimeArgsAsync(ContainerResource resource)
    {
        var args = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            await annotation.Callback(new ContainerRuntimeArgsCallbackContext(args, CancellationToken.None));
        }

        return args.Select(value => Assert.IsType<string>(value)).ToArray();
    }

    private static async Task<Dictionary<string, object>> GetResourceEnvironmentAsync(IResourceWithEnvironment resource)
    {
        var environmentVariables = new Dictionary<string, object>(StringComparer.Ordinal);
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            resource,
            environmentVariables,
            CancellationToken.None);

        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        return environmentVariables;
    }

    private static void AssertContainerImage(ContainerResource container, string expectedImage, string expectedTag)
    {
        var image = Assert.Single(container.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(expectedImage, image.Image);
        Assert.Equal(expectedTag, image.Tag);
    }

    private static string CreateIsolatedOwnershipLockArgument() =>
        $"--Testing:StackOwnership:LockPath={Path.Combine(Path.GetTempPath(), $"duck-aspire-model-{Guid.NewGuid():N}.lock")}";

    private static void AssertSingleWait(IResource resource, IResource dependency, WaitType type) =>
        AssertWaits(resource, (dependency, type));

    private static void AssertWaits(IResource resource, params (IResource Dependency, WaitType Type)[] dependencies)
    {
        var waits = resource.Annotations.OfType<WaitAnnotation>().ToArray();
        Assert.Equal(dependencies.Length, waits.Length);
        foreach (var (dependency, type) in dependencies)
        {
            var wait = Assert.Single(waits, annotation => ReferenceEquals(annotation.Resource, dependency));
            Assert.Equal(type, wait.WaitType);
        }
    }

    [Theory]
    [InlineData("aspire-probe", "aspire-probe", "10.1.0.2", "10.1.0.3", true)]
    [InlineData("default", "bridge", "10.1.0.2", "10.1.0.3", true)]
    [InlineData("host", "bridge", "10.1.0.2", "10.1.0.3", false)]
    [InlineData("none", "bridge", "10.1.0.2", "10.1.0.3", false)]
    [InlineData("", "bridge", "10.1.0.2", "10.1.0.3", false)]
    [InlineData("container:owner", "bridge", "10.1.0.2", "10.1.0.3", false)]
    [InlineData("unknown-mode", "bridge", "10.1.0.2", "10.1.0.3", false)]
    [InlineData("aspire-probe", "aspire-probe", "127.0.0.1", "10.1.0.3", false)]
    [InlineData("aspire-probe", "aspire-probe", "not-an-ip", "10.1.0.3", false)]
    public void BridgeVerifierRequiresOneSharedStandardNetworkAndRoutableAddresses(
        string mode,
        string networkName,
        string ownerAddress,
        string followerAddress,
        bool expected)
    {
        var owner = CreateBridgeInspect(mode, networkName, "a", ownerAddress);
        var follower = CreateBridgeInspect(mode, networkName, "a", followerAddress);

        if (expected)
        {
            var proof = NamespaceProbeBridgeNetworkVerifier.Verify(owner, follower);
            Assert.Equal(networkName, proof.NetworkName);
            Assert.Equal(ownerAddress, proof.OwnerAddress);
            Assert.Equal(followerAddress, proof.FollowerAddress);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() =>
                NamespaceProbeBridgeNetworkVerifier.Verify(owner, follower));
        }
    }

    [Fact]
    public void BridgeVerifierRejectsDifferentNetworkIdentitiesAndMultipleAttachments()
    {
        var owner = CreateBridgeInspect("aspire-probe", "aspire-probe", "a", "10.1.0.2");
        var differentNetwork = CreateBridgeInspect("aspire-probe", "aspire-probe", "b", "10.1.0.3");
        Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeBridgeNetworkVerifier.Verify(owner, differentNetwork));

        var duplicateAddress = CreateBridgeInspect("aspire-probe", "aspire-probe", "a", "10.1.0.2");
        Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeBridgeNetworkVerifier.Verify(owner, duplicateAddress));

        var multiple = """
            {"HostConfig":{"NetworkMode":"aspire-probe"},"NetworkSettings":{"Networks":{
              "aspire-probe":{"NetworkID":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","IPAddress":"10.1.0.3"},
              "other":{"NetworkID":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","IPAddress":"10.2.0.3"}
            }}}
            """;
        Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeBridgeNetworkVerifier.Verify(owner, multiple));
    }

    private static string CreateBridgeInspect(string mode, string networkName, string idVariant, string address)
    {
        var networkId = idVariant == "a"
            ? "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            : "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            HostConfig = new { NetworkMode = mode },
            NetworkSettings = new
            {
                Networks = new Dictionary<string, object>
                {
                    [networkName] = new { NetworkID = networkId, IPAddress = address },
                },
            },
        });
    }

}
