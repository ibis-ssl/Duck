using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Duck.Testing.DevelopmentUdpTransfer;

if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException("The container entry point requires Linux.");

var eth0 = NetworkInterface.GetAllNetworkInterfaces().Single(nic => nic.Name == "eth0");
var eth0Address = eth0.GetIPProperties().UnicastAddresses
    .Select(address => address.Address)
    .First(address => address.AddressFamily == AddressFamily.InterNetwork);
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
