using System.Net;
using System.Net.Sockets;

namespace Duck.Testing.CraneLaunchOverlay;

/// <summary>Wraps per-robot Crane datagrams in the 11-slot Ibis input format.</summary>
internal sealed class CraneIbisPacketAdapter : IAsyncDisposable
{
    internal const int RobotCount = 11;
    internal const int CommandBytes = 64;
    internal const int SlotBytes = CommandBytes + 1;
    internal const int AggregatePacketBytes = RobotCount * SlotBytes;
    internal const int DefaultCraneCommandPort = 12345;
    internal const int DefaultCraneConfigPort = 12350;
    private const byte EmptyRobotId = 0xff;

    private readonly IPAddress targetAddress;
    private readonly int targetCommandPort;
    private readonly int targetConfigPort;
    private readonly UdpClient commandReceiver;
    private readonly UdpClient configReceiver;
    private readonly UdpClient sender = new(AddressFamily.InterNetwork);
    private readonly CancellationTokenSource stop = new();
    private Task? commandForwarder;
    private Task? configForwarder;
    internal Task Completion { get; private set; } = Task.CompletedTask;
    private int invalidCommandPackets;

    internal IPEndPoint CommandInputEndpoint => (IPEndPoint)commandReceiver.Client.LocalEndPoint!;
    internal IPEndPoint ConfigInputEndpoint => (IPEndPoint)configReceiver.Client.LocalEndPoint!;

    internal CraneIbisPacketAdapter(
        IPAddress targetAddress,
        int commandInputPort = DefaultCraneCommandPort,
        int configInputPort = DefaultCraneConfigPort,
        int targetCommandPort = DefaultCraneCommandPort,
        int targetConfigPort = DefaultCraneConfigPort)
    {
        ArgumentNullException.ThrowIfNull(targetAddress);
        if (targetAddress.AddressFamily != AddressFamily.InterNetwork || targetAddress.Equals(IPAddress.Any) ||
            targetAddress.Equals(IPAddress.Broadcast) || targetAddress.GetAddressBytes()[0] >= 224 ||
            commandInputPort is < 0 or > 65535 || configInputPort is < 0 or > 65535 ||
            targetCommandPort is < 1 or > 65535 || targetConfigPort is < 1 or > 65535)
        {
            throw new ArgumentException("The Ibis target and UDP ports are invalid.", nameof(targetAddress));
        }

        this.targetAddress = targetAddress;
        this.targetCommandPort = targetCommandPort;
        this.targetConfigPort = targetConfigPort;
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
            $"Crane Ibis adapter listening on 127.0.0.1:{CommandInputEndpoint.Port}/{ConfigInputEndpoint.Port}; " +
            $"forwarding to {targetAddress}:{targetCommandPort}/{targetConfigPort}.");
        commandForwarder = ForwardCommandsAsync(stop.Token);
        configForwarder = ForwardConfigurationAsync(stop.Token);
        Completion = SuperviseForwardersAsync(commandForwarder, configForwarder, stop);
    }

    internal static async Task SuperviseForwardersAsync(
        Task commands,
        Task configuration,
        CancellationTokenSource stop)
    {
        var first = await Task.WhenAny(commands, configuration).ConfigureAwait(false);
        if (stop.IsCancellationRequested)
        {
            try { await Task.WhenAll(commands, configuration).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            return;
        }

        stop.Cancel();
        try { await Task.WhenAll(commands, configuration).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
        if (first.IsFaulted)
            await first.ConfigureAwait(false);
        throw new InvalidOperationException("A Crane Ibis forwarding loop stopped unexpectedly.");
    }

    private async Task ForwardCommandsAsync(CancellationToken cancellationToken)
    {
        var destination = new IPEndPoint(targetAddress, targetCommandPort);
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
        var destination = new IPEndPoint(targetAddress, targetConfigPort);
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
            Console.Error.WriteLine($"Crane Ibis adapter dropped {invalid} datagrams with an invalid size or robot ID.");
        stop.Dispose();
    }
}
