using System.Text.Json;
using System.Text.Json.Nodes;
using Duck.Testing.Diagnostics;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.CaptureReplay;
using Tracker.Diagnostics.Mcap;
using Tracker.RuntimeHost;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public sealed class RuntimeMatchRecordingTests
{
    [Fact]
    public void Writer_SeparatesReceiveSequenceFromEngineProcessingOrder()
    {
        using var directory = new TemporaryDirectory();
        var recordingPath = Path.Combine(directory.Path, "order.mcap");
        var receivedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var packetFromCameraTwo = TrackerContractTestData.CreateDetectionPacket(frameNumber: 20, cameraId: 2).ToByteArray();
        var packetFromCameraOne = TrackerContractTestData.CreateDetectionPacket(frameNumber: 21, cameraId: 1).ToByteArray();
        using var writer = new RecordingMcapWriter(
            recordingPath,
            "receive-order",
            receivedAt,
            CreateEffectiveConfiguration(),
            "runtime-initial");

        writer.WriteRawPacket(packetFromCameraTwo, receivedAt, "camera-two", sequence: 0);
        writer.WriteRawPacket(packetFromCameraOne, receivedAt, "camera-one", sequence: 1);
        writer.WritePacketContext(new RecordingMcapPacketContext(0, 2, "runtime-initial", true, true, null), receivedAt);
        writer.WritePacketContext(new RecordingMcapPacketContext(1, 1, "runtime-initial", true, true, null), receivedAt);
        writer.Complete(receivedAt.AddSeconds(1));

        var snapshot = RecordingMcapReader.Read(recordingPath);
        Assert.Equal([0UL, 1UL], snapshot.RawPackets.Select(packet => packet.Sequence));
        Assert.Equal([2L, 1L], snapshot.PacketContexts.OrderBy(context => context.PacketSequence)
            .Select(context => context.EngineUpdateOrder));
    }

    [Fact]
    public void LateDetection_DoesNotDropAppliedGeometryUpdateOrReset()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var geometryBeforeRecording = TrackerContractTestData.CreateGeometryPacket(fieldLength: 12000, fieldWidth: 9000).Geometry!;
        var geometryAppliedWithLatePacket = TrackerContractTestData.CreateGeometryPacket(fieldLength: 15000, fieldWidth: 11000).Geometry!;
        var packet = TrackerContractTestData.CreateDetectionPacket(frameNumber: 30, cameraId: 0);
        packet.Geometry = geometryAppliedWithLatePacket;
        using var recording = new RuntimeMatchRecordingSession(NullLogger<RuntimeMatchRecordingSession>.Instance);
        recording.Start(directory.Path, RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions()), startedAt, geometryBeforeRecording);
        var stamp = recording.WriteRaw(packet.ToByteArray(), startedAt.AddMilliseconds(1), "camera-zero");

        recording.WriteProcessed(
            stamp,
            packet,
            new TrackerUpdateResult
            {
                Diagnostics = new TrackerEngineDiagnostics { LatePacketDropCount = 1 },
                EmittedEvents = [new TrackerEvent { Kind = TrackerEventKind.GeometryReset }],
            },
            startedAt.AddMilliseconds(1));
        recording.Complete(startedAt.AddSeconds(1));

        var snapshot = RecordingMcapReader.Read(Assert.Single(Directory.GetFiles(directory.Path, "*.mcap")));
        Assert.False(Assert.Single(snapshot.PacketContexts).TrackingEnabled);
        Assert.Equal(["geometry-updated", "geometry-reset"], snapshot.ControlEvents.Select(item => item.EventKind));
        Assert.All(snapshot.ControlEvents, item => Assert.Null(item.PacketSequence));
        Assert.Equal(0UL, snapshot.ControlEvents[0].Payload.GetProperty("sourcePacketSequence").GetUInt64());
        Assert.Equal(Convert.ToBase64String(geometryBeforeRecording.ToByteArray()),
            snapshot.EffectiveConfigurations[0].InitialGeometryBase64);
    }

    [Fact]
    public void Start_CapturesGeometryAlreadyAppliedBeforeRecording()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var resolvedOptions = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions());
        using var recording = new RuntimeMatchRecordingSession(NullLogger<RuntimeMatchRecordingSession>.Instance);
        var packetBuffer = new RuntimeVisionPacketBuffer();
        var operationLoop = new RuntimeHostOperationLoop(packetBuffer, CreateCoordinator(new TrackerEngine(), resolvedOptions), resolvedOptions, recording);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var initialGeometry = TrackerContractTestData.CreateGeometryPacket(fieldLength: 12000, fieldWidth: 9000);

        Assert.True(Receive(initialGeometry, startedAt, packetBuffer, diagnostics, recording));
        Assert.True(operationLoop.ProcessLatestPacket());
        Assert.Equal(initialGeometry.Geometry, operationLoop.LastAppliedGeometry);
        recording.Start(directory.Path, resolvedOptions, startedAt.AddMilliseconds(1), operationLoop.LastAppliedGeometry);
        recording.Complete(startedAt.AddSeconds(1));

        var snapshot = RecordingMcapReader.Read(Assert.Single(Directory.GetFiles(directory.Path, "*.mcap")));
        Assert.Equal(Convert.ToBase64String(initialGeometry.Geometry!.ToByteArray()),
            snapshot.EffectiveConfigurations[0].InitialGeometryBase64);
        Assert.Empty(snapshot.RawPackets);
    }

    [Fact]
    public async Task Stop_WaitsForPacketAlreadyTakenByOperationLoop()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var resolvedOptions = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions());
        using var recording = new RuntimeMatchRecordingSession(NullLogger<RuntimeMatchRecordingSession>.Instance);
        var blockingEngine = new BlockingEngine();
        var packetBuffer = new RuntimeVisionPacketBuffer();
        var operationLoop = new RuntimeHostOperationLoop(packetBuffer, CreateCoordinator(blockingEngine, resolvedOptions), resolvedOptions, recording);
        var timeProvider = new TestTimeProvider(startedAt);
        var recordingHost = new RuntimeMatchRecordingHostedService(
            Options.Create(new RuntimeMatchRecordingOptions { DirectoryPath = directory.Path }),
            resolvedOptions,
            recording,
            timeProvider,
            operationLoop,
            NullLogger<RuntimeMatchRecordingHostedService>.Instance);
        await recordingHost.StartAsync(CancellationToken.None);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        Assert.True(Receive(TrackerContractTestData.CreateDetectionPacket(frameNumber: 40, cameraId: 0), startedAt,
            packetBuffer, diagnostics, recording));

        var processTask = Task.Run(operationLoop.ProcessLatestPacket);
        await blockingEngine.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopTask = Task.Run(async () =>
        {
            stopStarted.SetResult();
            await recordingHost.StopAsync(CancellationToken.None);
        });
        await stopStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        Assert.False(stopTask.IsCompleted);

        blockingEngine.Release.Set();
        Assert.True(await processTask.WaitAsync(TimeSpan.FromSeconds(5)));
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        var snapshot = RecordingMcapReader.Read(Assert.Single(Directory.GetFiles(directory.Path, "*.mcap")));
        Assert.Single(snapshot.RawPackets);
        Assert.True(Assert.Single(snapshot.PacketContexts).TrackingEnabled);
    }

    [Fact]
    public void CompletionAndReader_RejectDecodedInputWithoutProcessingResultOrReason()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var recordingPath = Path.Combine(directory.Path, "missing-result.mcap");
        using var writer = new RecordingMcapWriter(recordingPath, "missing-result", startedAt,
            CreateEffectiveConfiguration(), "runtime-initial");
        writer.WriteRawPacket(new SSL_WrapperPacket().ToByteArray(), startedAt, "camera-zero", 0);
        writer.WritePacketContext(new RecordingMcapPacketContext(0, null, "runtime-initial", true, false, null), startedAt);

        var completionFailure = Assert.Throws<InvalidDataException>(() => writer.Complete(startedAt.AddSeconds(1)));
        var readFailure = Assert.Throws<InvalidDataException>(() => RecordingMcapReader.ValidateCompletedPartial(recordingPath + ".partial"));
        Assert.Equal("録画 MCAP の packet context が raw・設定世代・処理結果と整合しません。", completionFailure.Message);
        Assert.Equal(completionFailure.Message, readFailure.Message);
        Assert.False(File.Exists(recordingPath));
    }

    [Fact]
    public void Completion_RejectsAppliedGeometryWhenBothGeometryEventsAreMissing()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var packet = TrackerContractTestData.CreateGeometryPacket(fieldLength: 12000, fieldWidth: 9000);
        using var writer = new RecordingMcapWriter(Path.Combine(directory.Path, "missing-geometry-events.mcap"),
            "missing-geometry-events", startedAt, CreateEffectiveConfiguration(), "runtime-initial");
        writer.WriteRawPacket(packet.ToByteArray(), startedAt, "camera-zero", 0);
        writer.WritePacketContext(new RecordingMcapPacketContext(0, 1, "runtime-initial", true, true, null), startedAt);

        var error = Assert.Throws<InvalidDataException>(() => writer.Complete(startedAt.AddSeconds(1)));

        Assert.Contains("geometry update", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(directory.Path, "missing-geometry-events.mcap")));
    }

    [Fact]
    public async Task Start_DrainsPreexistingPacketBeforeOpeningRecordingBoundary()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var resolvedOptions = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions());
        using var recording = new RuntimeMatchRecordingSession(NullLogger<RuntimeMatchRecordingSession>.Instance);
        var buffer = new RuntimeVisionPacketBuffer();
        var operationLoop = new RuntimeHostOperationLoop(buffer, CreateCoordinator(new TrackerEngine(), resolvedOptions), resolvedOptions, recording);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var preexistingGeometry = TrackerContractTestData.CreateGeometryPacket(fieldLength: 12000, fieldWidth: 9000);
        Assert.True(Receive(preexistingGeometry, startedAt, buffer, diagnostics, recording));
        var host = new RuntimeMatchRecordingHostedService(
            Options.Create(new RuntimeMatchRecordingOptions { DirectoryPath = directory.Path }), resolvedOptions, recording,
            new TestTimeProvider(startedAt.AddSeconds(1)), operationLoop, NullLogger<RuntimeMatchRecordingHostedService>.Instance);

        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);

        var snapshot = RecordingMcapReader.Read(Assert.Single(Directory.GetFiles(directory.Path, "*.mcap")));
        Assert.Empty(snapshot.RawPackets);
        Assert.Equal(Convert.ToBase64String(preexistingGeometry.Geometry!.ToByteArray()),
            snapshot.EffectiveConfigurations[0].InitialGeometryBase64);
    }

    [Fact]
    public async Task Stop_WaitsForRawAdmissionAndBufferStoreToShareBoundary()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var resolvedOptions = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions());
        using var recording = new RuntimeMatchRecordingSession(NullLogger<RuntimeMatchRecordingSession>.Instance);
        var buffer = new RuntimeVisionPacketBuffer();
        var operationLoop = new RuntimeHostOperationLoop(buffer, CreateCoordinator(new RecordingEngine(), resolvedOptions), resolvedOptions, recording);
        var timeProvider = new TestTimeProvider(startedAt);
        var host = new RuntimeMatchRecordingHostedService(
            Options.Create(new RuntimeMatchRecordingOptions { DirectoryPath = directory.Path }), resolvedOptions, recording,
            timeProvider, operationLoop, NullLogger<RuntimeMatchRecordingHostedService>.Instance);
        await host.StartAsync(CancellationToken.None);
        var packet = TrackerContractTestData.CreateDetectionPacket(frameNumber: 80, cameraId: 0);
        var admitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishAdmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var admissionTask = Task.Run(() => recording.RunAtAdmissionBoundary(() =>
        {
            var stamp = recording.WriteRaw(packet.ToByteArray(), startedAt.AddMilliseconds(1100), "camera-zero");
            admitted.SetResult();
            finishAdmission.Task.GetAwaiter().GetResult();
            buffer.StorePacket(packet, startedAt.AddMilliseconds(1100), stamp);
        }));
        await admitted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var stopTask = Task.Run(() => host.StopAsync(CancellationToken.None));
        await Task.Delay(50);
        Assert.False(stopTask.IsCompleted);
        timeProvider.SetUtcNow(startedAt.AddSeconds(2));
        finishAdmission.SetResult();
        await admissionTask.WaitAsync(TimeSpan.FromSeconds(5));
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        var snapshot = RecordingMcapReader.Read(Assert.Single(Directory.GetFiles(directory.Path, "*.mcap")));
        Assert.Single(snapshot.RawPackets);
        Assert.True(Assert.Single(snapshot.PacketContexts).TrackingEnabled);
    }

    [Fact]
    public void Replay_AppliesProfileSwitchAndRejectsOldGenerationReference()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var fastConfiguration = CreateConfiguration("fast");
        using var writer = new RecordingMcapWriter(Path.Combine(directory.Path, "profile-switch.mcap"), "profile-switch",
            startedAt, CreateEffectiveConfiguration(), "runtime-initial");
        writer.WriteEffectiveConfiguration("runtime-fast", fastConfiguration, startedAt.AddMilliseconds(1));
        var first = TrackerContractTestData.CreateDetectionPacket(frameNumber: 90, cameraId: 0,
            balls: [TrackerContractTestData.CreateBall(x: 100)], captureTimeSeconds: 1.0);
        var second = TrackerContractTestData.CreateDetectionPacket(frameNumber: 91, cameraId: 0,
            balls: [TrackerContractTestData.CreateBall(x: 200)], captureTimeSeconds: 2.0);
        var third = TrackerContractTestData.CreateDetectionPacket(frameNumber: 92, cameraId: 0,
            balls: [TrackerContractTestData.CreateBall(x: 300)], captureTimeSeconds: 3.0);
        writer.WriteRawPacket(first.ToByteArray(), startedAt.AddMilliseconds(10), "camera-zero", 0);
        writer.WritePacketContext(new RecordingMcapPacketContext(0, 1, "runtime-initial", true, true, null), startedAt.AddMilliseconds(10));
        writer.WriteControlEvent(new RecordingMcapControlEvent(2, "profile-switch-applied", null, "runtime-fast",
            JsonSerializer.SerializeToElement(new { profileName = "fast" })), startedAt.AddMilliseconds(11));
        writer.WriteRawPacket(second.ToByteArray(), startedAt.AddMilliseconds(12), "camera-zero", 1);
        writer.WritePacketContext(new RecordingMcapPacketContext(1, 3, "runtime-fast", true, true, null), startedAt.AddMilliseconds(12));
        writer.WriteRawPacket(third.ToByteArray(), startedAt.AddMilliseconds(500), "camera-zero", 2);
        writer.WritePacketContext(new RecordingMcapPacketContext(2, 4, "runtime-fast", true, true, null), startedAt.AddMilliseconds(500));
        writer.Complete(startedAt.AddSeconds(1));
        var snapshot = RecordingMcapReader.Read(Path.Combine(directory.Path, "profile-switch.mcap"));

        Assert.Equal(1, CaptureReplayRunner.Run(snapshot, [], 10, 16).CommittedFrameCount);

        var oldGenerationReference = snapshot with
        {
            PacketContexts = snapshot.PacketContexts.Select(context => context.PacketSequence == 1
                ? context with { ConfigurationGenerationId = "runtime-initial" }
                : context).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => CaptureReplayRunner.Run(oldGenerationReference, [], 10, 16));
    }

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
        var movedDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "moved")).FullName;
        var movedPath = Path.Combine(movedDirectory, Path.GetFileName(path));
        File.Move(path, movedPath);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.mcap"));
        var snapshot = RecordingMcapReader.Read(movedPath);
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

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CaptureReplayCommand.Run(
            ["--capture", movedPath, "--expect", "packets==3"],
            stdout,
            stderr);
        Assert.Equal(0, exitCode);
        Assert.Empty(stderr.ToString());
        Assert.Contains("settingsSource=recording generations=1", stdout.ToString());
        Assert.Contains("profile=default", stdout.ToString());
        Assert.Contains("packets=3 detections=0 geometries=2", stdout.ToString());
        Assert.Contains("expect packets==3: actual=3 result=ok", stdout.ToString());

        var missingAreaState = JsonNode.Parse(snapshot.EffectiveConfigurations[0].Configuration.GetRawText())!.AsObject();
        missingAreaState.Remove("AreaSelection");
        using var missingAreaDocument = JsonDocument.Parse(missingAreaState.ToJsonString());
        var missingSettings = snapshot with
        {
            EffectiveConfigurations =
            [
                snapshot.EffectiveConfigurations[0] with
                {
                    Configuration = missingAreaDocument.RootElement.Clone(),
                },
            ],
        };
        var settingsFailure = Assert.Throws<InvalidDataException>(() => CaptureReplayRunner.Run(
            missingSettings,
            [],
            maxDetails: 40,
            maxDetailRobots: 16));
        Assert.Equal("Recording MCAP effective tracker settings are missing required field 'AreaSelection'.", settingsFailure.Message);

        var missingGeometryEvents = snapshot with
        {
            ControlEvents = snapshot.ControlEvents.Where(item =>
                !(item.EngineUpdateOrder == 3 && item.EventKind is "geometry-updated" or "geometry-reset")).ToArray(),
        };
        var geometryFailure = Assert.Throws<InvalidDataException>(() => CaptureReplayRunner.Run(
            missingGeometryEvents,
            [],
            maxDetails: 40,
            maxDetailRobots: 16));
        Assert.Equal("Recording MCAP is missing geometry update for raw sequence 3.", geometryFailure.Message);
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

    private static TrackerCoordinator CreateCoordinator(ITrackerEngine engine, TrackerRuntimeResolvedOptions resolvedOptions) =>
        new(
            engine,
            new TrackerPacketGenerator("runtime-source", "runtime-uuid"),
            resolvedOptions,
            new TrackedSnapshotStore(),
            new RecordingPublisher(),
            []);

    private static JsonElement CreateEffectiveConfiguration()
    {
        var settings = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions()).EngineSettings;
        var configuration = JsonNode.Parse(JsonSerializer.Serialize(settings))!.AsObject();
        configuration["AreaSelection"] = new JsonObject
        {
            ["Enabled"] = false,
            ["Scope"] = "all-field",
            ["CoordinateSystem"] = "ssl-field-center",
            ["Units"] = "mm",
        };
        using var document = JsonDocument.Parse(configuration.ToJsonString());
        return document.RootElement.Clone();
    }

    private static JsonElement CreateConfiguration(string profileName)
    {
        var settings = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions()).EngineSettings;
        var configuration = JsonNode.Parse(JsonSerializer.Serialize(settings))!.AsObject();
        configuration["ProfileName"] = profileName;
        configuration["AreaSelection"] = new JsonObject
        {
            ["Enabled"] = false,
            ["Scope"] = "all-field",
            ["CoordinateSystem"] = "ssl-field-center",
            ["Units"] = "mm",
        };
        using var document = JsonDocument.Parse(configuration.ToJsonString());
        return document.RootElement.Clone();
    }

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

    private sealed class BlockingEngine : ITrackerEngine
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ManualResetEventSlim Release { get; } = new(false);

        public TrackerUpdateResult Update(
            SSL_WrapperPacket? packet,
            TrackerEngineSettings settings,
            TrackerProfileSwitchRequest? profileSwitchRequest = null)
        {
            Entered.SetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Test engine was not released before its deadline.");
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
