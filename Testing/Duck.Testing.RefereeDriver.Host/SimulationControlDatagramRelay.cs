using System.Diagnostics;
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
    private readonly object stopGate = new();
    private Task? stopTask;

    internal SimulationControlDatagramRelay(
        IPAddress simulatorAddress,
        int controlListenPort = SimulatorControlPort,
        int blueListenPort = SimulatorBlueTeamPort,
        int controlSimulatorPort = SimulatorControlPort,
        int blueSimulatorPort = SimulatorBlueTeamPort,
        ILogger<SimulationControlDatagramRelay>? logger = null,
        bool blueIngressDiagnosticEnabled = false,
        TimeSpan? blueIngressDiagnosticInterval = null)
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
            new UdpPortRelay("blue", simulatorAddress, blueSimulatorPort, blueListenPort, LogBlueCommand,
                blueIngressDiagnosticEnabled ? this.logger : null,
                blueIngressDiagnosticInterval ?? TimeSpan.FromSeconds(5)),
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
        var completion = GetStopTask();
        try
        {
            await completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private Task GetStopTask()
    {
        lock (stopGate)
            return stopTask ??= StopCoreAsync();
    }

    private async Task StopCoreAsync()
    {
        try
        {
            stop.Cancel();
            foreach (var relay in relays)
                relay.DisposeSockets();
            await Task.WhenAll(relays.Select(relay => relay.Completion)).ConfigureAwait(false);
        }
        finally
        {
            stop.Dispose();
        }
    }

    public ValueTask DisposeAsync() => new(GetStopTask());

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

    // Separate timer: an idle ReceiveAsync must not prevent zero-receipt observations.
    private sealed class BlueIngressDiagnostic(ILogger<SimulationControlDatagramRelay> logger, TimeSpan interval)
    {
        private const int MaxIntervals = 360;
        private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(30);
        private const int MaxInspectedPacketsPerInterval = 10000;
        private readonly object gate = new();
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private DateTimeOffset start = DateTimeOffset.UtcNow;
        private bool active = true;
        private long received, forwarded, failed, inspected, invalid;
        private int pendingAtStart, pending;
        private object? robot3, robot8;

        internal void RecordReceived(byte[] packet)
        {
            // Diagnostics must never mask the original SendAsync exception.
            try
            {
                lock (gate)
                {
                    if (!active) return;
                    received++;
                    pending++;
                    if (inspected >= MaxInspectedPacketsPerInterval) return;
                    inspected++;
                    try
                    {
                        foreach (var robot in global::RobotControl.Parser.ParseFrom(packet).RobotCommands)
                        {
                            if (robot.Id is not (3 or 8)) continue;
                            var move = robot.MoveCommand?.CommandCase switch
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
                            };
                            if (move is null) continue;
                            if (robot.Id == 3) robot3 = move; else robot8 = move;
                        }
                    }
                    catch (Exception) { invalid++; }
                }
            }
            catch (Exception) { }
        }

        internal void RecordResult(bool sent)
        {
            try
            {
                lock (gate)
                {
                    if (!active || pending == 0) return;
                    pending--;
                    if (sent) forwarded++; else failed++;
                }
            }
            catch (Exception) { }
        }

        internal async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var timer = new PeriodicTimer(interval);
                for (var i = 0; i < MaxIntervals; i++)
                {
                    await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
                    var final = i == MaxIntervals - 1 || elapsed.Elapsed >= MaxDuration;
                    Emit(final);
                    if (final) return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Emit(final: true);
            }
            catch (Exception) { }
        }

        private void Emit(bool final)
        {
            try
            {
                object observation;
                lock (gate)
                {
                    var end = DateTimeOffset.UtcNow;
                    observation = new
                    {
                        StartUtc = start, EndUtc = end,
                        Received = received,
                        // Send completions are counted in the interval in which they occur.
                        Forwarded = forwarded, ForwardFailed = failed,
                        PendingAtStart = pendingAtStart, PendingAtEnd = pending,
                        Inspected = inspected, Invalid = invalid, Uninspected = received - inspected,
                        Robot3HasMove = robot3 is not null, Robot3Move = robot3,
                        Robot8HasMove = robot8 is not null, Robot8Move = robot8,
                        Final = final,
                    };
                    start = end;
                    pendingAtStart = pending;
                    received = forwarded = failed = inspected = invalid = 0;
                    robot3 = robot8 = null;
                    if (final) active = false;
                }
                logger.LogInformation("MATCH_BLUE_INGRESS_INTERVAL {Observation}",
                    JsonSerializer.Serialize(observation, new JsonSerializerOptions
                    {
                        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
                    }));
            }
            catch (Exception) { }
        }
    }

    private sealed class UdpPortRelay(
        string name,
        IPAddress simulatorAddress,
        int simulatorPort,
        int listenPort,
        Action<byte[], IPEndPoint, DateTimeOffset, DateTimeOffset> observe,
        ILogger<SimulationControlDatagramRelay>? ingressLogger = null,
        TimeSpan ingressInterval = default)
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
            using var diagnosticStop = ingressLogger is null ? null : new CancellationTokenSource();
            var diagnostic = ingressLogger is null ? null : new BlueIngressDiagnostic(ingressLogger, ingressInterval);
            var diagnosticTask = diagnostic?.RunAsync(diagnosticStop!.Token);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var request = await incoming.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    Volatile.Write(ref lastClient, request.RemoteEndPoint);
                    var receivedAtUtc = DateTimeOffset.UtcNow;
                    if (diagnostic is null)
                    {
                        await upstream.SendAsync(request.Buffer, destination, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        diagnostic.RecordReceived(request.Buffer);
                        var forwarded = false;
                        try
                        {
                            await upstream.SendAsync(request.Buffer, destination, cancellationToken).ConfigureAwait(false);
                            forwarded = true;
                        }
                        finally
                        {
                            diagnostic.RecordResult(forwarded);
                        }
                    }
                    observe(request.Buffer, request.RemoteEndPoint, receivedAtUtc, DateTimeOffset.UtcNow);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                if (diagnosticStop is not null)
                {
                    diagnosticStop.Cancel();
                    await diagnosticTask!.ConfigureAwait(false);
                }
            }
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
