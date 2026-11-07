using System.Net;
using System.Net.Sockets;

namespace Duck.Testing.CraneLaunchOverlay;

/// <summary>Adapts per-robot Crane mode-4 datagrams to the pinned CM4 simulator's 11-slot input.</summary>
internal sealed class CraneMode4PacketAdapter : IAsyncDisposable
{
    internal const int RobotCount = 11;
    internal const int CommandBytes = 64;
    internal const int SlotBytes = CommandBytes + 1;
    internal const int AggregatePacketBytes = RobotCount * SlotBytes;
    internal const int DefaultCraneCommandPort = 12345;
    internal const int DefaultCraneConfigPort = 12350;
    private const byte EmptyRobotId = 0xff;

    private readonly IPAddress cm4Address;
    private readonly int cm4CommandPort;
    private readonly int cm4ConfigPort;
    private readonly UdpClient commandReceiver;
    private readonly UdpClient configReceiver;
    private readonly UdpClient sender = new(AddressFamily.InterNetwork);
    private readonly CancellationTokenSource stop = new();
    private Task? commandForwarder;
    private Task? configForwarder;
    private int invalidCommandPackets;

    internal IPEndPoint CommandInputEndpoint => (IPEndPoint)commandReceiver.Client.LocalEndPoint!;
    internal IPEndPoint ConfigInputEndpoint => (IPEndPoint)configReceiver.Client.LocalEndPoint!;

    internal CraneMode4PacketAdapter(
        IPAddress cm4Address,
        int commandInputPort = DefaultCraneCommandPort,
        int configInputPort = DefaultCraneConfigPort,
        int cm4CommandPort = DefaultCraneCommandPort,
        int cm4ConfigPort = DefaultCraneConfigPort)
    {
        ArgumentNullException.ThrowIfNull(cm4Address);
        if (cm4Address.AddressFamily != AddressFamily.InterNetwork || cm4Address.Equals(IPAddress.Any) ||
            cm4Address.Equals(IPAddress.Broadcast) || cm4Address.GetAddressBytes()[0] >= 224 ||
            commandInputPort is < 0 or > 65535 || configInputPort is < 0 or > 65535 ||
            cm4CommandPort is < 1 or > 65535 || cm4ConfigPort is < 1 or > 65535)
        {
            throw new ArgumentException("The CM4 target and UDP ports are invalid.", nameof(cm4Address));
        }

        this.cm4Address = cm4Address;
        this.cm4CommandPort = cm4CommandPort;
        this.cm4ConfigPort = cm4ConfigPort;
        commandReceiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, commandInputPort));
        try
        {
            configReceiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, configInputPort));
        }
        catch
        {
            commandReceiver.Dispose();
            sender.Dispose();
            stop.Dispose();
            throw;
        }
    }

    internal static bool TryCreateAggregateFrame(ReadOnlySpan<byte> command, out byte[] frame)
    {
        frame = [];
        if (command.Length != SlotBytes || command[0] >= RobotCount)
            return false;

        frame = new byte[AggregatePacketBytes];
        for (var slot = 0; slot < RobotCount; slot++)
            frame[slot * SlotBytes] = EmptyRobotId;
        command.CopyTo(frame.AsSpan(command[0] * SlotBytes, SlotBytes));
        return true;
    }

    internal void Start()
    {
        if (commandForwarder is not null || configForwarder is not null)
            throw new InvalidOperationException("The Crane packet adapter is already running.");

        Console.WriteLine(
            $"Crane mode-4 adapter listening on 127.0.0.1:{CommandInputEndpoint.Port}/{ConfigInputEndpoint.Port}; " +
            $"forwarding to CM4 {cm4Address}:{cm4CommandPort}/{cm4ConfigPort}.");
        commandForwarder = ForwardCommandsAsync(stop.Token);
        configForwarder = ForwardConfigurationAsync(stop.Token);
    }

    private async Task ForwardCommandsAsync(CancellationToken cancellationToken)
    {
        var destination = new IPEndPoint(cm4Address, cm4CommandPort);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var received = await commandReceiver.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (!TryCreateAggregateFrame(received.Buffer, out var frame))
                {
                    Interlocked.Increment(ref invalidCommandPackets);
                    continue;
                }
                await sender.SendAsync(frame, destination, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task ForwardConfigurationAsync(CancellationToken cancellationToken)
    {
        var destination = new IPEndPoint(cm4Address, cm4ConfigPort);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var received = await configReceiver.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (received.Buffer.Length == 0 || received.Buffer.Length > 1024)
                    continue;
                await sender.SendAsync(received.Buffer, destination, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        commandReceiver.Dispose();
        configReceiver.Dispose();
        sender.Dispose();
        var loops = new[] { commandForwarder, configForwarder }.Where(task => task is not null).Cast<Task>();
        try
        {
            await Task.WhenAll(loops).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        var invalid = Volatile.Read(ref invalidCommandPackets);
        if (invalid > 0)
            Console.Error.WriteLine($"Crane mode-4 adapter dropped {invalid} datagrams with an invalid size or robot ID.");
        stop.Dispose();
    }
}
