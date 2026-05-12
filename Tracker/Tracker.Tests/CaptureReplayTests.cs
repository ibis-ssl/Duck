using System.IO.Compression;
using System.Text.Json;
using Tracker.Core;
using Tracker.CaptureReplay;
using Tracker.Server.Tracking;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: Tracker.CaptureReplay の CLI parse と detail 出力の調査用 contract を検証する。
/// </summary>
public class CaptureReplayTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public CaptureReplayTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// 何を確認しているか: detail filter が committed frame number で対象 frame を絞り込めることを確認する。
    /// </summary>
    [Fact]
    public void Parse_AllowsFrameDetailFilter()
    {
        var options = ReplayOptions.Parse(["--capture", "capture.jsonl.gz", "--detail-filter", "frame==1234"]);

        var filter = Assert.Single(options.DetailFilters);
        Assert.Null(options.Error);
        Assert.Equal("frame", filter.Metric);
        Assert.Equal(ComparisonOperator.Equal, filter.Operator);
        Assert.Equal(1234, filter.Expected);
    }

    /// <summary>
    /// 何を確認しているか: robot detail に向きと角速度が出力され、replay CLI だけで姿勢差分を調査できることを確認する。
    /// </summary>
    [Fact]
    public void FormatFrame_IncludesRobotOrientationAndAngularVelocity()
    {
        var frame = new TrackerFrame
        {
            FrameNumber = 1234,
            DataTimestampNs = 1_000_000_000,
            Robots =
            [
                new TrackedRobotState
                {
                    Team = TrackerTeam.Blue,
                    RobotId = 3,
                    XMm = 120.2,
                    YMm = -340.4,
                    OrientationRad = -1.569,
                    AngularVelocityRadPerS = 0.42,
                    Visibility = 1.0f,
                    Quality = 0.05,
                },
            ],
        };

        var detail = ReplayFrameFormatter.FormatFrame(
            packetIndex: 42,
            receivedAt: new DateTimeOffset(2026, 5, 11, 13, 31, 13, TimeSpan.Zero),
            frame);

        Assert.Contains("committedFrame=1234", detail);
        Assert.Contains("B3:x=120.2,y=-340.4,o=-1.569,w=0.420,vis=1.000", detail);
    }

    /// <summary>
    /// 何を確認しているか: detail frame ごとの robot 表示件数を CLI option で増やせることを確認する。
    /// </summary>
    [Fact]
    public void Parse_AllowsMaxDetailRobots()
    {
        var options = ReplayOptions.Parse(["--capture", "capture.jsonl.gz", "--max-detail-robots", "32"]);

        Assert.Null(options.Error);
        Assert.Equal(32, options.MaxDetailRobots);
    }

    /// <summary>
    /// 何を確認しているか: maxDetailRobots により省略されていた後続 robot まで detail 出力できることを確認する。
    /// </summary>
    [Fact]
    public void FormatFrame_UsesConfiguredRobotDetailLimit()
    {
        var robots = new List<TrackedRobotState>();
        for (uint robotId = 0; robotId <= 17; robotId++)
        {
            robots.Add(new TrackedRobotState
            {
                Team = TrackerTeam.Blue,
                RobotId = robotId,
                Visibility = 1.0f,
            });
        }

        var frame = new TrackerFrame
        {
            FrameNumber = 1234,
            Robots = robots,
        };

        var detail = ReplayFrameFormatter.FormatFrame(
            packetIndex: 42,
            receivedAt: new DateTimeOffset(2026, 5, 11, 13, 31, 13, TimeSpan.Zero),
            frame,
            maxDetailRobots: 18);

        Assert.Contains("B17:x=0.0,y=0.0,o=0.000,w=0.000,vis=1.000", detail);
        Assert.DoesNotContain("... +", detail);
    }

    /// <summary>
    /// 何を確認しているか: appsettings 形状の replay settings から Kalman scale 系設定と robot 向き tuning を失わずに復元できることを確認する。
    /// </summary>
    [Fact]
    public void CreateSettings_FromAppsettingsShapePreservesKalmanScalesAndRobotOrientationTuning()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"capture-replay-settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "Tracker": {
                    "Profiles": {
                      "sim": {
                        "Engine": {
                          "ReorderWindowNs": 123,
                          "MergeWindowNs": 456,
                          "KalmanInitialVelocityVariance": 111,
                          "KalmanProcessNoiseScale": 222,
                          "MeasurementNoiseVarianceScale": 333
                        },
                        "RobotTracker": {
                          "IdentitySwitchDistanceMm": 145,
                          "OrientationMeasurementNoiseRad": 0.07,
                          "OrientationProcessNoise": 0.08,
                          "InitialAngularVelocityVariance": 12,
                          "AngularVelocityLimitRadPerS": 5.0
                        }
                      }
                    }
                  }
                }
                """);

            var settings = TrackerSettingsFactory.Create(
                "sim",
                settingsPath,
                new TrackerSettingOverrides(
                    BallGate: null,
                    BallOutlierLimitMm: null,
                    BallOutputVisibility: null,
                    BallTrackLifetimeNs: null,
                    MergeWindowNs: null,
                    ReorderWindowNs: null));

            Assert.Equal(123, settings.ReorderWindowNs);
            Assert.Equal(456, settings.MergeWindowNs);
            Assert.Equal(111d, settings.KalmanInitialVelocityVariance);
            Assert.Equal(222d, settings.KalmanProcessNoiseScale);
            Assert.Equal(333d, settings.MeasurementNoiseVarianceScale);
            Assert.Equal(145d, settings.RobotTracker.IdentitySwitchDistanceMm);
            Assert.Equal(0.07d, settings.RobotTracker.OrientationMeasurementNoiseRad);
            Assert.Equal(0.08d, settings.RobotTracker.OrientationProcessNoise);
            Assert.Equal(12d, settings.RobotTracker.InitialAngularVelocityVariance);
            Assert.Equal(5.0d, settings.RobotTracker.AngularVelocityLimitRadPerS);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    /// <summary>
    /// 何を確認しているか: capture metadata の解決済み settings を CLI override に通しても Kalman scale 系設定が保持されることを確認する。
    /// </summary>
    [Fact]
    public void CreateSettings_FromResolvedMetadataPreservesKalmanScalesWhenApplyingCliOverrides()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"capture-replay-metadata-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "ResolvedTrackerOptions": {
                    "EngineSettings": {
                      "ProfileName": "sim",
                      "ReorderWindowNs": 123,
                      "MergeWindowNs": 456,
                      "KalmanInitialVelocityVariance": 111,
                      "KalmanProcessNoiseScale": 222,
                      "MeasurementNoiseVarianceScale": 333,
                      "RobotTracker": {
                        "IdentitySwitchDistanceMm": 145,
                        "OrientationMeasurementNoiseRad": 0.07,
                        "OrientationProcessNoise": 0.08,
                        "InitialAngularVelocityVariance": 12,
                        "AngularVelocityLimitRadPerS": 5.0
                      }
                    }
                  }
                }
                """);

            var settings = TrackerSettingsFactory.Create(
                "sim",
                settingsPath,
                new TrackerSettingOverrides(
                    BallGate: null,
                    BallOutlierLimitMm: null,
                    BallOutputVisibility: null,
                    BallTrackLifetimeNs: null,
                    MergeWindowNs: 789,
                    ReorderWindowNs: null));

            Assert.Equal(123, settings.ReorderWindowNs);
            Assert.Equal(789, settings.MergeWindowNs);
            Assert.Equal(111d, settings.KalmanInitialVelocityVariance);
            Assert.Equal(222d, settings.KalmanProcessNoiseScale);
            Assert.Equal(333d, settings.MeasurementNoiseVarianceScale);
            Assert.Equal(145d, settings.RobotTracker.IdentitySwitchDistanceMm);
            Assert.Equal(0.07d, settings.RobotTracker.OrientationMeasurementNoiseRad);
            Assert.Equal(0.08d, settings.RobotTracker.OrientationProcessNoise);
            Assert.Equal(12d, settings.RobotTracker.InitialAngularVelocityVariance);
            Assert.Equal(5.0d, settings.RobotTracker.AngularVelocityLimitRadPerS);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    /// <summary>
    /// 何を確認しているか: CaptureReplay が metadata relative path から tracker snapshot sidecar を読み、比較表示に必要な source / count / raw payload / nearest summary を返すことを確認する。
    /// </summary>
    [Fact]
    public void Run_WithMetadataSnapshotSidecar_ReturnsTrackerSnapshotComparisonLines()
    {
        var session = CreateSnapshotReplaySession();
        var settings = TrackerSettingsFactory.Create(
            "sim",
            settingsPath: null,
            new TrackerSettingOverrides(
                BallGate: null,
                BallOutlierLimitMm: null,
                BallOutputVisibility: null,
                BallTrackLifetimeNs: null,
                MergeWindowNs: null,
                ReorderWindowNs: null));

        var summary = CaptureReplayRunner.Run(
            session.CapturePath,
            settings,
            [],
            maxDetails: 40,
            maxDetailRobots: 16,
            metadataPath: session.MetadataPath);

        Assert.Contains(
            "trackerSnapshot source=thirdparty-replay role=external trackedFrame=8101 trackedTs=88001000000 balls=1 robots=1 rawPayloadRestored=True",
            summary.TrackerSnapshotLines);
        Assert.Contains(
            "trackerComparison rule=nearest-timestamp ibisTs=88000000000 source=thirdparty-replay role=external nearestTs=88001000000 balls=1 robots=1 rawPayloadRestored=True",
            summary.TrackerSnapshotLines);
    }

    /// <summary>
    /// 何を確認しているか: metadata に snapshot sidecar がない既存 capture / diagnostics / render snapshot では追加比較行を出さず、既存 replay summary を維持することを確認する。
    /// </summary>
    [Fact]
    public void Run_WithLegacyMetadataWithoutSnapshotSidecar_KeepsExistingReplaySummary()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"capture-replay-legacy-{Guid.NewGuid():N}");
        var sessionFolder = Path.Combine(captureDirectory, "legacy-session");
        Directory.CreateDirectory(sessionFolder);
        var capturePath = Path.Combine(sessionFolder, "packets.jsonl.gz");
        WriteEmptyCapture(capturePath);
        var metadataPath = Path.Combine(sessionFolder, "legacy-session.metadata.json");
        File.WriteAllText(
            metadataPath,
            JsonSerializer.Serialize(new
            {
                SessionFolder = "legacy-session",
                PacketPath = Path.Combine("legacy-session", Path.GetFileName(capturePath)),
                DiagnosticsLogPath = Path.Combine("legacy-session", "legacy-session.tracker-diagnostics.log"),
                RenderSnapshotPath = Path.Combine("legacy-session", "legacy-session.render-snapshots.jsonl.gz"),
            }));
        var settings = TrackerSettingsFactory.Create(
            "sim",
            settingsPath: null,
            new TrackerSettingOverrides(
                BallGate: null,
                BallOutlierLimitMm: null,
                BallOutputVisibility: null,
                BallTrackLifetimeNs: null,
                MergeWindowNs: null,
                ReorderWindowNs: null));

        var summary = CaptureReplayRunner.Run(
            capturePath,
            settings,
            [],
            maxDetails: 40,
            maxDetailRobots: 16,
            metadataPath: metadataPath);

        Assert.Equal(0, summary.PacketCount);
        Assert.Empty(summary.TrackerSnapshotLines);
    }

    private SnapshotReplaySession CreateSnapshotReplaySession()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"capture-replay-snapshot-{Guid.NewGuid():N}");
        var sessionFolder = "snapshot-session";
        var sessionFolderPath = Path.Combine(captureDirectory, sessionFolder);
        Directory.CreateDirectory(sessionFolderPath);

        var capturePath = Path.Combine(sessionFolderPath, "packets.jsonl.gz");
        WriteEmptyCapture(capturePath);

        var ownPacket = CreateTrackerPacket("ibis-runtime", "ibis", 8100, 88_000_000_000);
        var externalPacket = CreateTrackerPacket("external-replay", "thirdparty-replay", 8101, 88_001_000_000);
        var sidecarPath = Path.Combine(sessionFolderPath, TrackerPacketSnapshotLogReader.SidecarFileName);
        File.WriteAllLines(
            sidecarPath,
            [
                JsonSerializer.Serialize(TrackerPacketSnapshotRecord.FromPacket(
                    ownPacket,
                    new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero),
                    remoteEndpoint: "self",
                    sourceRole: "own",
                    sourceLabel: "ibis")),
                JsonSerializer.Serialize(TrackerPacketSnapshotRecord.FromPacket(
                    externalPacket,
                    new DateTimeOffset(2026, 5, 12, 12, 0, 1, TimeSpan.Zero),
                    remoteEndpoint: "192.0.2.77:10010",
                    sourceRole: "external",
                    sourceLabel: "thirdparty-replay")),
            ]);

        var diagnosticsPath = Path.Combine(sessionFolderPath, "snapshot-session.tracker-diagnostics.log");
        File.WriteAllText(
            diagnosticsPath,
            $"2026-05-12T12:00:00.0000000+00:00 Tracker diagnostics profile=sim rawFrame=8001 rawCamera=0 rawBalls=1 rawBallDetails=[x=100,y=200,z=0,c=1] rawBlue=[] rawYellow=[] trackedFrame=8100 trackedBalls=1 trackedBallDetails=[#1:x=100,y=200,z=0,vis=1,q=1,cams=0] trackedRobots=1 trackedRobotDetails=[Y3:x=1200,y=-300,o=0,w=0,vis=1,q=1] ballOutVisibility=0 ballHalfLifeSec=1 ballLifetimeNs=1000000000{Environment.NewLine}");

        var metadataPath = Path.Combine(sessionFolderPath, "snapshot-session.metadata.json");
        File.WriteAllText(
            metadataPath,
            JsonSerializer.Serialize(new
            {
                SessionFolder = sessionFolder,
                PacketPath = Path.Combine(sessionFolder, Path.GetFileName(capturePath)),
                DiagnosticsLogPath = Path.Combine(sessionFolder, Path.GetFileName(diagnosticsPath)),
                RenderSnapshotPath = Path.Combine(sessionFolder, "snapshot-session.render-snapshots.jsonl.gz"),
                TrackerSnapshotSidecarPath = Path.Combine(sessionFolder, TrackerPacketSnapshotLogReader.SidecarFileName),
                TrackerSnapshotLog = new
                {
                    Format = "jsonl",
                    IsCreated = true,
                    RecordCount = 2,
                    SkippedRecordCount = 0,
                    ErrorCount = 0,
                },
            }));

        return new SnapshotReplaySession(capturePath, metadataPath);
    }

    private TrackerWrapperPacket CreateTrackerPacket(
        string sourceUuid,
        string sourceName,
        uint frameNumber,
        long timestampNs)
    {
        var frame = fixture.CreateFrame(
            frameNumber: frameNumber,
            dataTimestampNs: timestampNs,
            balls:
            [
                fixture.CreateTrackedBall(trackId: 10, xMm: 100, yMm: 200),
            ],
            robots:
            [
                new TrackedRobotState { Team = TrackerTeam.Yellow, RobotId = 3, XMm = 1200, YMm = -300 },
            ],
            primaryBallTrackId: 10);
        return fixture.CreatePacketGenerator(sourceName, sourceUuid).Generate(frame);
    }

    private static void WriteEmptyCapture(string capturePath)
    {
        using var fileStream = File.Create(capturePath);
        using var gzipStream = new GZipStream(fileStream, CompressionLevel.SmallestSize);
        using var writer = new StreamWriter(gzipStream);
    }

    private sealed record SnapshotReplaySession(string CapturePath, string MetadataPath);
}
