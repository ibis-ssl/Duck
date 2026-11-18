using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace Tracker.Tests;

public sealed class RuntimeSimulatorVisionPortConfigurationTests
{
    [Fact]
    public void CreateCommandUsesExistingSimulatorConfigVisionPortField()
    {
        var command = global::SimulatorCommand.Parser.ParseFrom(
            Tracker.RuntimeHost.RuntimeSimulatorVisionPortConfigurationService.CreateCommand(10021));

        Assert.NotNull(command.Config);
        Assert.True(command.Config.HasVisionPort);
        Assert.Equal(10021u, command.Config.VisionPort);
        Assert.Null(command.Control);
    }

    [Fact]
    public async Task ServiceSendsVisionPortAndDetectionSetupCommandsOnce()
    {
        using var simulatorControl = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var controlPort = ((IPEndPoint)simulatorControl.Client.LocalEndPoint!).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var diagnostics = new Tracker.RuntimeHost.RuntimeSimulatorVisionPortDiagnostics();
        using var service = new Tracker.RuntimeHost.RuntimeSimulatorVisionPortConfigurationService(
            Options.Create(new Tracker.RuntimeHost.RuntimeSimulatorVisionPortOptions
            {
                Enabled = true,
                ControlPort = controlPort,
                VisionPort = 10021,
                SpawnDetectionRobot = true,
            }),
            diagnostics,
            NullLogger<Tracker.RuntimeHost.RuntimeSimulatorVisionPortConfigurationService>.Instance);

        await service.StartAsync(timeout.Token);
        var received = await simulatorControl.ReceiveAsync(timeout.Token);
        var command = global::SimulatorCommand.Parser.ParseFrom(received.Buffer);
        Assert.Equal(10021u, command.Config.VisionPort);

        var setupRequest = await simulatorControl.ReceiveAsync(timeout.Token);
        var setupCommand = global::SimulatorCommand.Parser.ParseFrom(setupRequest.Buffer);
        var robot = Assert.Single(setupCommand.Control.TeleportRobot);
        Assert.Equal(global::Team.Yellow, robot.Id.Team);
        Assert.True(robot.Present);
        Assert.True(diagnostics.Snapshot().VisionPortCommandSent);
        Assert.True(diagnostics.Snapshot().DetectionRobotCommandSent);
        await service.StopAsync(timeout.Token);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void CreateCommandRejectsInvalidVisionPorts(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Tracker.RuntimeHost.RuntimeSimulatorVisionPortConfigurationService.CreateCommand(port));
    }

    [Fact]
    public void DetectionRobotObservationCountsOnlyTheTargetRobotInDetectionFrames()
    {
        var diagnostics = new Tracker.RuntimeHost.RuntimeSimulatorVisionPortDiagnostics();
        var packet = new global::SSL_WrapperPacket
        {
            Detection = new global::SSL_DetectionFrame
            {
                RobotsYellow = { new global::SSL_DetectionRobot { RobotId = 0 } },
            },
        };

        Tracker.RuntimeHost.RuntimeVisionReceiverService.RecordDetectionRobotObservation(
            packet.ToByteArray(), diagnostics);

        Assert.Equal(1, diagnostics.Snapshot().DetectionRobotObservedCount);
    }
}
