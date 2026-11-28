using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Duck.Testing.Diagnostics;
using Fizzy.McapSharp;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.CaptureReplay;
using Tracker.Core;
using Tracker.Diagnostics.Mcap;
using Tracker.RuntimeHost;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// Duck #177: independent normal-review reproductions from candidate 6159543.
/// These are regression expectations, intentionally red on that exact candidate.
/// </summary>
public sealed class RuntimeMatchRecordingReviewRegressionTests
{
    private static readonly DateTimeOffset StartTime = new(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    // NR-177-02: reproduce the actual late detection + applied geometry, and remove
    // both control records at the third engine update, retaining the original raw/context.
    [Fact]
    public async Task NR17702_CompletionRejectsLateGeometryWithBothEventsOmitted()
    {
        using var fixture = new RuntimeFixture();
        var original = await CaptureActualLateGeometry(fixture);
        var badPath = Path.Combine(fixture.DirectoryPath, "missing-both.mcap");
        var configuration = Assert.Single(original.EffectiveConfigurations);
        using var writer = new RecordingMcapWriter(badPath, "omitted-events",
            original.Manifest.StartedAtUtc, configuration.Configuration, configuration.GenerationId);

        foreach (var raw in original.RawPackets)
        {
            writer.WriteRawPacket(raw.Payload, raw.ReceivedAtUtc, raw.Source, raw.Sequence);
        }
        foreach (var context in original.PacketContexts)
        {
            writer.WritePacketContext(context,
                original.RawPackets.Single(raw => raw.Sequence == context.PacketSequence).ReceivedAtUtc);
        }
        foreach (var control in original.ControlEvents.Where(item => item.EngineUpdateOrder != 3))
        {
            writer.WriteControlEvent(control, original.RawPackets[0].ReceivedAtUtc);
        }

        Assert.Throws<InvalidDataException>(() => writer.Complete(original.Manifest.StoppedAtUtc));
        Assert.False(File.Exists(badPath));
    }

    [Fact]
    public async Task NR17702_ReaderRejectsCompleteMcapWithBothLateGeometryEventsOmitted()
    {
        using var fixture = new RuntimeFixture();
        var original = await CaptureActualLateGeometry(fixture);
        var badPath = Path.Combine(fixture.DirectoryPath, "missing-both-reader.mcap");
        WriteUncheckedMcap(badPath, original, omitLateGeometryEvents: true);

        // An otherwise identical MCAP produced by the same test helper must be valid.
        var intactPath = Path.Combine(fixture.DirectoryPath, "intact-reader.mcap");
        WriteUncheckedMcap(intactPath, original, omitLateGeometryEvents: false);
        Assert.Equal(3, RecordingMcapReader.Read(intactPath).RawPackets.Count);
        Assert.Throws<InvalidDataException>(() => RecordingMcapReader.Read(badPath));
    }

    [Fact]
    public async Task NR17702_CliRejectsCompleteMcapWithBothLateGeometryEventsOmitted()
    {
        using var fixture = new RuntimeFixture();
        var original = await CaptureActualLateGeometry(fixture);
        var badPath = Path.Combine(fixture.DirectoryPath, "missing-both-cli.mcap");
        WriteUncheckedMcap(badPath, original, omitLateGeometryEvents: true);
        var intactPath = Path.Combine(fixture.DirectoryPath, "intact-cli.mcap");
        WriteUncheckedMcap(intactPath, original, omitLateGeometryEvents: false);

        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, CaptureReplayCommand.Run(["--capture", intactPath], output, error));
        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        int? exitCode = null;
        var failure = Record.Exception(() =>
        {
            exitCode = CaptureReplayCommand.Run(["--capture", badPath], output, error);
        });
        // The command may reject via InvalidDataException or a nonzero exit status,
        // but must not report a successful recomputation of the incomplete recording.
        Assert.True(failure is InvalidDataException || (failure is null && exitCode != 0),
            $"Incomplete MCAP accepted: exit={exitCode}, output={output}, error={error}, failure={failure}");
    }

    // NR-177-03: a datagram arrives while the start boundary is draining shape A.
    // The receive timestamp (-1 ms) is before the eventual start timestamp, as in
    // the review probe; the gate delays its admission until after StartTime.
    [Fact]
    public async Task NR17703_ArrivalDuringStartDrainDoesNotInvalidateRecording()
    {
        using var fixture = new RuntimeFixture();
        Assert.True(fixture.Receive(Geometry(9000), -100));
        fixture.Engine.Block = true;
        var starting = Task.Run(() => fixture.Host.StartAsync(CancellationToken.None));
        Task<bool>? arriving = null;
        try
        {
            Assert.True(fixture.Engine.Entered.Wait(Deadline), "Start drain did not enter the engine.");
            var receiveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            arriving = Task.Run(() =>
            {
                receiveStarted.SetResult();
                return fixture.Receive(Geometry(11000), -1);
            });
            await receiveStarted.Task.WaitAsync(Deadline);
            // Same bounded overlap used by the independent review. The timestamp
            // is fixed before the gate even if task scheduling differs.
            await Task.Delay(100);
        }
        finally
        {
            fixture.Engine.Release.Set();
        }

        await starting.WaitAsync(Deadline);
        Assert.True(await arriving!.WaitAsync(Deadline));
        _ = fixture.Loop.ProcessLatestPacket();
        fixture.Recording.Complete(StartTime.AddSeconds(1));

        // The recording may exclude pre-boundary input or keep it under a
        // consistent boundary, but must not disrupt A -> B live processing.
        Assert.Equal([9000, 11000], fixture.Engine.AppliedGeometryWidths);
        var completed = Directory.GetFiles(fixture.DirectoryPath, "*.mcap");
        Assert.True(completed.Length == 1,
            $"Expected a completed recording but got {completed.Length}; recording logs: {string.Join(" | ", fixture.RecordingLog.Messages)}");
        _ = RecordingMcapReader.Read(Assert.Single(completed));
    }

    [Fact]
    public async Task NR17703_StopClosesRecordingEvenIfEngineThrowsWhileDraining()
    {
        using var fixture = new RuntimeFixture();
        await fixture.Host.StartAsync(CancellationToken.None);
        Assert.True(fixture.Receive(Detection(1, 1), 1));
        fixture.Engine.ThrowNext = true;

        await fixture.Host.StopAsync(CancellationToken.None).WaitAsync(Deadline);
        var stillOpen = fixture.Recording.IsRecording;

        // As in the original review, prove normal processing subsequently works.
        Assert.True(fixture.Receive(Detection(2, 2), 2));
        Assert.True(fixture.Loop.ProcessLatestPacket());
        Assert.Equal([2u], fixture.Engine.ProcessedDetectionFrames);
        Assert.False(stillOpen, "Recording was still open when StopAsync returned after the engine exception.");
        Assert.False(fixture.Recording.IsRecording);
    }

    // NR-177-05: exact values/order from the review's switch-merge-window probe.
    [Fact]
    public void NR17705_CliMatchesLiveSwitchWhenMergeWindowChanges()
    {
        using var fixture = new RuntimeFixture();
        var oldSettings = new TrackerEngineSettings
        {
            ProfileName = "previous", ReorderWindowNs = 1_000_000_000, MergeWindowNs = 0,
            GeometryResetFieldLengthThresholdMm = 500, GeometryResetFieldWidthThresholdMm = 500,
        };
        var newSettings = new TrackerEngineSettings
        {
            ProfileName = "next", ReorderWindowNs = 0, MergeWindowNs = 100_000_000,
            GeometryResetFieldLengthThresholdMm = 500, GeometryResetFieldWidthThresholdMm = 500,
        };
        var early = Detection(1, 1);
        var close = Detection(2, 1.05);
        var later = Detection(3, 1.5);

        // Live TrackerEngine uses the *previous* settings when clearing the
        // pre-switch pending state; the switch request supplies the new settings.
        var live = new TrackerEngine();
        var first = live.Update(early, oldSettings);
        var switched = live.Update(null, oldSettings, new TrackerProfileSwitchRequest
        {
            RequestVersion = 1, ProfileName = newSettings.ProfileName, ResolvedBaseSettings = newSettings,
        });
        var next = live.Update(close, newSettings);
        var last = live.Update(later, newSettings);
        Assert.Equal(0, next.Diagnostics.LatePacketDropCount);
        var liveFrames = first.CommittedFrames.Count + switched.CommittedFrames.Count
            + next.CommittedFrames.Count + last.CommittedFrames.Count;
        Assert.Equal(1, liveFrames);

        var path = Path.Combine(fixture.DirectoryPath, "merge-switch.mcap");
        using (var writer = new RecordingMcapWriter(path, "merge-switch", StartTime,
            Configuration(oldSettings), "G0"))
        {
            writer.WriteRawPacket(early.ToByteArray(), StartTime.AddMilliseconds(1), "source", 0);
            writer.WritePacketContext(new RecordingMcapPacketContext(0, 1, "G0", true, true, null),
                StartTime.AddMilliseconds(1));
            writer.WriteEffectiveConfiguration("G1", Configuration(newSettings), StartTime.AddMilliseconds(2));
            writer.WriteControlEvent(new RecordingMcapControlEvent(2, "profile-switch-applied", null, "G1",
                JsonSerializer.SerializeToElement(new { profileName = newSettings.ProfileName })),
                StartTime.AddMilliseconds(2));
            writer.WriteRawPacket(close.ToByteArray(), StartTime.AddMilliseconds(3), "source", 1);
            writer.WritePacketContext(new RecordingMcapPacketContext(1, 3, "G1", true, true, null),
                StartTime.AddMilliseconds(3));
            writer.WriteRawPacket(later.ToByteArray(), StartTime.AddMilliseconds(4), "source", 2);
            writer.WritePacketContext(new RecordingMcapPacketContext(2, 4, "G1", true, true, null),
                StartTime.AddMilliseconds(4));
            writer.Complete(StartTime.AddSeconds(1));
        }

        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, CaptureReplayCommand.Run(["--capture", path], output, error));
        Assert.Contains("settingsSource=recording generations=2", output.ToString(), StringComparison.Ordinal);
        Assert.True(output.ToString().Contains($"committedFrames={liveFrames}", StringComparison.Ordinal),
            $"Live engine committedFrames={liveFrames}; CLI output: {output}");
    }

    private static async Task<RecordingMcapSnapshot> CaptureActualLateGeometry(RuntimeFixture fixture)
    {
        await fixture.Host.StartAsync(CancellationToken.None);
        Assert.True(fixture.Receive(Geometry(9000), 1));
        Assert.True(fixture.Loop.ProcessLatestPacket());
        Assert.True(fixture.Receive(Detection(10, 10), 2));
        Assert.True(fixture.Loop.ProcessLatestPacket());
        var late = Detection(2, 2);
        late.Geometry = Geometry(11000).Geometry;
        Assert.True(fixture.Receive(late, 3));
        Assert.True(fixture.Loop.ProcessLatestPacket());
        Assert.Equal(1, fixture.Engine.LastResult!.Diagnostics.LatePacketDropCount);
        Assert.Single(fixture.Engine.LastResult.EmittedEvents.Where(item => item.Kind == TrackerEventKind.GeometryReset));
        fixture.Recording.Complete(StartTime.AddSeconds(1));

        var original = RecordingMcapReader.Read(Assert.Single(Directory.GetFiles(fixture.DirectoryPath, "*.mcap")));
        Assert.Equal([0UL, 1UL, 2UL], original.RawPackets.Select(raw => raw.Sequence));
        var lateContext = Assert.Single(original.PacketContexts.Where(item => item.PacketSequence == 2));
        Assert.Equal("tracker-late-input-dropped", lateContext.Reason);
        Assert.False(lateContext.TrackingEnabled);
        Assert.Equal(["geometry-updated", "geometry-reset"],
            original.ControlEvents.Where(item => item.EngineUpdateOrder == 3).Select(item => item.EventKind));
        return original;
    }

    // A structurally complete, CRC-valid test fixture independent of production
    // RecordingMcapWriter's rejection. Same payloads as the actual recording.
    // This lets Reader and CLI negative tests continue after the writer is fixed.
    private static void WriteUncheckedMcap(string path, RecordingMcapSnapshot source, bool omitLateGeometryEvents)
    {
        using var writer = new McapWriter(File.Create(path), new McapWriterOptions
        {
            UseChunks = false, EmitSummaryRecords = true,
            CalculateDataSectionCrc = true, CalculateSummarySectionCrc = true,
        });
        var manifest = writer.RegisterChannel(RecordingMcapWriter.ManifestTopic, "json", 0, null);
        var raw = writer.RegisterChannel(RecordingMcapWriter.RawVisionTopic, "json", 0, null);
        var config = writer.RegisterChannel(RecordingMcapWriter.EffectiveConfigurationTopic, "json", 0, null);
        var context = writer.RegisterChannel(RecordingMcapWriter.PacketContextTopic, "json", 0, null);
        var control = writer.RegisterChannel(RecordingMcapWriter.ControlEventTopic, "json", 0, null);
        uint sequence = 0;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        void Write<T>(ushort channel, T value, DateTimeOffset time)
        {
            var nanoseconds = checked((ulong)((time - DateTimeOffset.UnixEpoch).Ticks * 100));
            var header = new McapMessageHeader(channel, sequence++, nanoseconds, nanoseconds);
            writer.WriteToKnownChannel(in header, JsonSerializer.SerializeToUtf8Bytes(value, json));
        }

        Write(manifest, source.Manifest with { StoppedAtUtc = null }, source.Manifest.StartedAtUtc);
        foreach (var configuration in source.EffectiveConfigurations)
        {
            Write(config, configuration, configuration.EffectiveAtUtc);
        }
        foreach (var packet in source.RawPackets)
        {
            Write(raw, packet, packet.ReceivedAtUtc);
        }
        foreach (var packetContext in source.PacketContexts)
        {
            Write(context, packetContext,
                source.RawPackets.Single(packet => packet.Sequence == packetContext.PacketSequence).ReceivedAtUtc);
        }
        foreach (var item in source.ControlEvents.Where(item => !omitLateGeometryEvents || item.EngineUpdateOrder != 3))
        {
            Write(control, item, source.RawPackets[0].ReceivedAtUtc);
        }
        Write(manifest, source.Manifest, source.Manifest.StoppedAtUtc!.Value);
        writer.Complete();
    }

    private static JsonElement Configuration(TrackerEngineSettings settings)
    {
        var configuration = JsonNode.Parse(JsonSerializer.Serialize(settings))!.AsObject();
        configuration["AreaSelection"] = JsonSerializer.SerializeToNode(new
        {
            Enabled = false, Scope = "all-field", CoordinateSystem = "ssl-field-center", Units = "mm",
        });
        return JsonSerializer.SerializeToElement(configuration);
    }

    private static SSL_WrapperPacket Detection(uint frame, double seconds) =>
        TrackerContractTestData.CreateDetectionPacket(frame, 0, captureTimeSeconds: seconds, sentTimeSeconds: seconds);

    private static SSL_WrapperPacket Geometry(int fieldWidth) =>
        TrackerContractTestData.CreateGeometryPacket(fieldLength: 12000, fieldWidth: fieldWidth);

    private sealed class RuntimeFixture : IDisposable
    {
        private readonly RuntimeVisionPacketBuffer buffer = new();
        private readonly RuntimeVisionReceiverDiagnostics diagnostics = new(new ServiceReceiveDiagnostics());

        public RuntimeFixture()
        {
            DirectoryPath = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),
                $"duck177-review-{Guid.NewGuid():N}")).FullName;
            var resolved = RuntimeTrackerConfigurationResolver.Resolve(new RuntimeTrackerOptions
            {
                Profiles = new()
                {
                    ["default"] = new()
                    {
                        Engine = new()
                        {
                            ReorderWindowNs = 0, MergeWindowNs = 0,
                            GeometryResetFieldLengthThresholdMm = 500, GeometryResetFieldWidthThresholdMm = 500,
                        },
                    },
                },
            });
            Recording = new RuntimeMatchRecordingSession(RecordingLog);
            Engine = new TraceEngine();
            var coordinator = new TrackerCoordinator(Engine, new TrackerPacketGenerator("runtime-source", "runtime-uuid"),
                resolved, new TrackedSnapshotStore(), new RecordingPublisher(), []);
            Loop = new RuntimeHostOperationLoop(buffer, coordinator, resolved, Recording);
            Host = new RuntimeMatchRecordingHostedService(
                Options.Create(new RuntimeMatchRecordingOptions { DirectoryPath = DirectoryPath }), resolved,
                Recording, new FixedBoundaryClock(), Loop, NullLogger<RuntimeMatchRecordingHostedService>.Instance);
        }

        public string DirectoryPath { get; }
        public RuntimeMatchRecordingSession Recording { get; }
        public TestLogger<RuntimeMatchRecordingSession> RecordingLog { get; } = new();
        public RuntimeHostOperationLoop Loop { get; }
        public RuntimeMatchRecordingHostedService Host { get; }
        public TraceEngine Engine { get; }

        public bool Receive(SSL_WrapperPacket packet, int milliseconds) =>
            RuntimeVisionReceiverService.ProcessDatagram(packet.ToByteArray(), StartTime.AddMilliseconds(milliseconds),
                buffer, diagnostics, NullLogger.Instance, remoteEndpoint: "127.0.0.1:10020", recordingSession: Recording);

        public void Dispose()
        {
            Engine.Release.Set();
            Recording.Dispose();
            Engine.Entered.Dispose();
            Engine.Release.Dispose();
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    // Delegate to the real engine. Synchronization and a one-shot injected
    // exception are the only test controls, matching the earlier review probes.
    private sealed class TraceEngine : ITrackerEngine
    {
        private readonly TrackerEngine inner = new();
        public bool Block { get; set; }
        public bool ThrowNext { get; set; }
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public List<int> AppliedGeometryWidths { get; } = [];
        public List<uint> ProcessedDetectionFrames { get; } = [];
        public TrackerUpdateResult? LastResult { get; private set; }

        public TrackerUpdateResult Update(SSL_WrapperPacket? packet, TrackerEngineSettings settings,
            TrackerProfileSwitchRequest? profileSwitchRequest = null)
        {
            if (ThrowNext)
            {
                ThrowNext = false;
                throw new InvalidOperationException("injected engine exception");
            }
            if (Block)
            {
                Entered.Set();
                if (!Release.Wait(Deadline))
                {
                    throw new TimeoutException("test engine was not released");
                }
            }
            var result = inner.Update(packet, settings, profileSwitchRequest);
            LastResult = result;
            if (packet?.Geometry is { } geometry)
            {
                AppliedGeometryWidths.Add(geometry.Field.FieldWidth);
            }
            if (packet?.Detection is { } detection)
            {
                ProcessedDetectionFrames.Add(detection.FrameNumber);
            }
            return result;
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Enqueue($"{formatter(state, exception)} {exception?.Message}");
    }

    private sealed class FixedBoundaryClock : TimeProvider
    {
        private int calls;
        public override DateTimeOffset GetUtcNow() =>
            Interlocked.Increment(ref calls) == 1 ? StartTime : StartTime.AddSeconds(1);
    }

    private sealed class RecordingPublisher : ITrackerPacketPublisher
    {
        public void ApplyConfiguration(TrackerPublisherOptions options) { }
        public void Publish(TrackerWrapperPacket packet) { }
    }
}
