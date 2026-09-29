var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Tracker_RuntimeHost>("duck");

builder.AddContainer("game-controller", "robocupssl/ssl-game-controller", "3.20.3")
    .WithContainerRuntimeArgs("--network", "host")
    .WithArgs(
        "-visionAddress", "224.5.23.2:10020",
        "-trackerAddress", "224.5.23.2:11010",
        "-publishAddress", "224.5.23.1:11003",
        "-address", ":8082");

builder.Build().Run();
