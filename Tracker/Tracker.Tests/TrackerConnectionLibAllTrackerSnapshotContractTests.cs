using Google.Protobuf;
using System.Net;
using TrackerConnectionLib;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: CaptureOn 比較ログで見えている tracker packet を source role にかかわらず保持し、snapshot replay 可能な contract を検証する。
/// </summary>
public class TrackerConnectionLibAllTrackerSnapshotContractTests : IClassFixture<TrackerContractFixture>
{
    private const string IbisUuid = "ibis-runtime-uuid";
    private const string IbisSourceName = "ibis-runtime-source";

    private readonly TrackerContractFixture fixture;

    public TrackerConnectionLibAllTrackerSnapshotContractTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// 何を確認しているか: ibis 自身の uuid / sourceName と一致する packet も snapshot 対象として保持されることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WhenPacketMatchesIbisIdentity_KeepsOwnTrackerSnapshot()
    {
        var manager = CreateManager();
        var remoteEndpoint = Endpoint(12002);
        var receivedAt = new DateTimeOffset(2026, 5, 12, 11, 0, 0, TimeSpan.Zero);
        var packet = CreateAdapter(IbisUuid, IbisSourceName, frameNumber: 101);

        manager.ProcessPacket(packet, remoteEndpoint, receivedAt);

        var state = Assert.Single(manager.Trackers.Values);
        Assert.Equal(IbisUuid, state.Uuid);
        Assert.Equal(IbisSourceName, state.SourceName);
        Assert.Equal(remoteEndpoint, state.RemoteEndpoint);
        Assert.Equal(receivedAt, state.ReceivedAt);
        Assert.Same(packet, state.LastPacket);
    }

    /// <summary>
    /// 何を確認しているか: ibis と異なる uuid / sourceName の packet も external tracker snapshot として保持されることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WhenPacketIdentityDiffersFromIbis_KeepsExternalTrackerSnapshot()
    {
        var manager = CreateManager();
        var remoteEndpoint = Endpoint(12001);
        var receivedAt = new DateTimeOffset(2026, 5, 12, 11, 0, 1, TimeSpan.Zero);
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
    /// 何を確認しているか: own / external を同時に保持し、role 判定を保存除外条件にしないことを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WithOwnAndExternalSources_KeepsAllVisibleTrackerSnapshots()
    {
        var manager = CreateManager();
        var ownEndpoint = Endpoint(12003);
        var externalEndpoint = Endpoint(12004);
        var own = CreateAdapter(IbisUuid, IbisSourceName, frameNumber: 200);
        var external = CreateAdapter("thirdparty-b-uuid", "thirdparty-b-source", frameNumber: 300);

        manager.ProcessPacket(own, ownEndpoint, new DateTimeOffset(2026, 5, 12, 11, 0, 2, TimeSpan.Zero));
        manager.ProcessPacket(external, externalEndpoint, new DateTimeOffset(2026, 5, 12, 11, 0, 3, TimeSpan.Zero));

        var states = manager.Trackers.Values.OrderBy(state => state.SourceName, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, states.Length);
        Assert.Collection(
            states,
            state =>
            {
                Assert.Equal(IbisUuid, state.Uuid);
                Assert.Equal(IbisSourceName, state.SourceName);
                Assert.Equal(ownEndpoint, state.RemoteEndpoint);
                Assert.Same(own, state.LastPacket);
                Assert.Equal(200u, state.LastPacket?.Packet.TrackedFrame.FrameNumber);
            },
            state =>
            {
                Assert.Equal("thirdparty-b-uuid", state.Uuid);
                Assert.Equal("thirdparty-b-source", state.SourceName);
                Assert.Equal(externalEndpoint, state.RemoteEndpoint);
                Assert.Same(external, state.LastPacket);
                Assert.Equal(300u, state.LastPacket?.Packet.TrackedFrame.FrameNumber);
            });
    }

    /// <summary>
    /// 何を確認しているか: source role / label は保存後の表示・比較用 metadata であり、unknown でも保存を落とさないことを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WhenSourceRoleIsUnknown_KeepsSnapshotAndExposesDisplayMetadata()
    {
        var manager = CreateManager();
        var endpoint = Endpoint(12005);
        var packet = CreateAdapter(string.Empty, string.Empty, frameNumber: 400);

        manager.ProcessPacket(packet, endpoint, new DateTimeOffset(2026, 5, 12, 11, 0, 4, TimeSpan.Zero));

        var state = Assert.Single(manager.Trackers.Values);
        Assert.Equal(string.Empty, state.Uuid);
        Assert.Equal(string.Empty, state.SourceName);
        Assert.Same(packet, state.LastPacket);
        AssertSourceMetadata(state, expectedRole: "unknown", expectedLabel: "unknown");
    }

    /// <summary>
    /// 何を確認しているか: snapshot replay の前提として、保存された state から official packet payload を後で復元できることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_ForSnapshotReplay_KeepsReplayReadableRawPayloadForEveryVisiblePacket()
    {
        var manager = CreateManager();
        var own = CreateAdapter(IbisUuid, IbisSourceName, frameNumber: 500);
        var external = CreateAdapter("thirdparty-c-uuid", "thirdparty-c-source", frameNumber: 501);

        manager.ProcessPacket(own, Endpoint(12006), new DateTimeOffset(2026, 5, 12, 11, 0, 5, TimeSpan.Zero));
        manager.ProcessPacket(external, Endpoint(12007), new DateTimeOffset(2026, 5, 12, 11, 0, 6, TimeSpan.Zero));

        var statesByUuid = manager.Trackers.Values.ToDictionary(state => state.Uuid, StringComparer.Ordinal);
        Assert.Equal(2, statesByUuid.Count);
        Assert.Equal(own.Packet.ToByteArray(), statesByUuid[IbisUuid].LastPacket?.Packet.ToByteArray());
        Assert.Equal(external.Packet.ToByteArray(), statesByUuid["thirdparty-c-uuid"].LastPacket?.Packet.ToByteArray());
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

    private static void AssertSourceMetadata(
        TrackerState<TrackerPacketAdapter> state,
        string expectedRole,
        string expectedLabel)
    {
        Assert.Equal(expectedRole, GetRequiredStringProperty(state, "SourceRole"));
        Assert.Equal(expectedLabel, GetRequiredStringProperty(state, "SourceLabel"));
    }

    private static string? GetRequiredStringProperty(TrackerState<TrackerPacketAdapter> state, string propertyName)
    {
        var property = state.GetType().GetProperty(propertyName);
        Assert.True(property is not null, $"TrackerState must expose {propertyName} for display/comparison metadata.");
        Assert.Equal(typeof(string), property.PropertyType);
        return (string?)property.GetValue(state);
    }
}
