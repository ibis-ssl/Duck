using TrackerConnectionLib;

var port = args.Length > 0 ? int.Parse(args[0]) : 11010;

var receiver = new UdpTrackerReceiver<TrackerPacketAdapter>(
    port,
    new TrackerWrapperPacketDeserializer());

var manager = new MultiTrackerManager<TrackerPacketAdapter>();

receiver.PacketReceived += (packet, remoteEndPoint) =>
{
    manager.ProcessPacket(packet);

    Console.WriteLine(
        $"Received tracker packet: uuid={packet.Uuid}, source={packet.SourceName}, remote={remoteEndPoint}");
};

manager.TrackerUpdated += state =>
{
    Console.WriteLine(
        $"Tracker updated: uuid={state.Uuid}, source={state.SourceName}, last={state.LastUpdateUtc:O}");
};

manager.ActiveTrackerUpdated += state =>
{
    var raw = state.LastPacket?.Packet;
    var frame = raw?.TrackedFrame;
    
    Console.WriteLine(
        $"Active tracker: uuid={state.Uuid}, balls={frame?.Balls.Count ?? 0}, robots={frame?.Robots.Count ?? 0}");
};

receiver.Start();

Console.WriteLine($"Listening tracker packets on UDP port {port}");
Console.WriteLine("Press Enter to stop.");

Console.ReadLine();

await receiver.StopAsync();