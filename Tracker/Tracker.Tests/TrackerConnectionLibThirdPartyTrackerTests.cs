using System.Net;
using TrackerConnectionLib;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: CaptureOn 比較ログで 3rdparty tracker packet を source identity ごとに保持し、ibis 自身を除外する契約を検証する。
/// </summary>
public class TrackerConnectionLibThirdPartyTrackerTests : IClassFixture<TrackerContractFixture>
{
    private const string IbisUuid = "ibis-runtime-uuid";
    private const string IbisSourceName = "ibis-runtime-source";

    private readonly TrackerContractFixture fixture;

    public TrackerConnectionLibThirdPartyTrackerTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// 何を確認しているか: ibis と異なる uuid / sourceName の packet が 3rdparty 候補として最新状態に保持されることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WhenPacketIdentityDiffersFromIbis_KeepsThirdPartyCandidate()
    {
        var manager = CreateManager();
        var remoteEndpoint = Endpoint(12001);
        var receivedAt = new DateTimeOffset(2026, 5, 12, 11, 0, 0, TimeSpan.Zero);
        var packet = CreateAdapter("thirdparty-a-uuid", "thirdparty-a-source", frameNumber: 100);

        manager.ProcessPacket(packet, remoteEndpoint, receivedAt);

        var state = Assert.Single(manager.Trackers.Values);
        Assert.Equal("thirdparty-a-uuid", state.Uuid);
        Assert.Equal("thirdparty-a-source", state.SourceName);
        Assert.Equal(remoteEndpoint, state.RemoteEndpoint);
        Assert.Equal(receivedAt, state.ReceivedAt);
        Assert.Same(packet, state.LastPacket);
    }

    /// <summary>
    /// 何を確認しているか: ibis 自身の uuid / sourceName と一致する packet が比較対象から除外されることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WhenPacketMatchesIbisIdentity_ExcludesSelfPacket()
    {
        var manager = CreateManager();

        manager.ProcessPacket(
            CreateAdapter(IbisUuid, IbisSourceName, frameNumber: 101),
            Endpoint(12002),
            new DateTimeOffset(2026, 5, 12, 11, 0, 1, TimeSpan.Zero));

        Assert.Empty(manager.Trackers);
    }

    /// <summary>
    /// 何を確認しているか: uuid / sourceName / remote endpoint の組で複数 source を識別し、各 source の最新 packet へ更新されることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WithMultipleSources_SeparatesByUuidSourceNameAndRemoteEndpoint()
    {
        var manager = CreateManager();
        var firstEndpoint = Endpoint(12003);
        var secondEndpoint = Endpoint(12004);
        var firstInitial = CreateAdapter("shared-uuid", "source-a", frameNumber: 200);
        var firstLatest = CreateAdapter("shared-uuid", "source-a", frameNumber: 201);
        var second = CreateAdapter("shared-uuid", "source-b", frameNumber: 300);

        manager.ProcessPacket(firstInitial, firstEndpoint, new DateTimeOffset(2026, 5, 12, 11, 0, 2, TimeSpan.Zero));
        manager.ProcessPacket(second, secondEndpoint, new DateTimeOffset(2026, 5, 12, 11, 0, 3, TimeSpan.Zero));
        manager.ProcessPacket(firstLatest, firstEndpoint, new DateTimeOffset(2026, 5, 12, 11, 0, 4, TimeSpan.Zero));

        Assert.Collection(
            manager.Trackers.Values.OrderBy(state => state.SourceName, StringComparer.Ordinal),
            state =>
            {
                Assert.Equal("shared-uuid", state.Uuid);
                Assert.Equal("source-a", state.SourceName);
                Assert.Equal(firstEndpoint, state.RemoteEndpoint);
                Assert.Same(firstLatest, state.LastPacket);
                Assert.Equal(201u, state.LastPacket?.Packet.TrackedFrame.FrameNumber);
            },
            state =>
            {
                Assert.Equal("shared-uuid", state.Uuid);
                Assert.Equal("source-b", state.SourceName);
                Assert.Equal(secondEndpoint, state.RemoteEndpoint);
                Assert.Same(second, state.LastPacket);
                Assert.Equal(300u, state.LastPacket?.Packet.TrackedFrame.FrameNumber);
            });
    }

    private static MultiTrackerManager<TrackerPacketAdapter> CreateManager()
    {
        return new MultiTrackerManager<TrackerPacketAdapter>(IbisUuid, IbisSourceName);
    }

    private TrackerPacketAdapter CreateAdapter(string uuid, string sourceName, uint frameNumber)
    {
        var frame = fixture.CreateFrame(frameNumber: frameNumber);
        var packet = fixture.CreatePacketGenerator(sourceName, uuid).Generate(frame);
        return new TrackerPacketAdapter(packet);
    }

    private static IPEndPoint Endpoint(int port)
    {
        return new IPEndPoint(IPAddress.Parse("192.0.2.10"), port);
    }
}
