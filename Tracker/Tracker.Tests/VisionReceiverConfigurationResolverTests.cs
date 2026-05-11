using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;

namespace Tracker.Tests;

public class VisionReceiverConfigurationResolverTests
{
    /// <summary>
    /// 何を確認しているか: Tracker active profile と一致する VisionReceiver profile の値を使用すること。
    /// </summary>
    [Fact]
    public void Resolve_WithMatchingProfile_UsesProfileSpecificValues()
    {
        var options = new VisionReceiverOptions
        {
            MulticastAddress = "224.5.23.2",
            Port = 10020,
            InterfaceAddress = "192.168.10.2",
            Profiles = new Dictionary<string, VisionReceiverProfileOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["sim"] = new()
                {
                    MulticastAddress = "239.0.0.2",
                    Port = 12020,
                    InterfaceAddress = "10.0.0.5",
                },
            },
        };

        var resolved = VisionReceiverConfigurationResolver.Resolve(options, "sim");

        Assert.Equal("239.0.0.2", resolved.MulticastAddress);
        Assert.Equal(12020, resolved.Port);
        Assert.Equal("10.0.0.5", resolved.InterfaceAddress);
    }

    /// <summary>
    /// 何を確認しているか: 一致する profile がない場合に top-level の VisionReceiver 設定へ fallback すること。
    /// </summary>
    [Fact]
    public void Resolve_WithoutMatchingProfile_FallsBackToTopLevelValues()
    {
        var options = new VisionReceiverOptions
        {
            MulticastAddress = "224.5.23.2",
            Port = 10020,
            InterfaceAddress = "192.168.10.2",
        };

        var resolved = VisionReceiverConfigurationResolver.Resolve(options, "missing");

        Assert.Equal("224.5.23.2", resolved.MulticastAddress);
        Assert.Equal(10020, resolved.Port);
        Assert.Equal("192.168.10.2", resolved.InterfaceAddress);
    }

    /// <summary>
    /// 何を確認しているか: runtime options store の設定更新が既存 snapshot の change token を cancel すること。
    /// </summary>
    [Fact]
    public void RuntimeOptionsStore_ApplyConfiguration_CancelsPreviousSnapshot()
    {
        var store = new VisionReceiverRuntimeOptionsStore(
            new VisionReceiverResolvedOptions
            {
                MulticastAddress = "224.5.23.2",
                Port = 10020,
                InterfaceAddress = null,
            });

        var firstSnapshot = store.GetSnapshot();

        store.ApplyConfiguration(
            new VisionReceiverResolvedOptions
            {
                MulticastAddress = "239.0.0.2",
                Port = 12020,
                InterfaceAddress = "10.0.0.5",
            });

        Assert.True(firstSnapshot.ChangeToken.IsCancellationRequested);

        var secondSnapshot = store.GetSnapshot();
        Assert.Equal("239.0.0.2", secondSnapshot.Options.MulticastAddress);
        Assert.Equal(12020, secondSnapshot.Options.Port);
        Assert.Equal("10.0.0.5", secondSnapshot.Options.InterfaceAddress);
    }

    /// <summary>
    /// 何を確認しているか: profile switch observer が一致する receiver profile を runtime store へ反映すること。
    /// </summary>
    [Fact]
    public void ProfileSwitchObserver_OnProfileSwitched_AppliesMatchingReceiverProfile()
    {
        var store = new VisionReceiverRuntimeOptionsStore(
            new VisionReceiverResolvedOptions
            {
                MulticastAddress = "224.5.23.2",
                Port = 10020,
            });
        var observer = new VisionReceiverProfileSwitchObserver(
            Options.Create(new VisionReceiverOptions
            {
                MulticastAddress = "224.5.23.2",
                Port = 10020,
                Profiles = new Dictionary<string, VisionReceiverProfileOptions>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sim"] = new()
                    {
                        MulticastAddress = "239.0.0.2",
                        Port = 12020,
                    },
                },
            }),
            store);

        observer.OnProfileSwitched("sim");

        var snapshot = store.GetSnapshot();
        Assert.Equal("239.0.0.2", snapshot.Options.MulticastAddress);
        Assert.Equal(12020, snapshot.Options.Port);
    }

    /// <summary>
    /// 何を確認しているか: startup 登録が Tracker active profile から receiver profile を解決すること。
    /// </summary>
    [Fact]
    public void StartupRegistrations_ResolveReceiverProfileFromTrackerActiveProfile()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Tracker:ActiveProfileName"] = "sim",
                    ["VisionReceiver:MulticastAddress"] = "224.5.23.2",
                    ["VisionReceiver:Port"] = "10020",
                    ["VisionReceiver:Profiles:sim:MulticastAddress"] = "239.0.0.2",
                    ["VisionReceiver:Profiles:sim:Port"] = "12020",
                    ["VisionReceiver:Profiles:sim:InterfaceAddress"] = "10.0.0.5",
                })
            .Build();
        var services = new ServiceCollection();

        services.AddOptions();
        services.Configure<TrackerOptions>(configuration.GetSection("Tracker"));
        services.Configure<VisionReceiverOptions>(configuration.GetSection("VisionReceiver"));
        services.AddSingleton(serviceProvider =>
        {
            var trackerOptions = serviceProvider.GetRequiredService<IOptions<TrackerOptions>>().Value;
            var visionOptions = serviceProvider.GetRequiredService<IOptions<VisionReceiverOptions>>().Value;
            return new VisionReceiverRuntimeOptionsStore(
                VisionReceiverConfigurationResolver.Resolve(visionOptions, trackerOptions.ActiveProfileName));
        });

        using var serviceProvider = services.BuildServiceProvider();

        var snapshot = serviceProvider.GetRequiredService<VisionReceiverRuntimeOptionsStore>().GetSnapshot();

        Assert.Equal("239.0.0.2", snapshot.Options.MulticastAddress);
        Assert.Equal(12020, snapshot.Options.Port);
        Assert.Equal("10.0.0.5", snapshot.Options.InterfaceAddress);
    }

    /// <summary>
    /// 何を確認しているか: appsettings.json が packet capture の既定値を公開していること。
    /// </summary>
    [Fact]
    public void AppsettingsJson_ExposesPacketCaptureDefaults()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(repositoryRoot)
            .AddJsonFile("Tracker/Tracker.Server/appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var options = configuration.GetSection("VisionReceiver").Get<VisionReceiverOptions>();
        var receiverOptions = Assert.IsType<VisionReceiverOptions>(options);

        Assert.False(receiverOptions.PacketCapture.Enabled);
        Assert.Equal("packet-captures", receiverOptions.PacketCapture.DirectoryPath);
        Assert.Equal("ssl-vision-packets", receiverOptions.PacketCapture.FilePrefix);
        Assert.True(receiverOptions.PacketCapture.FlushEachPacket);
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
