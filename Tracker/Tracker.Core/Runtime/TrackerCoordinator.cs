namespace Tracker.Core;

/// <summary>
/// raw vision packet を tracker engine に投入し、snapshot、publisher、observer へ反映する UI 非依存の runtime 調停役。
/// </summary>
public sealed partial class TrackerCoordinator
{
    private readonly object gate = new();
    private readonly ITrackerEngine engine;
    private readonly TrackerPacketGenerator packetGenerator;
    private readonly TrackedSnapshotStore snapshotStore;
    private readonly ITrackerPacketPublisher publisher;
    private readonly IReadOnlyList<ITrackerObserver> observers;
    private TrackerEngineSettings currentSettings;
    private TrackerPublisherOptions currentPublisherOptions;
    private TrackerRuntimeResolvedOptions appliedOptions;
    private TrackerRuntimeResolvedOptions desiredOptions;
    private TrackerRuntimeOverrides desiredRuntimeOverrides = new();
    private PendingProfileSwitchRequest? pendingRequest;
    private PendingProfileSwitchRequest? inFlightRequest;
    private int nextRequestVersion = 1;
    private bool isProcessingUpdate;

    /// <summary>
    /// tracker engine と配信先を受け取り、初期 publisher 設定を適用する。
    /// </summary>
    public TrackerCoordinator(
        ITrackerEngine engine,
        TrackerPacketGenerator packetGenerator,
        TrackerRuntimeResolvedOptions resolvedOptions,
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        IEnumerable<ITrackerObserver> observers)
    {
        this.engine = engine;
        this.packetGenerator = packetGenerator;
        currentSettings = TrackerRuntimeOptionsCloner.CloneSettings(resolvedOptions.EngineSettings);
        currentPublisherOptions = TrackerRuntimeOptionsCloner.ClonePublisherOptions(resolvedOptions.PublisherOptions);
        this.snapshotStore = snapshotStore;
        this.publisher = publisher;
        this.observers = observers.ToArray();
        appliedOptions = TrackerRuntimeOptionsCloner.CloneResolvedOptions(resolvedOptions);
        desiredOptions = TrackerRuntimeOptionsCloner.CloneResolvedOptions(appliedOptions);
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
        TrackerRuntimeResolvedOptions targetOptions,
        DateTimeOffset receivedAt,
        TrackerRuntimeOverrides? runtimeOverrides = null)
    {
        lock (gate)
        {
            var normalizedTarget = TrackerRuntimeOptionsCloner.CloneResolvedOptions(targetOptions);
            var normalizedRuntimeOverrides = runtimeOverrides is null
                ? new TrackerRuntimeOverrides()
                : TrackerRuntimeOptionsCloner.CloneRuntimeOverrides(runtimeOverrides);
            if (TrackerRuntimeResolvedOptionsComparer.AreResolvedOptionsEquivalent(desiredOptions, normalizedTarget) &&
                TrackerRuntimeResolvedOptionsComparer.AreRuntimeOverridesEquivalent(desiredRuntimeOverrides, normalizedRuntimeOverrides) &&
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
                DispatchResult(result, receivedAt);

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
