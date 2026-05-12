using System.Text.Json;
using Microsoft.Extensions.Options;
using Tracker.Server.Tracking;

namespace Tracker.Server.Vision;

public sealed class VisionPacketCaptureSession
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly object gate = new();
    private readonly VisionPacketCaptureOptions options;
    private readonly VisionPacketCaptureRuntimeControl runtimeControl;
    private readonly TrackerOptions trackerOptions;
    private readonly TrackerResolvedOptions resolvedTrackerOptions;
    private readonly ILogger<VisionPacketCaptureSession> logger;
    private VisionPacketCaptureSessionState? state;
    private bool metadataWriteFailed;

    public VisionPacketCaptureSession(
        IOptions<VisionReceiverOptions> visionReceiverOptions,
        IOptions<TrackerOptions> trackerOptions,
        TrackerResolvedOptions resolvedTrackerOptions,
        ILogger<VisionPacketCaptureSession> logger,
        VisionPacketCaptureRuntimeControl? runtimeControl = null)
    {
        options = visionReceiverOptions.Value.PacketCapture;
        this.trackerOptions = trackerOptions.Value;
        this.resolvedTrackerOptions = resolvedTrackerOptions;
        this.logger = logger;
        this.runtimeControl = runtimeControl ?? new VisionPacketCaptureRuntimeControl(options.Enabled);
    }

    public bool Enabled => runtimeControl.Enabled;

    public bool FlushEachPacket => options.FlushEachPacket;

    public string DirectoryPath => VisionPacketCaptureFile.ResolveDirectoryPath(options.DirectoryPath);

    public VisionPacketCaptureSessionState? Current
    {
        get
        {
            lock (gate)
            {
                return state;
            }
        }
    }

    public VisionPacketCaptureSessionState? EnsureStarted(DateTimeOffset startedAt)
    {
        if (!runtimeControl.Enabled)
        {
            return null;
        }

        lock (gate)
        {
            if (state is not null)
            {
                return state;
            }

            var paths = VisionPacketCaptureFile.BuildCapturePaths(options, startedAt);
            state = new VisionPacketCaptureSessionState(
                startedAt.ToUniversalTime(),
                paths.SessionFolder,
                paths.PacketPath,
                paths.MetadataPath,
                paths.DiagnosticsLogPath,
                paths.RenderSnapshotPath,
                paths.TrackerSnapshotSidecarPath);
            WriteMetadata(state);
            return state;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            state = null;
            metadataWriteFailed = false;
        }
    }

    private void WriteMetadata(VisionPacketCaptureSessionState sessionState)
    {
        if (metadataWriteFailed)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(sessionState.MetadataPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var metadata = new VisionPacketCaptureMetadata
            {
                SchemaVersion = 1,
                StartedAt = sessionState.StartedAt,
                SessionFolder = sessionState.SessionFolder,
                PacketPath = ToCaptureDirectoryRelativePath(sessionState.PacketPath),
                MetadataPath = ToCaptureDirectoryRelativePath(sessionState.MetadataPath),
                DiagnosticsLogPath = ToCaptureDirectoryRelativePath(sessionState.DiagnosticsLogPath),
                RenderSnapshotPath = ToCaptureDirectoryRelativePath(sessionState.RenderSnapshotPath),
                TrackerSnapshotSidecarPath = ToCaptureDirectoryRelativePath(sessionState.TrackerSnapshotSidecarPath),
                TrackerSnapshotLog = new VisionPacketCaptureTrackerSnapshotLogMetadata
                {
                    Format = "jsonl",
                    IsCreated = File.Exists(sessionState.TrackerSnapshotSidecarPath),
                    RecordCount = 0,
                },
                TrackerSnapshotSources = [],
                TrackerOptions = trackerOptions,
                ResolvedTrackerOptions = resolvedTrackerOptions,
            };
            File.WriteAllText(
                sessionState.MetadataPath,
                JsonSerializer.Serialize(metadata, MetadataJsonOptions));
        }
        catch (Exception ex)
        {
            metadataWriteFailed = true;
            logger.LogWarning(ex, "Failed to write SSL-Vision packet capture metadata {MetadataPath}", sessionState.MetadataPath);
        }
    }

    private string ToCaptureDirectoryRelativePath(string path)
    {
        return Path.GetRelativePath(DirectoryPath, path);
    }

    private sealed class VisionPacketCaptureMetadata
    {
        public int SchemaVersion { get; init; }

        public DateTimeOffset StartedAt { get; init; }

        public string SessionFolder { get; init; } = "";

        public string PacketPath { get; init; } = "";

        public string MetadataPath { get; init; } = "";

        public string DiagnosticsLogPath { get; init; } = "";

        public string RenderSnapshotPath { get; init; } = "";

        public string TrackerSnapshotSidecarPath { get; init; } = "";

        public VisionPacketCaptureTrackerSnapshotLogMetadata TrackerSnapshotLog { get; init; } = new();

        public IReadOnlyList<VisionPacketCaptureTrackerSnapshotSourceMetadata> TrackerSnapshotSources { get; init; } = [];

        public TrackerOptions TrackerOptions { get; init; } = new();

        public TrackerResolvedOptions ResolvedTrackerOptions { get; init; } = new();
    }

    private sealed class VisionPacketCaptureTrackerSnapshotLogMetadata
    {
        public string Format { get; init; } = "jsonl";

        public bool IsCreated { get; init; }

        public int RecordCount { get; init; }
    }

    private sealed class VisionPacketCaptureTrackerSnapshotSourceMetadata
    {
        public string SourceUuid { get; init; } = "";

        public string SourceName { get; init; } = "";

        public string SourceRole { get; init; } = "";

        public string SourceLabel { get; init; } = "";
    }
}

public sealed record VisionPacketCaptureSessionState(
    DateTimeOffset StartedAt,
    string SessionFolder,
    string PacketPath,
    string MetadataPath,
    string DiagnosticsLogPath,
    string RenderSnapshotPath,
    string TrackerSnapshotSidecarPath);
