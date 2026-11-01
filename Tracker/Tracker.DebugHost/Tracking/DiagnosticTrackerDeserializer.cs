using System.Net;
using System.Runtime.CompilerServices;
using Duck.Testing.Diagnostics;
using TrackerConnectionLib;

namespace Tracker.DebugHost.Tracking;

/// <summary>このホスト自身の実際の復号を計測する。送信側の入力の正常性は示さない。</summary>
public sealed class DiagnosticTrackerDeserializer(ServiceReceiveDiagnostics diagnostics)
    : ITrackerDeserializer<TrackerPacketAdapter>
{
    private readonly TrackerWrapperPacketDeserializer inner = new();
    private readonly ConditionalWeakTable<TrackerPacketAdapter, ServiceReceiveDiagnostics.ReceiveAttempt> attempts = new();
    private ServiceReceiveDiagnostics.ReceiveEpoch epoch = diagnostics.BeginEpoch("tracker");

    public void BeginEpoch() => epoch = diagnostics.BeginEpoch("tracker");

    public bool TryDeserialize(ReadOnlySpan<byte> data, out TrackerPacketAdapter? packet)
    {
        var attempt = diagnostics.BeginReceive(epoch);
        if (!inner.TryDeserialize(data, out packet) || packet is null)
        {
            diagnostics.RecordFailure(attempt);
            return false;
        }
        attempts.Add(packet, attempt);
        return true;
    }

    internal void RecordPacket(TrackerPacketAdapter packet, IPEndPoint endpoint)
    {
        if (!attempts.TryGetValue(packet, out var attempt)) return;
        attempts.Remove(packet);
        if (packet.Packet.TrackedFrame is null) return;
        diagnostics.RecordSuccess(attempt, SourceKey(packet.SourceName, packet.Uuid, endpoint.ToString()),
            packet.SourceName, packet.Uuid, endpoint.ToString());
    }

    internal static string SourceKey(string? name, string? uuid, string? endpoint) =>
        TrackerComparisonSourceResolver.Resolve(name, name, uuid, endpoint, "external").IdentityKey +
        "\u001f" + name;
}
