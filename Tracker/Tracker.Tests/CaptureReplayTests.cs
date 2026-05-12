using Tracker.Core;
using Tracker.CaptureReplay;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: Tracker.CaptureReplay の CLI parse と detail 出力の調査用 contract を検証する。
/// </summary>
public class CaptureReplayTests
{
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
}
