using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Core;
using Tracker.Server.Tracking;

namespace Tracker.Tests;

public class TrackerConfigurationBindingTests
{
    /// <summary>
    /// 何を確認しているか: active profile と runtime override が解決済み Tracker 設定へ反映されること。
    /// </summary>
    [Fact]
    public void Resolve_UsesActiveProfileAndRuntimeOverrides()
    {
        var configuration = CreateTrackerConfiguration();

        var options = configuration.GetSection("Tracker").Get<TrackerOptions>();
        var resolved = TrackerConfigurationResolver.Resolve(Assert.IsType<TrackerOptions>(options));

        Assert.True(resolved.Enabled);
        Assert.Equal("simulation", resolved.EngineSettings.ProfileName);
        Assert.Equal(123, resolved.EngineSettings.ReorderWindowNs);
        Assert.Equal(45, resolved.EngineSettings.MergeWindowNs);
        Assert.Equal(678, resolved.EngineSettings.GeometryResetFieldLengthThresholdMm);
        Assert.Equal(910, resolved.EngineSettings.GeometryResetFieldWidthThresholdMm);
        Assert.Equal(111d, resolved.EngineSettings.KalmanInitialVelocityVariance);
        Assert.Equal(222d, resolved.EngineSettings.KalmanProcessNoiseScale);
        Assert.Equal(333d, resolved.EngineSettings.MeasurementNoiseVarianceScale);
        Assert.Equal(1.5d, resolved.EngineSettings.RobotTracker.ProcessNoise);
        Assert.Equal(0.8d, resolved.EngineSettings.RobotTracker.MeasurementNoise);
        Assert.Equal(1.4d, resolved.EngineSettings.RobotTracker.VisibilityHalfLifeSeconds);
        Assert.Equal(0.2d, resolved.EngineSettings.RobotTracker.OutputVisibilityThreshold);
        Assert.Equal(2.0d, resolved.EngineSettings.RobotTracker.Gate);
        Assert.Equal(350d, resolved.EngineSettings.RobotTracker.OutlierLimitMm);
        Assert.Equal(145d, resolved.EngineSettings.RobotTracker.IdentitySwitchDistanceMm);
        Assert.Equal(0.07d, resolved.EngineSettings.RobotTracker.OrientationMeasurementNoiseRad);
        Assert.Equal(0.09d, resolved.EngineSettings.RobotTracker.OrientationProcessNoise);
        Assert.Equal(12d, resolved.EngineSettings.RobotTracker.InitialAngularVelocityVariance);
        Assert.Equal(4.5d, resolved.EngineSettings.RobotTracker.AngularVelocityLimitRadPerS);
        Assert.Equal(1.7d, resolved.EngineSettings.BallTracker.ProcessNoise);
        Assert.Equal(0.6d, resolved.EngineSettings.BallTracker.MeasurementNoise);
        Assert.Equal(0.7d, resolved.EngineSettings.BallTracker.VisibilityHalfLifeSeconds);
        Assert.Equal(0.4d, resolved.EngineSettings.BallTracker.OutputVisibilityThreshold);
        Assert.Equal(1.8d, resolved.EngineSettings.BallTracker.Gate);
        Assert.Equal(500d, resolved.EngineSettings.BallTracker.OutlierLimitMm);
        Assert.Equal(2_200_000_000L, resolved.EngineSettings.BallTracker.TrackLifetimeNs);
        Assert.Equal(2600d, resolved.EngineSettings.KickDetector.KickSpeedThresholdMmPerS);
        Assert.Equal(125d, resolved.EngineSettings.KickDetector.ChipHeightThresholdMm);
        Assert.Equal(30d, resolved.EngineSettings.KickDetector.ContactMarginMm);
        Assert.Equal("239.1.2.3", resolved.PublisherOptions.MulticastAddress);
        Assert.Equal(13000, resolved.PublisherOptions.Port);
        Assert.Equal("runtime-source", resolved.PublisherOptions.SourceName);
        Assert.Equal("runtime-uuid", resolved.PublisherOptions.Uuid);
        Assert.False(resolved.PublisherOptions.PublishUdp);
        Assert.Equal("tracker-diagnostics-test.log", resolved.Diagnostics.FilePath);
    }

    /// <summary>
    /// 何を確認しているか: active profile が存在しない場合に設定解決が profile 名付きで失敗すること。
    /// </summary>
    [Fact]
    public void Resolve_WithMissingActiveProfile_Throws()
    {
        var options = new TrackerOptions
        {
            ActiveProfileName = "missing",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => TrackerConfigurationResolver.Resolve(options));

        Assert.Contains("missing", ex.Message);
    }

    /// <summary>
    /// 何を確認しているか: DI 登録から解決済み engine / publisher / diagnostics 設定を取得できること。
    /// </summary>
    [Fact]
    public void StartupRegistrations_ExposeResolvedEngineAndPublisherSettings()
    {
        var configuration = CreateTrackerConfiguration();
        var services = new ServiceCollection();

        services.AddOptions();
        services.Configure<TrackerOptions>(configuration.GetSection("Tracker"));
        services.AddSingleton(serviceProvider =>
            TrackerConfigurationResolver.Resolve(serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TrackerOptions>>().Value));
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<TrackerResolvedOptions>().EngineSettings);
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<TrackerResolvedOptions>().PublisherOptions);
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<TrackerResolvedOptions>().Diagnostics);

        using var serviceProvider = services.BuildServiceProvider();

        var engineSettings = serviceProvider.GetRequiredService<TrackerEngineSettings>();
        var publisherOptions = serviceProvider.GetRequiredService<TrackerPublisherOptions>();
        var diagnostics = serviceProvider.GetRequiredService<TrackerDiagnosticsOptions>();

        Assert.Equal("simulation", engineSettings.ProfileName);
        Assert.Equal(111d, engineSettings.KalmanInitialVelocityVariance);
        Assert.Equal(222d, engineSettings.KalmanProcessNoiseScale);
        Assert.Equal(333d, engineSettings.MeasurementNoiseVarianceScale);
        Assert.Equal(1.4d, engineSettings.RobotTracker.VisibilityHalfLifeSeconds);
        Assert.Equal(0.2d, engineSettings.RobotTracker.OutputVisibilityThreshold);
        Assert.Equal(350d, engineSettings.RobotTracker.OutlierLimitMm);
        Assert.Equal(145d, engineSettings.RobotTracker.IdentitySwitchDistanceMm);
        Assert.Equal(0.07d, engineSettings.RobotTracker.OrientationMeasurementNoiseRad);
        Assert.Equal(0.09d, engineSettings.RobotTracker.OrientationProcessNoise);
        Assert.Equal(12d, engineSettings.RobotTracker.InitialAngularVelocityVariance);
        Assert.Equal(4.5d, engineSettings.RobotTracker.AngularVelocityLimitRadPerS);
        Assert.Equal(0.7d, engineSettings.BallTracker.VisibilityHalfLifeSeconds);
        Assert.Equal(0.4d, engineSettings.BallTracker.OutputVisibilityThreshold);
        Assert.Equal(1.8d, engineSettings.BallTracker.Gate);
        Assert.Equal(2_200_000_000L, engineSettings.BallTracker.TrackLifetimeNs);
        Assert.Equal(30d, engineSettings.KickDetector.ContactMarginMm);
        Assert.Equal("239.1.2.3", publisherOptions.MulticastAddress);
        Assert.Equal(13000, publisherOptions.Port);
        Assert.Equal("runtime-source", publisherOptions.SourceName);
        Assert.Equal("runtime-uuid", publisherOptions.Uuid);
        Assert.Equal("tracker-diagnostics-test.log", diagnostics.FilePath);
    }

    /// <summary>
    /// 何を確認しているか: appsettings.json が Tigers 準拠の Tracker 既定 profile と解決値を公開していること。
    /// </summary>
    [Fact]
    public void AppsettingsJson_ExposesTigersAlignedTrackerDefaults()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(repositoryRoot)
            .AddJsonFile("Tracker/Tracker.Server/appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var options = configuration.GetSection("Tracker").Get<TrackerOptions>();
        var trackerOptions = Assert.IsType<TrackerOptions>(options);

        Assert.Equal("sim", trackerOptions.ActiveProfileName);
        Assert.Null(trackerOptions.Diagnostics.FilePath);
        AssertTigersAlignedProfile(trackerOptions.Profiles["default"], expectedPublishPort: 10010, expectedGate: 1.0d);
        AssertTigersAlignedProfile(trackerOptions.Profiles["sim"], expectedPublishPort: 11010, expectedGate: 1.0d);
        AssertTigersAlignedProfile(trackerOptions.Profiles["fast"], expectedPublishPort: 10011, expectedGate: 0.85d);

        var resolved = TrackerConfigurationResolver.Resolve(trackerOptions);

        Assert.Equal("sim", resolved.EngineSettings.ProfileName);
        Assert.Equal(11010, resolved.PublisherOptions.Port);
        Assert.Equal(10_000d, resolved.EngineSettings.KalmanInitialVelocityVariance);
        Assert.Equal(10_000d, resolved.EngineSettings.KalmanProcessNoiseScale);
        Assert.Equal(100d, resolved.EngineSettings.MeasurementNoiseVarianceScale);
        Assert.Equal(0.462756d, resolved.EngineSettings.RobotTracker.VisibilityHalfLifeSeconds);
        Assert.Equal(0.05d, resolved.EngineSettings.RobotTracker.OutputVisibilityThreshold);
        Assert.Equal(TrackerEngineSettings.DefaultRobotOrientationMeasurementNoiseRad, resolved.EngineSettings.RobotTracker.OrientationMeasurementNoiseRad);
        Assert.Equal(TrackerEngineSettings.DefaultRobotOrientationProcessNoise, resolved.EngineSettings.RobotTracker.OrientationProcessNoise);
        Assert.Equal(TrackerEngineSettings.DefaultRobotInitialAngularVelocityVariance, resolved.EngineSettings.RobotTracker.InitialAngularVelocityVariance);
        Assert.Equal(TrackerEngineSettings.DefaultRobotAngularVelocityLimitRadPerS, resolved.EngineSettings.RobotTracker.AngularVelocityLimitRadPerS);
        Assert.Equal(TrackerEngineSettings.DefaultRobotIdentitySwitchDistanceMm, resolved.EngineSettings.RobotTracker.IdentitySwitchDistanceMm);
        Assert.Equal(1_000_000_000L, resolved.EngineSettings.BallTracker.TrackLifetimeNs);
    }

    private static IConfigurationRoot CreateTrackerConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Tracker:Enabled"] = "true",
                    ["Tracker:PublishUdp"] = "false",
                    ["Tracker:SourceName"] = "configured-source",
                    ["Tracker:Uuid"] = "configured-uuid",
                    ["Tracker:ActiveProfileName"] = "simulation",
                    ["Tracker:Diagnostics:FilePath"] = "tracker-diagnostics-test.log",
                    ["Tracker:Profiles:simulation:Publish:MulticastAddress"] = "239.1.2.3",
                    ["Tracker:Profiles:simulation:Publish:Port"] = "12000",
                    ["Tracker:Profiles:simulation:Engine:ReorderWindowNs"] = "123",
                    ["Tracker:Profiles:simulation:Engine:MergeWindowNs"] = "45",
                    ["Tracker:Profiles:simulation:Engine:GeometryResetFieldLengthThresholdMm"] = "678",
                    ["Tracker:Profiles:simulation:Engine:GeometryResetFieldWidthThresholdMm"] = "910",
                    ["Tracker:Profiles:simulation:Engine:KalmanInitialVelocityVariance"] = "111",
                    ["Tracker:Profiles:simulation:Engine:KalmanProcessNoiseScale"] = "222",
                    ["Tracker:Profiles:simulation:Engine:MeasurementNoiseVarianceScale"] = "333",
                    ["Tracker:Profiles:simulation:RobotTracker:ProcessNoise"] = "1.5",
                    ["Tracker:Profiles:simulation:RobotTracker:MeasurementNoise"] = "0.8",
                    ["Tracker:Profiles:simulation:RobotTracker:VisibilityHalfLifeSeconds"] = "1.2",
                    ["Tracker:Profiles:simulation:RobotTracker:OutputVisibilityThreshold"] = "0.1",
                    ["Tracker:Profiles:simulation:RobotTracker:Gate"] = "2.0",
                    ["Tracker:Profiles:simulation:RobotTracker:OutlierLimitMm"] = "300",
                    ["Tracker:Profiles:simulation:RobotTracker:IdentitySwitchDistanceMm"] = "140",
                    ["Tracker:Profiles:simulation:RobotTracker:OrientationMeasurementNoiseRad"] = "0.07",
                    ["Tracker:Profiles:simulation:RobotTracker:OrientationProcessNoise"] = "0.08",
                    ["Tracker:Profiles:simulation:RobotTracker:InitialAngularVelocityVariance"] = "12",
                    ["Tracker:Profiles:simulation:RobotTracker:AngularVelocityLimitRadPerS"] = "5.0",
                    ["Tracker:Profiles:simulation:BallTracker:ProcessNoise"] = "1.7",
                    ["Tracker:Profiles:simulation:BallTracker:MeasurementNoise"] = "0.6",
                    ["Tracker:Profiles:simulation:BallTracker:VisibilityHalfLifeSeconds"] = "0.9",
                    ["Tracker:Profiles:simulation:BallTracker:OutputVisibilityThreshold"] = "0.3",
                    ["Tracker:Profiles:simulation:BallTracker:Gate"] = "2.5",
                    ["Tracker:Profiles:simulation:BallTracker:OutlierLimitMm"] = "500",
                    ["Tracker:Profiles:simulation:BallTracker:TrackLifetimeNs"] = "2000000000",
                    ["Tracker:Profiles:simulation:KickDetector:KickSpeedThresholdMmPerS"] = "2500",
                    ["Tracker:Profiles:simulation:KickDetector:ChipHeightThresholdMm"] = "125",
                    ["Tracker:Profiles:simulation:KickDetector:ContactMarginMm"] = "25",
                    ["Tracker:RuntimeOverrides:Publish:Port"] = "13000",
                    ["Tracker:RuntimeOverrides:Publish:SourceName"] = "runtime-source",
                    ["Tracker:RuntimeOverrides:Publish:Uuid"] = "runtime-uuid",
                    ["Tracker:RuntimeOverrides:RobotTracker:VisibilityHalfLifeSeconds"] = "1.4",
                    ["Tracker:RuntimeOverrides:RobotTracker:OutputVisibilityThreshold"] = "0.2",
                    ["Tracker:RuntimeOverrides:RobotTracker:OutlierLimitMm"] = "350",
                    ["Tracker:RuntimeOverrides:RobotTracker:IdentitySwitchDistanceMm"] = "145",
                    ["Tracker:RuntimeOverrides:RobotTracker:OrientationProcessNoise"] = "0.09",
                    ["Tracker:RuntimeOverrides:RobotTracker:AngularVelocityLimitRadPerS"] = "4.5",
                    ["Tracker:RuntimeOverrides:BallTracker:VisibilityHalfLifeSeconds"] = "0.7",
                    ["Tracker:RuntimeOverrides:BallTracker:OutputVisibilityThreshold"] = "0.4",
                    ["Tracker:RuntimeOverrides:BallTracker:Gate"] = "1.8",
                    ["Tracker:RuntimeOverrides:BallTracker:TrackLifetimeNs"] = "2200000000",
                    ["Tracker:RuntimeOverrides:KickDetector:KickSpeedThresholdMmPerS"] = "2600",
                    ["Tracker:RuntimeOverrides:KickDetector:ContactMarginMm"] = "30",
                })
            .Build();
    }

    private static void AssertTigersAlignedProfile(
        TrackerProfileOptions profile,
        int expectedPublishPort,
        double expectedGate)
    {
        Assert.Equal(expectedPublishPort, profile.Publish.Port);
        Assert.Equal(10_000d, profile.Engine.KalmanInitialVelocityVariance);
        Assert.Equal(10_000d, profile.Engine.KalmanProcessNoiseScale);
        Assert.Equal(100d, profile.Engine.MeasurementNoiseVarianceScale);
        Assert.Equal(0.1d, profile.RobotTracker.ProcessNoise);
        Assert.Equal(20.0d, profile.RobotTracker.MeasurementNoise);
        Assert.Equal(0.462756d, profile.RobotTracker.VisibilityHalfLifeSeconds);
        Assert.Equal(0.05d, profile.RobotTracker.OutputVisibilityThreshold);
        Assert.Equal(expectedGate, profile.RobotTracker.Gate);
        Assert.Equal(TrackerEngineSettings.DefaultRobotIdentitySwitchDistanceMm, profile.RobotTracker.IdentitySwitchDistanceMm);
        Assert.Equal(TrackerEngineSettings.DefaultRobotOrientationMeasurementNoiseRad, profile.RobotTracker.OrientationMeasurementNoiseRad);
        Assert.Equal(TrackerEngineSettings.DefaultRobotOrientationProcessNoise, profile.RobotTracker.OrientationProcessNoise);
        Assert.Equal(TrackerEngineSettings.DefaultRobotInitialAngularVelocityVariance, profile.RobotTracker.InitialAngularVelocityVariance);
        Assert.Equal(TrackerEngineSettings.DefaultRobotAngularVelocityLimitRadPerS, profile.RobotTracker.AngularVelocityLimitRadPerS);
        Assert.Equal(0.1d, profile.BallTracker.ProcessNoise);
        Assert.Equal(100.0d, profile.BallTracker.MeasurementNoise);
        Assert.Equal(1.0d, profile.BallTracker.VisibilityHalfLifeSeconds);
        Assert.Equal(0.0d, profile.BallTracker.OutputVisibilityThreshold);
        Assert.Equal(expectedGate, profile.BallTracker.Gate);
        Assert.Equal(1_000_000_000L, profile.BallTracker.TrackLifetimeNs);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Tracker", "Tracker.Server", "appsettings.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tracker/Tracker.Server/appsettings.json.");
    }
}
