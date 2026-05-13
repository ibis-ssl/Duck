using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;
using Tracker.Tests.Contracts;
using TrackerConnectionLib;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TRACKER-046 review blocker の multicast receive と安全な起動条件を production 実装前に固定する。
/// </summary>
public class TrackerMulticastReceiverReviewFixTddTests : IClassFixture<TrackerContractFixture>
{
    private const string OfficialMulticastAddress = "224.5.23.2";
    private const int OfficialTrackerPort = 10010;

    private readonly TrackerContractFixture fixture;
    private readonly TrackerCoordinatorTestFactory factory;

    public TrackerMulticastReceiverReviewFixTddTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
        factory = new TrackerCoordinatorTestFactory(fixture);
    }

    /// <summary>
    /// 何を確認しているか: official tracker multicast endpoint を設定した receiver は group join を契約として持つ。
    /// </summary>
    [Fact]
    public void OfficialMulticastEndpoint_ReceiverContractRequiresConfiguredGroupJoin()
    {
        var receiverSource = ReadRepositoryFile("TrackerConnectionLib/src/UdpTrackerReceiver.cs");
        var receiverConstructors = typeof(UdpTrackerReceiver<TrackerPacketAdapter>)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        var acceptsMulticastEndpoint = receiverConstructors.Any(constructor =>
            constructor.GetParameters().Any(IsMulticastEndpointParameter));
        var joinsMulticastGroup = receiverSource.Contains("JoinMulticastGroup", StringComparison.Ordinal);

        Assert.True(
            acceptsMulticastEndpoint && joinsMulticastGroup,
            "official tracker packets are multicast; UdpTrackerReceiver must accept the configured multicast endpoint and call JoinMulticastGroup instead of proving only loopback unicast receive.");
    }

    /// <summary>
    /// 何を確認しているか: runtime 登録は official multicast address と port を receiver へ渡し、unicast-only 証跡に退行しない。
    /// </summary>
    [Fact]
    public void RuntimeStartup_ConnectsReceiverToOfficialMulticastEndpoint()
    {
        var programSource = ReadRepositoryFile("Tracker/Tracker.Server/Program.cs");

        Assert.Contains("UdpTrackerReceiver<TrackerPacketAdapter>", programSource, StringComparison.Ordinal);
        Assert.Contains("MulticastAddress", programSource, StringComparison.Ordinal);
        Assert.Contains("Port", programSource, StringComparison.Ordinal);
        Assert.Contains(OfficialMulticastAddress, new TrackerPublisherOptions().MulticastAddress);
        Assert.Equal(OfficialTrackerPort, new TrackerPublisherOptions().Port);
    }

    /// <summary>
    /// 何を確認しているか: receiver は明示設定で有効化され、既定では危険な常時ネットワーク受信を始めない。
    /// </summary>
    [Fact]
    public void RuntimeStartup_DefaultsToNoLiveReceiveUntilExplicitlyEnabled()
    {
        var receiveGate = FindLiveReceiveEnableGate(typeof(TrackerOptions));

        Assert.NotNull(receiveGate);
        Assert.False(
            receiveGate.GetBooleanValue(new TrackerOptions()),
            $"live receiver enable gate '{receiveGate.Path}' must default to false so default startup does not bind and receive network traffic.");
    }

    /// <summary>
    /// 何を確認しているか: CaptureOff 中は受信 packet が manager へ届いても session sidecar を作成・追記しない。
    /// </summary>
    [Fact]
    public async Task CaptureOff_LiveReceiverReceivesPacketButDoesNotWriteSessionSidecar()
    {
        var captureDirectory = CreateCaptureDirectory("tracker-multicast-captureoff");
        var session = factory.CreateCaptureSession(captureDirectory, enabled: false);
        using var writer = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        var manager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-captureoff", "ibis-captureoff");
        using var recorder = new TrackerConnectionLibSnapshotRecorder(manager, writer);
        var port = GetFreeUdpPort();
        var handledCount = 0;
        using var receiver = new UdpTrackerReceiver<TrackerPacketAdapter>(
            port,
            new TrackerWrapperPacketDeserializer());
        receiver.PacketReceived += (packet, endpoint) =>
        {
            Interlocked.Increment(ref handledCount);
            manager.ProcessPacket(packet, endpoint, DateTimeOffset.UtcNow);
        };

        receiver.Start();
        try
        {
            await SendPacketAsync(port, CreatePacket("thirdparty-captureoff", "thirdparty-captureoff", 5801));
            var received = await WaitUntilAsync(
                () => Volatile.Read(ref handledCount) >= 1,
                TimeSpan.FromSeconds(1));
            writer.Flush();

            Assert.True(received, "test setup must prove the live receiver delivered a packet while CaptureOff.");
            Assert.Equal(0, writer.RecordCount);
            Assert.Empty(Directory.GetFiles(captureDirectory, TrackerPacketSnapshotLogReader.SidecarFileName, SearchOption.AllDirectories));
        }
        finally
        {
            await receiver.StopAsync();
        }
    }

    private TrackerWrapperPacket CreatePacket(string uuid, string sourceName, uint frameNumber)
    {
        var frame = fixture.CreateFrame(
            frameNumber: frameNumber,
            dataTimestampNs: 12_900_000_000 + frameNumber,
            balls:
            [
                fixture.CreateTrackedBall(trackId: 11, xMm: 140, yMm: -220),
            ],
            robots:
            [
                new TrackedRobotState { Team = TrackerTeam.Blue, RobotId = 4, XMm = -800, YMm = 430 },
            ],
            primaryBallTrackId: 11);
        return fixture.CreatePacketGenerator(sourceName, uuid).Generate(frame);
    }

    private static bool IsMulticastEndpointParameter(ParameterInfo parameter)
    {
        return parameter.ParameterType == typeof(IPAddress)
            || parameter.ParameterType == typeof(IPEndPoint)
            || parameter.Name?.Contains("multicast", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static LiveReceiveEnableGate? FindLiveReceiveEnableGate(Type optionsType)
    {
        foreach (var property in optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType == typeof(bool) &&
                IsReceiveGatePath($"{optionsType.Name}.{property.Name}", property.Name))
            {
                return new LiveReceiveEnableGate(
                    property.Name,
                    root => (bool)(property.GetValue(root) ?? false));
            }

            if (!IsOptionsContainer(property.PropertyType))
            {
                continue;
            }

            foreach (var nestedProperty in property.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var path = $"{property.Name}.{nestedProperty.Name}";
                if (nestedProperty.PropertyType == typeof(bool) &&
                    IsReceiveGatePath(path, nestedProperty.Name))
                {
                    return new LiveReceiveEnableGate(
                        path,
                        root =>
                        {
                            var nested = property.GetValue(root);
                            return nested is not null && (bool)(nestedProperty.GetValue(nested) ?? false);
                        });
                }
            }
        }

        return null;
    }

    private static bool IsOptionsContainer(Type type)
    {
        return type is { IsClass: true } &&
            type != typeof(string) &&
            type.Namespace == typeof(TrackerOptions).Namespace;
    }

    private static bool IsReceiveGatePath(string path, string propertyName)
    {
        return path.Contains("Receive", StringComparison.OrdinalIgnoreCase) &&
            (propertyName.Contains("Enabled", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Equals("ReceiveUdp", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Equals("ReceiverEnabled", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task SendPacketAsync(int port, IMessage packet)
    {
        using var udpClient = new UdpClient(AddressFamily.InterNetwork);
        var payload = packet.ToByteArray();
        await udpClient.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, port));
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        return condition();
    }

    private static int GetFreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Tracker/Tracker.Server/Program.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing Tracker/Tracker.Server/Program.cs was not found.");
    }

    private static string CreateCaptureDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record LiveReceiveEnableGate(string Path, Func<TrackerOptions, bool> GetBooleanValue);
}
