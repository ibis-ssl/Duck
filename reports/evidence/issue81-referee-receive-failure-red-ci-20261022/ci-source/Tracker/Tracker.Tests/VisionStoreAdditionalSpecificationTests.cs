using System.Net;
using Tracker.DebugHost.Vision;

namespace Issue81.IndependentSpecificationTests;

/// <summary>DebugHost公開packet storeに固定公式vision datagramを入力する限定的追加試験。</summary>
public sealed class VisionStoreAdditionalSpecificationTests
{
    /// <summary>V01: 公式検出10件の件数・入力時刻・検出内容を独立fixtureで確認する。</summary>
    [Fact]
    public void V01_OfficialVisionDatagramsRetainCountTimeAndDetection()
    {
        var store = new VisionPacketStore();
        var clock = new IndependentClock();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.0.2.10"), 10020);
        for (uint frame = 1; frame <= 10; frame++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            store.StoreDatagram(IndependentVisionWireFixture.Detection(frame), endpoint, clock.GetUtcNow());
        }
        var snapshot = store.GetSnapshot();
        Assert.Equal(10L, snapshot.PacketCount);
        Assert.Equal(0L, snapshot.ErrorCount);
        Assert.Equal(clock.GetUtcNow(), snapshot.ReceivedAt);
        Assert.NotNull(snapshot.RemoteEndpoint);
        Assert.NotNull(snapshot.LatestPacket);
        Assert.NotNull(snapshot.Detection);
        Assert.Equal(10u, snapshot.Detection.FrameNumber);
        var ball = Assert.Single(snapshot.Detection.Balls);
        Assert.Equal(123f, ball.X);
        Assert.Equal(-456f, ball.Y);
    }

    /// <summary>V02: 到着した別内容の検出と受信時刻を最新snapshotへ対応付ける。</summary>
    [Fact]
    public void V02_NewSnapshotKeepsItsOwnInputContentAndReceptionTime()
    {
        var store = new VisionPacketStore();
        var clock = new IndependentClock();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.0.2.10"), 10020);
        store.StoreDatagram(IndependentVisionWireFixture.Detection(1, 1f, 2f), endpoint, clock.GetUtcNow());
        clock.Advance(TimeSpan.FromMilliseconds(20));
        store.StoreDatagram(IndependentVisionWireFixture.Detection(2, 100f, 200f), endpoint, clock.GetUtcNow());
        var snapshot = store.GetSnapshot();
        Assert.Equal(clock.GetUtcNow(), snapshot.ReceivedAt);
        Assert.NotNull(snapshot.Detection);
        Assert.Equal(2u, snapshot.Detection.FrameNumber);
        var ball = Assert.Single(snapshot.Detection.Balls);
        Assert.Equal(100f, ball.X);
        Assert.Equal(200f, ball.Y);
    }

    /// <summary>V03: 明示的な復号失敗記録を成功件数へ加算せず、失敗の診断を保持する。</summary>
    [Fact]
    public void V03_RecordedDecodeFailureDoesNotBecomeSuccessfulPacket()
    {
        var store = new VisionPacketStore();
        store.RecordDecodeError(new InvalidDataException("independent-malformed-vision-fixture"));
        var snapshot = store.GetSnapshot();
        Assert.Equal(0L, snapshot.PacketCount);
        Assert.Equal(1L, snapshot.ErrorCount);
        Assert.False(string.IsNullOrEmpty(snapshot.LastError));
    }

    /// <summary>V04: geometryだけの正常wrapperを検出frameの受信と取り違えない。</summary>
    [Fact]
    public void V04_GeometryOnlyIsNotDetectionEvidence()
    {
        var store = new VisionPacketStore();
        var receivedAt = new IndependentClock().GetUtcNow();
        store.StoreDatagram(IndependentVisionWireFixture.GeometryOnly(),
            new IPEndPoint(IPAddress.Parse("192.0.2.10"), 10020), receivedAt);
        var snapshot = store.GetSnapshot();
        Assert.Equal(1L, snapshot.PacketCount);
        Assert.Equal(0L, snapshot.ErrorCount);
        Assert.NotNull(snapshot.Geometry);
        Assert.Null(snapshot.Detection);
    }
}
