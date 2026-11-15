using System.Net;
using System.Net.Sockets;

var settings = RelaySettings.Parse(args);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

using var vision = settings.VisionForwardEnabled
    ? CreateMulticastListener(settings.VisionGroup, settings.VisionPort, settings.VisionInterfaceAddress)
    : null;
using var tracker = new UdpClient(AddressFamily.InterNetwork);
tracker.Client.ExclusiveAddressUse = false;
tracker.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
tracker.Client.Bind(new IPEndPoint(IPAddress.Any, settings.TrackerListenPort));
using var trackerOutput = new UdpClient(AddressFamily.InterNetwork);
var visionTarget = settings.VisionForwardEnabled
    ? new IPEndPoint(await ResolveIpv4Async(settings.HostAddress), settings.HostPort)
    : null;
var trackerTarget = new IPEndPoint(settings.TrackerGroup, settings.TrackerMulticastPort);
using var health = new HttpListener();
health.Prefixes.Add($"http://+:{settings.HealthPort}/");
health.Start();
Console.WriteLine(settings.VisionForwardEnabled
    ? $"relay-ready vision={settings.VisionGroup}:{settings.VisionPort}->{visionTarget} tracker=0.0.0.0:{settings.TrackerListenPort}->{trackerTarget}"
    : $"relay-ready vision=disabled tracker=0.0.0.0:{settings.TrackerListenPort}->{trackerTarget}");

var trackerTask = ForwardMulticastAsync(tracker, trackerOutput, trackerTarget, cancellation.Token);
var healthTask = ServeHealthAsync(health, cancellation.Token);
try
{
    var activeTasks = new List<Task> { trackerTask, healthTask };
    if (vision is not null && visionTarget is not null)
    {
        activeTasks.Add(ForwardAsync(vision, visionTarget, cancellation.Token));
    }
    await Task.WhenAny(activeTasks);
    cancellation.Cancel();
    await Task.WhenAll(activeTasks);
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
}
finally
{
    cancellation.Cancel();
}

static UdpClient CreateMulticastListener(IPAddress group, int port, string? interfaceAddress)
{
    var client = new UdpClient(AddressFamily.InterNetwork);
    client.Client.ExclusiveAddressUse = false;
    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
    if (string.IsNullOrWhiteSpace(interfaceAddress))
        client.JoinMulticastGroup(group);
    else if (IPAddress.TryParse(interfaceAddress, out var localAddress) && localAddress.AddressFamily == AddressFamily.InterNetwork)
        client.JoinMulticastGroup(group, localAddress);
    else
    {
        client.Dispose();
        throw new ArgumentException("The Vision multicast interface must be an IPv4 literal.", nameof(interfaceAddress));
    }
    return client;
}

static async Task ForwardAsync(UdpClient source, IPEndPoint target, CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        var packet = await source.ReceiveAsync(token);
        await source.SendAsync(packet.Buffer, target, token);
    }
}

static async Task ForwardMulticastAsync(
    UdpClient source,
    UdpClient sender,
    IPEndPoint target,
    CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        var packet = await source.ReceiveAsync(token);
        await sender.SendAsync(packet.Buffer, target, token);
    }
}

static async Task ServeHealthAsync(HttpListener listener, CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        var context = await listener.GetContextAsync().WaitAsync(token);
        context.Response.StatusCode = context.Request.Url?.AbsolutePath == "/health/ready" ? 200 : 404;
        context.Response.Close();
    }
}

static async Task<IPAddress> ResolveIpv4Async(string host)
{
    if (IPAddress.TryParse(host, out var address) && address.AddressFamily == AddressFamily.InterNetwork)
        return address;
    return (await Dns.GetHostAddressesAsync(host)).First(value => value.AddressFamily == AddressFamily.InterNetwork);
}

internal sealed record RelaySettings(
    IPAddress VisionGroup,
    int VisionPort,
    string? VisionInterfaceAddress,
    bool VisionForwardEnabled,
    string HostAddress,
    int HostPort,
    int TrackerListenPort,
    IPAddress TrackerGroup,
    int TrackerMulticastPort,
    int HealthPort)
{
    internal static RelaySettings Parse(string[] arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length || !arguments[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Relay options must be supplied as --name value pairs.");
            values.Add(arguments[index][2..], arguments[index + 1]);
        }

        string Required(string name)
        {
            var envName = "DUCK_RELAY_" + name.Replace('-', '_').ToUpperInvariant();
            var value = values.GetValueOrDefault(name) ?? Environment.GetEnvironmentVariable(envName);
            return !string.IsNullOrWhiteSpace(value)
                ? value : throw new ArgumentException($"--{name} or {envName} is required.");
        }
        string? Optional(string name)
        {
            var envName = "DUCK_RELAY_" + name.Replace('-', '_').ToUpperInvariant();
            var value = values.GetValueOrDefault(name) ?? Environment.GetEnvironmentVariable(envName);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        bool OptionalBoolean(string name, bool defaultValue)
        {
            var value = Optional(name);
            if (value is null)
                return defaultValue;
            if (bool.TryParse(value, out var parsed))
                return parsed;
            throw new ArgumentException($"--{name} or DUCK_RELAY_{name.Replace('-', '_').ToUpperInvariant()} must be true or false.");
        }
        int Port(string name)
        {
            if (!int.TryParse(Required(name), out var port) || port is < 1 or > 65535)
                throw new ArgumentException($"--{name} must be a valid UDP port.");
            return port;
        }
        IPAddress Address(string name, bool multicast)
        {
            if (!IPAddress.TryParse(Required(name), out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException($"--{name} must be an IPv4 literal.");
            var isMulticast = address.GetAddressBytes()[0] is >= 224 and <= 239;
            if (isMulticast != multicast)
                throw new ArgumentException($"--{name} must be {(multicast ? "multicast" : "unicast")} IPv4.");
            return address;
        }

        return new(
            Address("vision-group", multicast: true), Port("vision-port"), Optional("vision-interface-address"),
            OptionalBoolean("vision-forward-enabled", defaultValue: true),
            Required("host-address"),
            Port("host-port"), Port("tracker-listen-port"), Address("tracker-group", multicast: true),
            Port("tracker-multicast-port"), Port("health-port"));
    }
}
