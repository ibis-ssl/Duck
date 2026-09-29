var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Tracker_RuntimeHost>("duck");

builder.Build().Run();
