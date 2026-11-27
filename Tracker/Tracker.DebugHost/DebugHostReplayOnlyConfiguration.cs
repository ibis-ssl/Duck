using Microsoft.Extensions.Configuration;

namespace Tracker.DebugHost;

internal static class DebugHostReplayOnlyConfiguration
{
    internal static void Apply(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration["VisionReceiver:PacketCapture:Enabled"] = "false";
        configuration["Tracker:Enabled"] = "false";
        configuration["Tracker:PublishUdp"] = "false";
        configuration["Tracker:Receive:Enabled"] = "false";
    }
}
