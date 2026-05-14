using System.Globalization;
using System.Threading;
using Tracker.Core;
using Tracker.DebugHost.Components.Vision;
using Tracker.DebugHost.Tracking;
using TrackerConnectionLib;

namespace Tracker.DebugHost.Vision;

/// <summary>
/// Vision live comparison で選択できる source 種別。
/// </summary>
public enum VisionLiveComparisonSourceKind
{
    /// <summary>
    /// raw SSL-Vision の camera 集約表示。
    /// </summary>
    RawAggregate,

    /// <summary>
    /// raw SSL-Vision の camera 別表示。
    /// </summary>
    RawCamera,

    /// <summary>
    /// ibis tracker 出力。
    /// </summary>
    Tracked,

    /// <summary>
    /// TrackerConnectionLib で受信した 3rd party tracker 出力。
    /// </summary>
    ThirdPartyTracker,
}

/// <summary>
/// Vision live comparison の表示 mode。
/// </summary>
public enum VisionLiveComparisonMode
{
    /// <summary>
    /// Layer A/B を左右に並べる。
    /// </summary>
    Split,

    /// <summary>
    /// Layer A/B を 1 つの field に重ねる。
    /// </summary>
    Overlay,
}

/// <summary>
/// Vision live comparison layer の状態。
/// </summary>
public enum VisionLiveComparisonLayerStatus
{
    /// <summary>
    /// 描画できる snapshot がある。
    /// </summary>
    Ready,

    /// <summary>
    /// 選択 source の snapshot がない。
    /// </summary>
    Missing,
}

/// <summary>
/// Vision live comparison の source option。
/// </summary>
public sealed record VisionLiveComparisonSourceOption(
    VisionLiveComparisonSourceKind Kind,
    string Key,
    string Label,
    int? CameraId,
    bool IsAvailable,
    string MissingReason);

/// <summary>
/// Vision live comparison の layer 選択状態。
/// </summary>
public sealed record VisionLiveComparisonLayerSelection(
    VisionLiveComparisonSourceOption Source,
    bool IsVisible);

/// <summary>
/// Raw aggregate source の immutable snapshot。
/// </summary>
public sealed record VisionLiveComparisonRawAggregateSnapshot(
    string Key,
    string Label,
    DateTimeOffset? ReceivedAt,
    long? TimestampNs,
    IReadOnlyList<SSL_DetectionBall> Balls,
    IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
    IReadOnlyList<SSL_DetectionRobot> RobotsBlue);

/// <summary>
/// Raw camera source の immutable snapshot。
/// </summary>
public sealed record VisionLiveComparisonRawCameraSnapshot(
    int CameraId,
    SSL_WrapperPacket? LatestPacket,
    SSL_DetectionFrame Detection,
    string? RemoteEndpoint,
    DateTimeOffset? ReceivedAt);

/// <summary>
/// Tracked source の immutable snapshot。
/// </summary>
public sealed record VisionLiveComparisonTrackedSnapshot(
    string Key,
    string Label,
    DateTimeOffset? ReceivedAt,
    long? TimestampNs,
    IReadOnlyList<SSL_DetectionBall> Balls,
    IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
    IReadOnlyList<SSL_DetectionRobot> RobotsBlue);

/// <summary>
/// 3rd party tracker source の immutable snapshot。
/// </summary>
public sealed record VisionLiveComparisonThirdPartyTrackerSnapshot(
    string Key,
    string Label,
    DateTimeOffset? ReceivedAt,
    long? TimestampNs,
    IReadOnlyList<SSL_DetectionBall> Balls,
    IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
    IReadOnlyList<SSL_DetectionRobot> RobotsBlue,
    string SourceRole,
    string SourceUuid,
    string RemoteEndpoint);

/// <summary>
/// 1 回の UI render tick で固定した Vision live comparison snapshot。
/// </summary>
public sealed record VisionLiveComparisonRenderSnapshot(
    DateTimeOffset SampledAt,
    long RenderTickId,
    IReadOnlyList<VisionLiveComparisonRawAggregateSnapshot> RawAggregateSnapshots,
    IReadOnlyList<VisionLiveComparisonRawCameraSnapshot> RawCameraSnapshots,
    VisionLiveComparisonTrackedSnapshot? TrackedSnapshot,
    IReadOnlyList<VisionLiveComparisonThirdPartyTrackerSnapshot> ThirdPartyTrackerSnapshots,
    SSL_GeometryData? Geometry,
    string GeometrySource,
    string GeometrySourceLabel,
    bool IsImmutable)
{
    /// <summary>
    /// test / reflection construction 用の互換 constructor。
    /// </summary>
    public VisionLiveComparisonRenderSnapshot(
        DateTimeOffset sampledAt,
        long renderTickId,
        IReadOnlyList<VisionLiveComparisonRawAggregateSnapshot> rawAggregateSnapshots,
        IReadOnlyList<VisionLiveComparisonRawCameraSnapshot> rawCameraSnapshots,
        VisionLiveComparisonTrackedSnapshot? trackedSnapshot,
        IReadOnlyList<VisionLiveComparisonThirdPartyTrackerSnapshot> thirdPartyTrackerSnapshots,
        SSL_GeometryData? geometry,
        bool isImmutable)
        : this(
            sampledAt,
            renderTickId,
            rawAggregateSnapshots,
            rawCameraSnapshots,
            trackedSnapshot,
            thirdPartyTrackerSnapshots,
            geometry,
            geometry is null ? "Missing" : "RawAggregate",
            geometry is null ? "Missing" : "Raw Aggregate",
            isImmutable)
    {
    }
}

/// <summary>
/// Vision live comparison layer の field 描画 DTO。
/// </summary>
public sealed record VisionLiveComparisonField(
    SSL_GeometryData? Geometry,
    IReadOnlyList<SSL_DetectionBall> Balls,
    IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
    IReadOnlyList<SSL_DetectionRobot> RobotsBlue);

/// <summary>
/// Vision live comparison legend item。
/// </summary>
public sealed record VisionLiveComparisonLegendItem(
    string LayerName,
    string SourceLabel,
    string Status,
    bool IsVisible,
    bool IsSameSourceCollapsed,
    string AccentColor,
    string MissingReason);

/// <summary>
/// Vision live comparison layer details。
/// </summary>
public sealed record VisionLiveComparisonLayerDetails(
    string SourceLabel,
    string SourceKind,
    DateTimeOffset? ReceivedAt,
    long? TimestampNs,
    int? BallCount,
    int? RobotCount,
    string MissingReason);

/// <summary>
/// Vision live comparison layer。
/// </summary>
public sealed record VisionLiveComparisonLayer(
    string LayerName,
    VisionLiveComparisonLayerStatus Status,
    bool IsVisible,
    bool IsSameSourceCollapsed,
    string AccentColor,
    string SourceLabel,
    string MissingReason,
    DateTimeOffset? SourceReceivedAt,
    long RenderTickId,
    VisionLiveComparisonField Field,
    VisionLiveComparisonLegendItem Legend,
    VisionLiveComparisonLayerDetails Details);

/// <summary>
/// Vision live comparison の UI 非依存 view-state。
/// </summary>
public sealed record VisionLiveComparisonViewState(
    VisionLiveComparisonMode Mode,
    VisionLiveComparisonRenderSnapshot RenderSnapshot,
    IReadOnlyList<VisionLiveComparisonSourceOption> SourceOptions,
    VisionLiveComparisonLayerSelection LayerASelection,
    VisionLiveComparisonLayerSelection LayerBSelection)
{
    private const string LayerAAccentColor = "#68d8ff";
    private const string LayerBAccentColor = "#ff7ad9";

    /// <summary>
    /// Layer A の現在状態。
    /// </summary>
    public VisionLiveComparisonLayer LayerA => CreateLayer("Layer A", LayerASelection, isSameSourceCollapsed: false, LayerAAccentColor);

    /// <summary>
    /// Layer B の現在状態。
    /// </summary>
    public VisionLiveComparisonLayer LayerB => CreateLayer("Layer B", LayerBSelection, isSameSourceCollapsed: false, LayerBAccentColor);

    /// <summary>
    /// legend に表示する layer item。
    /// </summary>
    public IReadOnlyList<VisionLiveComparisonLegendItem> LegendItems => CreateOverlayLayers()
        .Select(layer => layer.Legend)
        .ToArray();

    /// <summary>
    /// details に表示する layer metadata。
    /// </summary>
    public IReadOnlyList<VisionLiveComparisonLayerDetails> LayerDetails => CreateOverlayLayers()
        .Select(layer => layer.Details)
        .ToArray();

    /// <summary>
    /// split mode 用の layer list を作る。
    /// </summary>
    public IReadOnlyList<VisionLiveComparisonLayer> CreateSplitLayers()
    {
        return
        [
            LayerA,
            LayerB,
        ];
    }

    /// <summary>
    /// overlay mode 用の layer list を作る。
    /// </summary>
    public IReadOnlyList<VisionLiveComparisonLayer> CreateOverlayLayers()
    {
        if (string.Equals(
                LayerASelection.Source.Key,
                LayerBSelection.Source.Key,
                StringComparison.Ordinal))
        {
            var mergedSelection = LayerASelection with
            {
                IsVisible = LayerASelection.IsVisible || LayerBSelection.IsVisible,
            };
            return [CreateLayer("Layer A/B", mergedSelection, isSameSourceCollapsed: true, LayerAAccentColor)];
        }

        return CreateSplitLayers();
    }

    private VisionLiveComparisonLayer CreateLayer(
        string layerName,
        VisionLiveComparisonLayerSelection selection,
        bool isSameSourceCollapsed,
        string accentColor)
    {
        var source = selection.Source;
        var sourceSnapshot = ResolveSourceSnapshot(source);
        var status = source.IsAvailable
            ? VisionLiveComparisonLayerStatus.Ready
            : VisionLiveComparisonLayerStatus.Missing;
        var missingReason = status == VisionLiveComparisonLayerStatus.Missing
            ? source.MissingReason
            : string.Empty;
        var field = new VisionLiveComparisonField(
            RenderSnapshot.Geometry,
            sourceSnapshot.Balls,
            sourceSnapshot.RobotsYellow,
            sourceSnapshot.RobotsBlue);
        var legend = new VisionLiveComparisonLegendItem(
            layerName,
            source.Label,
            status.ToString(),
            selection.IsVisible,
            isSameSourceCollapsed,
            accentColor,
            missingReason);
        var details = new VisionLiveComparisonLayerDetails(
            source.Label,
            source.Kind.ToString(),
            sourceSnapshot.ReceivedAt,
            sourceSnapshot.TimestampNs,
            sourceSnapshot.BallCount,
            sourceSnapshot.RobotCount,
            missingReason);

        return new VisionLiveComparisonLayer(
            layerName,
            status,
            selection.IsVisible,
            isSameSourceCollapsed,
            accentColor,
            source.Label,
            missingReason,
            sourceSnapshot.ReceivedAt,
            RenderSnapshot.RenderTickId,
            field,
            legend,
            details);
    }

    private SourceSnapshot ResolveSourceSnapshot(VisionLiveComparisonSourceOption source)
    {
        if (!source.IsAvailable)
        {
            return SourceSnapshot.Empty;
        }

        return source.Kind switch
        {
            VisionLiveComparisonSourceKind.RawAggregate => ResolveRawAggregateSnapshot(),
            VisionLiveComparisonSourceKind.RawCamera => ResolveRawCameraSnapshot(source.CameraId),
            VisionLiveComparisonSourceKind.Tracked => ResolveTrackedSnapshot(),
            VisionLiveComparisonSourceKind.ThirdPartyTracker => ResolveThirdPartyTrackerSnapshot(source.Key),
            _ => SourceSnapshot.Empty,
        };
    }

    private SourceSnapshot ResolveRawAggregateSnapshot()
    {
        var snapshot = RenderSnapshot.RawAggregateSnapshots.FirstOrDefault();
        return snapshot is null
            ? SourceSnapshot.Empty
            : new SourceSnapshot(
                snapshot.ReceivedAt,
                snapshot.TimestampNs,
                snapshot.Balls.Count,
                snapshot.RobotsYellow.Count + snapshot.RobotsBlue.Count,
                snapshot.Balls,
                snapshot.RobotsYellow,
                snapshot.RobotsBlue);
    }

    private SourceSnapshot ResolveRawCameraSnapshot(int? cameraId)
    {
        var snapshot = cameraId is null
            ? null
            : RenderSnapshot.RawCameraSnapshots.FirstOrDefault(camera => camera.CameraId == cameraId.Value);
        return snapshot is null
            ? SourceSnapshot.Empty
            : new SourceSnapshot(
                snapshot.ReceivedAt,
                snapshot.Detection.FrameNumber,
                snapshot.Detection.Balls.Count,
                snapshot.Detection.RobotsYellow.Count + snapshot.Detection.RobotsBlue.Count,
                snapshot.Detection.Balls,
                snapshot.Detection.RobotsYellow,
                snapshot.Detection.RobotsBlue);
    }

    private SourceSnapshot ResolveTrackedSnapshot()
    {
        var snapshot = RenderSnapshot.TrackedSnapshot;
        return snapshot is null
            ? SourceSnapshot.Empty
            : new SourceSnapshot(
                snapshot.ReceivedAt,
                snapshot.TimestampNs,
                snapshot.Balls.Count,
                snapshot.RobotsYellow.Count + snapshot.RobotsBlue.Count,
                snapshot.Balls,
                snapshot.RobotsYellow,
                snapshot.RobotsBlue);
    }

    private SourceSnapshot ResolveThirdPartyTrackerSnapshot(string key)
    {
        var snapshot = RenderSnapshot.ThirdPartyTrackerSnapshots
            .FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
        return snapshot is null
            ? SourceSnapshot.Empty
            : new SourceSnapshot(
                snapshot.ReceivedAt,
                snapshot.TimestampNs,
                snapshot.Balls.Count,
                snapshot.RobotsYellow.Count + snapshot.RobotsBlue.Count,
                snapshot.Balls,
                snapshot.RobotsYellow,
                snapshot.RobotsBlue);
    }

    private sealed record SourceSnapshot(
        DateTimeOffset? ReceivedAt,
        long? TimestampNs,
        int? BallCount,
        int? RobotCount,
        IReadOnlyList<SSL_DetectionBall> Balls,
        IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
        IReadOnlyList<SSL_DetectionRobot> RobotsBlue)
    {
        public static SourceSnapshot Empty { get; } = new(
            null,
            null,
            null,
            null,
            Array.Empty<SSL_DetectionBall>(),
            Array.Empty<SSL_DetectionRobot>(),
            Array.Empty<SSL_DetectionRobot>());
    }
}

/// <summary>
/// Vision live comparison 用に同一 render tick の immutable snapshot を合成する。
/// </summary>
public sealed class VisionLiveComparisonSnapshotComposer
{
    private readonly VisionPacketStore visionPacketStore;
    private readonly TrackedSnapshotStore? trackedSnapshotStore;
    private readonly MultiTrackerManager<TrackerPacketAdapter>? externalTrackerManager;
    private long renderTickId;

    /// <summary>
    /// raw / tracked / external tracker store を使う composer を作る。
    /// </summary>
    public VisionLiveComparisonSnapshotComposer(
        VisionPacketStore visionPacketStore,
        TrackedSnapshotStore? trackedSnapshotStore = null,
        MultiTrackerManager<TrackerPacketAdapter>? externalTrackerManager = null)
    {
        this.visionPacketStore = visionPacketStore;
        this.trackedSnapshotStore = trackedSnapshotStore;
        this.externalTrackerManager = externalTrackerManager;
    }

    /// <summary>
    /// 1 回の UI render tick で参照する source snapshot を固定する。
    /// </summary>
    public VisionLiveComparisonRenderSnapshot CaptureRenderTickSnapshot()
    {
        var rawSnapshot = visionPacketStore.GetSnapshot();
        var trackedSnapshot = trackedSnapshotStore?.GetSnapshot();
        var trackedView = trackedSnapshot is null
            ? null
            : TrackedVisionViewState.FromSnapshot(trackedSnapshot);
        var thirdPartySnapshots = CaptureThirdPartyTrackerSnapshots();
        var rawGeometry = rawSnapshot.Geometry?.Clone();
        var trackedGeometry = trackedView?.Geometry?.Clone();
        var geometry = rawGeometry ?? trackedGeometry;
        var geometrySource = rawGeometry is not null
            ? "RawAggregate"
            : trackedGeometry is not null ? "Tracked" : "Missing";
        var geometrySourceLabel = rawGeometry is not null
            ? "Raw Aggregate"
            : trackedGeometry is not null ? "Tracked" : "Missing";
        var rawAggregateSnapshots = rawSnapshot.Cameras.Count == 0
            ? Array.Empty<VisionLiveComparisonRawAggregateSnapshot>()
            :
            [
                new VisionLiveComparisonRawAggregateSnapshot(
                    "raw:aggregate",
                    "Raw Aggregate",
                    rawSnapshot.ReceivedAt,
                    rawSnapshot.Detection?.FrameNumber,
                    rawSnapshot.AggregateDetection.Balls.Select(ball => ball.Clone()).ToArray(),
                    rawSnapshot.AggregateDetection.RobotsYellow.Select(robot => robot.Clone()).ToArray(),
                    rawSnapshot.AggregateDetection.RobotsBlue.Select(robot => robot.Clone()).ToArray()),
            ];

        return new VisionLiveComparisonRenderSnapshot(
            DateTimeOffset.UtcNow,
            Interlocked.Increment(ref renderTickId),
            rawAggregateSnapshots,
            rawSnapshot.Cameras
                .Select(camera => new VisionLiveComparisonRawCameraSnapshot(
                    checked((int)camera.CameraId),
                    camera.LatestPacket?.Clone(),
                    camera.Detection.Clone(),
                    camera.RemoteEndpoint,
                    camera.ReceivedAt))
                .ToArray(),
            trackedView is { HasFrame: true }
                ? new VisionLiveComparisonTrackedSnapshot(
                    "tracked:ibis",
                    "Tracked",
                    trackedView.ReceivedAt,
                    trackedView.Diagnostics.DataTimestampNs,
                    trackedView.Balls.Select(ball => ball.Clone()).ToArray(),
                    trackedView.RobotsYellow.Select(robot => robot.Clone()).ToArray(),
                    trackedView.RobotsBlue.Select(robot => robot.Clone()).ToArray())
                : null,
            thirdPartySnapshots,
            geometry,
            geometrySource,
            geometrySourceLabel,
            IsImmutable: true);
    }

    /// <summary>
    /// render tick snapshot から Vision live comparison view-state を作る。
    /// </summary>
    public VisionLiveComparisonViewState CreateViewState(VisionLiveComparisonRenderSnapshot renderSnapshot)
    {
        var sourceOptions = CreateSourceOptions(renderSnapshot);
        var layerA = sourceOptions.First();
        var layerB = sourceOptions.FirstOrDefault(option => option.Kind == VisionLiveComparisonSourceKind.Tracked)
            ?? layerA;
        return new VisionLiveComparisonViewState(
            VisionLiveComparisonMode.Split,
            renderSnapshot,
            sourceOptions,
            new VisionLiveComparisonLayerSelection(layerA, IsVisible: true),
            new VisionLiveComparisonLayerSelection(layerB, IsVisible: true));
    }

    private static IReadOnlyList<VisionLiveComparisonSourceOption> CreateSourceOptions(
        VisionLiveComparisonRenderSnapshot renderSnapshot)
    {
        var options = new List<VisionLiveComparisonSourceOption>
        {
            renderSnapshot.RawAggregateSnapshots.Count > 0
                ? new VisionLiveComparisonSourceOption(
                    VisionLiveComparisonSourceKind.RawAggregate,
                    "raw:aggregate",
                    "Raw Aggregate",
                    CameraId: null,
                    IsAvailable: true,
                    MissingReason: string.Empty)
                : new VisionLiveComparisonSourceOption(
                    VisionLiveComparisonSourceKind.RawAggregate,
                    "raw:aggregate",
                    "Raw Aggregate",
                    CameraId: null,
                    IsAvailable: false,
                    MissingReason: "No raw aggregate snapshot in current render tick."),
        };

        options.AddRange(renderSnapshot.RawCameraSnapshots.Select(camera =>
            new VisionLiveComparisonSourceOption(
                VisionLiveComparisonSourceKind.RawCamera,
                $"raw:camera:{camera.CameraId.ToString(CultureInfo.InvariantCulture)}",
                $"Raw Camera {camera.CameraId.ToString(CultureInfo.InvariantCulture)}",
                checked((int)camera.CameraId),
                IsAvailable: true,
                MissingReason: string.Empty)));
        options.Add(renderSnapshot.TrackedSnapshot is null
            ? new VisionLiveComparisonSourceOption(
                VisionLiveComparisonSourceKind.Tracked,
                "tracked:ibis",
                "Tracked",
                CameraId: null,
                IsAvailable: false,
                MissingReason: "No tracked snapshot in current render tick.")
            : new VisionLiveComparisonSourceOption(
                VisionLiveComparisonSourceKind.Tracked,
                "tracked:ibis",
                "Tracked",
                CameraId: null,
                IsAvailable: true,
                MissingReason: string.Empty));
        options.AddRange(renderSnapshot.ThirdPartyTrackerSnapshots.Select(snapshot =>
            new VisionLiveComparisonSourceOption(
                VisionLiveComparisonSourceKind.ThirdPartyTracker,
                snapshot.Key,
                snapshot.Label,
                CameraId: null,
                IsAvailable: true,
                MissingReason: string.Empty)));
        if (renderSnapshot.ThirdPartyTrackerSnapshots.Count == 0)
        {
            options.Add(new VisionLiveComparisonSourceOption(
                VisionLiveComparisonSourceKind.ThirdPartyTracker,
                "third-party:default",
                "3rd party tracker",
                CameraId: null,
                IsAvailable: false,
                MissingReason: "No 3rd party tracker snapshot in current render tick."));
        }

        return options;
    }

    private IReadOnlyList<VisionLiveComparisonThirdPartyTrackerSnapshot> CaptureThirdPartyTrackerSnapshots()
    {
        if (externalTrackerManager is null)
        {
            return [];
        }

        var snapshots = externalTrackerManager.Trackers.Values
            .Where(state => state.LastPacket is not null)
            .OrderBy(state => state.SourceLabel, StringComparer.Ordinal)
            .ThenBy(state => state.RemoteEndpoint?.ToString(), StringComparer.Ordinal)
            .Select(CreateThirdPartyTrackerSnapshot)
            .GroupBy(snapshot => snapshot.Key, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(snapshot => snapshot.ReceivedAt ?? DateTimeOffset.MinValue)
                .ThenBy(snapshot => snapshot.RemoteEndpoint, StringComparer.Ordinal)
                .First())
            .ToArray();

        return DisambiguateThirdPartyTrackerLabels(snapshots);
    }

    private static VisionLiveComparisonThirdPartyTrackerSnapshot CreateThirdPartyTrackerSnapshot(
        TrackerState<TrackerPacketAdapter> state)
    {
        var packet = state.LastPacket!.Packet.Clone();
        var sourceUuid = packet.Uuid ?? state.Uuid ?? string.Empty;
        var remoteEndpoint = state.RemoteEndpoint?.ToString() ?? string.Empty;
        var sourceLabel = TrackerPacketSnapshotRecord.NormalizeSourceLabel(
            state.SourceLabel,
            state.SourceName,
            sourceUuid,
            remoteEndpoint,
            state.SourceRole);
        var sourceKey = TrackerSourceIdentity.CreateUuidPreferredKey(
            "third-party",
            sourceLabel,
            state.SourceName,
            sourceUuid,
            remoteEndpoint,
            state.SourceRole);
        var summary = TrackerPacketSnapshotSemanticSummary.FromPacket(
            packet,
            state.SourceRole,
            sourceLabel);
        var balls = summary.Balls.Select(ball => new SSL_DetectionBall
        {
            Confidence = ball.Visibility,
            X = (float)ball.XMm,
            Y = (float)ball.YMm,
            Z = (float)ball.ZMm,
        }).ToArray();
        var robotsYellow = summary.Robots
            .Where(robot => string.Equals(robot.Team, Team.Yellow.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(CreateRobot)
            .ToArray();
        var robotsBlue = summary.Robots
            .Where(robot => string.Equals(robot.Team, Team.Blue.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(CreateRobot)
            .ToArray();

        return new VisionLiveComparisonThirdPartyTrackerSnapshot(
            sourceKey,
            sourceLabel,
            state.ReceivedAt,
            summary.TrackedFrameTimestampNs,
            balls,
            robotsYellow,
            robotsBlue,
            state.SourceRole,
            sourceUuid,
            remoteEndpoint);
    }

    private static IReadOnlyList<VisionLiveComparisonThirdPartyTrackerSnapshot> DisambiguateThirdPartyTrackerLabels(
        IReadOnlyList<VisionLiveComparisonThirdPartyTrackerSnapshot> snapshots)
    {
        var duplicatedLabels = snapshots
            .GroupBy(snapshot => snapshot.Label, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        return snapshots
            .Select(snapshot => duplicatedLabels.Contains(snapshot.Label)
                ? snapshot with
                {
                    Label = TrackerSourceIdentity.CreateDisambiguatedLabel(
                        snapshot.Label,
                        snapshot.SourceUuid,
                        snapshot.RemoteEndpoint),
                }
                : snapshot)
            .OrderBy(snapshot => snapshot.Label, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static SSL_DetectionRobot CreateRobot(TrackerPacketSnapshotRobotSummary robot)
    {
        return new SSL_DetectionRobot
        {
            Confidence = robot.Visibility,
            RobotId = robot.RobotId,
            X = (float)robot.XMm,
            Y = (float)robot.YMm,
            Orientation = robot.OrientationRad,
        };
    }
}
