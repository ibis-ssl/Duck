using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Tracker.RuntimeHost;
using Tracker.Diagnostics.Mcap;
using Microsoft.Extensions.Logging;

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
    var diagnosticsLogProvider = new McapDiagnosticsLoggerProvider(builder.Configuration["Tracker:Diagnostics:DirectoryPath"]);
    builder.Logging.AddProvider(diagnosticsLogProvider);
    builder.Services.AddRuntimeHost(builder.Configuration);
    if (RuntimeVisionDiagnosticsHttpService.IsEnabled(builder.Configuration))
    {
        builder.Services.AddHostedService<RuntimeVisionDiagnosticsHttpService>();
    }

    var host = builder.Build();
    try
    {
        await host.RunAsync();
        diagnosticsLogProvider.Complete();
    }
    catch
    {
        diagnosticsLogProvider.Complete();
        throw;
    }
}
