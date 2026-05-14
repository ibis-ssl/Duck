using System.Threading;
using Tracker.Core;
using Tracker.DebugHost.Tracking;

namespace Tracker.DebugHost.Vision;

/// <summary>
/// DebugHost live display の 1 render tick で使う read-side snapshot を合成する。
/// </summary>
public sealed class VisionLiveDisplaySnapshotProvider
{
    private readonly VisionPacketStore visionPacketStore;
    private readonly TrackedSnapshotStore trackedSnapshotStore;
    private readonly ExternalTrackerSnapshotStore externalTrackerSnapshotStore;
    private readonly VisionLiveComparisonSnapshotComposer comparisonSnapshotComposer;
    private long renderTickId;

    /// <summary>
    /// raw / tracked / external tracker の read-side store を provider に接続する。
    /// </summary>
    public VisionLiveDisplaySnapshotProvider(
        VisionPacketStore visionPacketStore,
        TrackedSnapshotStore trackedSnapshotStore,
        ExternalTrackerSnapshotStore externalTrackerSnapshotStore,
        VisionLiveComparisonSnapshotComposer comparisonSnapshotComposer)
    {
        this.visionPacketStore = visionPacketStore;
        this.trackedSnapshotStore = trackedSnapshotStore;
        this.externalTrackerSnapshotStore = externalTrackerSnapshotStore;
        this.comparisonSnapshotComposer = comparisonSnapshotComposer;
    }

    /// <summary>
    /// 1 回の UI render tick で raw / tracked / external tracker を 1 回ずつ固定する。
    /// </summary>
    public VisionLiveDisplayRenderSnapshot CaptureRenderTickSnapshot()
    {
        var sampledAt = DateTimeOffset.UtcNow;
        var renderTickId = Interlocked.Increment(ref this.renderTickId);
        var rawSnapshot = visionPacketStore.GetSnapshot();
        var trackedSnapshot = trackedSnapshotStore.GetSnapshot();
        var externalTrackerSnapshots = externalTrackerSnapshotStore.GetSnapshot();
        var comparisonSnapshot = comparisonSnapshotComposer.CaptureRenderTickSnapshot(
            sampledAt,
            renderTickId,
            rawSnapshot,
            trackedSnapshot,
            externalTrackerSnapshots);

        return new VisionLiveDisplayRenderSnapshot(
            sampledAt,
            renderTickId,
            rawSnapshot,
            trackedSnapshot,
            externalTrackerSnapshots,
            comparisonSnapshot,
            IsImmutable: true);
    }

    /// <summary>
    /// 固定済み live display snapshot から comparison view-state を作る。
    /// </summary>
    public VisionLiveComparisonViewState CreateComparisonViewState(
        VisionLiveDisplayRenderSnapshot renderSnapshot,
        VisionLiveComparisonMode comparisonMode,
        string layerASourceKey,
        string layerBSourceKey,
        bool layerAVisible,
        bool layerBVisible)
    {
        var defaultViewState = comparisonSnapshotComposer.CreateViewState(renderSnapshot.ComparisonSnapshot);
        var layerA = FindSourceOption(defaultViewState.SourceOptions, layerASourceKey);
        var layerB = FindSourceOption(defaultViewState.SourceOptions, layerBSourceKey);

        return new VisionLiveComparisonViewState(
            comparisonMode,
            renderSnapshot.ComparisonSnapshot,
            defaultViewState.SourceOptions,
            new VisionLiveComparisonLayerSelection(layerA, layerAVisible),
            new VisionLiveComparisonLayerSelection(layerB, layerBVisible));
    }

    private static VisionLiveComparisonSourceOption FindSourceOption(
        IReadOnlyList<VisionLiveComparisonSourceOption> options,
        string key)
    {
        return options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.Ordinal))
            ?? options.First();
    }
}

/// <summary>
/// DebugHost live display の 1 render tick で固定した composite read-side snapshot。
/// </summary>
public sealed record VisionLiveDisplayRenderSnapshot(
    DateTimeOffset SampledAt,
    long RenderTickId,
    VisionPacketSnapshot RawSnapshot,
    TrackedSnapshot TrackedSnapshot,
    IReadOnlyList<ExternalTrackerReadSideSnapshot> ExternalTrackerSnapshots,
    VisionLiveComparisonRenderSnapshot ComparisonSnapshot,
    bool IsImmutable)
{
    /// <summary>
    /// 初期 render 前の空 snapshot。
    /// </summary>
    public static VisionLiveDisplayRenderSnapshot Empty { get; } = new(
        DateTimeOffset.UnixEpoch,
        0,
        VisionPacketSnapshot.Empty,
        new TrackedSnapshot(null, null, "default", 0, 0),
        Array.Empty<ExternalTrackerReadSideSnapshot>(),
        new VisionLiveComparisonRenderSnapshot(
            DateTimeOffset.UnixEpoch,
            0,
            Array.Empty<VisionLiveComparisonRawAggregateSnapshot>(),
            Array.Empty<VisionLiveComparisonRawCameraSnapshot>(),
            null,
            Array.Empty<VisionLiveComparisonThirdPartyTrackerSnapshot>(),
            null,
            "Missing",
            "Missing",
            IsImmutable: true),
        IsImmutable: true);
}
