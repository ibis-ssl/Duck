using System.Collections;
using System.Net;
using System.Reflection;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.Tests.Contracts;
using TrackerConnectionLib;
using Tracker.DebugHost.Vision;

namespace Tracker.Tests;

/// <summary>
/// RAW-VISION-014 の Vision split / overlay live contract を固定する。
/// </summary>
public class VisionLiveComparisonViewStateTests
{
    private static readonly TrackerContractFixture Fixture = new();

    /// <summary>
    /// Vision 画面は raw aggregate / raw camera / tracked / 3rd party tracker を選択肢として公開する。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonViewState_DeclaresFixedSourceOptionsAndSelectionContract()
    {
        var sourceKindType = RequiredVisionType("VisionLiveComparisonSourceKind");
        Assert.True(sourceKindType.IsEnum);
        Assert.Contains("RawAggregate", Enum.GetNames(sourceKindType));
        Assert.Contains("RawCamera", Enum.GetNames(sourceKindType));
        Assert.Contains("Tracked", Enum.GetNames(sourceKindType));
        Assert.Contains("ThirdPartyTracker", Enum.GetNames(sourceKindType));

        var sourceOptionType = RequiredVisionType("VisionLiveComparisonSourceOption");
        AssertProperty(sourceOptionType, "Kind", sourceKindType);
        AssertProperty(sourceOptionType, "Key", typeof(string));
        AssertProperty(sourceOptionType, "Label", typeof(string));
        AssertProperty(sourceOptionType, "CameraId", typeof(int?));
        AssertProperty(sourceOptionType, "IsAvailable", typeof(bool));
        AssertProperty(sourceOptionType, "MissingReason", typeof(string));

        var layerSelectionType = RequiredVisionType("VisionLiveComparisonLayerSelection");
        AssertProperty(layerSelectionType, "Source", sourceOptionType);
        AssertProperty(layerSelectionType, "IsVisible", typeof(bool));

        var viewStateType = RequiredVisionType("VisionLiveComparisonViewState");
        AssertEnumerableProperty(viewStateType, "SourceOptions");
        AssertProperty(viewStateType, "LayerASelection", layerSelectionType);
        AssertProperty(viewStateType, "LayerBSelection", layerSelectionType);
    }

    /// <summary>
    /// Vision 画面は 1 回の UI render tick で固定した immutable snapshot だけを Layer A/B に渡す。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonViewState_DeclaresSameRenderTickImmutableSnapshotContract()
    {
        var renderSnapshotType = RequiredVisionType("VisionLiveComparisonRenderSnapshot");
        AssertProperty(renderSnapshotType, "SampledAt", typeof(DateTimeOffset));
        AssertProperty(renderSnapshotType, "RenderTickId", typeof(long));
        AssertEnumerableProperty(renderSnapshotType, "RawAggregateSnapshots");
        AssertEnumerableProperty(renderSnapshotType, "RawCameraSnapshots");
        AssertProperty(renderSnapshotType, "TrackedSnapshot");
        AssertEnumerableProperty(renderSnapshotType, "ThirdPartyTrackerSnapshots");
        AssertProperty(renderSnapshotType, "Geometry");
        AssertProperty(renderSnapshotType, "GeometrySource", typeof(string));
        AssertProperty(renderSnapshotType, "GeometrySourceLabel", typeof(string));
        AssertProperty(renderSnapshotType, "IsImmutable", typeof(bool));

        var composerType = RequiredVisionType("VisionLiveComparisonSnapshotComposer");
        AssertMethod(composerType, "CaptureRenderTickSnapshot", renderSnapshotType);
        AssertMethod(composerType, "CreateViewState", RequiredVisionType("VisionLiveComparisonViewState"));
    }

    /// <summary>
    /// split / overlay は same-source を 1 layer に畳み、missing layer があっても ready layer を残す。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonViewState_DeclaresOverlayLayerAndMissingLayerContract()
    {
        var modeType = RequiredVisionType("VisionLiveComparisonMode");
        Assert.True(modeType.IsEnum);
        Assert.Contains("Split", Enum.GetNames(modeType));
        Assert.Contains("Overlay", Enum.GetNames(modeType));

        var layerStatusType = RequiredVisionType("VisionLiveComparisonLayerStatus");
        Assert.True(layerStatusType.IsEnum);
        Assert.Contains("Ready", Enum.GetNames(layerStatusType));
        Assert.Contains("Missing", Enum.GetNames(layerStatusType));

        var layerType = RequiredVisionType("VisionLiveComparisonLayer");
        AssertProperty(layerType, "LayerName", typeof(string));
        AssertProperty(layerType, "Status", layerStatusType);
        AssertProperty(layerType, "IsVisible", typeof(bool));
        AssertProperty(layerType, "IsSameSourceCollapsed", typeof(bool));
        AssertProperty(layerType, "AccentColor", typeof(string));
        AssertProperty(layerType, "SourceLabel", typeof(string));
        AssertProperty(layerType, "MissingReason", typeof(string));
        AssertProperty(layerType, "SourceReceivedAt", typeof(DateTimeOffset?));
        AssertProperty(layerType, "RenderTickId", typeof(long));
        AssertProperty(layerType, "Field");
        AssertProperty(layerType, "Legend");
        AssertProperty(layerType, "Details");

        var viewStateType = RequiredVisionType("VisionLiveComparisonViewState");
        AssertProperty(viewStateType, "Mode", modeType);
        AssertProperty(viewStateType, "LayerA", layerType);
        AssertProperty(viewStateType, "LayerB", layerType);
        AssertMethod(viewStateType, "CreateSplitLayers", typeof(IReadOnlyList<>), assertGenericDefinition: true);
        AssertMethod(viewStateType, "CreateOverlayLayers", typeof(IReadOnlyList<>), assertGenericDefinition: true);
    }

    /// <summary>
    /// legend / details は diagnostics と同じく source、timestamp metadata、missing reason を UI に渡す。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonViewState_DeclaresDiagnosticsLikeLegendAndDetailsMetadata()
    {
        var legendItemType = RequiredVisionType("VisionLiveComparisonLegendItem");
        AssertProperty(legendItemType, "LayerName", typeof(string));
        AssertProperty(legendItemType, "SourceLabel", typeof(string));
        AssertProperty(legendItemType, "Status", typeof(string));
        AssertProperty(legendItemType, "IsVisible", typeof(bool));
        AssertProperty(legendItemType, "IsSameSourceCollapsed", typeof(bool));
        AssertProperty(legendItemType, "AccentColor", typeof(string));
        AssertProperty(legendItemType, "MissingReason", typeof(string));

        var detailsType = RequiredVisionType("VisionLiveComparisonLayerDetails");
        AssertProperty(detailsType, "SourceLabel", typeof(string));
        AssertProperty(detailsType, "SourceKind", typeof(string));
        AssertProperty(detailsType, "ReceivedAt", typeof(DateTimeOffset?));
        AssertProperty(detailsType, "TimestampNs", typeof(long?));
        AssertProperty(detailsType, "BallCount", typeof(int?));
        AssertProperty(detailsType, "RobotCount", typeof(int?));
        AssertProperty(detailsType, "MissingReason", typeof(string));

        var viewStateType = RequiredVisionType("VisionLiveComparisonViewState");
        AssertEnumerableProperty(viewStateType, "LegendItems");
        AssertEnumerableProperty(viewStateType, "LayerDetails");
    }

    /// <summary>
    /// composer は store 更新前の render tick snapshot から source candidates を値として生成する。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonSnapshotComposer_CapturesImmutableRenderSnapshotAndCreatesSourceCandidates()
    {
        var store = new VisionPacketStore();
        var firstReceivedAt = new DateTimeOffset(2026, 5, 14, 8, 0, 0, TimeSpan.Zero);
        store.StorePacket(
            CreatePacket(cameraId: 1, frameNumber: 10, ballX: 120, ballY: -30, includeGeometry: true),
            new IPEndPoint(IPAddress.Loopback, 10006),
            firstReceivedAt);

        var composerType = RequiredVisionType("VisionLiveComparisonSnapshotComposer");
        var renderSnapshotType = RequiredVisionType("VisionLiveComparisonRenderSnapshot");
        var viewStateType = RequiredVisionType("VisionLiveComparisonViewState");
        var composer = CreateObject(composerType);

        var captureMethod = RequiredMethod(
            composerType,
            "CaptureRenderTickSnapshot",
            renderSnapshotType,
            typeof(DateTimeOffset),
            typeof(long),
            typeof(VisionPacketSnapshot),
            typeof(TrackedSnapshot),
            typeof(IReadOnlyList<ExternalTrackerReadSideSnapshot>));
        var createViewStateMethod = RequiredMethod(
            composerType,
            "CreateViewState",
            viewStateType,
            renderSnapshotType);
        var renderSnapshot = captureMethod.Invoke(
            composer,
            [
                firstReceivedAt,
                1L,
                store.GetSnapshot(),
                new TrackedSnapshot(null, null, "default", 0, 0),
                Array.Empty<ExternalTrackerReadSideSnapshot>(),
            ]);

        Assert.NotNull(renderSnapshot);
        Assert.Equal(firstReceivedAt, GetValue(GetSingle(GetValues(renderSnapshot!, "RawCameraSnapshots")), "ReceivedAt"));
        Assert.Equal(1, GetValue(GetSingle(GetValues(renderSnapshot!, "RawCameraSnapshots")), "CameraId"));
        Assert.Equal((uint)10, GetValue(GetValue(GetSingle(GetValues(renderSnapshot!, "RawCameraSnapshots")), "Detection")!, "FrameNumber"));
        Assert.True((bool)GetValue(renderSnapshot!, "IsImmutable")!);
        AssertGeometry(renderSnapshot!, "RawAggregate", "Raw Aggregate", fieldLength: 9000, fieldWidth: 6000);

        store.StorePacket(
            CreatePacket(cameraId: 2, frameNumber: 20, ballX: -440, ballY: 60, includeGeometry: false),
            new IPEndPoint(IPAddress.Loopback, 10007),
            firstReceivedAt.AddMilliseconds(40));

        var frozenCameras = GetValues(renderSnapshot!, "RawCameraSnapshots");
        var frozenCamera = GetSingle(frozenCameras);
        Assert.Single(frozenCameras);
        Assert.Equal(1, GetValue(frozenCamera, "CameraId"));
        Assert.Equal((uint)10, GetValue(GetValue(frozenCamera, "Detection")!, "FrameNumber"));

        var viewState = createViewStateMethod.Invoke(composer, [renderSnapshot]);
        Assert.NotNull(viewState);
        var options = GetValues(viewState!, "SourceOptions");
        AssertSourceOption(options, "RawAggregate", "raw:aggregate", "Raw Aggregate", cameraId: null, isAvailable: true, missingReason: "");
        AssertSourceOption(options, "RawCamera", "raw:camera:1", "Raw Camera 1", cameraId: 1, isAvailable: true, missingReason: "");
        Assert.DoesNotContain(options, option => Equals("raw:camera:2", GetValue(option, "Key")));
        AssertSourceOption(options, "Tracked", "tracked:ibis", "Tracked", cameraId: null, isAvailable: false, missingReason: "No tracked snapshot in current render tick.");
        AssertSourceOption(options, "ThirdPartyTracker", "third-party:default", "3rd party tracker", cameraId: null, isAvailable: false, missingReason: "No 3rd party tracker snapshot in current render tick.");
    }

    /// <summary>
    /// geometry は raw を優先し、raw geometry がまだ無い場合のみ tracked geometry に fallback する。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonSnapshotComposer_UsesTrackedGeometryOnlyWhenRawGeometryIsMissing()
    {
        var store = new VisionPacketStore();
        store.StorePacket(
            CreatePacket(cameraId: 1, frameNumber: 10, ballX: 120, ballY: -30, includeGeometry: false),
            new IPEndPoint(IPAddress.Loopback, 10006),
            new DateTimeOffset(2026, 5, 14, 8, 0, 0, TimeSpan.Zero));
        var trackedStore = new TrackedSnapshotStore();
        trackedStore.UpdateLatestFrame(
            CreateTrackedFrameWithGeometry(fieldLength: 12000, fieldWidth: 9000),
            new DateTimeOffset(2026, 5, 14, 8, 0, 0, TimeSpan.Zero).AddMilliseconds(10));

        var renderSnapshot = CaptureRenderSnapshot(store, trackedStore);

        AssertGeometry(renderSnapshot, "Tracked", "Tracked", fieldLength: 12000, fieldWidth: 9000);
    }

    /// <summary>
    /// raw/tracked geometry が無い場合、3rd party tracker packet から field geometry を復元しない。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonSnapshotComposer_WhenOnlyThirdPartyTrackerHasFrame_DoesNotUseItAsGeometrySource()
    {
        var store = new VisionPacketStore();
        store.StorePacket(
            CreatePacket(cameraId: 1, frameNumber: 10, ballX: 120, ballY: -30, includeGeometry: false),
            new IPEndPoint(IPAddress.Loopback, 10006),
            new DateTimeOffset(2026, 5, 14, 8, 0, 0, TimeSpan.Zero));
        var manager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis");
        manager.ProcessPacket(
            CreateExternalTrackerAdapter(fieldLength: 15000, fieldWidth: 10000),
            new IPEndPoint(IPAddress.Parse("192.0.2.10"), 12001),
            new DateTimeOffset(2026, 5, 14, 8, 0, 0, TimeSpan.Zero).AddMilliseconds(20));

        var renderSnapshot = CaptureRenderSnapshot(
            store,
            trackedStore: new TrackedSnapshotStore(),
            externalTrackerManager: manager);

        Assert.Null(GetValue(renderSnapshot, "Geometry"));
        Assert.Equal("Missing", GetValue(renderSnapshot, "GeometrySource"));
        Assert.NotEqual("ThirdPartyTracker", GetValue(renderSnapshot, "GeometrySource"));
        Assert.NotEqual("3rd party tracker", GetValue(renderSnapshot, "GeometrySourceLabel"));
    }

    /// <summary>
    /// 3rd party tracker は uuid が同じなら endpoint が違っても 1 source に統合し、最新受信 snapshot だけを代表描画する。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonSnapshotComposer_WhenThirdPartyTrackersShareUuid_CollapsesEndpointsToLatestRepresentative()
    {
        var store = new VisionPacketStore();
        var manager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis");
        var firstReceivedAt = new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
        manager.ProcessPacket(
            CreateExternalTrackerAdapter("ER-FORCE", "er-force-uuid", frameNumber: 101, timestampNs: 10_100, ballCount: 1, robotCount: 1),
            new IPEndPoint(IPAddress.Parse("192.0.2.11"), 12010),
            firstReceivedAt);
        manager.ProcessPacket(
            CreateExternalTrackerAdapter("ER-FORCE", "er-force-uuid", frameNumber: 102, timestampNs: 10_200, ballCount: 2, robotCount: 3),
            new IPEndPoint(IPAddress.Parse("192.0.2.12"), 12010),
            firstReceivedAt.AddMilliseconds(20));

        var composer = new VisionLiveComparisonSnapshotComposer();
        var renderSnapshot = CaptureRenderSnapshot(store, externalTrackerManager: manager);
        var viewState = composer.CreateViewState(renderSnapshot);

        var snapshot = Assert.Single(renderSnapshot.ThirdPartyTrackerSnapshots);
        Assert.Equal("third-party:uuid:er-force-uuid", snapshot.Key);
        Assert.Equal("ER-FORCE", snapshot.Label);
        Assert.Equal(firstReceivedAt.AddMilliseconds(20), snapshot.ReceivedAt);
        Assert.Equal(10_200, snapshot.TimestampNs);
        Assert.Equal(2, snapshot.Balls.Count);
        Assert.Equal(3, snapshot.RobotsYellow.Count + snapshot.RobotsBlue.Count);

        var option = Assert.Single(viewState.SourceOptions, option => option.Kind == VisionLiveComparisonSourceKind.ThirdPartyTracker);
        Assert.Equal("third-party:uuid:er-force-uuid", option.Key);
        Assert.Equal("ER-FORCE", option.Label);

        var overlayState = viewState with
        {
            Mode = VisionLiveComparisonMode.Overlay,
            LayerASelection = new VisionLiveComparisonLayerSelection(option, IsVisible: true),
            LayerBSelection = new VisionLiveComparisonLayerSelection(option, IsVisible: true),
        };
        var layer = Assert.Single(overlayState.CreateOverlayLayers());
        Assert.True(layer.IsSameSourceCollapsed);
        Assert.Equal(2, layer.Details.BallCount);
        Assert.Equal(3, layer.Details.RobotCount);
    }

    /// <summary>
    /// source name が同じでも uuid が違う 3rd party tracker は別 source とし、label で区別できる。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonSnapshotComposer_WhenThirdPartyTrackersShareLabelButDifferentUuid_KeepsDistinctOptionsWithDisambiguatedLabels()
    {
        var store = new VisionPacketStore();
        var manager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis");
        var receivedAt = new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
        manager.ProcessPacket(
            CreateExternalTrackerAdapter("ER-FORCE", "er-force-a", frameNumber: 201, timestampNs: 20_100, ballCount: 1, robotCount: 1),
            new IPEndPoint(IPAddress.Parse("192.0.2.21"), 12010),
            receivedAt);
        manager.ProcessPacket(
            CreateExternalTrackerAdapter("ER-FORCE", "er-force-b", frameNumber: 202, timestampNs: 20_200, ballCount: 1, robotCount: 1),
            new IPEndPoint(IPAddress.Parse("192.0.2.22"), 12010),
            receivedAt.AddMilliseconds(10));

        var composer = new VisionLiveComparisonSnapshotComposer();
        var viewState = composer.CreateViewState(CaptureRenderSnapshot(store, externalTrackerManager: manager));
        var options = viewState.SourceOptions
            .Where(option => option.Kind == VisionLiveComparisonSourceKind.ThirdPartyTracker)
            .OrderBy(option => option.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.Collection(
            options,
            option =>
            {
                Assert.Equal("third-party:uuid:er-force-a", option.Key);
                Assert.Equal("ER-FORCE (er-force-a)", option.Label);
            },
            option =>
            {
                Assert.Equal("third-party:uuid:er-force-b", option.Key);
                Assert.Equal("ER-FORCE (er-force-b)", option.Label);
            });
    }

    /// <summary>
    /// uuid が空の 3rd party tracker だけ、source name と endpoint fallback で識別する。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonSnapshotComposer_WhenThirdPartyTrackerUuidIsEmpty_UsesSourceNameAndEndpointFallback()
    {
        var store = new VisionPacketStore();
        var manager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis");
        var receivedAt = new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
        manager.ProcessPacket(
            CreateExternalTrackerAdapter("ER-FORCE", string.Empty, frameNumber: 301, timestampNs: 30_100, ballCount: 1, robotCount: 1),
            new IPEndPoint(IPAddress.Parse("192.0.2.31"), 12010),
            receivedAt);
        manager.ProcessPacket(
            CreateExternalTrackerAdapter("ER-FORCE", string.Empty, frameNumber: 302, timestampNs: 30_200, ballCount: 1, robotCount: 1),
            new IPEndPoint(IPAddress.Parse("192.0.2.32"), 12010),
            receivedAt.AddMilliseconds(10));

        var composer = new VisionLiveComparisonSnapshotComposer();
        var viewState = composer.CreateViewState(CaptureRenderSnapshot(store, externalTrackerManager: manager));
        var options = viewState.SourceOptions
            .Where(option => option.Kind == VisionLiveComparisonSourceKind.ThirdPartyTracker)
            .OrderBy(option => option.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.Collection(
            options,
            option =>
            {
                Assert.Equal("third-party:fallback:ER-FORCE\u001f192.0.2.31:12010", option.Key);
                Assert.Equal("ER-FORCE (192.0.2.31:12010)", option.Label);
            },
            option =>
            {
                Assert.Equal("third-party:fallback:ER-FORCE\u001f192.0.2.32:12010", option.Key);
                Assert.Equal("ER-FORCE (192.0.2.32:12010)", option.Label);
            });
    }

    /// <summary>
    /// overlay は Layer A/B が same-source の場合に 1 layer へ畳み、visibility を維持する。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonViewState_CreateOverlayLayers_WhenSameSource_CollapsesToSingleVisibleLayer()
    {
        var contract = CreateLayerContract();
        var rawAggregate = CreateSourceOption(
            contract.SourceOptionType,
            contract.SourceKindType,
            kindName: "RawAggregate",
            key: "raw:aggregate",
            label: "Raw Aggregate",
            cameraId: null,
            isAvailable: true,
            missingReason: "");
        var viewState = CreateViewState(
            contract,
            modeName: "Overlay",
            sourceOptions: [rawAggregate],
            layerASource: rawAggregate,
            layerAVisible: true,
            layerBSource: rawAggregate,
            layerBVisible: false);

        var layers = InvokeLayerMethod(viewState, "CreateOverlayLayers");
        var layer = GetSingle(layers);

        Assert.Equal("Layer A/B", GetValue(layer, "LayerName"));
        Assert.Equal("Ready", GetValue(layer, "Status")!.ToString());
        Assert.True((bool)GetValue(layer, "IsVisible")!);
        Assert.True((bool)GetValue(layer, "IsSameSourceCollapsed")!);
        Assert.Equal("Raw Aggregate", GetValue(layer, "SourceLabel"));
        Assert.Equal("", GetValue(layer, "MissingReason"));
        Assert.Equal(3001L, GetValue(layer, "RenderTickId"));
        Assert.Equal("#68d8ff", GetValue(layer, "AccentColor"));
        Assert.Equal("#68d8ff", GetValue(GetValue(layer, "Legend")!, "AccentColor"));
    }

    /// <summary>
    /// overlay は diagnostics と同じ考え方で Layer A/B の marker と legend swatch 用 accent color を view-state に渡す。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonViewState_CreateOverlayLayers_CarriesDiagnosticsAccentColors()
    {
        var contract = CreateLayerContract();
        var rawAggregate = CreateSourceOption(
            contract.SourceOptionType,
            contract.SourceKindType,
            kindName: "RawAggregate",
            key: "raw:aggregate",
            label: "Raw Aggregate",
            cameraId: null,
            isAvailable: true,
            missingReason: "");
        var tracked = CreateSourceOption(
            contract.SourceOptionType,
            contract.SourceKindType,
            kindName: "Tracked",
            key: "tracked:ibis",
            label: "Tracked",
            cameraId: null,
            isAvailable: true,
            missingReason: "");
        var rawCameraMissing = CreateSourceOption(
            contract.SourceOptionType,
            contract.SourceKindType,
            kindName: "RawCamera",
            key: "raw:camera:2",
            label: "Raw Camera 2",
            cameraId: 2,
            isAvailable: false,
            missingReason: "No raw camera 2 snapshot in current render tick.");
        var viewState = CreateViewState(
            contract,
            modeName: "Overlay",
            sourceOptions: [rawAggregate, tracked],
            layerASource: rawAggregate,
            layerAVisible: true,
            layerBSource: tracked,
            layerBVisible: true);

        var layers = InvokeLayerMethod(viewState, "CreateOverlayLayers");
        var layerA = Assert.Single(layers, layer => Equals("Layer A", GetValue(layer, "LayerName")));
        var layerB = Assert.Single(layers, layer => Equals("Layer B", GetValue(layer, "LayerName")));

        Assert.Equal("#68d8ff", GetValue(layerA, "AccentColor"));
        Assert.Equal("#68d8ff", GetValue(GetValue(layerA, "Legend")!, "AccentColor"));
        Assert.Equal("#ff7ad9", GetValue(layerB, "AccentColor"));
        Assert.Equal("#ff7ad9", GetValue(GetValue(layerB, "Legend")!, "AccentColor"));

        var missingViewState = CreateViewState(
            contract,
            modeName: "Overlay",
            sourceOptions: [rawAggregate, rawCameraMissing],
            layerASource: rawAggregate,
            layerAVisible: true,
            layerBSource: rawCameraMissing,
            layerBVisible: true);
        var missingLayers = InvokeLayerMethod(missingViewState, "CreateOverlayLayers");
        var missingLayer = Assert.Single(missingLayers, layer => Equals("Layer B", GetValue(layer, "LayerName")));
        Assert.Equal("Missing", GetValue(missingLayer, "Status")!.ToString());
        Assert.Equal("#ff7ad9", GetValue(missingLayer, "AccentColor"));
        Assert.Equal("#ff7ad9", GetValue(GetValue(missingLayer, "Legend")!, "AccentColor"));
    }

    /// <summary>
    /// split / overlay は片方の source が missing でも ready layer を残し、Layer A/B visibility を値として反映する。
    /// </summary>
    [Fact]
    public void VisionLiveComparisonViewState_CreateSplitAndOverlayLayers_WhenOneSourceMissing_KeepsReadyLayer()
    {
        var contract = CreateLayerContract();
        var rawAggregate = CreateSourceOption(
            contract.SourceOptionType,
            contract.SourceKindType,
            kindName: "RawAggregate",
            key: "raw:aggregate",
            label: "Raw Aggregate",
            cameraId: null,
            isAvailable: true,
            missingReason: "");
        var rawCameraMissing = CreateSourceOption(
            contract.SourceOptionType,
            contract.SourceKindType,
            kindName: "RawCamera",
            key: "raw:camera:2",
            label: "Raw Camera 2",
            cameraId: 2,
            isAvailable: false,
            missingReason: "No raw camera 2 snapshot in current render tick.");
        var viewState = CreateViewState(
            contract,
            modeName: "Split",
            sourceOptions: [rawAggregate, rawCameraMissing],
            layerASource: rawAggregate,
            layerAVisible: true,
            layerBSource: rawCameraMissing,
            layerBVisible: false);

        var splitLayers = InvokeLayerMethod(viewState, "CreateSplitLayers");
        var overlayLayers = InvokeLayerMethod(viewState, "CreateOverlayLayers");
        Assert.Equal(2, splitLayers.Count);
        Assert.Equal(2, overlayLayers.Count);

        var readyLayer = Assert.Single(overlayLayers, layer => Equals("Layer A", GetValue(layer, "LayerName")));
        Assert.Equal("Ready", GetValue(readyLayer, "Status")!.ToString());
        Assert.True((bool)GetValue(readyLayer, "IsVisible")!);
        Assert.False((bool)GetValue(readyLayer, "IsSameSourceCollapsed")!);
        Assert.Equal("Raw Aggregate", GetValue(readyLayer, "SourceLabel"));
        Assert.Equal("", GetValue(readyLayer, "MissingReason"));

        var missingLayer = Assert.Single(overlayLayers, layer => Equals("Layer B", GetValue(layer, "LayerName")));
        Assert.Equal("Missing", GetValue(missingLayer, "Status")!.ToString());
        Assert.False((bool)GetValue(missingLayer, "IsVisible")!);
        Assert.False((bool)GetValue(missingLayer, "IsSameSourceCollapsed")!);
        Assert.Equal("Raw Camera 2", GetValue(missingLayer, "SourceLabel"));
        Assert.Equal("No raw camera 2 snapshot in current render tick.", GetValue(missingLayer, "MissingReason"));
    }

    private static Type RequiredVisionType(string shortName)
    {
        var type = typeof(VisionPacketStore).Assembly.GetType($"Tracker.DebugHost.Vision.{shortName}");
        Assert.True(type is not null, $"Tracker.DebugHost.Vision.{shortName} must exist.");
        return type;
    }

    private static VisionLiveComparisonRenderSnapshot CaptureRenderSnapshot(
        VisionPacketStore store,
        TrackedSnapshotStore? trackedStore = null,
        MultiTrackerManager<TrackerPacketAdapter>? externalTrackerManager = null)
    {
        var composerType = RequiredVisionType("VisionLiveComparisonSnapshotComposer");
        var renderSnapshotType = RequiredVisionType("VisionLiveComparisonRenderSnapshot");
        var composer = CreateObject(composerType);
        var externalSnapshots = externalTrackerManager is null
            ? Array.Empty<ExternalTrackerReadSideSnapshot>()
            : new ExternalTrackerSnapshotStore(externalTrackerManager).GetSnapshot();
        var captureMethod = RequiredMethod(
            composerType,
            "CaptureRenderTickSnapshot",
            renderSnapshotType,
            typeof(DateTimeOffset),
            typeof(long),
            typeof(VisionPacketSnapshot),
            typeof(TrackedSnapshot),
            typeof(IReadOnlyList<ExternalTrackerReadSideSnapshot>));
        var renderSnapshot = captureMethod.Invoke(
            composer,
            [
                new DateTimeOffset(2026, 5, 14, 8, 0, 0, TimeSpan.Zero),
                1L,
                store.GetSnapshot(),
                trackedStore?.GetSnapshot() ?? new TrackedSnapshot(null, null, "default", 0, 0),
                externalSnapshots,
            ]);
        Assert.NotNull(renderSnapshot);
        return (VisionLiveComparisonRenderSnapshot)renderSnapshot!;
    }

    private static MethodInfo RequiredMethod(Type declaringType, string methodName, Type returnType, params Type[] parameterTypes)
    {
        var method = declaringType.GetMethod(methodName, parameterTypes);
        Assert.True(method is not null, $"{declaringType.Name}.{methodName}({string.Join(", ", parameterTypes.Select(type => type.Name))}) method must exist.");
        Assert.Equal(returnType, method!.ReturnType);
        return method;
    }

    private static void AssertProperty(Type declaringType, string propertyName, Type? propertyType = null)
    {
        var property = declaringType.GetProperty(propertyName);
        Assert.True(property is not null, $"{declaringType.Name}.{propertyName} property must exist.");
        if (propertyType is not null)
        {
            Assert.Equal(propertyType, property.PropertyType);
        }
    }

    private static void AssertEnumerableProperty(Type declaringType, string propertyName)
    {
        var property = declaringType.GetProperty(propertyName);
        Assert.True(property is not null, $"{declaringType.Name}.{propertyName} property must exist.");
        Assert.True(
            property!.PropertyType != typeof(string) &&
            typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType),
            $"{declaringType.Name}.{propertyName} must expose a stable enumerable snapshot.");
    }

    private static void AssertMethod(
        Type declaringType,
        string methodName,
        Type returnType,
        bool assertGenericDefinition = false)
    {
        var method = declaringType.GetMethod(methodName);
        Assert.True(method is not null, $"{declaringType.Name}.{methodName} method must exist.");
        var actualReturnType = method!.ReturnType;
        if (assertGenericDefinition)
        {
            Assert.True(actualReturnType.IsGenericType, $"{declaringType.Name}.{methodName} must return a generic collection.");
            Assert.Equal(returnType, actualReturnType.GetGenericTypeDefinition());
            return;
        }

        Assert.Equal(returnType, actualReturnType);
    }

    private static VisionLayerContract CreateLayerContract()
    {
        return new VisionLayerContract(
            RequiredVisionType("VisionLiveComparisonSourceKind"),
            RequiredVisionType("VisionLiveComparisonSourceOption"),
            RequiredVisionType("VisionLiveComparisonLayerSelection"),
            RequiredVisionType("VisionLiveComparisonRenderSnapshot"),
            RequiredVisionType("VisionLiveComparisonViewState"),
            RequiredVisionType("VisionLiveComparisonMode"));
    }

    private static object CreateViewState(
        VisionLayerContract contract,
        string modeName,
        object[] sourceOptions,
        object layerASource,
        bool layerAVisible,
        object layerBSource,
        bool layerBVisible)
    {
        var renderSnapshot = CreateRenderSnapshot(contract.RenderSnapshotType, renderTickId: 3001);
        return CreateObject(
            contract.ViewStateType,
            ("Mode", Enum.Parse(contract.ModeType, modeName)),
            ("RenderSnapshot", renderSnapshot),
            ("SourceOptions", CreateArray(contract.SourceOptionType, sourceOptions)),
            ("LayerASelection", CreateObject(
                contract.LayerSelectionType,
                ("Source", layerASource),
                ("IsVisible", layerAVisible))),
            ("LayerBSelection", CreateObject(
                contract.LayerSelectionType,
                ("Source", layerBSource),
                ("IsVisible", layerBVisible))));
    }

    private static object CreateRenderSnapshot(Type renderSnapshotType, long renderTickId)
    {
        return CreateObject(
            renderSnapshotType,
            ("SampledAt", new DateTimeOffset(2026, 5, 14, 8, 0, 1, TimeSpan.Zero)),
            ("RenderTickId", renderTickId),
            ("RawAggregateSnapshots", EmptyEnumerableForProperty(renderSnapshotType, "RawAggregateSnapshots")),
            ("RawCameraSnapshots", EmptyEnumerableForProperty(renderSnapshotType, "RawCameraSnapshots")),
            ("TrackedSnapshot", null),
            ("ThirdPartyTrackerSnapshots", EmptyEnumerableForProperty(renderSnapshotType, "ThirdPartyTrackerSnapshots")),
            ("Geometry", null),
            ("IsImmutable", true));
    }

    private static object CreateSourceOption(
        Type sourceOptionType,
        Type sourceKindType,
        string kindName,
        string key,
        string label,
        int? cameraId,
        bool isAvailable,
        string missingReason)
    {
        return CreateObject(
            sourceOptionType,
            ("Kind", Enum.Parse(sourceKindType, kindName)),
            ("Key", key),
            ("Label", label),
            ("CameraId", cameraId),
            ("IsAvailable", isAvailable),
            ("MissingReason", missingReason));
    }

    private static void AssertGeometry(
        object renderSnapshot,
        string expectedSource,
        string expectedSourceLabel,
        int fieldLength,
        int fieldWidth)
    {
        var geometry = GetValue(renderSnapshot, "Geometry");
        Assert.NotNull(geometry);
        var field = GetValue(geometry!, "Field");
        Assert.NotNull(field);
        Assert.Equal(fieldLength, GetValue(field!, "FieldLength"));
        Assert.Equal(fieldWidth, GetValue(field!, "FieldWidth"));
        Assert.Equal(expectedSource, GetValue(renderSnapshot, "GeometrySource"));
        Assert.Equal(expectedSourceLabel, GetValue(renderSnapshot, "GeometrySourceLabel"));
        Assert.NotEqual("ThirdPartyTracker", GetValue(renderSnapshot, "GeometrySource"));
    }

    private static IReadOnlyList<object> InvokeLayerMethod(object viewState, string methodName)
    {
        var method = viewState.GetType().GetMethod(methodName, Type.EmptyTypes);
        Assert.True(method is not null, $"{viewState.GetType().Name}.{methodName}() method must exist.");
        return ToObjectList(method!.Invoke(viewState, []), $"{viewState.GetType().Name}.{methodName}()");
    }

    private static object CreateObject(Type type, params (string Name, object? Value)[] values)
    {
        var valueMap = values.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .ToArray();

        foreach (var constructor in constructors)
        {
            if (!TryBuildConstructorArguments(constructor, values, valueMap, out var arguments))
            {
                continue;
            }

            var instance = constructor.Invoke(arguments);
            SetWritableProperties(instance, valueMap);
            return instance;
        }

        if (type.GetConstructor(Type.EmptyTypes) is { } defaultConstructor)
        {
            var instance = defaultConstructor.Invoke([]);
            SetWritableProperties(instance, valueMap);
            return instance;
        }

        Assert.Fail($"{type.Name} must expose a public constructor that accepts: {string.Join(", ", valueMap.Keys)}.");
        throw new InvalidOperationException("Unreachable.");
    }

    private static bool TryBuildConstructorArguments(
        ConstructorInfo constructor,
        (string Name, object? Value)[] values,
        IReadOnlyDictionary<string, object?> valueMap,
        out object?[] arguments)
    {
        var parameters = constructor.GetParameters();
        arguments = new object?[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            if (valueMap.TryGetValue(parameter.Name ?? "", out var value))
            {
                arguments[index] = CoerceValue(value, parameter.ParameterType);
                continue;
            }

            var typeMatch = values
                .Where(entry => entry.Value is not null && parameter.ParameterType.IsInstanceOfType(entry.Value))
                .Select(entry => entry.Value)
                .Take(2)
                .ToArray();
            if (typeMatch.Length == 1)
            {
                arguments[index] = typeMatch[0];
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                arguments[index] = parameter.DefaultValue;
                continue;
            }

            if (!parameter.ParameterType.IsValueType || Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
            {
                arguments[index] = null;
                continue;
            }

            return false;
        }

        return true;
    }

    private static void SetWritableProperties(object instance, IReadOnlyDictionary<string, object?> valueMap)
    {
        foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || !valueMap.TryGetValue(property.Name, out var value))
            {
                continue;
            }

            property.SetValue(instance, CoerceValue(value, property.PropertyType));
        }
    }

    private static object? CoerceValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        var nullableTarget = Nullable.GetUnderlyingType(targetType);
        if (nullableTarget is not null)
        {
            return CoerceValue(value, nullableTarget);
        }

        if (targetType.IsEnum && value is string enumName)
        {
            return Enum.Parse(targetType, enumName);
        }

        return value;
    }

    private static object EmptyEnumerableForProperty(Type declaringType, string propertyName)
    {
        var property = declaringType.GetProperty(propertyName);
        Assert.True(property is not null, $"{declaringType.Name}.{propertyName} property must exist.");
        return Array.CreateInstance(GetEnumerableElementType(property!.PropertyType) ?? typeof(object), 0);
    }

    private static object CreateArray(Type elementType, IReadOnlyList<object> values)
    {
        var array = Array.CreateInstance(elementType, values.Count);
        for (var index = 0; index < values.Count; index++)
        {
            array.SetValue(values[index], index);
        }

        return array;
    }

    private static Type? GetEnumerableElementType(Type enumerableType)
    {
        if (enumerableType.IsArray)
        {
            return enumerableType.GetElementType();
        }

        if (enumerableType.IsGenericType && enumerableType.GetGenericArguments().Length == 1)
        {
            return enumerableType.GetGenericArguments()[0];
        }

        return enumerableType
            .GetInterfaces()
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(type => type.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    private static IReadOnlyList<object> GetValues(object source, string propertyName)
    {
        return ToObjectList(GetValue(source, propertyName), $"{source.GetType().Name}.{propertyName}");
    }

    private static IReadOnlyList<object> ToObjectList(object? value, string sourceName)
    {
        Assert.True(value is IEnumerable, $"{sourceName} must return an enumerable value.");
        return ((IEnumerable)value!).Cast<object>().ToArray();
    }

    private static object GetSingle(IReadOnlyList<object> values)
    {
        return Assert.Single(values);
    }

    private static object? GetValue(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        Assert.True(property is not null, $"{source.GetType().Name}.{propertyName} property must exist.");
        return property!.GetValue(source);
    }

    private static void AssertSourceOption(
        IReadOnlyList<object> options,
        string kindName,
        string key,
        string label,
        int? cameraId,
        bool isAvailable,
        string missingReason)
    {
        var option = Assert.Single(options, candidate => Equals(key, GetValue(candidate, "Key")));
        Assert.Equal(kindName, GetValue(option, "Kind")!.ToString());
        Assert.Equal(label, GetValue(option, "Label"));
        Assert.Equal(cameraId, GetValue(option, "CameraId"));
        Assert.Equal(isAvailable, GetValue(option, "IsAvailable"));
        Assert.Equal(missingReason, GetValue(option, "MissingReason"));
    }

    private static SSL_WrapperPacket CreatePacket(
        uint cameraId,
        uint frameNumber,
        float ballX,
        float ballY,
        bool includeGeometry)
    {
        var packet = new SSL_WrapperPacket
        {
            Detection = new SSL_DetectionFrame
            {
                CameraId = cameraId,
                FrameNumber = frameNumber,
                Balls =
                {
                    new SSL_DetectionBall
                    {
                        Confidence = 0.9f,
                        X = ballX,
                        Y = ballY,
                    },
                },
            },
        };

        if (includeGeometry)
        {
            packet.Geometry = new SSL_GeometryData
            {
                Field = new SSL_GeometryFieldSize
                {
                    FieldLength = 9000,
                    FieldWidth = 6000,
                },
            };
        }

        return packet;
    }

    private static TrackerFrame CreateTrackedFrameWithGeometry(int fieldLength, int fieldWidth)
    {
        return new TrackerFrame
        {
            FrameNumber = 900,
            DataTimestampNs = 9_000_000_000,
            ProcessedAtNs = 9_001_000_000,
            Balls = [],
            Robots = [],
            PrimaryBallTrackId = null,
            GeometrySnapshot = new TrackerGeometrySnapshot
            {
                FieldLengthMm = fieldLength,
                FieldWidthMm = fieldWidth,
                GoalWidthMm = 1800,
                GoalDepthMm = 200,
                BoundaryWidthMm = 300,
                BoundaryWidthGoalLineMm = 350,
                PenaltyAreaDepthMm = 1200,
                PenaltyAreaWidthMm = 2400,
                CenterCircleRadiusMm = 600,
                LineThicknessMm = 10,
            },
            Metadata = new TrackerFrameMetadata
            {
                ProfileName = "tracked",
            },
        };
    }

    private static TrackerPacketAdapter CreateExternalTrackerAdapter(int fieldLength, int fieldWidth)
    {
        var packet = Fixture
            .CreatePacketGenerator(sourceName: "third-party-live", uuid: "third-party-uuid")
            .Generate(CreateTrackedFrameWithGeometry(fieldLength, fieldWidth));
        return new TrackerPacketAdapter(packet);
    }

    private static TrackerPacketAdapter CreateExternalTrackerAdapter(
        string sourceName,
        string uuid,
        uint frameNumber,
        long timestampNs,
        int ballCount,
        int robotCount)
    {
        var balls = Enumerable.Range(0, ballCount)
            .Select(index => Fixture.CreateTrackedBall(
                trackId: index + 1,
                xMm: 100 + (index * 100),
                yMm: 200 + (index * 100)))
            .ToArray();
        var robots = Enumerable.Range(0, robotCount)
            .Select(index => new TrackedRobotState
            {
                Team = index % 2 == 0 ? TrackerTeam.Yellow : TrackerTeam.Blue,
                RobotId = (uint)(index + 1),
                XMm = 1200 + (index * 100),
                YMm = -300 - (index * 100),
                Visibility = 1.0f,
            })
            .ToArray();
        var frame = Fixture.CreateFrame(
            frameNumber: frameNumber,
            dataTimestampNs: timestampNs,
            balls: balls,
            robots: robots,
            primaryBallTrackId: ballCount > 0 ? 1 : null);
        var packet = Fixture.CreatePacketGenerator(sourceName: sourceName, uuid: uuid).Generate(frame);
        return new TrackerPacketAdapter(packet);
    }

    private sealed record VisionLayerContract(
        Type SourceKindType,
        Type SourceOptionType,
        Type LayerSelectionType,
        Type RenderSnapshotType,
        Type ViewStateType,
        Type ModeType);
}
