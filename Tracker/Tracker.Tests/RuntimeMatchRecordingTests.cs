using System.Text.Json;
using System.Text.Json.Nodes;
using Duck.Testing.Diagnostics;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.Diagnostics.Mcap;
using Tracker.RuntimeHost;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public sealed class RuntimeMatchRecordingTests
{
    [Fact]
    public async Task DisabledTracker_DoesNotStartMatchRecording()
    {
        using var directory = new TemporaryDirectory();
        var resolvedOptions = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions { Enabled = false });
        using var recording = new RuntimeMatchRecordingSession(NullLogger<RuntimeMatchRecordingSession>.Instance);
        var coordinator = new TrackerCoordinator(
            new TrackerEngine(),
            new TrackerPacketGenerator("runtime-source", "runtime-uuid"),
            resolvedOptions,
            new TrackedSnapshotStore(),
            new RecordingPublisher(),
            []);
        var operationLoop = new RuntimeHostOperationLoop(new RuntimeVisionPacketBuffer(), coordinator, resolvedOptions, recording);
        var recordingHost = new RuntimeMatchRecordingHostedService(
            Options.Create(new RuntimeMatchRecordingOptions { DirectoryPath = directory.Path }),
            resolvedOptions,
            recording,
            new TestTimeProvider(DateTimeOffset.UtcNow),
            operationLoop,
            NullLogger<RuntimeMatchRecordingHostedService>.Instance);

        await recordingHost.StartAsync(CancellationToken.None);
        await recordingHost.StopAsync(CancellationToken.None);

        Assert.False(recording.IsRecording);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task SameCameraPackets_RecordBothButOnlyLatestIsTracked_AndMoveAsOneMcap()
    {
        using var directory = new TemporaryDirectory();
        var captureDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "recording")).FullName;
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var resolvedOptions = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions());
        using var recording = new RuntimeMatchRecordingSession(NullLogger<RuntimeMatchRecordingSession>.Instance);

        var engine = new RecordingEngine();
        var coordinator = new TrackerCoordinator(
            engine,
            new TrackerPacketGenerator("runtime-source", "runtime-uuid"),
            resolvedOptions,
            new TrackedSnapshotStore(),
            new RecordingPublisher(),
            []);
        var packetBuffer = new RuntimeVisionPacketBuffer();
        var operationLoop = new RuntimeHostOperationLoop(packetBuffer, coordinator, resolvedOptions, recording);
        var timeProvider = new TestTimeProvider(startedAt);
        var recordingHost = new RuntimeMatchRecordingHostedService(
            Options.Create(new RuntimeMatchRecordingOptions { DirectoryPath = captureDirectory }),
            resolvedOptions,
            recording,
            timeProvider,
            operationLoop,
            NullLogger<RuntimeMatchRecordingHostedService>.Instance);
        await recordingHost.StartAsync(CancellationToken.None);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var first = TrackerContractTestData.CreateDetectionPacket(frameNumber: 10, cameraId: 2);
        var latest = TrackerContractTestData.CreateDetectionPacket(frameNumber: 11, cameraId: 2);
        var firstAt = startedAt.AddMilliseconds(1);
        var latestAt = startedAt.AddMilliseconds(2);

        Assert.True(Receive(first, firstAt, packetBuffer, diagnostics, recording));
        Assert.True(Receive(latest, latestAt, packetBuffer, diagnostics, recording));
        Assert.True(operationLoop.ProcessLatestPacket());
        timeProvider.SetUtcNow(startedAt.AddSeconds(1));
        await recordingHost.StopAsync(CancellationToken.None);

        var completedPath = Assert.Single(Directory.GetFiles(captureDirectory, "*.mcap"));
        var movedDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "moved")).FullName;
        var movedPath = Path.Combine(movedDirectory, Path.GetFileName(completedPath));
        File.Move(completedPath, movedPath);
        Assert.Empty(Directory.GetFiles(captureDirectory));
        Assert.Single(Directory.GetFiles(movedDirectory));

        var recordingSnapshot = RecordingMcapReader.Read(movedPath);
        Assert.Equal([0UL, 1UL], recordingSnapshot.RawPackets.Select(packet => packet.Sequence));
        Assert.Equal([10u, 11u], recordingSnapshot.RawPackets
            .Select(packet => SSL_WrapperPacket.Parser.ParseFrom(packet.Payload).Detection.FrameNumber));
        Assert.Equal([false, true], recordingSnapshot.PacketContexts
            .OrderBy(context => context.PacketSequence)
            .Select(context => context.TrackingEnabled));
        Assert.Equal("superseded-before-operation-loop", recordingSnapshot.PacketContexts[0].Reason);
        Assert.Equal([11u], engine.Packets.Select(packet => packet.Detection.FrameNumber));

        var effectiveConfiguration = Assert.Single(recordingSnapshot.EffectiveConfigurations).Configuration;
        var areaSelection = effectiveConfiguration.GetProperty("AreaSelection");
        Assert.False(areaSelection.GetProperty("Enabled").GetBoolean());
        Assert.Equal("all-field", areaSelection.GetProperty("Scope").GetString());
        Assert.Equal("ssl-field-center", areaSelection.GetProperty("CoordinateSystem").GetString());
        Assert.Equal("mm", areaSelection.GetProperty("Units").GetString());

        var configurationWithoutAreaState = JsonNode.Parse(effectiveConfiguration.GetRawText())!.AsObject();
        configurationWithoutAreaState.Remove("AreaSelection");
        using var invalidConfigurationDocument = JsonDocument.Parse(configurationWithoutAreaState.ToJsonString());
        Assert.Throws<InvalidDataException>(() => RecordingMcapSettingsValidator.Read(
            recordingSnapshot.EffectiveConfigurations[0] with
            {
                Configuration = invalidConfigurationDocument.RootElement.Clone(),
            }));
    }

    [Fact]
    public void SameTickGeometryThenEmptyPacket_RetainsRawButDoesNotApplyReplacedGeometry()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var resolvedOptions = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions());
        using var recording = new RuntimeMatchRecordingSession(NullLogger<RuntimeMatchRecordingSession>.Instance);
        recording.Start(directory.Path, resolvedOptions, startedAt);

        var coordinator = new TrackerCoordinator(
            new TrackerEngine(),
            new TrackerPacketGenerator("runtime-source", "runtime-uuid"),
            resolvedOptions,
            new TrackedSnapshotStore(),
            new RecordingPublisher(),
            []);
        var packetBuffer = new RuntimeVisionPacketBuffer();
        var operationLoop = new RuntimeHostOperationLoop(packetBuffer, coordinator, resolvedOptions, recording);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var geometryA = TrackerContractTestData.CreateGeometryPacket(fieldLength: 12000, fieldWidth: 9000);
        var geometryB = TrackerContractTestData.CreateGeometryPacket(fieldLength: 15000, fieldWidth: 11000);
        var geometryC = TrackerContractTestData.CreateGeometryPacket(fieldLength: 13000, fieldWidth: 10000);

        Assert.True(Receive(geometryA, startedAt.AddMilliseconds(1), packetBuffer, diagnostics, recording));
        Assert.True(operationLoop.ProcessLatestPacket());
        Assert.True(Receive(geometryB, startedAt.AddMilliseconds(2), packetBuffer, diagnostics, recording));
        Assert.True(Receive(new SSL_WrapperPacket(), startedAt.AddMilliseconds(3), packetBuffer, diagnostics, recording));
        Assert.True(operationLoop.ProcessLatestPacket());
        Assert.True(Receive(geometryC, startedAt.AddMilliseconds(4), packetBuffer, diagnostics, recording));
        Assert.True(operationLoop.ProcessLatestPacket());
        recording.Complete(startedAt.AddSeconds(1));

        var path = Assert.Single(Directory.GetFiles(directory.Path, "*.mcap"));
        var snapshot = RecordingMcapReader.Read(path);
        RecordingMcapReplayContractValidator.Validate(snapshot);
        var rawGeometryLengths = snapshot.RawPackets
            .Select(packet => SSL_WrapperPacket.Parser.ParseFrom(packet.Payload).Geometry?.Field?.FieldLength)
            .ToArray();
        Assert.Equal([12000, 15000, null, 13000], rawGeometryLengths);
        var contextsBySequence = snapshot.PacketContexts.ToDictionary(context => context.PacketSequence);
        Assert.True(contextsBySequence[0].TrackingEnabled);
        Assert.False(contextsBySequence[1].TrackingEnabled);
        Assert.Equal("superseded-before-operation-loop", contextsBySequence[1].Reason);
        Assert.True(contextsBySequence[2].TrackingEnabled);
        Assert.True(contextsBySequence[3].TrackingEnabled);
        Assert.Equal([1L, 3L], snapshot.ControlEvents
            .Where(item => item.EventKind == "geometry-updated")
            .Select(item => item.EngineUpdateOrder));
        Assert.Equal([3L], snapshot.ControlEvents
            .Where(item => item.EventKind == "geometry-reset")
            .Select(item => item.EngineUpdateOrder));
        Assert.Equal([12000, 13000], snapshot.ControlEvents
            .Where(item => item.EventKind == "geometry-updated")
            .Select(item => SSL_GeometryData.Parser.ParseFrom(
                Convert.FromBase64String(item.Payload.GetProperty("protobufBase64").GetString()!)).Field.FieldLength));
    }

    private static bool Receive(
        SSL_WrapperPacket packet,
        DateTimeOffset receivedAt,
        RuntimeVisionPacketBuffer packetBuffer,
        RuntimeVisionReceiverDiagnostics diagnostics,
        RuntimeMatchRecordingSession recording) =>
        RuntimeVisionReceiverService.ProcessDatagram(
            packet.ToByteArray(),
            receivedAt,
            packetBuffer,
            diagnostics,
            NullLogger.Instance,
            remoteEndpoint: "127.0.0.1:10020",
            recordingSession: recording);

    private sealed class RecordingEngine : ITrackerEngine
    {
        public List<SSL_WrapperPacket> Packets { get; } = [];

        public TrackerUpdateResult Update(
            SSL_WrapperPacket? packet,
            TrackerEngineSettings settings,
            TrackerProfileSwitchRequest? profileSwitchRequest = null)
        {
            if (packet is not null)
            {
                Packets.Add(packet.Clone());
            }

            return new TrackerUpdateResult();
        }
    }

    private sealed class RecordingPublisher : ITrackerPacketPublisher
    {
        public void ApplyConfiguration(TrackerPublisherOptions options)
        {
        }

        public void Publish(TrackerWrapperPacket packet)
        {
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"runtime-match-recording-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void SetUtcNow(DateTimeOffset value) => currentUtcNow = value;
    }
}
