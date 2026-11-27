using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text.Json;
using TrackerConnectionLib;

static class Program
{
    static int FreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    static void Main()
    {
        var rows = new List<object>();
        for (int i = 0; i < 10; i++)
            rows.Add(Run(false));
        rows.Add(Run(true));
        Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }

    static object Run(bool backlog)
    {
        using var release = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        var workers = backlog
            ? Enumerable.Range(0, 64).Select(_ => Task.Run(() => { started.Set(); release.Wait(); })).ToArray()
            : Array.Empty<Task>();
        if (backlog && !started.Wait(TimeSpan.FromSeconds(5))) throw new Exception("診断用workerが開始しなかった。");
        var decoder = new ProbeDecoder();
        int count = 0;
        int port = FreePort();
        using var receiver = new UdpTrackerReceiver<ProbePacket>(port, decoder);
        receiver.PacketReceived += (_, _) =>
        {
            if (Interlocked.Increment(ref count) == 1)
                throw new InvalidOperationException("診断用handler例外。");
        };
        using var sender = new UdpClient(AddressFamily.InterNetwork);
        var timer = Stopwatch.StartNew();
        receiver.Start();
        sender.Send(new byte[] { 1 }, new IPEndPoint(IPAddress.Loopback, port));
        var firstWithinOneSecond = SpinWait.SpinUntil(() => Volatile.Read(ref count) >= 1, TimeSpan.FromSeconds(1));
        var elapsedBeforeRelease = timer.Elapsed.TotalMilliseconds;
        var decodeCountBeforeRelease = Volatile.Read(ref decoder.Count);
        release.Set();
        var firstAfterRelease = SpinWait.SpinUntil(() => Volatile.Read(ref count) >= 1, TimeSpan.FromSeconds(5));
        sender.Send(new byte[] { 2 }, new IPEndPoint(IPAddress.Loopback, port));
        var secondAfterRelease = SpinWait.SpinUntil(() => Volatile.Read(ref count) >= 2, TimeSpan.FromSeconds(5));
        receiver.StopAsync().GetAwaiter().GetResult();
        Task.WaitAll(workers);
        return new { backlog, firstWithinOneSecond, elapsedBeforeRelease, decodeCountBeforeRelease,
            firstAfterRelease, secondAfterRelease, handlerErrors = receiver.HandlerErrorCount, handled = count };
    }
}

sealed record ProbePacket : ITrackerPacket
{
    public string Uuid => "probe";
    public string? SourceName => "probe";
}
sealed class ProbeDecoder : ITrackerDeserializer<ProbePacket>
{
    public int Count;
    public bool TryDeserialize(ReadOnlySpan<byte> data, out ProbePacket? packet)
    {
        Interlocked.Increment(ref Count);
        packet = new ProbePacket();
        return true;
    }
}
