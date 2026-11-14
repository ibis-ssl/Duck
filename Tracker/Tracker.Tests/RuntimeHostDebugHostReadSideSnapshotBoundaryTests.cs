using System.Net;
using Microsoft.Extensions.Configuration;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Tests.Contracts;
using TrackerConnectionLib;

namespace Tracker.Tests;

/// <summary>
/// RUNTIME-HOST-006 の DebugHost live display read-side snapshot 境界を固定する。
/// </summary>
public class RuntimeHostDebugHostReadSideSnapshotBoundaryTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public RuntimeHostDebugHostReadSideSnapshotBoundaryTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// 何を確認しているか: Home live display は raw/tracked store を直接 inject せず composite provider だけを読む。
    /// </summary>
    [Fact]
    public void HomeLiveDisplay_DoesNotInjectRawOrTrackedStoresDirectly()
    {
        var homeSource = File.ReadAllText(RepositoryPath("Tracker", "Tracker.DebugHost", "Components", "Pages", "Home.razor"));

        Assert.Contains("@inject VisionLiveDisplaySnapshotProvider", homeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("@inject VisionPacketStore", homeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("@inject TrackedSnapshotStore", homeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Store.GetSnapshot(", homeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrackedStore.GetSnapshot(", homeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeLiveComparison_UsesTheConfiguredTrackerComparisonGate()
    {
        var homeSource = File.ReadAllText(RepositoryPath("Tracker", "Tracker.DebugHost", "Components", "Pages", "Home.razor"));

        Assert.Contains("TrackerComparison:BallDistanceGateMeters", homeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Tracker:Comparison:BallDistanceGateMeters", homeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveComparisonProvider_UsesConfiguredNonDefaultBallDistanceGate()
    {
        var receivedAt = DateTimeOffset.UtcNow;
        var trackedStore = new TrackedSnapshotStore();
        trackedStore.UpdateLatestFrame(CreateTrackedFrame(frameNumber: 10, dataTimestampNs: 10_000, ballX: 0), receivedAt);
        var manager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis");
        manager.ProcessPacket(
            CreateExternalAdapter("TIGERs", "tigers-id", frameNumber: 30, timestampNs: 30_000, ballX: 3_000),
            new IPEndPoint(IPAddress.Loopback, 12010), receivedAt);
        using var externalStore = new ExternalTrackerSnapshotStore(manager);
        var provider = new VisionLiveDisplaySnapshotProvider(
            new VisionPacketStore(), trackedStore, externalStore, new VisionLiveComparisonSnapshotComposer());
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TrackerComparison:BallDistanceGateMeters"] = "5",
            })
            .Build();

        var renderSnapshot = provider.CaptureRenderTickSnapshot();
        var tigerKey = Assert.Single(renderSnapshot.ComparisonSnapshot.ThirdPartyTrackerSnapshots).Key;
        var state = provider.CreateComparisonViewState(
            renderSnapshot,
            VisionLiveComparisonMode.Split,
            "tracked:ibis",
            tigerKey,
            layerAVisible: true,
            layerBVisible: true,
            ballDistanceGateMeters: double.Parse(
                configuration["TrackerComparison:BallDistanceGateMeters"]!,
                System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(5, state.BallDistanceGateMeters);
        Assert.Equal(TrackerDifferencePairStatus.Compared, state.TrackerDifference.Status);
        Assert.Single(state.TrackerDifference.Difference!.Balls.Matches);
    }

    [Fact]
    public void LiveComparisonProvider_PreservesSelectedThirdPartySourceWhenUnavailableAndRestoresIt()
    {
        var receivedAt = DateTimeOffset.UtcNow;
        var trackedStore = new TrackedSnapshotStore();
        trackedStore.UpdateLatestFrame(CreateTrackedFrame(frameNumber: 10, dataTimestampNs: 10_000, ballX: 0), receivedAt);

        var initialManager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis");
        initialManager.ProcessPacket(
            CreateExternalAdapter("TIGERs", "tigers-id", frameNumber: 30, timestampNs: 30_000, ballX: 100),
            new IPEndPoint(IPAddress.Loopback, 12010), receivedAt);
        initialManager.ProcessPacket(
            CreateExternalAdapter("ER-FORCE", "erforce-id", frameNumber: 40, timestampNs: 40_000, ballX: 200),
            new IPEndPoint(IPAddress.Loopback, 12011), receivedAt);
        using var initialStore = new ExternalTrackerSnapshotStore(initialManager);
        var initialProvider = new VisionLiveDisplaySnapshotProvider(
            new VisionPacketStore(), trackedStore, initialStore, new VisionLiveComparisonSnapshotComposer());
        var initialSnapshot = initialProvider.CaptureRenderTickSnapshot();
        var tigerKey = Assert.Single(initialSnapshot.ComparisonSnapshot.ThirdPartyTrackerSnapshots,
            source => source.Label == "TIGERs").Key;
        var initialState = initialProvider.CreateComparisonViewState(
            initialSnapshot, VisionLiveComparisonMode.Split, "tracked:ibis", tigerKey, true, true);

        var missingManager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis");
        missingManager.ProcessPacket(
            CreateExternalAdapter("ER-FORCE", "erforce-id", frameNumber: 41, timestampNs: 41_000, ballX: 200),
            new IPEndPoint(IPAddress.Loopback, 12011), receivedAt.AddMilliseconds(1));
        using var missingStore = new ExternalTrackerSnapshotStore(missingManager);
        var missingProvider = new VisionLiveDisplaySnapshotProvider(
            new VisionPacketStore(), trackedStore, missingStore, new VisionLiveComparisonSnapshotComposer());
        var missingState = missingProvider.CreateComparisonViewState(
            missingProvider.CaptureRenderTickSnapshot(),
            VisionLiveComparisonMode.Split,
            "tracked:ibis",
            tigerKey,
            layerAVisible: true,
            layerBVisible: true,
            fallbackLayerBSource: initialState.LayerBSelection.Source);

        Assert.Equal(tigerKey, initialState.LayerBSelection.Source.Key);
        Assert.Equal(tigerKey, missingState.LayerBSelection.Source.Key);
        Assert.Equal(VisionLiveComparisonSourceKind.ThirdPartyTracker, missingState.LayerBSelection.Source.Kind);
        Assert.False(missingState.LayerBSelection.Source.IsAvailable);
        Assert.Contains(missingState.SourceOptions, option => option.Key == tigerKey && !option.IsAvailable);
        Assert.Equal(TrackerDifferencePairStatus.MissingComparisonFrame, missingState.TrackerDifference.Status);

        var restoredManager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis");
        restoredManager.ProcessPacket(
            CreateExternalAdapter("TIGERs", "tigers-id", frameNumber: 31, timestampNs: 31_000, ballX: 100),
            new IPEndPoint(IPAddress.Loopback, 12010), receivedAt.AddMilliseconds(2));
        restoredManager.ProcessPacket(
            CreateExternalAdapter("ER-FORCE", "erforce-id", frameNumber: 42, timestampNs: 42_000, ballX: 200),
            new IPEndPoint(IPAddress.Loopback, 12011), receivedAt.AddMilliseconds(2));
        using var restoredStore = new ExternalTrackerSnapshotStore(restoredManager);
        var restoredProvider = new VisionLiveDisplaySnapshotProvider(
            new VisionPacketStore(), trackedStore, restoredStore, new VisionLiveComparisonSnapshotComposer());
        var restoredState = restoredProvider.CreateComparisonViewState(
            restoredProvider.CaptureRenderTickSnapshot(), VisionLiveComparisonMode.Split, "tracked:ibis", tigerKey, true, true);

        Assert.Equal(tigerKey, restoredState.LayerBSelection.Source.Key);
        Assert.True(restoredState.LayerBSelection.Source.IsAvailable);
        Assert.Equal(TrackerDifferencePairStatus.Compared, restoredState.TrackerDifference.Status);
    }

    /// <summary>
    /// 何を確認しているか: 1 render tick の raw / tracked / external snapshot は capture 後の更新から隔離される。
    /// </summary>
    [Fact]
    public void LiveDisplayProvider_CapturesRawTrackedAndExternalSnapshotsOncePerRenderTick()
    {
        var rawStore = new VisionPacketStore();
        var trackedStore = new TrackedSnapshotStore();
        var externalManager = new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis");
        var externalStore = new ExternalTrackerSnapshotStore(externalManager);
        var provider = new VisionLiveDisplaySnapshotProvider(
            rawStore,
            trackedStore,
            externalStore,
            new VisionLiveComparisonSnapshotComposer());
        var firstReceivedAt = new DateTimeOffset(2026, 5, 14, 18, 20, 0, TimeSpan.Zero);

        rawStore.StorePacket(
            CreateRawPacket(cameraId: 1, frameNumber: 10, ballX: 100),
            new IPEndPoint(IPAddress.Loopback, 10006),
            firstReceivedAt);
        trackedStore.UpdateLatestFrame(
            CreateTrackedFrame(frameNumber: 20, dataTimestampNs: 20_000, ballX: 200),
            firstReceivedAt.AddMilliseconds(1));
        externalManager.ProcessPacket(
            CreateExternalAdapter("third-party-a", "third-party-a-uuid", frameNumber: 30, timestampNs: 30_000, ballX: 300),
            new IPEndPoint(IPAddress.Parse("192.0.2.10"), 12010),
            firstReceivedAt.AddMilliseconds(2));

        var renderSnapshot = provider.CaptureRenderTickSnapshot();
        var frozenComparison = renderSnapshot.ComparisonSnapshot;
        Assert.NotNull(frozenComparison.TrackedSnapshot);
        var frozenTrackedLayer = frozenComparison.TrackedSnapshot;
        var frozenExternalLayer = Assert.Single(frozenComparison.ThirdPartyTrackerSnapshots);

        rawStore.StorePacket(
            CreateRawPacket(cameraId: 2, frameNumber: 11, ballX: 900),
            new IPEndPoint(IPAddress.Loopback, 10007),
            firstReceivedAt.AddMilliseconds(20));
        trackedStore.UpdateLatestFrame(
            CreateTrackedFrame(frameNumber: 21, dataTimestampNs: 21_000, ballX: 950),
            firstReceivedAt.AddMilliseconds(21));
        externalManager.ProcessPacket(
            CreateExternalAdapter("third-party-a", "third-party-a-uuid", frameNumber: 31, timestampNs: 31_000, ballX: 990),
            new IPEndPoint(IPAddress.Parse("192.0.2.11"), 12010),
            firstReceivedAt.AddMilliseconds(22));

        Assert.Equal((uint)10, renderSnapshot.RawSnapshot.Detection?.FrameNumber);
        Assert.Equal((uint)20, renderSnapshot.TrackedSnapshot.LatestFrame?.FrameNumber);
        Assert.Equal((uint)10, Assert.Single(frozenComparison.RawCameraSnapshots).Detection.FrameNumber);
        Assert.Equal(20_000, frozenTrackedLayer.TimestampNs);
        Assert.Equal(30_000, frozenExternalLayer.TimestampNs);
        Assert.Equal(300, Assert.Single(frozenExternalLayer.Balls).X);

        var comparisonViewState = provider.CreateComparisonViewState(
            renderSnapshot,
            VisionLiveComparisonMode.Split,
            "raw:aggregate",
            "third-party:uuid:third-party-a-uuid",
            layerAVisible: true,
            layerBVisible: true);
        var externalDetail = Assert.Single(
            comparisonViewState.LayerDetails,
            detail => detail.SourceKind == VisionLiveComparisonSourceKind.ThirdPartyTracker.ToString());

        Assert.Equal(30_000, externalDetail.TimestampNs);
        Assert.Equal(1, externalDetail.BallCount);
    }

    /// <summary>
    /// 何を確認しているか: live display / comparison read-side source は tracker operation loop API を参照しない。
    /// </summary>
    [Fact]
    public void LiveDisplayAndComparisonSource_DoNotReferenceTrackerOperationLoopApis()
    {
        var sourceText = string.Join(
            Environment.NewLine,
            new[]
            {
                RepositoryPath("Tracker", "Tracker.DebugHost", "Components", "Pages", "Home.razor"),
                RepositoryPath("Tracker", "Tracker.DebugHost", "Vision", "VisionLiveDisplaySnapshotProvider.cs"),
                RepositoryPath("Tracker", "Tracker.DebugHost", "Vision", "VisionLiveComparisonViewState.cs"),
            }.Select(File.ReadAllText));
        var forbiddenTokens = new[]
        {
            "TrackerCoordinator",
            "ITrackerEngine",
            "TrackerPacketGenerator",
            "ITrackerPacketPublisher",
            "ProcessPacket(",
        };
        var hits = forbiddenTokens
            .Where(token => sourceText.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            hits.Length == 0,
            $"DebugHost live display/comparison read-side source must not drive tracker operation loop. Found: {string.Join(", ", hits)}");
    }

    private SSL_WrapperPacket CreateRawPacket(uint cameraId, uint frameNumber, float ballX)
    {
        return new SSL_WrapperPacket
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
                    },
                },
            },
            Geometry = new SSL_GeometryData
            {
                Field = new SSL_GeometryFieldSize
                {
                    FieldLength = 9000,
                    FieldWidth = 6000,
                },
            },
        };
    }

    private TrackerFrame CreateTrackedFrame(uint frameNumber, long dataTimestampNs, double ballX)
    {
        return fixture.CreateFrame(
            frameNumber: frameNumber,
            dataTimestampNs: dataTimestampNs,
            balls:
            [
                fixture.CreateTrackedBall(trackId: 1, xMm: ballX, yMm: 0),
            ],
            robots: [],
            primaryBallTrackId: 1);
    }

    private TrackerPacketAdapter CreateExternalAdapter(
        string sourceName,
        string uuid,
        uint frameNumber,
        long timestampNs,
        double ballX)
    {
        var frame = fixture.CreateFrame(
            frameNumber: frameNumber,
            dataTimestampNs: timestampNs,
            balls:
            [
                fixture.CreateTrackedBall(trackId: 1, xMm: ballX, yMm: 0),
            ],
            robots: [],
            primaryBallTrackId: 1);
        var packet = fixture.CreatePacketGenerator(sourceName: sourceName, uuid: uuid).Generate(frame);
        return new TrackerPacketAdapter(packet);
    }

    private static string RepositoryPath(params string[] segments)
    {
        return Path.Combine([FindRepositoryRoot(), .. segments]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Tracker", "Tracker.Tests", "Tracker.Tests.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing Tracker/Tracker.Tests/Tracker.Tests.csproj was not found.");
    }
}
