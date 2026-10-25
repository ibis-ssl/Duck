using Duck.Testing.RefereeDriver.Hosting;

if (RefereeIdentityControlProtocol.IsRelayCommand(args))
{
    return await RefereeIdentityControlProtocol.RelayAsync(
        Console.In, Console.Out, RefereeIdentityControlProtocol.SocketPath, CancellationToken.None);
}

await RefereeDriverApplication.RunAsync(args);
return 0;
