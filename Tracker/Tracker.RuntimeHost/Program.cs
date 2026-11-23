using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tracker.RuntimeHost;
using Tracker.Diagnostics.Mcap;

if (RuntimeIdentityControlRelay.IsRelayCommand(args))
{
    using var relayCancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelRelay = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        relayCancellation.Cancel();
    };
    Console.CancelKeyPress += cancelRelay;
    try
    {
        Environment.ExitCode = await RuntimeIdentityControlRelay.RunAsync(
            Console.In, Console.Out, RuntimeIdentityControlSocket.DefaultPath, relayCancellation.Token);
    }
    finally
    {
        Console.CancelKeyPress -= cancelRelay;
    }
}
else
{
    var builder = Host.CreateApplicationBuilder(args);
    RuntimeHostCommandLine.ApplyOverrides(builder.Configuration, args);
    builder.Services.AddRuntimeHost(builder.Configuration);
    if (RuntimeVisionDiagnosticsHttpService.IsEnabled(builder.Configuration))
    {
        builder.Services.AddHostedService<RuntimeVisionDiagnosticsHttpService>();
    }

    var host = builder.Build();
    var diagnosticsLogProvider = new McapDiagnosticsLoggerProvider(builder.Configuration["Tracker:Diagnostics:DirectoryPath"]);
    await McapDiagnosticsHostRunner.RunAsync(
        host.Services.GetRequiredService<ILoggerFactory>(),
        diagnosticsLogProvider,
        () => host.RunAsync());
}
