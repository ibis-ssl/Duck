using System.Net;
using System.Net.Sockets;

namespace TrackerConnectionLib;

public sealed class UdpTrackerReceiver<TPacket> : IDisposable
    where TPacket : ITrackerPacket
{
    private readonly UdpClient _udpClient;
    private readonly ITrackerDeserializer<TPacket> _deserializer;

    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    public event Action<TPacket, IPEndPoint>? PacketReceived;

    public UdpTrackerReceiver(
        int port,
        ITrackerDeserializer<TPacket> deserializer)
    {
        _deserializer = deserializer;

        _udpClient = new UdpClient(AddressFamily.InterNetwork);

        _udpClient.Client.SetSocketOption(
            SocketOptionLevel.Socket,
            SocketOptionName.ReuseAddress,
            true);

        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
    }
    public void Start()
    {
        if (_receiveTask is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync();
        _udpClient.Close();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                // UDP close による例外は停止処理として無視
            }
        }

        _cts.Dispose();
        _cts = null;
        _receiveTask = null;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;

            try
            {
                result = await _udpClient.ReceiveAsync(cancellationToken);
            }
            catch
            {
                break;
            }

            if (_deserializer.TryDeserialize(result.Buffer, out var packet) &&
                packet is not null)
            {
                PacketReceived?.Invoke(packet, result.RemoteEndPoint);
            }
        }
    }

    public void Dispose()
    {
        _ = StopAsync();
        _udpClient.Dispose();
    }
}