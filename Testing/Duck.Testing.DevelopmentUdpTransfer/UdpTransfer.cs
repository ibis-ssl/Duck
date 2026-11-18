using System.Net;
using System.Net.Sockets;

namespace Duck.Testing.DevelopmentUdpTransfer;

public static class UdpTransfer
{
    public const int WindowsVisionTransportPort = 39109;
    public const int LinuxTrackerTransportPort = 39111;
    public static readonly IPEndPoint Vision = new(IPAddress.Parse("224.5.23.2"), 10020);
    public static readonly IPEndPoint Tracker = new(Vision.Address, 11010);

    public static UdpClient ListenMulticast(IPEndPoint group, IPAddress interfaceAddress)
    {
        var socket = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            socket.ExclusiveAddressUse = false;
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, group.Port));
            socket.JoinMulticastGroup(group.Address, interfaceAddress);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static UdpClient SendMulticast(IPAddress interfaceAddress)
    {
        var socket = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, interfaceAddress.GetAddressBytes());
            socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            socket.MulticastLoopback = true;
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static async Task RunAsync(
        UdpClient visionInput,
        UdpClient visionOutput,
        Func<IPEndPoint> visionDestination,
        UdpClient trackerInput,
        UdpClient trackerOutput,
        Func<IPEndPoint> trackerDestination,
        Action<string> firstPacketSent,
        CancellationToken stoppingToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var vision = ForwardAsync(visionInput, visionOutput, visionDestination, () => firstPacketSent("Vision"), cancellation.Token);
        var tracker = ForwardAsync(trackerInput, trackerOutput, trackerDestination, () => firstPacketSent("Tracker"), cancellation.Token);
        await Task.WhenAny(vision, tracker);
        await cancellation.CancelAsync();
        try
        {
            await Task.WhenAll(vision, tracker);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static async Task ForwardAsync(
        UdpClient input,
        UdpClient output,
        Func<IPEndPoint> destination,
        Action firstPacketSent,
        CancellationToken cancellationToken)
    {
        var sentFirstPacket = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            var datagram = await input.ReceiveAsync(cancellationToken);
            await output.SendAsync(datagram.Buffer, destination(), cancellationToken);
            if (!sentFirstPacket)
            {
                firstPacketSent();
                sentFirstPacket = true;
            }
        }
    }
}
