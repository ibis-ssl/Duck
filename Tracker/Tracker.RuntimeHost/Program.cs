using Microsoft.Extensions.Hosting;
using Tracker.RuntimeHost;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRuntimeHost(builder.Configuration);

var host = builder.Build();
await host.RunAsync();
