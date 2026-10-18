using TrackerConnectionLib;
using System.Net;
using System.Net.Sockets;

namespace Duck.Testing.AppHost;

internal sealed class TrackerPacketReadinessMonitor : IDisposable
{
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromSeconds(5);
    private readonly string expectedSourceName;
    private readonly UdpTrackerReceiver<TrackerPacketAdapter> receiver;
    private long lastMatchingPacketTicks;
    private readonly UdpClient visionReceiver;
    private readonly CancellationTokenSource visionCancellation = new();
    private readonly Task visionReceiveTask;
    private long lastVisionPacketTicks;

    public TrackerPacketReadinessMonitor(string expectedSourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSourceName);
        this.expectedSourceName = expectedSourceName;
        receiver = new UdpTrackerReceiver<TrackerPacketAdapter>(
            11010,
            "224.5.23.2",
            new TrackerWrapperPacketDeserializer());
        receiver.PacketReceived += OnPacketReceived;
        receiver.Start();

        visionReceiver = new UdpClient(AddressFamily.InterNetwork);
        visionReceiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        visionReceiver.Client.Bind(new IPEndPoint(IPAddress.Any, 10020));
        visionReceiver.JoinMulticastGroup(IPAddress.Parse("224.5.23.2"));
        visionReceiveTask = ReceiveVisionPacketsAsync(visionCancellation.Token);
    }

    public bool IsFresh
    {
        get
        {
            var ticks = Interlocked.Read(ref lastMatchingPacketTicks);
            var visionTicks = Interlocked.Read(ref lastVisionPacketTicks);
            var now = DateTimeOffset.UtcNow;
            return IsTimestampFresh(ticks, now) && IsTimestampFresh(visionTicks, now);
        }
    }

    public void Dispose()
    {
        receiver.PacketReceived -= OnPacketReceived;
        receiver.Dispose();
        visionCancellation.Cancel();
        visionReceiver.Dispose();
        visionCancellation.Dispose();
    }

    private void OnPacketReceived(TrackerPacketAdapter packet, System.Net.IPEndPoint endpoint)
    {
        if (DockerServiceReadiness.TrackerPacketMatchesExpectedSource(packet.SourceName, expectedSourceName))
        {
            Interlocked.Exchange(ref lastMatchingPacketTicks, DateTimeOffset.UtcNow.UtcTicks);
        }
    }

    private async Task ReceiveVisionPacketsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await visionReceiver.ReceiveAsync(cancellationToken);
                if (DockerServiceReadiness.IsDecodableVisionPacket(result.Buffer))
                {
                    Interlocked.Exchange(ref lastVisionPacketTicks, DateTimeOffset.UtcNow.UtcTicks);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static bool IsTimestampFresh(long ticks, DateTimeOffset now) =>
        ticks != 0 && now - new DateTimeOffset(ticks, TimeSpan.Zero) <= FreshnessWindow;
}
