using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: Tracker contract fixture が contract test 用の安定した既定値と生成物を返すことを検証する。
/// </summary>
public class TrackerContractFixtureTests
{
    /// <summary>
    /// 何を確認しているか: CreateSettings が contract test 用の安定した default profile と timing / Kalman 設定を返すことを確認する。
    /// </summary>
    [Fact]
    public void CreateSettings_ProvidesStableDefaultsForContractTests()
    {
        var fixture = new TrackerContractFixture();

        var settings = fixture.CreateSettings();

        Assert.Equal("default", settings.ProfileName);
        Assert.Equal(100_000_000L, settings.ReorderWindowNs);
        Assert.Equal(20_000_000L, settings.MergeWindowNs);
        Assert.Equal(500, settings.GeometryResetFieldLengthThresholdMm);
        Assert.Equal(500, settings.GeometryResetFieldWidthThresholdMm);
        Assert.Equal(10_000d, settings.KalmanInitialVelocityVariance);
        Assert.Equal(10_000d, settings.KalmanProcessNoiseScale);
        Assert.Equal(100d, settings.MeasurementNoiseVarianceScale);
    }

    /// <summary>
    /// 何を確認しているか: profile switch request に指定 profile と解決済み settings が反映されることを確認する。
    /// </summary>
    [Fact]
    public void CreateProfileSwitchRequest_UsesProvidedProfileAndResolvedSettings()
    {
        var fixture = new TrackerContractFixture();

        var request = fixture.CreateProfileSwitchRequest(requestVersion: 2, profileName: "fast");

        Assert.Equal(2, request.RequestVersion);
        Assert.Equal("fast", request.ProfileName);
        Assert.Equal("fast", request.ResolvedBaseSettings.ProfileName);
        Assert.NotNull(request.RuntimeOverrides);
    }

    /// <summary>
    /// 何を確認しているか: packet generator が contract fixture の固定 source metadata を使うことを確認する。
    /// </summary>
    [Fact]
    public void CreatePacketGenerator_UsesStableContractBootstrapMetadata()
    {
        var fixture = new TrackerContractFixture();

        var generator = fixture.CreatePacketGenerator();

        Assert.Equal("test-source", generator.SourceName);
        Assert.Equal("test-uuid", generator.Uuid);
    }

    /// <summary>
    /// 何を確認しているか: fixture の CreateEngine が Tracker.Core の engine 実装を返すことを確認する。
    /// </summary>
    [Fact]
    public void CreateEngine_ReturnsTrackerCoreEngine()
    {
        var fixture = new TrackerContractFixture();

        Assert.IsType<TrackerEngine>(fixture.CreateEngine());
    }
}
