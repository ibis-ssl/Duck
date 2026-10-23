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

var testingMode = builder.Configuration["Testing:Mode"] ?? "base";
if (testingMode is not "base" and not "comparison")
{
    throw new InvalidOperationException($"Unsupported Testing:Mode '{testingMode}'. Supported modes are 'base' and 'comparison'.");
}

var simulatorIbisPort =
    builder.Configuration["Testing:Simulator:IbisPort"] ?? "12346";
var cranePlanner = builder.Configuration["Testing:Crane:Planner"] ?? "visibility_graph";
var useCm4Simulator = string.Equals(cranePlanner, "visibility_graph", StringComparison.Ordinal);
if (!int.TryParse(simulatorIbisPort, out var simulatorIbisPortNumber))
{
    throw new InvalidOperationException($"Testing:Simulator:IbisPort must be a valid UDP port number; received '{simulatorIbisPort}'.");
}
var ownershipLockPath =
    builder.Configuration["Testing:StackOwnership:LockPath"] ??
    Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock");
using var stackOwnershipLease = StackOwnershipLease.Acquire(ownershipLockPath);
var preflightPorts = ExclusiveControlPortPreflight
    .CreateBaseConfigurationPorts(simulatorIbisPortNumber, useCm4Simulator)
    .ToList();
ExclusiveControlPortPreflight.EnsureAvailable(preflightPorts);

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
CraneFixtureManifest.WriteAtomically(fixtureManifestPath, stackId, craneFixtureSettings);

var simulator = builder.AddDockerWrapper(
    new DockerContainerSpec(
        "simulator",
        $"ghcr.io/ibis-ssl/framework-simulatorcli:{simulatorImageTag}",
        [
            "tini", "--", "./bin/simulator-cli", "-g", simulatorGeometry,
            "--realism", simulatorRealism, "--ibis-port", simulatorIbisPort,
            "--ibis-team-color", simulatorIbisTeamColor,
        ],
        new Dictionary<string, string>(StringComparer.Ordinal),
        stackId),
    39101,
    "simulator-cli",
    "simulator");

var duckBuilder = AddPublishedContainer("duck", "duck-runtime", "Tracker/Tracker.RuntimeHost")
    .WithEnvironment("Tracker__ActiveProfileName", "sim")
    .WithEnvironment("Testing__Mode", testingMode)
    .WithEnvironment("Testing__ContainerMode", "true")
    .WithHttpEndpoint(targetPort: 39108, name: "vision-health", isProxied: false)
    .WithHttpHealthCheck("/health/vision", endpointName: "vision-health");
var visionHealthEndpoint = duckBuilder.Resource.Annotations
    .OfType<EndpointAnnotation>()
    .Single(endpoint => endpoint.Name == "vision-health");
visionHealthEndpoint.Port = null;
visionHealthEndpoint.TargetHost = "127.0.0.1";

var duck = duckBuilder
    .WithEnvironment("VisionReceiver__MulticastAddress", "224.5.23.2")
    .WithEnvironment("VisionReceiver__Port", "10020")
    .WaitFor(simulator);

var gameController = builder.AddDockerWrapper(
    new DockerContainerSpec(
        "game-controller",
        "robocupssl/ssl-game-controller:3.20.3",
        [
            "-visionAddress", "224.5.23.2:10020",
            "-trackerAddress", "224.5.23.2:11010",
            "-publishAddress", "224.5.23.1:11003",
            "-address", ":8082",
        ],
        new Dictionary<string, string>(StringComparer.Ordinal),
        stackId),
    39102,
    "app",
    "game-controller",
    expectedCraneTeamName: craneTeamName);

if (IsEnabled(builder.Configuration["RefereeDriver:Enabled"]) &&
    IsEnabled(builder.Configuration["Testing:ContainerMode"]))
{
    AddPublishedContainer("referee-driver", "duck-referee-driver", "Testing/Duck.Testing.RefereeDriver.Host")
        .WithEnvironment("RefereeDriver__Enabled", "true")
        .WithEnvironment("RefereeDriver__GameControllerUri", "ws://game-controller:8082/api/control")
        .WithEnvironment("Testing__ContainerMode", "true")
        .WaitFor(gameController);
}

IResourceBuilder<ExecutableResource>? cm4Simulator = null;
if (useCm4Simulator)
{
    cm4Simulator = builder.AddDockerWrapper(
        new DockerContainerSpec(
            "cm4-sim",
            $"ghcr.io/ibis-ssl/orion-cm4-sim:{cm4SimulatorImageTag}",
            [
                "tini", "--", "cm4_sim", "--robot-ids", "0,1,2,3,4,5,6,7,8,9,10",
                "--in-port", "12345", "--out-addr", "127.0.0.1", "--out-port", simulatorIbisPort,
                "--feedback-port-base", "50100", "--multicast-if", "127.0.0.1", "--rate-hz", "1000",
                "--rx-delay-ms", "0", "--rx-jitter-ms", "0", "--rx-loss-rate", "0.0", "--seed", "0",
            ],
            new Dictionary<string, string>(StringComparer.Ordinal),
            stackId),
        39103,
        "cm4_sim",
        "cm4-sim")
        .WaitFor(simulator);
}

var craneEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["PLANNER"] = cranePlanner,
    ["CRANE_TARGET_PORT"] = useCm4Simulator ? "12345" : simulatorIbisPort,
};
if (!useCm4Simulator)
{
    craneEnvironment["FEEDBACK_SIM_MODE"] = "true";
}

var craneBuilder = builder
    .AddDockerWrapper(
        new DockerContainerSpec(
            "crane",
            $"ghcr.io/ibis-ssl/crane:{craneImageTag}",
            [
                "bash", "-c",
                $"source /root/ibis_ws/install/setup.bash && ros2 launch crane_bringup crane.launch.xml sim:=true speak:=false team:={craneTeamName} team_color:={craneTeamColor} planner:=${{PLANNER}}",
            ],
            craneEnvironment,
            stackId),
        39104,
        "ros2",
        "crane",
        startupTimeoutSeconds: 360,
        expectedCraneTeamName: craneTeamName);

var crane = craneBuilder
    .WaitForStart(duck)
    .WaitFor(gameController);

if (cm4Simulator is not null)
{
    crane.WaitFor(cm4Simulator);
}

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
        .WaitFor(simulator)
        .WaitFor(gameController);

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
        .WaitFor(simulator)
        .WaitFor(gameController);

    var debugHost = AddPublishedContainer("debug-host", "duck-debug-host", "Tracker/Tracker.DebugHost")
        .WithEnvironment("Testing__Mode", "comparison")
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
    var httpEndpoint = debugHost.Resource.Annotations
        .OfType<EndpointAnnotation>()
        .Single(endpoint => endpoint.Name == "http");
    httpEndpoint.Port = null;
    httpEndpoint.TargetHost = "127.0.0.1";
    debugHost
        .WaitForStart(simulator)
        .WaitForStart(duck)
        .WaitFor(trackerTigers)
        .WaitFor(trackerErForce)
        .WithHttpHealthCheck("/health/comparison", endpointName: "http");
}

builder.Build().Run();
return 0;
