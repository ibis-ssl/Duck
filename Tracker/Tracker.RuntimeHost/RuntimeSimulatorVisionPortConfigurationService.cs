using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tracker.RuntimeHost;

/// <summary>Sends one-shot commands through the existing SSL Simulation Control UDP protocol.</summary>
internal sealed class RuntimeSimulatorVisionPortConfigurationService(
    IOptions<RuntimeSimulatorVisionPortOptions> options,
    RuntimeSimulatorVisionPortDiagnostics diagnostics,
    ILogger<RuntimeSimulatorVisionPortConfigurationService> logger) : BackgroundService
{
    internal static byte[] CreateCommand(int visionPort)
    {
        if (visionPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(visionPort));
        }

        return new global::SimulatorCommand
        {
            Config = new global::SimulatorConfig { VisionPort = (uint)visionPort },
        }.ToByteArray();
    }

    internal static byte[] CreateDetectionRobotCommand()
    {
        return new global::SimulatorCommand
        {
            Control = new global::SimulatorControl
            {
                TeleportRobot =
                {
                    new global::TeleportRobot
                    {
                        Id = new global::RobotId { Team = global::Team.Yellow, Id = 0 },
                        X = 1.2f,
                        Y = 0,
                        Orientation = MathF.PI,
                        VX = 0,
                        VY = 0,
                        VAngular = 0,
                        Present = true,
                    },
                },
            },
        }.ToByteArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuration = options.Value;
        if (!configuration.Enabled)
        {
            return;
        }

        if (!IPAddress.TryParse(configuration.ControlAddress, out var address) ||
            address.AddressFamily != AddressFamily.InterNetwork ||
            configuration.ControlPort is < 1 or > 65535 ||
            configuration.VisionPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("SimulatorVisionPort settings are invalid.");
        }

        var endpoint = new IPEndPoint(address, configuration.ControlPort);
        using var client = new UdpClient(AddressFamily.InterNetwork);
        await SendCommandOnceAsync(
            client, endpoint, CreateCommand(configuration.VisionPort),
            "Vision port configuration", configuration.VisionPort, stoppingToken);

        if (configuration.SpawnDetectionRobot && !stoppingToken.IsCancellationRequested)
        {
            await SendCommandOnceAsync(
                client, endpoint, CreateDetectionRobotCommand(),
                "Detection robot setup", configuration.VisionPort, stoppingToken);
        }
    }

    private async Task SendCommandOnceAsync(
        UdpClient client,
        IPEndPoint endpoint,
        byte[] command,
        string commandName,
        int visionPort,
        CancellationToken stoppingToken)
    {
        try
        {
            await client.SendAsync(command, endpoint, stoppingToken);
            if (commandName == "Vision port configuration")
            {
                diagnostics.RecordVisionPortCommandSent(visionPort);
            }
            else
            {
                diagnostics.RecordDetectionRobotCommandSent();
            }

            logger.LogInformation(
                "Sent Simulator {CommandName} once to {Endpoint}; this Simulator version has no positive success response",
                commandName, endpoint);
        }
        catch (SocketException exception)
        {
            logger.LogError(exception, "Failed to send Simulator {CommandName} to {Endpoint}", commandName, endpoint);
        }
    }
}
