using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;

if (args.Length > 0 && args[0] == "--docker-wrapper")
{
    return await DockerContainerWrapper.RunEntryPointAsync(args[1..]);
}

var builder = DistributedApplication.CreateBuilder(args);
var ownershipLockPath =
    builder.Configuration["Testing:StackOwnership:LockPath"] ??
    Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock");
using var stackOwnershipLease = StackOwnershipLease.Acquire(ownershipLockPath);

var simulatorImageTag =
    builder.Configuration["Testing:Simulator:ImageTag"] ?? "a52b6bd";
var simulatorGeometry =
    builder.Configuration["Testing:Simulator:Geometry"] ?? "2020B";
var simulatorRealism =
    builder.Configuration["Testing:Simulator:Realism"] ?? "None";
var simulatorIbisPort =
    builder.Configuration["Testing:Simulator:IbisPort"] ?? "12346";
var simulatorIbisTeamColor =
    builder.Configuration["Testing:Simulator:IbisTeamColor"] ?? "yellow";
var craneImageTag =
    builder.Configuration["Testing:Crane:ImageTag"] ?? "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d";
var craneTeam = builder.Configuration["Testing:Crane:Team"] ?? "Yellow";
var cranePlanner = builder.Configuration["Testing:Crane:Planner"] ?? "visibility_graph";
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
    "java",
    "game-controller");

var useCm4Simulator = string.Equals(cranePlanner, "visibility_graph", StringComparison.Ordinal);

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
        "crane");

var crane = craneBuilder
    .WaitForStart(duck)
    .WaitFor(gameController);

if (cm4Simulator is not null)
{
    crane.WaitFor(cm4Simulator);
}

builder.Build().Run();
return 0;
