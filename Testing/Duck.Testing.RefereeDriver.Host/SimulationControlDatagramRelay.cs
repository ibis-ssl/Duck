using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duck.Testing.RefereeDriver.Hosting;

/// <summary>
/// Transparently forwards Sumatra's existing simulation-control UDP traffic while recording
/// timestamped Blue robot commands and Simulator responses for a MATCH diagnostic.
/// </summary>
internal sealed class SimulationControlDatagramRelay : IHostedService, IAsyncDisposable
{
    internal const int SimulatorControlPort = 10300;
    internal const int SimulatorBlueTeamPort = 10301;

    private readonly UdpPortRelay[] relays;
    private readonly ILogger<SimulationControlDatagramRelay> logger;
    private readonly CancellationTokenSource stop = new();

    internal SimulationControlDatagramRelay(
        IPAddress simulatorAddress,
        int controlListenPort = SimulatorControlPort,
        int blueListenPort = SimulatorBlueTeamPort,
        int controlSimulatorPort = SimulatorControlPort,
        int blueSimulatorPort = SimulatorBlueTeamPort,
        ILogger<SimulationControlDatagramRelay>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(simulatorAddress);
        if (simulatorAddress.AddressFamily != AddressFamily.InterNetwork ||
            simulatorAddress.Equals(IPAddress.Any) || simulatorAddress.Equals(IPAddress.Broadcast) ||
            simulatorAddress.GetAddressBytes()[0] >= 224)
            throw new ArgumentException("Simulator address must be a unicast IPv4 address.", nameof(simulatorAddress));

        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SimulationControlDatagramRelay>.Instance;
        relays =
        [
            new UdpPortRelay("control", simulatorAddress, controlSimulatorPort, controlListenPort, LogControl),
            new UdpPortRelay("blue", simulatorAddress, blueSimulatorPort, blueListenPort, LogBlueCommand),
        ];
    }

    internal int ControlListenPort => relays[0].ListenPort;
    internal int BlueListenPort => relays[1].ListenPort;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var relay in relays)
            relay.Start(stop.Token);
        logger.LogInformation(
            "MATCH simulation-control diagnostic relay listening on UDP {ControlPort}/{BluePort}, forwarding byte-for-byte to Simulator {SimulatorAddress}:{ControlPort}/{BluePort}",
            relays[0].ListenPort, relays[1].ListenPort,
            relays[0].SimulatorAddress, SimulatorControlPort, SimulatorBlueTeamPort);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        stop.Cancel();
        foreach (var relay in relays)
            relay.DisposeSockets();
        try
        {
            await Task.WhenAll(relays.Select(relay => relay.Completion)).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
        catch (SocketException) when (stop.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        stop.Dispose();
    }

    private void LogControl(byte[] packet, IPEndPoint client, DateTimeOffset receivedAtUtc, DateTimeOffset forwardedAtUtc)
    {
        try
        {
            var command = global::SimulatorCommand.Parser.ParseFrom(packet);
            logger.LogInformation(
                "MATCH_SIMULATION_CONTROL {Observation}",
                JsonSerializer.Serialize(new
                {
                    ReceivedAtUtc = receivedAtUtc,
                    ForwardedAtUtc = forwardedAtUtc,
                    Client = client.ToString(),
                    PacketBytes = packet.Length,
                    HasControl = command.Control is not null,
                    HasTeleportBall = command.Control?.TeleportBall is not null,
                    HasTeleportRobot = command.Control?.TeleportRobot.Count > 0,
                }));
        }
        catch (InvalidProtocolBufferException)
        {
            logger.LogWarning("MATCH_SIMULATION_CONTROL_INVALID receivedAtUtc={ReceivedAtUtc:o} client={Client} bytes={PacketBytes}", receivedAtUtc, client, packet.Length);
        }
    }

    private void LogBlueCommand(byte[] packet, IPEndPoint client, DateTimeOffset receivedAtUtc, DateTimeOffset forwardedAtUtc)
    {
        try
        {
            var command = global::RobotControl.Parser.ParseFrom(packet);
            var robots = command.RobotCommands.Select(robot => new
            {
                robot.Id,
                Move = robot.MoveCommand?.CommandCase switch
                {
                    global::RobotMoveCommand.CommandOneofCase.LocalVelocity => (object)new
                    {
                        Type = "local",
                        robot.MoveCommand.LocalVelocity.Forward,
                        robot.MoveCommand.LocalVelocity.Left,
                        robot.MoveCommand.LocalVelocity.Angular,
                    },
                    global::RobotMoveCommand.CommandOneofCase.GlobalVelocity => new
                    {
                        Type = "global",
                        robot.MoveCommand.GlobalVelocity.X,
                        robot.MoveCommand.GlobalVelocity.Y,
                        robot.MoveCommand.GlobalVelocity.Angular,
                    },
                    global::RobotMoveCommand.CommandOneofCase.WheelVelocity => new
                    {
                        Type = "wheel",
                        robot.MoveCommand.WheelVelocity.FrontRight,
                        robot.MoveCommand.WheelVelocity.BackRight,
                        robot.MoveCommand.WheelVelocity.BackLeft,
                        robot.MoveCommand.WheelVelocity.FrontLeft,
                    },
                    _ => null,
                },
                robot.KickSpeed,
                robot.KickAngle,
                robot.DribblerSpeed,
            }).ToArray();
            logger.LogInformation(
                "MATCH_BLUE_ROBOT_COMMAND {Observation}",
                JsonSerializer.Serialize(new
                {
                    ReceivedAtUtc = receivedAtUtc,
                    ForwardedAtUtc = forwardedAtUtc,
                    Client = client.ToString(),
                    PacketBytes = packet.Length,
                    Robots = robots,
                }));
        }
        catch (InvalidProtocolBufferException)
        {
            logger.LogWarning("MATCH_BLUE_ROBOT_COMMAND_INVALID receivedAtUtc={ReceivedAtUtc:o} client={Client} bytes={PacketBytes}", receivedAtUtc, client, packet.Length);
        }
    }

    private sealed class UdpPortRelay(
        string name,
        IPAddress simulatorAddress,
        int simulatorPort,
        int listenPort,
        Action<byte[], IPEndPoint, DateTimeOffset, DateTimeOffset> observe)
    {
        private readonly UdpClient incoming = new(new IPEndPoint(IPAddress.Any, listenPort));
        private readonly UdpClient upstream = new(new IPEndPoint(IPAddress.Any, 0));
        private IPEndPoint? lastClient;
        private Task requests = Task.CompletedTask;
        private Task responses = Task.CompletedTask;

        internal IPAddress SimulatorAddress => simulatorAddress;
        internal int ListenPort => ((IPEndPoint)incoming.Client.LocalEndPoint!).Port;
        internal Task Completion => Task.WhenAll(requests, responses);

        internal void Start(CancellationToken cancellationToken)
        {
            requests = ForwardRequestsAsync(cancellationToken);
            responses = ForwardResponsesAsync(cancellationToken);
        }

        private async Task ForwardRequestsAsync(CancellationToken cancellationToken)
        {
            var destination = new IPEndPoint(simulatorAddress, simulatorPort);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var request = await incoming.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    Volatile.Write(ref lastClient, request.RemoteEndPoint);
                    var receivedAtUtc = DateTimeOffset.UtcNow;
                    await upstream.SendAsync(request.Buffer, destination, cancellationToken).ConfigureAwait(false);
                    observe(request.Buffer, request.RemoteEndPoint, receivedAtUtc, DateTimeOffset.UtcNow);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
        }

        private async Task ForwardResponsesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var response = await upstream.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    var client = Volatile.Read(ref lastClient);
                    if (client is null)
                        continue;
                    var receivedAtUtc = DateTimeOffset.UtcNow;
                    await incoming.SendAsync(response.Buffer, client, cancellationToken).ConfigureAwait(false);
                    if (name == "blue" && response.RemoteEndPoint.Address.Equals(simulatorAddress) &&
                        response.RemoteEndPoint.Port == simulatorPort)
                        LogBlueResponse(response.Buffer, response.RemoteEndPoint, client, receivedAtUtc);
                    else if (name == "control" && response.RemoteEndPoint.Address.Equals(simulatorAddress) &&
                        response.RemoteEndPoint.Port == simulatorPort)
                        LogControlResponse(response.Buffer, response.RemoteEndPoint, client, receivedAtUtc);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
        }

        private static void LogBlueResponse(byte[] packet, IPEndPoint simulator, IPEndPoint client, DateTimeOffset receivedAtUtc)
        {
            try
            {
                var response = global::RobotControlResponse.Parser.ParseFrom(packet);
                Console.WriteLine("MATCH_BLUE_SIMULATOR_RESPONSE " + JsonSerializer.Serialize(new
                {
                    ReceivedAtUtc = receivedAtUtc,
                    Simulator = simulator.ToString(),
                    Client = client.ToString(),
                    PacketBytes = packet.Length,
                    Errors = response.Errors.Select(error => new
                    {
                        Code = error.HasCode ? error.Code : null,
                        Message = error.HasMessage ? error.Message : null,
                    }),
                    FeedbackRobotIds = response.Feedback.Select(feedback => feedback.Id).ToArray(),
                }));
            }
            catch (InvalidProtocolBufferException)
            {
                Console.WriteLine($"MATCH_BLUE_SIMULATOR_RESPONSE_INVALID receivedAtUtc={receivedAtUtc:O} simulator={simulator} bytes={packet.Length}");
            }
        }

        private static void LogControlResponse(byte[] packet, IPEndPoint simulator, IPEndPoint client, DateTimeOffset receivedAtUtc)
        {
            try
            {
                var response = global::SimulatorResponse.Parser.ParseFrom(packet);
                Console.WriteLine("MATCH_SIMULATOR_CONTROL_RESPONSE " + JsonSerializer.Serialize(new
                {
                    ReceivedAtUtc = receivedAtUtc,
                    Simulator = simulator.ToString(),
                    Client = client.ToString(),
                    PacketBytes = packet.Length,
                    Errors = response.Errors.Select(error => new
                    {
                        Code = error.HasCode ? error.Code : null,
                        Message = error.HasMessage ? error.Message : null,
                    }),
                }));
            }
            catch (InvalidProtocolBufferException)
            {
                Console.WriteLine($"MATCH_SIMULATOR_CONTROL_RESPONSE_INVALID receivedAtUtc={receivedAtUtc:O} simulator={simulator} bytes={packet.Length}");
            }
        }

        internal void DisposeSockets()
        {
            incoming.Dispose();
            upstream.Dispose();
        }
    }
}
