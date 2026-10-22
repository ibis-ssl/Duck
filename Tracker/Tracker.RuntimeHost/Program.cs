using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Tracker.RuntimeHost;

var builder = Host.CreateApplicationBuilder(args);
RuntimeHostCommandLine.ApplyOverrides(builder.Configuration, args);
builder.Services.AddRuntimeHost(builder.Configuration);
if (RuntimeVisionDiagnosticsHttpService.IsEnabled(builder.Configuration))
{
    builder.Services.AddHostedService<RuntimeVisionDiagnosticsHttpService>();
}

var host = builder.Build();
await host.RunAsync();
