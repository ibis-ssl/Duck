using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

internal sealed class RecordingRefereeCommandSource(
    IRefereeCommandSource inner,
    ConcurrentQueue<RefereeCommandSnapshot> snapshots,
    Action<string>? writeLine = null) : IRefereeCommandSource, IDisposable
{
    public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
        uint? previousCommandCounter,
        CancellationToken cancellationToken)
    {
        var snapshot = await inner.WaitForChangeAsync(previousCommandCounter, cancellationToken);
        snapshots.Enqueue(snapshot);
        writeLine?.Invoke($"referee command={snapshot.Command} counter={snapshot.CommandCounter}");
        return snapshot;
    }

    public void Dispose()
    {
        (inner as IDisposable)?.Dispose();
    }
}

internal sealed class LoopbackUdpRefereePacketReceiver : IRefereePacketReceiver, IDisposable
{
    private readonly UdpClient client = new(AddressFamily.InterNetwork);
    private readonly IPEndPoint endpoint;

    public IPEndPoint Endpoint => endpoint;

    public bool IsDisposed { get; private set; }

    public LoopbackUdpRefereePacketReceiver()
    {
        client.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        endpoint = (IPEndPoint)client.Client.LocalEndPoint!;
    }

    public async Task PublishAsync(Referee.Types.Command command, uint commandCounter)
    {
        var packet = new Referee
        {
            PacketTimestamp = commandCounter,
            Stage = Referee.Types.Stage.NormalFirstHalf,
            Command = command,
            CommandCounter = commandCounter,
            CommandTimestamp = commandCounter,
            Yellow = new Referee.Types.TeamInfo { Name = "crane" },
        };

        await client.SendAsync(packet.ToByteArray(), endpoint);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
        (await client.ReceiveAsync(cancellationToken)).Buffer;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            client.Dispose();
            IsDisposed = true;
        }
    }
}
