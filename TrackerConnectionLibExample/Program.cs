using TrackerConnectionLib;

var port = args.Length > 0 ? int.Parse(args[0]) : 11010;
var multicastAddress = args.Length > 1 ? NullIfEmpty(args[1]) : "224.5.23.2";
var interfaceAddress = args.Length > 2 ? NullIfEmpty(args[2]) : null;
var durationSeconds = args.Length > 3 ? int.Parse(args[3]) : 0;
var sourceFilter = args.Length > 4 ? NullIfEmpty(args[4]) : null;
var sourceStats = new Dictionary<string, SourceStats>(StringComparer.Ordinal);
var gate = new object();
var totalPackets = 0L;
var matchedPackets = 0L;

var receiver = new UdpTrackerReceiver<TrackerPacketAdapter>(
    port,
    multicastAddress,
    new TrackerWrapperPacketDeserializer(),
    interfaceAddress);

var manager = new MultiTrackerManager<TrackerPacketAdapter>();

receiver.PacketReceived += (packet, remoteEndPoint) =>
{
    if (!MatchesSourceFilter(packet, sourceFilter))
    {
        lock (gate)
        {
            totalPackets++;
        }

        return;
    }

    var receivedAt = DateTimeOffset.UtcNow;
    manager.ProcessPacket(packet, remoteEndPoint, receivedAt);

    var frame = packet.Packet.TrackedFrame;
    var key = string.Join('\u001f', packet.Uuid, packet.SourceName ?? "", remoteEndPoint.ToString());
    lock (gate)
    {
        totalPackets++;
        matchedPackets++;
        if (!sourceStats.TryGetValue(key, out var stats))
        {
            stats = new SourceStats(packet.Uuid, packet.SourceName ?? "", remoteEndPoint.ToString());
            sourceStats.Add(key, stats);
            Console.WriteLine(
                $"Discovered source uuid={stats.Uuid}, source={stats.SourceName}, remote={stats.RemoteEndpoint}");
        }

        stats.Record(
            receivedAt,
            frame?.FrameNumber ?? 0,
            frame?.Timestamp ?? 0,
            frame?.Balls.Count ?? 0,
            frame?.Robots.Count ?? 0,
            frame?.Robots.Select(robot => $"{robot.RobotId.Team}:{robot.RobotId.Id}").ToArray() ?? []);
    }
};

receiver.Start();

Console.WriteLine($"Listening tracker packets on UDP port {port}");
Console.WriteLine($"Multicast group: {multicastAddress ?? "-"}");
Console.WriteLine($"Interface address: {interfaceAddress ?? "-"}");
Console.WriteLine($"Source filter: {sourceFilter ?? "-"}");

if (durationSeconds > 0)
{
    Console.WriteLine($"Stopping after {durationSeconds} seconds.");
    for (var elapsedSeconds = 0; elapsedSeconds < durationSeconds; elapsedSeconds++)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        PrintSummary(elapsedSeconds + 1);
    }
}
else
{
    Console.WriteLine("Press Enter to stop.");
    Console.ReadLine();
}

await receiver.StopAsync();
PrintSummary(durationSeconds);

static string? NullIfEmpty(string value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static bool MatchesSourceFilter(TrackerPacketAdapter packet, string? sourceFilter)
{
    return string.IsNullOrWhiteSpace(sourceFilter) ||
           string.Equals(packet.Uuid, sourceFilter, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(packet.SourceName, sourceFilter, StringComparison.OrdinalIgnoreCase);
}

void PrintSummary(int elapsedSeconds)
{
    lock (gate)
    {
        Console.WriteLine(
            $"--- summary t={elapsedSeconds}s totalPackets={totalPackets} matchedPackets={matchedPackets} sources={sourceStats.Count} ---");
        foreach (var stats in sourceStats.Values.OrderBy(stats => stats.SourceName).ThenBy(stats => stats.RemoteEndpoint))
        {
            Console.WriteLine(stats.Format());
        }
    }
}

sealed class SourceStats
{
    public SourceStats(string uuid, string sourceName, string remoteEndpoint)
    {
        Uuid = uuid;
        SourceName = sourceName;
        RemoteEndpoint = remoteEndpoint;
    }

    public string Uuid { get; }

    public string SourceName { get; }

    public string RemoteEndpoint { get; }

    public long Count { get; private set; }

    public DateTimeOffset FirstReceivedAt { get; private set; }

    public DateTimeOffset LastReceivedAt { get; private set; }

    public uint FirstFrameNumber { get; private set; }

    public uint LastFrameNumber { get; private set; }

    public double FirstTimestamp { get; private set; }

    public double LastTimestamp { get; private set; }

    public int MinBalls { get; private set; } = int.MaxValue;

    public int MaxBalls { get; private set; }

    public int MinRobots { get; private set; } = int.MaxValue;

    public int MaxRobots { get; private set; }

    public uint MinRobotFrameNumber { get; private set; }

    public IReadOnlyList<string> MinRobotIds { get; private set; } = [];

    public void Record(
        DateTimeOffset receivedAt,
        uint frameNumber,
        double timestamp,
        int ballCount,
        int robotCount,
        IReadOnlyList<string> robotIds)
    {
        if (Count == 0)
        {
            FirstReceivedAt = receivedAt;
            FirstFrameNumber = frameNumber;
            FirstTimestamp = timestamp;
        }

        Count++;
        LastReceivedAt = receivedAt;
        LastFrameNumber = frameNumber;
        LastTimestamp = timestamp;
        MinBalls = Math.Min(MinBalls, ballCount);
        MaxBalls = Math.Max(MaxBalls, ballCount);
        if (robotCount < MinRobots)
        {
            MinRobots = robotCount;
            MinRobotFrameNumber = frameNumber;
            MinRobotIds = robotIds;
        }

        MaxRobots = Math.Max(MaxRobots, robotCount);
    }

    public string Format()
    {
        var durationSeconds = Math.Max((LastReceivedAt - FirstReceivedAt).TotalSeconds, 0.001);
        var rateHz = Count <= 1 ? 0 : (Count - 1) / durationSeconds;
        return
            $"uuid={Uuid}, source={SourceName}, remote={RemoteEndpoint}, count={Count}, rateHz={rateHz:F1}, " +
            $"frames={FirstFrameNumber}->{LastFrameNumber}, timestamps={FirstTimestamp}->{LastTimestamp}, " +
            $"balls={MinBalls}..{MaxBalls}, robots={MinRobots}..{MaxRobots}, " +
            $"minRobotFrame={MinRobotFrameNumber}, minRobotIds=[{string.Join(",", MinRobotIds)}]";
    }
}
