using Tracker.Core;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// raw vision packet を tracker engine に投入し、snapshot、publisher、observer へ反映する調停役。
/// </summary>
public sealed partial class TrackerCoordinator
{
    private static readonly TimeSpan TrackerDiagnosticsLogInterval = TimeSpan.FromSeconds(1);
    private readonly object gate = new();
    private readonly ITrackerEngine engine;
    private readonly TrackerPacketGenerator packetGenerator;
    private TrackerEngineSettings currentSettings;
    private TrackerPublisherOptions currentPublisherOptions;
    private readonly TrackerDiagnosticsOptions diagnosticsOptions;
    private readonly TrackedSnapshotStore snapshotStore;
    private readonly ITrackerPacketPublisher publisher;
    private readonly IReadOnlyList<ITrackerObserver> observers;
    private readonly ILogger<TrackerCoordinator> logger;
    private readonly VisionPacketCaptureSession? packetCaptureSession;
    private readonly TrackerRenderSnapshotCaptureWriter? renderSnapshotCaptureWriter;
    private readonly TrackerPacketSnapshotLogWriter? trackerPacketSnapshotLogWriter;
    private readonly TrackerSnapshotAlignmentLogWriter? trackerSnapshotAlignmentLogWriter;
    private TrackerResolvedOptions appliedOptions;
    private TrackerResolvedOptions desiredOptions;
    private TrackerRuntimeOverrides desiredRuntimeOverrides = new();
    private PendingProfileSwitchRequest? pendingRequest;
    private PendingProfileSwitchRequest? inFlightRequest;
    private int nextRequestVersion = 1;
    private bool isProcessingUpdate;
    private DateTimeOffset lastTrackerDiagnosticsLogAt = DateTimeOffset.MinValue;
    private readonly HashSet<string> failedTrackerDiagnosticsLogPaths = new(StringComparer.Ordinal);
    private string? defaultTrackerDiagnosticsLogPath;

    /// <summary>
    /// tracker engine と配信先を受け取り、初期 publisher 設定を適用する。
    /// </summary>
    public TrackerCoordinator(
        ITrackerEngine engine,
        TrackerPacketGenerator packetGenerator,
        TrackerEngineSettings settings,
        TrackerPublisherOptions publisherOptions,
        TrackerDiagnosticsOptions diagnosticsOptions,
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        IEnumerable<ITrackerObserver> observers,
        ILogger<TrackerCoordinator> logger,
        VisionPacketCaptureSession? packetCaptureSession = null,
        TrackerRenderSnapshotCaptureWriter? renderSnapshotCaptureWriter = null,
        TrackerPacketSnapshotLogWriter? trackerPacketSnapshotLogWriter = null,
        TrackerSnapshotAlignmentLogWriter? trackerSnapshotAlignmentLogWriter = null)
    {
        this.engine = engine;
        this.packetGenerator = packetGenerator;
        currentSettings = TrackerOptionsCloner.CloneSettings(settings);
        currentPublisherOptions = TrackerOptionsCloner.ClonePublisherOptions(publisherOptions);
        this.diagnosticsOptions = TrackerOptionsCloner.CloneDiagnosticsOptions(diagnosticsOptions);
        this.snapshotStore = snapshotStore;
        this.publisher = publisher;
        this.observers = observers.ToArray();
        this.logger = logger;
        this.packetCaptureSession = packetCaptureSession;
        this.renderSnapshotCaptureWriter = renderSnapshotCaptureWriter;
        this.trackerPacketSnapshotLogWriter = trackerPacketSnapshotLogWriter;
        this.trackerSnapshotAlignmentLogWriter = trackerSnapshotAlignmentLogWriter;
        appliedOptions = new TrackerResolvedOptions
        {
            Enabled = true,
            EngineSettings = TrackerOptionsCloner.CloneSettings(settings),
            PublisherOptions = TrackerOptionsCloner.ClonePublisherOptions(publisherOptions),
            Diagnostics = TrackerOptionsCloner.CloneDiagnosticsOptions(diagnosticsOptions),
        };
        desiredOptions = TrackerOptionsCloner.CloneResolvedOptions(appliedOptions);
        desiredRuntimeOverrides = new TrackerRuntimeOverrides();
        publisher.ApplyConfiguration(currentPublisherOptions);
    }

    /// <summary>
    /// 受信 packet と任意の明示的 profile switch 要求を同じ lock 上で処理する。
    /// </summary>
    public TrackerUpdateResult ProcessPacket(
        SSL_WrapperPacket? packet,
        DateTimeOffset receivedAt,
        TrackerProfileSwitchRequest? profileSwitchRequest = null)
    {
        lock (gate)
        {
            return ExecuteUpdates(packet, receivedAt, profileSwitchRequest);
        }
    }

    /// <summary>
    /// UI などから要求された profile switch を pending に積み、処理中でなければ control-only update を即時 drain する。
    /// </summary>
    public void RequestProfileSwitch(
        TrackerResolvedOptions targetOptions,
        DateTimeOffset receivedAt,
        TrackerRuntimeOverrides? runtimeOverrides = null)
    {
        lock (gate)
        {
            var normalizedTarget = TrackerOptionsCloner.CloneResolvedOptions(targetOptions);
            var normalizedRuntimeOverrides = runtimeOverrides is null
                ? new TrackerRuntimeOverrides()
                : TrackerOptionsCloner.CloneRuntimeOverrides(runtimeOverrides);
            if (TrackerResolvedOptionsComparer.AreResolvedOptionsEquivalent(desiredOptions, normalizedTarget) &&
                TrackerResolvedOptionsComparer.AreRuntimeOverridesEquivalent(desiredRuntimeOverrides, normalizedRuntimeOverrides) &&
                pendingRequest is null &&
                inFlightRequest is null)
            {
                return;
            }

            desiredOptions = normalizedTarget;
            desiredRuntimeOverrides = normalizedRuntimeOverrides;
            pendingRequest = new PendingProfileSwitchRequest(
                nextRequestVersion++,
                normalizedTarget,
                normalizedRuntimeOverrides);

            if (isProcessingUpdate)
            {
                return;
            }

            _ = ExecuteUpdates(packet: null, receivedAt, explicitProfileSwitchRequest: null);
        }
    }

    private TrackerUpdateResult ExecuteUpdates(
        SSL_WrapperPacket? packet,
        DateTimeOffset receivedAt,
        TrackerProfileSwitchRequest? explicitProfileSwitchRequest)
    {
        isProcessingUpdate = true;
        try
        {
            var committedFrames = new List<TrackerFrame>();
            var emittedEvents = new List<TrackerEvent>();
            var latePacketDropCount = 0;
            var firstIteration = true;

            do
            {
                var updatePacket = firstIteration ? packet : null;
                var switchRequest = explicitProfileSwitchRequest ?? PromotePendingRequest();
                var result = engine.Update(updatePacket, currentSettings, switchRequest);
                var diagnosticsFrame = LogTrackerDiagnostics(updatePacket, result, receivedAt);
                DispatchResult(result, receivedAt);
                if (diagnosticsFrame is not null)
                {
                    trackerSnapshotAlignmentLogWriter?.CaptureDiagnosticsEntry(diagnosticsFrame, receivedAt);
                }

                committedFrames.AddRange(result.CommittedFrames);
                emittedEvents.AddRange(result.EmittedEvents);
                latePacketDropCount += result.Diagnostics.LatePacketDropCount;
                firstIteration = false;
                explicitProfileSwitchRequest = null;
            }
            while (pendingRequest is not null);

            return new TrackerUpdateResult
            {
                CommittedFrames = committedFrames,
                EmittedEvents = emittedEvents,
                Diagnostics = new TrackerEngineDiagnostics
                {
                    LatePacketDropCount = latePacketDropCount,
                },
            };
        }
        finally
        {
            isProcessingUpdate = false;
        }
    }
}
