using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;

if (args.Length > 0 && args[0] == "--docker-wrapper")
{
    return await DockerContainerWrapper.RunEntryPointAsync(args[1..]);
}

var builder = DistributedApplication.CreateBuilder(args);
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
ExclusiveControlPortPreflight.EnsureAvailable(
    ExclusiveControlPortPreflight.CreateBaseConfigurationPorts(simulatorIbisPortNumber, useCm4Simulator));

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
var craneTeam = builder.Configuration["Testing:Crane:Team"] ?? "Yellow";
var cm4SimulatorImageTag =
    builder.Configuration["Testing:Cm4Sim:ImageTag"] ?? "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a";
var stackId = builder.Configuration["Testing:StackOwnership:StackId"] ?? Guid.NewGuid().ToString("N");

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

var duck = builder
    .AddProject<Projects.Tracker_RuntimeHost>("duck")
    .WithEnvironment("Tracker__ActiveProfileName", "sim")
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
    "game-controller");

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
                $"source /root/ibis_ws/install/setup.bash && ros2 launch crane_bringup crane.launch.xml sim:=true speak:=false team:={craneTeam} planner:=${{PLANNER}}",
            ],
            craneEnvironment,
            stackId),
        39104,
        "ros2",
        "crane",
        startupTimeoutSeconds: 360);

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
    var trackerTigers = builder
        .AddContainer("tracker-tigers", "tigersmannheim/sumatra", tigersImageTag)
        .WithBindMount(
            sumatraProtocolPath,
            "/Sumatra/config/moduli/simulation_protocol_comparison.xml",
            isReadOnly: true)
        .WithArgs(
            "--headless",
            "--visionAddress", "224.5.23.2:10020",
            "--refereeAddress", "224.5.23.1:11003",
            "--moduli", "simulation_protocol_comparison")
        .WithContainerRuntimeArgs("--network", "host")
        .WaitForStart(simulator)
        .WaitForStart(gameController);

    var trackerErForce = builder
        .AddContainer("tracker-erforce", "roboticserlangen/autoref", erForceImageTag)
        .WithArgs(
            "--vision-port", "10020",
            "--tracker-port", "11010",
            "--gc-port", "11003")
        .WithContainerRuntimeArgs("--network", "host")
        .WaitForStart(simulator)
        .WaitForStart(gameController);

    builder
        .AddProject<Projects.Tracker_DebugHost>("debug-host")
        .WithEnvironment("VisionReceiver__MulticastAddress", "224.5.23.2")
        .WithEnvironment("VisionReceiver__Port", "10020")
        .WithEnvironment("Tracker__Enabled", "false")
        .WithEnvironment("Tracker__PublishUdp", "false")
        .WithEnvironment("Tracker__Receive__Enabled", "true")
        .WithEnvironment("Tracker__Receive__MulticastAddress", "224.5.23.2")
        .WithEnvironment("Tracker__Receive__Port", "11010")
        .WithEnvironment("Tracker__Uuid", "debug-host-observer")
        .WithEnvironment("Tracker__SourceName", "debug-host-observer")
        .WaitForStart(simulator)
        .WaitForStart(trackerTigers)
        .WaitForStart(trackerErForce);
}

builder.Build().Run();
return 0;
