using System.Buffers.Binary;
using System.Text;
using Duck.Testing.Diagnostics;
using Tracker.DebugHost.Tracking;

namespace Issue81.IndependentSpecificationTests;

/// <summary>固定公式protoから独立に組み立てた追跡パケットで公開復号APIを検証する。</summary>
public sealed class TrackerDatagramSpecificationTests
{
    /// <summary>P01: 公式形式のUUID・名称・完全なTrackedFrameを復号できる。</summary>
    [Theory]
    [InlineData("ibis", "uuid-duck")]
    [InlineData("TIGERs", "uuid-tigers")]
    [InlineData("ER-FORCE", "uuid-erforce")]
    public void P01_OfficialTrackerPacketCanBeDecoded(string sourceName, string uuid)
    {
        var diagnostics = new ServiceReceiveDiagnostics();
        var parser = new DiagnosticTrackerDeserializer(diagnostics);
        parser.BeginEpoch();
        Assert.True(parser.TryDeserialize(Packet(uuid, sourceName), out var packet));
        Assert.NotNull(packet);
    }

    /// <summary>P02: 長さ情報または固定長値が途中で切れたdatagramを正常復号にしない。</summary>
    [Theory]
    [InlineData(new byte[] { 0x0a, 0x05, 0x61 })]
    [InlineData(new byte[] { 0x0a, 0x80 })]
    [InlineData(new byte[] { 0x1a, 0x09, 0x11, 0x00 })]
    public void P02_TruncatedWireDataIsNotSuccessfullyDecoded(byte[] datagram)
    {
        var parser = new DiagnosticTrackerDeserializer(new ServiceReceiveDiagnostics());
        parser.BeginEpoch();
        Assert.False(parser.TryDeserialize(datagram, out _));
    }

    /// <summary>固定protoのfield番号・wire型のみから、既存fixtureを使わずパケットを作る。</summary>
    private static byte[] Packet(string uuid, string sourceName)
    {
        var frame = new List<byte> { 0x08, 0x01, 0x11 };
        var timestamp = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(timestamp, BitConverter.DoubleToInt64Bits(1.0));
        frame.AddRange(timestamp);
        var packet = new List<byte>();
        AddLengthDelimited(packet, 1, Encoding.UTF8.GetBytes(uuid));
        AddLengthDelimited(packet, 2, Encoding.UTF8.GetBytes(sourceName));
        AddLengthDelimited(packet, 3, frame.ToArray());
        return packet.ToArray();
    }

    /// <summary>独立fixtureの短いlength-delimited fieldを符号化する。</summary>
    private static void AddLengthDelimited(List<byte> target, int field, byte[] data)
    {
        if (data.Length >= 128) throw new ArgumentOutOfRangeException(nameof(data));
        target.Add((byte)((field << 3) | 2));
        target.Add((byte)data.Length);
        target.AddRange(data);
    }
}
