using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Duck.Testing.AppHost;
using System.Xml.Linq;

namespace Tracker.Tests;

public sealed class AppHostApplicationModelTests
{
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
    public async Task BaseModelKeepsDuckAsContainerAndEachDockerServiceAsHealthyExecutableWrapper()
    {
        using var appHost = await CreateAppHostAsync();

        Assert.IsType<ContainerResource>(Assert.Single(appHost.Resources, resource => resource.Name == "duck"));
        foreach (var name in new[] { "simulator", "game-controller", "crane", "cm4-sim" })
        {
            var resource = Assert.IsType<ExecutableResource>(Assert.Single(appHost.Resources, item => item.Name == name));
            Assert.Single(resource.Annotations.OfType<HealthCheckAnnotation>());
            var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), endpoint => endpoint.Name == "health");
            var launch = await GetLaunchOptionsAsync(resource);
            Assert.Equal(launch.HealthPort, endpoint.TargetPort);
            Assert.False(endpoint.IsProxied);
            Assert.Equal("http", endpoint.UriScheme);

            Assert.Equal(name, launch.ResourceName);
            Assert.NotEmpty(launch.RunId);
            Assert.InRange(launch.HealthPort, 1, 65535);
            var spec = ToSpec(launch);
            Assert.Contains("--network", spec.CreateRunArguments());
            Assert.Contains("host", spec.CreateRunArguments());
            Assert.Contains("duck.aspire.run=" + launch.RunId, spec.CreateRunArguments());
        }
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
    public async Task ComparisonModeUsesPinnedDebugHostPublishOutputAndLoopbackDynamicTcp()
    {
        using var appHost = await CreateAppHostAsync("--Testing:Mode=comparison");

        var debugHost = Assert.IsType<ContainerResource>(Find(appHost, "debug-host"));
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
    }

    [Fact]
    public async Task ExplicitRefereeDriverSelectionUsesPinnedPublishOutputAndInternalGameControllerAddress()
    {
        using var defaultAppHost = await CreateAppHostAsync();
        Assert.DoesNotContain(defaultAppHost.Resources, resource => resource.Name == "referee-driver");

        using var selectedAppHost = await CreateAppHostAsync(
            "--RefereeDriver:Enabled=true",
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
        Assert.Equal("true", environment["Testing__ContainerMode"].ToString());
        Assert.DoesNotContain(
            refereeDriver.Annotations.OfType<EndpointAnnotation>(),
            endpoint => endpoint.Protocol == System.Net.Sockets.ProtocolType.Udp);
    }

    [Fact]
    public async Task ComparisonModeAddsPinnedExternalTrackersAndObserverWithoutAnotherRefereeProducer()
    {
        using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            ["--Testing:Mode=comparison", CreateIsolatedOwnershipLockArgument()]);

        var simulator = Assert.IsType<ExecutableResource>(Assert.Single(appHost.Resources, resource => resource.Name == "simulator"));
        var gameController = Assert.IsType<ExecutableResource>(Assert.Single(appHost.Resources, resource => resource.Name == "game-controller"));
        var tigers = Assert.IsType<ExecutableResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "tracker-tigers"));
        var erForce = Assert.IsType<ExecutableResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "tracker-erforce"));
        var debugHost = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "debug-host"));

        var tigersLaunch = await GetLaunchOptionsAsync(tigers);
        var erForceLaunch = await GetLaunchOptionsAsync(erForce);
        Assert.Equal("tigersmannheim/sumatra:2025", tigersLaunch.Image);
        Assert.Equal("roboticserlangen/autoref:2025.1.0", erForceLaunch.Image);
        Assert.Contains("host", ToSpec(tigersLaunch).CreateRunArguments());
        Assert.Contains("host", ToSpec(erForceLaunch).CreateRunArguments());
        Assert.Equal("tracker-tigers", tigersLaunch.ReadinessProfile);
        Assert.Equal("TIGERs", tigersLaunch.ExpectedTrackerSourceName);
        Assert.Equal("tracker-erforce", erForceLaunch.ReadinessProfile);
        Assert.Equal("ER-FORCE", erForceLaunch.ExpectedTrackerSourceName);
        AssertWaits(tigers, (simulator, WaitType.WaitUntilHealthy), (gameController, WaitType.WaitUntilHealthy));
        AssertWaits(erForce, (simulator, WaitType.WaitUntilHealthy), (gameController, WaitType.WaitUntilHealthy));

        Assert.Equal(
            ["--headless", "--visionAddress", "224.5.23.2:10020", "--refereeAddress", "224.5.23.1:11003", "--moduli", "simulation_protocol_comparison"],
            tigersLaunch.ContainerArguments);
        var protocolMount = Assert.Single(tigersLaunch.Mounts ?? []);
        Assert.Equal("/Sumatra/config/moduli/simulation_protocol_comparison.xml", protocolMount.Target);
        Assert.True(protocolMount.ReadOnly);
        var mountArguments = ToSpec(tigersLaunch).CreateRunArguments();
        var volumeIndex = Array.IndexOf(mountArguments, "--volume");
        Assert.True(volumeIndex >= 0);
        Assert.EndsWith(":/Sumatra/config/moduli/simulation_protocol_comparison.xml:ro", mountArguments[volumeIndex + 1], StringComparison.Ordinal);

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
            erForceLaunch.ContainerArguments);

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
        AssertWaits(debugHost, (simulator, WaitType.WaitUntilStarted), (Find(appHost, "duck"), WaitType.WaitUntilStarted), (tigers, WaitType.WaitUntilHealthy), (erForce, WaitType.WaitUntilHealthy));

        var gameControllerLaunch = await GetLaunchOptionsAsync(gameController);
        Assert.True(PublishesRefereeEndpoint(gameControllerLaunch.ContainerArguments));
        Assert.False(PublishesRefereeEndpoint(tigersLaunch.ContainerArguments));
        Assert.False(PublishesRefereeEndpoint(erForceLaunch.ContainerArguments));
    }

    [Fact]
    public async Task BaseModeDoesNotAddComparisonOnlyResources()
    {
        using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            [CreateIsolatedOwnershipLockArgument()]);

        Assert.DoesNotContain(appHost.Resources, resource => resource.Name is "tracker-tigers" or "tracker-erforce" or "debug-host");
    }

    [Theory]
    [InlineData("comparision")]
    [InlineData("match")]
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
    public async Task BaseModelKeepsPinnedSimulatorAndCraneArguments()
    {
        using var appHost = await CreateAppHostAsync();
        var simulator = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "simulator")));
        Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd", simulator.Image);
        Assert.Equal(
            ["tini", "--", "./bin/simulator-cli", "-g", "2020B", "--realism", "None", "--ibis-port", "12346", "--ibis-team-color", "yellow"],
            simulator.ContainerArguments);

        var crane = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "crane")));
        Assert.Equal("ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d", crane.Image);
        Assert.Contains("team:=crane", crane.ContainerArguments[^1]);
        Assert.Contains("team_color:=YELLOW", crane.ContainerArguments[^1]);
        Assert.Equal("visibility_graph", crane.Environment["PLANNER"]);
        Assert.Equal("12345", crane.Environment["CRANE_TARGET_PORT"]);
        Assert.Equal(360, crane.StartupTimeoutSeconds);
        Assert.DoesNotContain(crane.Environment, item => item.Key == "FEEDBACK_SIM_MODE");

        var cm4Simulator = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "cm4-sim")));
        Assert.Equal("ghcr.io/ibis-ssl/orion-cm4-sim:d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a", cm4Simulator.Image);
        AssertArgumentValue(ToSpec(cm4Simulator).CreateRunArguments(), "--network", "host");
        AssertArgumentValue(cm4Simulator.ContainerArguments, "--in-port", "12345");
        AssertArgumentValue(cm4Simulator.ContainerArguments, "--out-port", "12346");
    }

    [Fact]
    public async Task CraneLaunchUsesCraneTeamNameAndSeparateYellowColor()
    {
        using var appHost = await CreateAppHostAsync();
        var crane = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "crane")));

        Assert.Contains("team:=crane", crane.ContainerArguments[^1]);
        Assert.Contains("team_color:=YELLOW", crane.ContainerArguments[^1]);
    }

    [Fact]
    public async Task CustomCraneTeamNameIsSharedByCraneAndGameController()
    {
        const string teamName = "custom_crane_team";
        using var appHost = await CreateAppHostAsync("--Testing:Crane:TeamName=" + teamName);

        var crane = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "crane")));
        var gameController = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "game-controller")));

        Assert.Contains($"team:={teamName}", crane.ContainerArguments[^1]);
        Assert.Equal(teamName, gameController.ExpectedCraneTeamName);
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
    public async Task GameControllerIsTheOnlyRefereeProducerAndUsesPinnedHostNetworkContainerSpec()
    {
        using var appHost = await CreateAppHostAsync();
        var gameController = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "game-controller")));

        Assert.Equal("robocupssl/ssl-game-controller:3.20.3", gameController.Image);
        Assert.Equal("app", gameController.ExpectedProcessName);
        Assert.Equal(
            ["-visionAddress", "224.5.23.2:10020", "-trackerAddress", "224.5.23.2:11010", "-publishAddress", "224.5.23.1:11003", "-address", ":8082"],
            gameController.ContainerArguments);

        var producers = new List<string>();
        foreach (var resource in appHost.Resources.OfType<ExecutableResource>()
                     .Where(resource => resource.Name is "simulator" or "game-controller" or "crane" or "cm4-sim"))
        {
            var launch = await GetLaunchOptionsAsync(resource);
            if (launch.ContainerArguments.Contains("-publishAddress") && launch.ContainerArguments.Contains("224.5.23.1:11003"))
            {
                producers.Add(resource.Name);
            }
        }

        Assert.Equal(["game-controller"], producers);
    }

    [Fact]
    public async Task WrapperHealthDependenciesWaitForHealthyServicesAndDuckProjectStart()
    {
        using var appHost = await CreateAppHostAsync();
        var simulator = Find(appHost, "simulator");
        var cm4 = Find(appHost, "cm4-sim");
        var duck = Find(appHost, "duck");
        var gameController = Find(appHost, "game-controller");
        var crane = Find(appHost, "crane");

        AssertSingleWait(duck, simulator, WaitType.WaitUntilHealthy);
        AssertWaits(crane, (duck, WaitType.WaitUntilStarted), (gameController, WaitType.WaitUntilHealthy), (cm4, WaitType.WaitUntilHealthy));
        AssertSingleWait(cm4, simulator, WaitType.WaitUntilHealthy);
    }

    [Fact]
    public async Task ConfigOverridesAndNonVisibilityPlannerChangeWrapperSpecAndDependencies()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Simulator:ImageTag=custom-tag",
            "--Testing:Simulator:Geometry=custom-geometry",
            "--Testing:Simulator:Realism=Custom",
            "--Testing:Simulator:IbisPort=22346",
            "--Testing:Simulator:IbisTeamColor=blue",
            "--Testing:Crane:Planner=rvo2");

        Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "cm4-sim");
        var simulator = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "simulator")));
        Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli:custom-tag", simulator.Image);
        Assert.Contains("custom-geometry", simulator.ContainerArguments);
        Assert.Contains("Custom", simulator.ContainerArguments);
        Assert.Contains("22346", simulator.ContainerArguments);
        Assert.Contains("blue", simulator.ContainerArguments);

        var craneResource = Assert.IsType<ExecutableResource>(Find(appHost, "crane"));
        var crane = await GetLaunchOptionsAsync(craneResource);
        Assert.Equal("rvo2", crane.Environment["PLANNER"]);
        Assert.Equal("22346", crane.Environment["CRANE_TARGET_PORT"]);
        Assert.Equal("true", crane.Environment["FEEDBACK_SIM_MODE"]);
        AssertWaits(craneResource, (Find(appHost, "duck"), WaitType.WaitUntilStarted), (Find(appHost, "game-controller"), WaitType.WaitUntilHealthy));
    }

    [Fact]
    public async Task DuckUsesTheSimVisionProfileAndWaitsForSimulatorHealth()
    {
        using var appHost = await CreateAppHostAsync();
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
        Assert.Equal(WaitType.WaitUntilHealthy, wait.WaitType);
    }

    [Fact]
    public async Task DuckPublishesDecodedVisionReadinessOnItsInternalTcpEndpoint()
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
        Assert.Equal(WaitType.WaitUntilHealthy, wait.WaitType);
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
            "--Testing:Simulator:IbisPort=22346",
            "--Testing:Crane:Planner=" + planner);

        var simulator = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "simulator")));
        AssertArgumentValue(simulator.ContainerArguments, "--ibis-port", "22346");

        var crane = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "crane")));
        Assert.Equal(planner, crane.Environment["PLANNER"]);
        if (planner == "visibility_graph")
        {
            var cm4Simulator = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "cm4-sim")));
            AssertArgumentValue(cm4Simulator.ContainerArguments, "--out-port", "22346");
            Assert.Equal("12345", crane.Environment["CRANE_TARGET_PORT"]);
        }
        else
        {
            Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "cm4-sim");
            Assert.Equal("22346", crane.Environment["CRANE_TARGET_PORT"]);
        }
    }

    [Fact]
    public async Task CraneDiagnosticsPathReachesOnlyItsWrapperAndNotTheContainer()
    {
        var path = Path.Combine(Path.GetTempPath(), "duck-crane-model-" + Guid.NewGuid().ToString("N"), "crane-probe.jsonl");
        using var appHost = await CreateAppHostAsync("--Testing:Crane:DiagnosticsPath=" + path);
        foreach (var name in new[] { "simulator", "game-controller", "crane", "cm4-sim" })
        {
            var launch = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, name)));
            Assert.Equal(name == "crane" ? Path.GetFullPath(path) : null, launch.CraneDiagnosticsPath);
            Assert.DoesNotContain(launch.Environment, entry => entry.Key.Contains("DiagnosticsPath", StringComparison.Ordinal));
            Assert.DoesNotContain(ToSpec(launch).CreateRunArguments(), argument => argument.Contains(path, StringComparison.Ordinal));
        }
    }

    private static async Task<IDistributedApplicationTestingBuilder> CreateAppHostAsync(params string[] additionalArgs) =>
        await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            [.. additionalArgs, CreateIsolatedOwnershipLockArgument()]);

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

    private static async Task<string[]> GetArgumentsAsync(IResourceWithArgs resource)
    {
        var args = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
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

}
