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

internal sealed class AspireAcceptanceResultWriter(
    ConcurrentQueue<RefereeCommandSnapshot> refereeSnapshots)
{
    private Exception? failure;

    public void RecordFailure(Exception exception)
    {
        failure = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public static InvalidOperationException CreateMotionFailure(RefereeStateLostException exception) =>
        new(exception.StateLoss.ToFailureMessage(), exception);

    public async Task WriteAsync(
        string path,
        string stageReached,
        int refereeRawDatagramCount,
        int? visionPacketsConsumed,
        int? trackerPacketsConsumed,
        object? comparisonMeasurement,
        object? receiverSockets,
        object? movement)
    {
        var evidence = new
        {
            status = failure is null ? "passed" : "failed",
            failure = failure?.ToString(),
            stageReached,
            refereeCommands = refereeSnapshots.ToArray().Select(snapshot => new
            {
                command = snapshot.Command.ToString(),
                snapshot.CommandCounter,
            }),
            refereeRawDatagramCount,
            refereeStateLoss = FindRefereeStateLoss(failure),
            visionPacketsConsumed,
            trackerPacketsConsumed,
            comparisonMeasurement,
            packetCountSemantics = "Consumed counts record datagrams read by the harness; null means that reader stage was not reached and does not imply network ingress was zero.",
            receiverSockets,
            movement,
            runner = Environment.GetEnvironmentVariable("RUNNER_OS"),
            sha = Environment.GetEnvironmentVariable("GITHUB_SHA"),
        };

        await File.WriteAllTextAsync(
            path,
            System.Text.Json.JsonSerializer.Serialize(
                evidence,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static RefereeStateLossEvent? FindRefereeStateLoss(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is RefereeStateLostException stateLost)
            {
                return stateLost.StateLoss;
            }
        }

        return null;
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
