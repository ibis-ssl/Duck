using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Duck.Testing.DevelopmentUdpTransfer;

if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException("The container entry point requires Linux.");

if (args is ["--send-simulator-control", var encoded])
{
    byte[] datagram;
    try { datagram = Convert.FromBase64String(encoded); }
    catch (FormatException)
    {
        Console.Error.WriteLine("Simulator control payload must be base64.");
        return 2;
    }
    if (datagram.Length is 0 or > 65507)
    {
        Console.Error.WriteLine("Simulator control payload must be one IPv4 UDP datagram.");
        return 2;
    }

    using var sender = new UdpClient(AddressFamily.InterNetwork);
    await sender.SendAsync(datagram, new IPEndPoint(IPAddress.Loopback, 10300));
    return 0;
}

IPEndPoint? probeEndpoint = args is ["--observe", var kind] ? kind switch
{
    "vision" => UdpTransfer.Vision,
    "tracker" => UdpTransfer.Tracker,
    "referee" => new(IPAddress.Parse("224.5.23.1"), 11003),
    _ => null,
} : null;
if (args.Length > 0 && probeEndpoint is null)
{
    Console.Error.WriteLine("Expected --observe vision|tracker|referee or --send-simulator-control <base64>.");
    return 2;
}

var eth0 = NetworkInterface.GetAllNetworkInterfaces().Single(nic => nic.Name == "eth0");
var eth0Address = eth0.GetIPProperties().UnicastAddresses
    .Select(address => address.Address)
    .First(address => address.AddressFamily == AddressFamily.InterNetwork);
if (probeEndpoint is not null)
{
    using var observer = UdpTransfer.ListenMulticast(probeEndpoint, eth0Address, probeEndpoint.Address);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    try
    {
        var datagram = await observer.ReceiveAsync(timeout.Token);
        Console.WriteLine(Convert.ToBase64String(datagram.Buffer));
        return 0;
    }
    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
    {
        Console.Error.WriteLine($"No UDP datagram received for {probeEndpoint} within two seconds.");
        return 1;
    }
}

var windowsAddress = (await Dns.GetHostAddressesAsync("host.docker.internal", AddressFamily.InterNetwork)).First();

using var stopping = new CancellationTokenSource();
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    stopping.Cancel();
});
using var visionInput = UdpTransfer.ListenMulticast(UdpTransfer.Vision, eth0Address);
using var visionOutput = new UdpClient(AddressFamily.InterNetwork);
using var trackerInput = new UdpClient(new IPEndPoint(IPAddress.Any, UdpTransfer.LinuxTrackerTransportPort));
using var trackerOutput = UdpTransfer.SendMulticast(eth0Address);
var visionDestination = new IPEndPoint(windowsAddress, UdpTransfer.WindowsVisionTransportPort);
Console.WriteLine($"Listening for Vision on eth0 ({eth0Address}) and Tracker on UDP {UdpTransfer.LinuxTrackerTransportPort}.");
await UdpTransfer.RunAsync(
    visionInput, visionOutput, () => visionDestination,
    trackerInput, trackerOutput, () => UdpTransfer.Tracker,
    stream => Console.WriteLine($"Sent first {stream} datagram."), stopping.Token);
return 0;
