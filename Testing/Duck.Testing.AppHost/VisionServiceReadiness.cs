using System.Net;
using System.Net.Sockets;

namespace Duck.Testing.AppHost;

public sealed class VisionReadinessState
{
    private int decodedDetectionSeen;

    public bool IsReady => Volatile.Read(ref decodedDetectionSeen) != 0;

    public bool ObserveDatagram(ReadOnlySpan<byte> datagram)
    {
        try
        {
            var packet = SSL_WrapperPacket.Parser.ParseFrom(datagram.ToArray());
            if (packet.Detection is null)
            {
                return false;
            }

            Interlocked.Exchange(ref decodedDetectionSeen, 1);
            return true;
        }
        catch (Google.Protobuf.InvalidProtocolBufferException)
        {
            return false;
        }
    }
}

/// <summary>Waits for a successfully decoded SSL-Vision detection datagram from the simulator.</summary>
public sealed class VisionServiceReadiness : IAsyncDisposable
{
    private readonly UdpClient client;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task listener;
    private readonly VisionReadinessState state = new();

    public VisionServiceReadiness()
    {
        client = new UdpClient(AddressFamily.InterNetwork);
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, 10020));
        client.JoinMulticastGroup(IPAddress.Parse("224.5.23.2"));
        listener = ListenAsync(cancellation.Token);
    }

    public bool IsReady => state.IsReady;

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        client.Dispose();
        try
        {
            await listener;
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }

        cancellation.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult datagram;
            try
            {
                datagram = await client.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            state.ObserveDatagram(datagram.Buffer);
        }
    }
}
