using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerContractFixtureTests
{
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

    [Fact]
    public void CreatePacketGenerator_UsesStableContractBootstrapMetadata()
    {
        var fixture = new TrackerContractFixture();

        var generator = fixture.CreatePacketGenerator();

        Assert.Equal("test-source", generator.SourceName);
        Assert.Equal("test-uuid", generator.Uuid);
    }

    [Fact]
    public void CreateEngine_ReturnsTrackerCoreEngine()
    {
        var fixture = new TrackerContractFixture();

        Assert.IsType<TrackerEngine>(fixture.CreateEngine());
    }
}
