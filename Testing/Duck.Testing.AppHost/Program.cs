using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;

if (args.Length > 0 && args[0] == "--docker-wrapper")
{
    return await DockerContainerWrapper.RunEntryPointAsync(args[1..]);
}

var builder = DistributedApplication.CreateBuilder(args);
var repositoryRoot = FindRepositoryRoot();

IResourceBuilder<ContainerResource> AddPublishedContainer(string resourceName, string imageName, string projectPath)
{
    var projectDirectory = Path.Combine(repositoryRoot, projectPath);
    var publishDirectory = Path.Combine(projectDirectory, "bin", "Release", "net10.0", "publish");
    return builder.AddContainer(resourceName, imageName)
        .WithDockerfile(publishDirectory, Path.Combine(projectDirectory, "Dockerfile"));
}

static bool IsEnabled(string? value) => bool.TryParse(value, out var enabled) && enabled;

const string namespaceProbeSelectionKey = "Testing:NamespaceProbe:GenerateModel";
var namespaceProbeSelection = builder.Configuration[namespaceProbeSelectionKey];
if (namespaceProbeSelection is not null &&
    !bool.TryParse(namespaceProbeSelection, out _))
{
    throw new InvalidOperationException(
        $"{namespaceProbeSelectionKey} must be true or false when specified.");
}

if (IsEnabled(namespaceProbeSelection))
{
    if (IsEnabled(builder.Configuration["Testing:RuntimeHostOnly"]) ||
        IsEnabled(builder.Configuration["Testing:VisionSimulationOnly"]) ||
        IsEnabled(builder.Configuration["Testing:GameControllerVisionOnly"]) ||
        IsEnabled(builder.Configuration["Testing:OwnedReconfiguration:Enabled"]) ||
        IsEnabled(builder.Configuration["Testing:DebugHost:ReplayOnly"]) ||
        string.Equals(builder.Configuration["Testing:Mode"], "comparison", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Testing:NamespaceProbe:GenerateModel cannot be combined with another AppHost mode.");
    }

    var namespaceProbeLockPath = builder.Configuration["Testing:StackOwnership:LockPath"] ??
        Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock");
    using var namespaceProbeOwnershipLease = StackOwnershipLease.Acquire(namespaceProbeLockPath);
    var namespaceProbeRunId = builder.Configuration["Testing:Diagnostics:RunId"] ?? Guid.NewGuid().ToString("N");
    var refereeDriverPublishDirectory = Path.Combine(
        repositoryRoot,
        "Testing", "Duck.Testing.RefereeDriver.Host", "bin", "Release", "net10.0", "publish");
    var namespaceProbeModel = await NamespaceProbeModelFactory.AddResourcesAndProjectAsync(
        builder,
        namespaceProbeRunId,
        refereeDriverPublishDirectory);

    Console.WriteLine(NamespaceProbeModelFactory.Serialize(namespaceProbeModel));
    return 0;
}

var ownedReconfigurationEnabled = IsEnabled(builder.Configuration["Testing:OwnedReconfiguration:Enabled"]);
if (ownedReconfigurationEnabled && !IsEnabled(builder.Configuration["Testing:GameControllerVisionOnly"]))
{
    throw new InvalidOperationException(
        "Testing:OwnedReconfiguration:Enabled is supported only with Testing:GameControllerVisionOnly=true.");
}
if (ownedReconfigurationEnabled && !IsEnabled(builder.Configuration["Testing:IdentityControl:DesktopBridgeEnabled"]))
{
    throw new InvalidOperationException(
        "Testing:OwnedReconfiguration:Enabled requires Testing:IdentityControl:DesktopBridgeEnabled=true.");
}
if (ownedReconfigurationEnabled &&
    (IsEnabled(builder.Configuration["Testing:RuntimeHostOnly"]) ||
     IsEnabled(builder.Configuration["Testing:VisionSimulationOnly"]) ||
     IsEnabled(builder.Configuration["Testing:DebugHost:ReplayOnly"]) ||
     string.Equals(builder.Configuration["Testing:Mode"], "comparison", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException(
        "Testing:OwnedReconfiguration:Enabled cannot be combined with another AppHost mode.");
}

if (IsEnabled(builder.Configuration["Testing:DebugHost:ReplayOnly"]))
{
    var incompatibleModeSelected =
        IsEnabled(builder.Configuration["Testing:RuntimeHostOnly"]) ||
        IsEnabled(builder.Configuration["Testing:VisionSimulationOnly"]) ||
        IsEnabled(builder.Configuration["Testing:GameControllerVisionOnly"]) ||
        string.Equals(builder.Configuration["Testing:Mode"], "comparison", StringComparison.OrdinalIgnoreCase);
    if (incompatibleModeSelected)
    {
        throw new InvalidOperationException(
            "Testing:DebugHost:ReplayOnly cannot be combined with another AppHost mode.");
    }

    var replayLockPath = builder.Configuration["Testing:StackOwnership:LockPath"] ??
        Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock");
    using var replayOwnershipLease = StackOwnershipLease.Acquire(replayLockPath);
    var replayDirectoryPath = builder.Configuration["Testing:DebugHost:ReplayDirectoryPath"];
    if (string.IsNullOrWhiteSpace(replayDirectoryPath))
    {
        throw new InvalidOperationException(
            "Testing:DebugHost:ReplayDirectoryPath is required when DebugHost replay-only mode is enabled.");
    }

    var replaySettings = DebugHostCaptureMountSettings.CreateReplay(replayDirectoryPath);
    var replayHost = AddPublishedContainer("debug-host", "duck-debug-host", "Tracker/Tracker.DebugHost")
        .WithEnvironment("Testing__Mode", "comparison")
        .WithEnvironment("Testing__DebugHost__ReplayOnly", "true")
        .WithEnvironment("Testing__ContainerMode", "true")
        .WithEnvironment("Tracker__Enabled", "false")
        .WithEnvironment("Tracker__PublishUdp", "false")
        .WithEnvironment("Tracker__Receive__Enabled", "false")
        .WithHttpEndpoint(targetPort: 8080, name: "http", isProxied: false);
    replaySettings.ApplyTo(replayHost);
    var replayHttpEndpoint = replayHost.Resource.Annotations
        .OfType<EndpointAnnotation>()
        .Single(endpoint => endpoint.Name == "http");
    replayHttpEndpoint.Port = null;
    replayHttpEndpoint.TargetHost = "127.0.0.1";

    builder.Build().Run();
    return 0;
}

/// <summary>
/// 明示選択された場合だけ、所有ラベルと診断受渡しの常駐サービスを構成します。
/// </summary>
void EnableDesktopIdentityBridge(
    IResourceBuilder<ContainerResource> resource, string runId, string role,
    string executableAssembly = "Tracker.RuntimeHost.dll")
{
    if (!IsEnabled(builder.Configuration["Testing:IdentityControl:DesktopBridgeEnabled"]))
    {
        return;
    }

    var contextName = builder.Configuration["Testing:IdentityControl:DockerContext"];
    var expectedEngineId = builder.Configuration["Testing:IdentityControl:ExpectedEngineId"];
    if (string.IsNullOrWhiteSpace(contextName) || string.IsNullOrWhiteSpace(expectedEngineId))
    {
        throw new InvalidOperationException(
            "Testing:IdentityControl:DockerContext and Testing:IdentityControl:ExpectedEngineId are required when the desktop identity bridge is enabled.");
    }

    resource.WithContainerRuntimeArgs(
        "--label", $"{ContainerIdentityExecHandoff.RunIdLabel}={runId}",
        "--label", $"{ContainerIdentityExecHandoff.RoleLabel}={role}");

    var dockerExecutable = builder.Configuration["Testing:IdentityControl:DockerExecutable"] ??
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "DockerDesktop", "resources", "bin", "docker.exe");
    // 同じ具象型のwatcherを複数登録できるよう、資源ごとに独立したIHostedServiceを追加する。
    builder.Services.AddSingleton<IHostedService>(serviceProvider => new ContainerIdentityHandoffService(
        serviceProvider.GetRequiredService<ResourceNotificationService>(),
        resource.Resource,
        role,
        runId,
        contextName,
        expectedEngineId,
        new DockerCliCommandRunner(dockerExecutable),
        serviceProvider.GetRequiredService<ILogger<ContainerIdentityHandoffService>>(),
        builder.Configuration["Testing:IdentityControl:SafeTracePath"], executableAssembly));
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Testing", "Duck.Testing.AppHost", "Duck.Testing.AppHost.csproj")))
        {
            return directory.FullName;
        }
    }

    throw new DirectoryNotFoundException("DuckリポジトリのルートをAppHostの出力先から特定できません。");
}

if (IsEnabled(builder.Configuration["Testing:RuntimeHostOnly"]))
{
    var runtimeOnlyLockPath =
        builder.Configuration["Testing:StackOwnership:LockPath"] ??
        Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock");
    using var runtimeOnlyOwnershipLease = StackOwnershipLease.Acquire(runtimeOnlyLockPath);
    var runtimeOnlyRunId = builder.Configuration["Testing:Diagnostics:RunId"] ?? Guid.NewGuid().ToString("N");
    var runtimeOnlyDuck = AddPublishedContainer("duck", "duck-runtime", "Tracker/Tracker.RuntimeHost")
        .WithEnvironment("Tracker__ActiveProfileName", "sim")
        .WithEnvironment("Testing__Mode", "base")
        .WithEnvironment("Testing__ContainerMode", "true")
        .WithEnvironment("Testing__IdentityControl__Enabled", "true")
        .WithEnvironment("Testing__IdentityControl__ExpectedRole", "duck")
        .WithEnvironment("Testing__Diagnostics__RunId", runtimeOnlyRunId)
        .WithHttpEndpoint(targetPort: 39108, name: "vision-health", isProxied: false)
        .WithHttpHealthCheck("/health/vision", endpointName: "vision-health");
    EnableDesktopIdentityBridge(runtimeOnlyDuck, runtimeOnlyRunId, "duck");
    var runtimeOnlyEndpoint = runtimeOnlyDuck.Resource.Annotations
        .OfType<EndpointAnnotation>()
        .Single(endpoint => endpoint.Name == "vision-health");
    runtimeOnlyEndpoint.Port = null;
    runtimeOnlyEndpoint.TargetHost = "127.0.0.1";


    builder.Build().Run();
    return 0;
}

if (IsEnabled(builder.Configuration["Testing:VisionSimulationOnly"]))
{
    var configuredSimulatorTag = builder.Configuration["Testing:Simulator:ImageTag"] ?? "a52b6bd";
    if (!string.Equals(configuredSimulatorTag, "a52b6bd", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Testing:VisionSimulationOnly requires the simulator image locked as a52b6bd.");
    }

    var visionOnlyLockPath =
        builder.Configuration["Testing:StackOwnership:LockPath"] ??
        Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock");
    using var visionOnlyOwnershipLease = StackOwnershipLease.Acquire(visionOnlyLockPath);
    var visionOnlyRunId = builder.Configuration["Testing:Diagnostics:RunId"] ?? Guid.NewGuid().ToString("N");
    var visionSimulatorGeometry = builder.Configuration["Testing:Simulator:Geometry"] ?? "2020B";
    var visionSimulatorRealism = builder.Configuration["Testing:Simulator:Realism"] ?? "None";
    var visionSimulatorIbisPort = builder.Configuration["Testing:Simulator:IbisPort"] ?? "12346";
    var simulatorTeamColor = builder.Configuration["Testing:Simulator:IbisTeamColor"] ?? "yellow";
    if (!int.TryParse(visionSimulatorIbisPort, out var visionSimulatorIbisPortNumber) ||
        visionSimulatorIbisPortNumber is < 1 or > 65535)
    {
        throw new InvalidOperationException("Testing:Simulator:IbisPort must be a valid UDP port number.");
    }

    var visionSimulator = builder.AddContainer(
            "simulator",
            "ghcr.io/ibis-ssl/framework-simulatorcli",
            "a52b6bd")
        .WithImageSHA256("f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79")
        .WithEntrypoint("tini")
        .WithArgs(
            "--",
            "./bin/simulator-cli",
            "-g", visionSimulatorGeometry,
            "--realism", visionSimulatorRealism,
            "--ibis-port", visionSimulatorIbisPort,
            "--ibis-team-color", simulatorTeamColor);

    var visionOnlyDuck = AddPublishedContainer("duck", "duck-runtime", "Tracker/Tracker.RuntimeHost")
        .WithEnvironment("Tracker__ActiveProfileName", "sim")
        .WithEnvironment("Testing__Mode", "base")
        .WithEnvironment("Testing__ContainerMode", "true")
        .WithEnvironment("Testing__IdentityControl__Enabled", "true")
        .WithEnvironment("Testing__IdentityControl__ExpectedRole", "duck")
        .WithEnvironment("Testing__Diagnostics__RunId", visionOnlyRunId)
        .WithEnvironment("VisionReceiver__MulticastAddress", "224.5.23.2")
        .WithEnvironment("VisionReceiver__Port", "10020")
        .WithHttpEndpoint(targetPort: 39108, name: "vision-health", isProxied: false)
        .WithHttpHealthCheck("/health/vision", endpointName: "vision-health")
        .WaitForStart(visionSimulator);
    EnableDesktopIdentityBridge(visionOnlyDuck, visionOnlyRunId, "duck");
    var visionOnlyEndpoint = visionOnlyDuck.Resource.Annotations
        .OfType<EndpointAnnotation>()
        .Single(endpoint => endpoint.Name == "vision-health");
    visionOnlyEndpoint.Port = null;
    visionOnlyEndpoint.TargetHost = "127.0.0.1";

    builder.Build().Run();
    return 0;
}

if (IsEnabled(builder.Configuration["Testing:GameControllerVisionOnly"]))
{
    var gcVisionGameControllerOnlyLockPath =
        builder.Configuration["Testing:StackOwnership:LockPath"] ??
        Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock");
    using var gcVisionGameControllerOnlyOwnershipLease = StackOwnershipLease.Acquire(gcVisionGameControllerOnlyLockPath);
    var gcVisionGameControllerOnlyRunId = builder.Configuration["Testing:Diagnostics:RunId"] ?? Guid.NewGuid().ToString("N");
    var gcVisionOwnedReconfiguration = IsEnabled(builder.Configuration["Testing:OwnedReconfiguration:Enabled"]);
    var gcVisionDockerContext = builder.Configuration["Testing:IdentityControl:DockerContext"];
    var gcVisionExpectedEngineId = builder.Configuration["Testing:IdentityControl:ExpectedEngineId"];
    if (gcVisionOwnedReconfiguration &&
        (string.IsNullOrWhiteSpace(gcVisionDockerContext) || string.IsNullOrWhiteSpace(gcVisionExpectedEngineId)))
    {
        throw new InvalidOperationException(
            "Testing:OwnedReconfiguration:Enabled requires Testing:IdentityControl:DockerContext and Testing:IdentityControl:ExpectedEngineId.");
    }
    var gcVisionSimulatorGeometry = builder.Configuration["Testing:Simulator:Geometry"] ?? "2020B";
    var gcVisionSimulatorRealism = builder.Configuration["Testing:Simulator:Realism"] ?? "None";
    var gcVisionSimulatorIbisPort = builder.Configuration["Testing:Simulator:IbisPort"] ?? "12346";
    var gcVisionSimulatorTeamColor = builder.Configuration["Testing:Simulator:IbisTeamColor"] ?? "yellow";
    if (!int.TryParse(gcVisionSimulatorIbisPort, out var gcVisionSimulatorIbisPortNumber) ||
        gcVisionSimulatorIbisPortNumber is < 1 or > 65535)
    {
        throw new InvalidOperationException("Testing:Simulator:IbisPort must be a valid UDP port number.");
    }

    var gcVisionSimulator = builder.AddContainer(
            "simulator",
            "ghcr.io/ibis-ssl/framework-simulatorcli",
            "a52b6bd")
        .WithImageSHA256("f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79")
        .WithEntrypoint("tini")
        .WithArgs(
            "--",
            "./bin/simulator-cli",
            "-g", gcVisionSimulatorGeometry,
            "--realism", gcVisionSimulatorRealism,
            "--ibis-port", gcVisionSimulatorIbisPort,
            "--ibis-team-color", gcVisionSimulatorTeamColor);

    var gcVisionDuck = AddPublishedContainer("duck", "gcVisionDuck-runtime", "Tracker/Tracker.RuntimeHost")
        .WithEnvironment("Tracker__ActiveProfileName", "sim")
        .WithEnvironment("Testing__Mode", "base")
        .WithEnvironment("Testing__ContainerMode", "true")
        .WithEnvironment("Testing__IdentityControl__Enabled", "true")
        .WithEnvironment("Testing__IdentityControl__ExpectedRole", "duck")
        .WithEnvironment("Testing__Diagnostics__RunId", gcVisionGameControllerOnlyRunId)
        .WithEnvironment("VisionReceiver__MulticastAddress", "224.5.23.2")
        .WithEnvironment("VisionReceiver__Port", "10020")
        .WithHttpEndpoint(targetPort: 39108, name: "vision-health", isProxied: false)
        .WithHttpHealthCheck("/health/vision", endpointName: "vision-health")
        .WaitForStart(gcVisionSimulator);
    EnableDesktopIdentityBridge(gcVisionDuck, gcVisionGameControllerOnlyRunId, "duck");
    var gcVisionDuckVisionEndpoint = gcVisionDuck.Resource.Annotations
        .OfType<EndpointAnnotation>()
        .Single(endpoint => endpoint.Name == "vision-health");
    gcVisionDuckVisionEndpoint.Port = null;
    gcVisionDuckVisionEndpoint.TargetHost = "127.0.0.1";

    var gcVisionGameController = builder.AddContainer("game-controller", "robocupssl/ssl-game-controller")
        .WithImageSHA256("d62d772de063c2951f600a0b7898803f97822f4f46a412a731e42f7d82a99dc7")
        .WithArgs(
            "-visionAddress", "224.5.23.2:10020",
            "-trackerAddress", "224.5.23.2:11010",
            "-publishAddress", "224.5.23.1:11003",
            "-address", ":8082")
        .WithHttpEndpoint(targetPort: 8082, name: "game-controller-ui", isProxied: false);
    // 診断IDが未確定でもGCはDuckの起動後に開始できる。ブリッジ無効時は既定の健康待ちを残す。
    if (IsEnabled(builder.Configuration["Testing:IdentityControl:DesktopBridgeEnabled"]))
    {
        gcVisionGameController.WaitForStart(gcVisionDuck);
        // 標準資源は識別情報の受渡しを行わず、通常終了時の所有ID照合にだけ使う。
        gcVisionSimulator.WithContainerRuntimeArgs(
            "--label", $"{ContainerIdentityExecHandoff.RunIdLabel}={gcVisionGameControllerOnlyRunId}",
            "--label", $"{ContainerIdentityExecHandoff.RoleLabel}=simulator");
        gcVisionGameController.WithContainerRuntimeArgs(
            "--label", $"{ContainerIdentityExecHandoff.RunIdLabel}={gcVisionGameControllerOnlyRunId}",
            "--label", $"{ContainerIdentityExecHandoff.RoleLabel}=game-controller");
    }
    else
    {
        gcVisionGameController.WaitFor(gcVisionDuck);
    }
    var gcVisionEndpoint = gcVisionGameController.Resource.Annotations
        .OfType<EndpointAnnotation>()
        .Single(endpoint => endpoint.Name == "game-controller-ui");
    gcVisionEndpoint.Port = null;
    gcVisionEndpoint.TargetHost = "127.0.0.1";

    var gcVisionRefereeDriver = AddPublishedContainer("referee-driver", "gcVisionDuck-referee-driver", "Testing/Duck.Testing.RefereeDriver.Host")
        .WithEnvironment("RefereeDriver__Enabled", "true")
        .WithEnvironment("Testing__ContainerMode", "true")
        .WaitForStart(gcVisionGameController);
    var gcVisionEndpointState = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
    if (gcVisionOwnedReconfiguration)
    {
        gcVisionRefereeDriver.WithEnvironment(context =>
        {
            context.EnvironmentVariables["RefereeDriver__GameControllerUri"] = gcVisionEndpointState.GetUriForEnvironment();
        });

        var dockerExecutable = builder.Configuration["Testing:IdentityControl:DockerExecutable"] ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "DockerDesktop", "resources", "bin", "docker.exe");
        builder.Services.AddSingleton<IOwnedReconfigurationAdapter>(serviceProvider =>
            new AspireOwnedReconfigurationAdapter(
                serviceProvider.GetRequiredService<ResourceNotificationService>(),
                serviceProvider.GetRequiredService<ResourceCommandService>(),
                new DockerCliCommandRunner(dockerExecutable),
                gcVisionDockerContext!));
        builder.Services.AddSingleton(serviceProvider => new OwnedRefereeReconfigurationCoordinator(
            serviceProvider.GetRequiredService<IOwnedReconfigurationAdapter>(),
            gcVisionGameController.Resource,
            gcVisionRefereeDriver.Resource,
            gcVisionEndpointState,
            gcVisionGameControllerOnlyRunId,
            gcVisionExpectedEngineId!,
            TimeSpan.FromSeconds(45),
            serviceProvider.GetRequiredService<ILogger<OwnedRefereeReconfigurationCoordinator>>()));
        builder.Services.AddSingleton<IHostedService>(serviceProvider => new OwnedSourceGenerationMonitor(
            serviceProvider.GetRequiredService<IOwnedReconfigurationAdapter>(),
            serviceProvider.GetRequiredService<OwnedRefereeReconfigurationCoordinator>(),
            gcVisionGameController.Resource,
            serviceProvider.GetRequiredService<ILogger<OwnedSourceGenerationMonitor>>()));

        gcVisionGameController.WithCommand(
            "reconfigure-game-controller-endpoint",
            "再作成した Game Controller へ接続先を更新",
            async context =>
            {
                var result = await context.Services.GetRequiredService<OwnedRefereeReconfigurationCoordinator>()
                    .ExecuteAsync(context.CancellationToken);
                return new ExecuteCommandResult
                {
                    Success = result.Success,
                    Message = result.Message,
                    Canceled = result.Canceled,
                };
            });
    }
    else
    {
        gcVisionRefereeDriver.WithEnvironment("RefereeDriver__GameControllerUri", "ws://game-controller:8082/api/control");
    }
    if (IsEnabled(builder.Configuration["Testing:IdentityControl:DesktopBridgeEnabled"]))
    {
        gcVisionRefereeDriver
            .WithEnvironment("Testing__IdentityControl__Enabled", "true")
            .WithEnvironment("Testing__IdentityControl__ExpectedRole", "referee-driver")
            .WithEnvironment("Testing__Diagnostics__RunId", gcVisionGameControllerOnlyRunId);
        EnableDesktopIdentityBridge(gcVisionRefereeDriver, gcVisionGameControllerOnlyRunId,
            "referee-driver", "Duck.Testing.RefereeDriver.Host.dll");
    }

    builder.Build().Run();
    return 0;
}

var testingMode = builder.Configuration["Testing:Mode"] ?? "base";
if (testingMode is not "base" and not "comparison")
{
    throw new InvalidOperationException($"Unsupported Testing:Mode '{testingMode}'. Supported modes are 'base' and 'comparison'.");
}

var simulatorIbisPort =
    builder.Configuration["Testing:Simulator:IbisPort"] ?? "12346";
var cranePlanner = builder.Configuration["Testing:Crane:Planner"] ?? "visibility_graph";
var useCm4Simulator = BaseSimulationResourceRegistration.UsesCm4Simulator(cranePlanner);
var craneTargetAddressSettings = CraneTargetAddressSettings.FromConfiguration(
    builder.Configuration,
    cranePlanner,
    AppContext.BaseDirectory);
if (!int.TryParse(simulatorIbisPort, out var simulatorIbisPortNumber))
{
    throw new InvalidOperationException($"Testing:Simulator:IbisPort must be a valid UDP port number; received '{simulatorIbisPort}'.");
}
var ownershipLockPath =
    builder.Configuration["Testing:StackOwnership:LockPath"] ??
    Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock");
using var stackOwnershipLease = StackOwnershipLease.Acquire(ownershipLockPath);
if (testingMode == "comparison")
{
    var preflightPorts = ExclusiveControlPortPreflight
        .CreateBaseConfigurationPorts(simulatorIbisPortNumber, useCm4Simulator)
        .ToList();
    ExclusiveControlPortPreflight.EnsureAvailable(preflightPorts);
}

var simulatorImageTag =
    builder.Configuration["Testing:Simulator:ImageTag"] ?? "a52b6bd";
var simulatorGeometry =
    builder.Configuration["Testing:Simulator:Geometry"] ?? "2020B";
var simulatorRealism =
    builder.Configuration["Testing:Simulator:Realism"] ?? "None";
var simulatorIbisTeamColor =
    builder.Configuration["Testing:Simulator:IbisTeamColor"] ?? "yellow";
var craneImageTag =
    builder.Configuration["Testing:Crane:ImageTag"] ?? "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d";
var tigersImageTag = builder.Configuration["Testing:Comparison:TigersImageTag"] ?? "2025";
var erForceImageTag = builder.Configuration["Testing:Comparison:ErForceImageTag"] ?? "2025.1.0";
var craneFixtureSettings = CraneFixtureSettings.FromConfiguration(builder.Configuration);
var craneTeamName = craneFixtureSettings.TeamName;
var craneTeamColor = craneFixtureSettings.TeamColor;
var cm4SimulatorImageTag =
    builder.Configuration["Testing:Cm4Sim:ImageTag"] ?? "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a";
var stackId = builder.Configuration["Testing:StackOwnership:StackId"] ?? Guid.NewGuid().ToString("N");
var fixtureManifestPath =
    builder.Configuration["Testing:Crane:FixtureManifestPath"] ??
    CraneFixtureManifest.GetDefaultPath(stackId);

var duckRunId = builder.Configuration["Testing:Diagnostics:RunId"] ?? Guid.NewGuid().ToString("N");
var baseResources = BaseSimulationResourceRegistration.Add(
    builder,
    new BaseSimulationRegistrationOptions(
        simulatorIbisPort,
        simulatorImageTag,
        simulatorGeometry,
        simulatorRealism,
        simulatorIbisTeamColor,
        craneImageTag,
        cm4SimulatorImageTag,
        cranePlanner,
        stackId,
        craneTeamName,
        craneTeamColor,
        fixtureManifestPath,
        UseStandardDcpResources: testingMode == "base",
        DuckRunId: duckRunId,
        craneTargetAddressSettings,
        builder.Configuration["Testing:Crane:DiagnosticsPath"]),
    simulatorResource =>
    {
        var duckBuilder = AddPublishedContainer("duck", "duck-runtime", "Tracker/Tracker.RuntimeHost")
            .WithEnvironment("Tracker__ActiveProfileName", "sim")
            .WithEnvironment("Testing__Mode", testingMode)
            .WithEnvironment("Testing__ContainerMode", "true")
            .WithHttpEndpoint(targetPort: 39108, name: "vision-health", isProxied: false)
            .WithHttpHealthCheck("/health/vision", endpointName: "vision-health");
        if (IsEnabled(builder.Configuration["Testing:IdentityControl:DesktopBridgeEnabled"]))
        {
            duckBuilder
                .WithEnvironment("Testing__IdentityControl__Enabled", "true")
                .WithEnvironment("Testing__IdentityControl__ExpectedRole", "duck")
                .WithEnvironment("Testing__Diagnostics__RunId", duckRunId);
        }
        EnableDesktopIdentityBridge(duckBuilder, duckRunId, "duck");
        var visionHealthEndpoint = duckBuilder.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Single(endpoint => endpoint.Name == "vision-health");
        visionHealthEndpoint.Port = null;
        visionHealthEndpoint.TargetHost = "127.0.0.1";
        return duckBuilder;
    },
    gameController =>
    {
        if (IsEnabled(builder.Configuration["RefereeDriver:Enabled"]) &&
            IsEnabled(builder.Configuration["Testing:ContainerMode"]))
        {
            var refereeDriver = AddPublishedContainer("referee-driver", "duck-referee-driver", "Testing/Duck.Testing.RefereeDriver.Host")
                .WithEnvironment("RefereeDriver__Enabled", "true")
                .WithEnvironment("RefereeDriver__GameControllerUri", "ws://game-controller:8082/api/control")
                .WithEnvironment("Testing__ContainerMode", "true")
                .WaitFor(gameController);
            if (IsEnabled(builder.Configuration["Testing:IdentityControl:DesktopBridgeEnabled"]))
            {
                refereeDriver
                    .WithEnvironment("Testing__IdentityControl__Enabled", "true")
                    .WithEnvironment("Testing__IdentityControl__ExpectedRole", "referee-driver")
                    .WithEnvironment("Testing__Diagnostics__RunId", duckRunId);
                EnableDesktopIdentityBridge(refereeDriver, duckRunId,
                    "referee-driver", "Duck.Testing.RefereeDriver.Host.dll");
            }
        }
    });
var simulator = baseResources.Simulator;
var duck = baseResources.Duck;
var gameController = baseResources.GameController;
var cm4Simulator = baseResources.Cm4Simulator;
var crane = baseResources.Crane;
var simulatorDependency = builder.CreateResourceBuilder<IResource>(simulator.Resource);
var gameControllerDependency = builder.CreateResourceBuilder<IResource>(gameController.Resource);

if (testingMode == "comparison")
{
    var sumatraProtocolPath = Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "simulation_protocol_comparison.xml");
    var trackerTigers = builder.AddDockerWrapper(
        new DockerContainerSpec(
            "tracker-tigers",
            $"tigersmannheim/sumatra:{tigersImageTag}",
            [
            "--headless",
            "--visionAddress", "224.5.23.2:10020",
            "--refereeAddress", "224.5.23.1:11003",
            "--moduli", "simulation_protocol_comparison",
            ],
            new Dictionary<string, string>(StringComparer.Ordinal),
            stackId,
            mounts: [new DockerContainerMount(sumatraProtocolPath, "/Sumatra/config/moduli/simulation_protocol_comparison.xml", ReadOnly: true)]),
        39105,
        "java",
        "tracker-tigers",
        expectedTrackerSourceName: "TIGERs")
        .WaitFor(simulatorDependency)
        .WaitFor(gameControllerDependency);

    var trackerErForce = builder.AddDockerWrapper(
        new DockerContainerSpec(
            "tracker-erforce",
            $"roboticserlangen/autoref:{erForceImageTag}",
            [
            "--vision-port", "10020",
            "--tracker-port", "11010",
            "--gc-port", "11003",
            ],
            new Dictionary<string, string>(StringComparer.Ordinal),
            stackId),
        39106,
        "autoref",
        "tracker-erforce",
        expectedTrackerSourceName: "ER-FORCE")
        .WaitFor(simulatorDependency)
        .WaitFor(gameControllerDependency);

    var captureRoot = builder.Configuration["Testing:DebugHost:CaptureRootPath"] ??
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var captureSettings = DebugHostCaptureMountSettings.CreateCapture(captureRoot, duckRunId);
    var debugHost = AddPublishedContainer("debug-host", "duck-debug-host", "Tracker/Tracker.DebugHost")
        .WithEnvironment("Testing__Mode", "comparison")
        .WithEnvironment("Testing__Diagnostics__RunId", duckRunId)
        .WithEnvironment("VisionReceiver__MulticastAddress", "224.5.23.2")
        .WithEnvironment("VisionReceiver__Port", "10020")
        .WithEnvironment("Tracker__Enabled", "false")
        .WithEnvironment("Tracker__PublishUdp", "false")
        .WithEnvironment("Tracker__Receive__Enabled", "true")
        .WithEnvironment("Tracker__Receive__MulticastAddress", "224.5.23.2")
        .WithEnvironment("Tracker__Receive__Port", "11010")
        .WithEnvironment("Tracker__Uuid", "debug-host-observer")
        .WithEnvironment("Tracker__SourceName", "debug-host-observer")
        .WithEnvironment("Testing__ContainerMode", "true")
        .WithHttpEndpoint(targetPort: 8080, name: "http", isProxied: false);
    captureSettings.ApplyTo(debugHost);
    var httpEndpoint = debugHost.Resource.Annotations
        .OfType<EndpointAnnotation>()
        .Single(endpoint => endpoint.Name == "http");
    httpEndpoint.Port = null;
    httpEndpoint.TargetHost = "127.0.0.1";
    debugHost
        .WaitForStart(simulatorDependency)
        .WaitForStart(builder.CreateResourceBuilder<IResource>(duck.Resource))
        .WaitFor(trackerTigers)
        .WaitFor(trackerErForce)
        .WithHttpHealthCheck("/health/comparison", endpointName: "http");
}

builder.Build().Run();
return 0;
