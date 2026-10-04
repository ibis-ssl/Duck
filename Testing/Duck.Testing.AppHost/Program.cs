using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

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
    builder.Configuration["Testing:Crane:ImageTag"] ?? "scenario-a544db92b72b137c8974285b36940d0d4b5e7e69";
var craneTeam = builder.Configuration["Testing:Crane:Team"] ?? "Yellow";
var cranePlanner = builder.Configuration["Testing:Crane:Planner"] ?? "visibility_graph";
var cm4SimulatorImageTag =
    builder.Configuration["Testing:Cm4Sim:ImageTag"] ?? "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a";

var simulator = builder
    .AddContainer(
        "simulator",
        "ibis-ssl/framework-simulatorcli",
        simulatorImageTag)
    .WithImageRegistry("ghcr.io")
    .WithEntrypoint("tini")
    .WithArgs(
        "--",
        "./bin/simulator-cli",
        "-g",
        simulatorGeometry,
        "--realism",
        simulatorRealism,
        "--ibis-port",
        simulatorIbisPort,
        "--ibis-team-color",
        simulatorIbisTeamColor)
    .WithContainerRuntimeArgs("--network", "host");

var duck = builder
    .AddProject<Projects.Tracker_RuntimeHost>("duck")
    .WithEnvironment("Tracker__ActiveProfileName", "sim")
    .WithEnvironment("VisionReceiver__MulticastAddress", "224.5.23.2")
    .WithEnvironment("VisionReceiver__Port", "10020")
    .WaitForStart(simulator);

var gameController = builder.AddContainer("game-controller", "robocupssl/ssl-game-controller", "3.20.3")
    .WithContainerRuntimeArgs("--network", "host")
    .WithArgs(
        "-visionAddress", "224.5.23.2:10020",
        "-trackerAddress", "224.5.23.2:11010",
        "-publishAddress", "224.5.23.1:11003",
        "-address", ":8082");

var useCm4Simulator = string.Equals(cranePlanner, "visibility_graph", StringComparison.Ordinal);

IResourceBuilder<ContainerResource>? cm4Simulator = null;
if (useCm4Simulator)
{
    cm4Simulator = builder
        .AddContainer("cm4-sim", "ibis-ssl/orion-cm4-sim", cm4SimulatorImageTag)
        .WithImageRegistry("ghcr.io")
        .WithEntrypoint("tini")
        .WithArgs(
            "--",
            "cm4_sim",
            "--robot-ids", "0,1,2,3,4,5,6,7,8,9,10",
            "--in-port", "12345",
            "--out-addr", "127.0.0.1",
            "--out-port", "12346",
            "--feedback-port-base", "50100",
            "--multicast-if", "127.0.0.1",
            "--rate-hz", "1000",
            "--rx-delay-ms", "0",
            "--rx-jitter-ms", "0",
            "--rx-loss-rate", "0.0",
            "--seed", "0")
        .WithContainerRuntimeArgs("--network", "host")
        .WaitForStart(simulator);
}

var craneBuilder = builder
    .AddContainer("crane", "ibis-ssl/crane", craneImageTag)
    .WithImageRegistry("ghcr.io")
    .WithEnvironment("PLANNER", cranePlanner)
    .WithEnvironment("CRANE_TARGET_PORT", useCm4Simulator ? "12345" : "12346")
    .WithArgs(
        "bash",
        "-c",
        $"source /root/ibis_ws/install/setup.bash && ros2 launch crane_bringup crane.launch.xml sim:=true speak:=false team:={craneTeam} planner:=${{PLANNER}}")
    .WithContainerRuntimeArgs("--network", "host");

if (!useCm4Simulator)
{
    craneBuilder.WithEnvironment("FEEDBACK_SIM_MODE", "true");
}

var crane = craneBuilder
    .WaitForStart(duck)
    .WaitForStart(gameController);

if (cm4Simulator is not null)
{
    crane.WaitForStart(cm4Simulator);
}

builder.Build().Run();
