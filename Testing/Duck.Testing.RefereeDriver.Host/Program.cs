using Duck.Testing.RefereeDriver.Hosting;

if (RefereeIdentityControlProtocol.IsRelayCommand(args))
{
    return await RefereeIdentityControlProtocol.RelayAsync(
        Console.In, Console.Out, RefereeIdentityControlProtocol.SocketPath, CancellationToken.None);
}

if (args is ["--simulator-control-relay"])
{
    await SimulatorControlRelayApplication.RunAsync(CancellationToken.None);
    return 0;
}

await RefereeDriverApplication.RunAsync(args);
return 0;
