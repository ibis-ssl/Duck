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

builder
    .AddProject<Projects.Tracker_RuntimeHost>("duck")
    .WithEnvironment("Tracker__ActiveProfileName", "sim")
    .WithEnvironment("VisionReceiver__MulticastAddress", "224.5.23.2")
    .WithEnvironment("VisionReceiver__Port", "10020")
    .WaitForStart(simulator);

builder.AddContainer("game-controller", "robocupssl/ssl-game-controller", "3.20.3")
    .WithContainerRuntimeArgs("--network", "host")
    .WithArgs(
        "-visionAddress", "224.5.23.2:10020",
        "-trackerAddress", "224.5.23.2:11010",
        "-publishAddress", "224.5.23.1:11003",
        "-address", ":8082");

builder.Build().Run();
