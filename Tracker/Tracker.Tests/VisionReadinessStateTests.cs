using Google.Protobuf;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class VisionReadinessStateTests
{
    [Fact]
    public void ReadinessRequiresDecodedDetectionPacket()
    {
        var state = new VisionReadinessState();
        var geometryOnlyPacket = new SSL_WrapperPacket { Geometry = new SSL_GeometryData() };
        var detectionPacket = new SSL_WrapperPacket { Detection = new SSL_DetectionFrame() };

        Assert.False(state.IsReady);
        Assert.False(state.ObserveDatagram(geometryOnlyPacket.ToByteArray()));
        Assert.False(state.IsReady);
        Assert.False(state.ObserveDatagram([0xff]));
        Assert.False(state.IsReady);
        Assert.True(state.ObserveDatagram(detectionPacket.ToByteArray()));
        Assert.True(state.IsReady);
    }
}
