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
    builder.Services.AddRuntimeHost(builder.Configuration);
    if (RuntimeVisionDiagnosticsHttpService.IsEnabled(builder.Configuration))
    {
        builder.Services.AddHostedService<RuntimeVisionDiagnosticsHttpService>();
    }

    var host = builder.Build();
    var diagnosticsLogProvider = new McapDiagnosticsLoggerProvider(builder.Configuration["Tracker:Diagnostics:DirectoryPath"]);
    host.Services.GetRequiredService<ILoggerFactory>().AddProvider(diagnosticsLogProvider);
    Exception? hostFailure = null;
    try
    {
        await host.RunAsync();
    }
    catch (Exception exception)
    {
        hostFailure = exception;
    }

    Exception? recordingFailure = null;
    try
    {
        diagnosticsLogProvider.Complete(hostSucceeded: hostFailure is null);
    }
    catch (Exception exception)
    {
        recordingFailure = exception;
    }

    if (hostFailure is not null)
    {
        if (recordingFailure is not null)
        {
            Console.Error.WriteLine($"RuntimeHost 停止処理の診断記録も失敗しました: {recordingFailure}");
        }

        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(hostFailure).Throw();
    }

    if (recordingFailure is not null)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(recordingFailure).Throw();
    }
}
