using Tracker.DebugHost.Vision;
using Tracker.DebugHost;
using Microsoft.Extensions.Configuration;

namespace Tracker.Tests;

public sealed class VisionPacketCaptureRuntimeControlTests
{
    [Fact]
    public void ReplayOnlyConfigurationDisablesCaptureAndEveryTrackerUdpDirection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VisionReceiver:PacketCapture:Enabled"] = "true",
                ["Tracker:Enabled"] = "true",
                ["Tracker:PublishUdp"] = "true",
                ["Tracker:Receive:Enabled"] = "true",
            })
            .Build();

        DebugHostReplayOnlyConfiguration.Apply(configuration);

        Assert.Equal("false", configuration["VisionReceiver:PacketCapture:Enabled"]);
        Assert.Equal("false", configuration["Tracker:Enabled"]);
        Assert.Equal("false", configuration["Tracker:PublishUdp"]);
        Assert.Equal("false", configuration["Tracker:Receive:Enabled"]);
    }

    [Fact]
    public void ReplayOnlyControlRejectsCaptureEnableAndLeavesCaptureOff()
    {
        var control = new VisionPacketCaptureRuntimeControl(initialEnabled: false, allowEnable: false);

        Assert.False(control.CanEnable);
        Assert.False(control.SetEnabled(true));
        Assert.False(control.Enabled);
        Assert.True(control.SetEnabled(false));
    }

    [Fact]
    public void NormalControlPreservesCaptureOnAndCaptureOffBehavior()
    {
        var control = new VisionPacketCaptureRuntimeControl(initialEnabled: false);

        Assert.True(control.CanEnable);
        Assert.True(control.SetEnabled(true));
        Assert.True(control.Enabled);
        Assert.True(control.SetEnabled(false));
        Assert.False(control.Enabled);
    }
}
