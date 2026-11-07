using System.Buffers.Binary;

namespace Issue81.IndependentSpecificationTests;

/// <summary>固定公開SSL-Vision protoのfield番号とwire型だけから作る独立datagram fixture。</summary>
public static class IndependentVisionWireFixture
{
    /// <summary>一つの検出ボールと必須frame属性を持つ公式wrapperを作る。</summary>
    public static byte[] Detection(uint frameNumber, float x = 123f, float y = -456f)
    {
        var ball = new List<byte>();
        AddFloat(ball, 1, 1f);
        AddFloat(ball, 3, x);
        AddFloat(ball, 4, y);
        AddFloat(ball, 6, 12f);
        AddFloat(ball, 7, 34f);
        var frame = new List<byte>();
        AddVarintField(frame, 1, frameNumber);
        AddDouble(frame, 2, 1.25);
        AddDouble(frame, 3, 1.5);
        AddVarintField(frame, 4, 0);
        AddMessage(frame, 5, ball.ToArray());
        var wrapper = new List<byte>();
        AddMessage(wrapper, 1, frame.ToArray());
        return wrapper.ToArray();
    }

    /// <summary>検出frameを含まず、必須field寸法だけを持つ公式geometry wrapperを作る。</summary>
    public static byte[] GeometryOnly()
    {
        var field = new List<byte>();
        AddVarintField(field, 1, 6000);
        AddVarintField(field, 2, 4000);
        AddVarintField(field, 3, 1000);
        AddVarintField(field, 4, 180);
        AddVarintField(field, 5, 300);
        var geometry = new List<byte>();
        AddMessage(geometry, 1, field.ToArray());
        var wrapper = new List<byte>();
        AddMessage(wrapper, 2, geometry.ToArray());
        return wrapper.ToArray();
    }

    /// <summary>protobufの非負varintを独立に符号化する。</summary>
    private static void AddVarint(List<byte> target, ulong value)
    {
        while (value >= 128)
        {
            target.Add((byte)((value & 127) | 128));
            value >>= 7;
        }
        target.Add((byte)value);
    }

    /// <summary>整数fieldを符号化する。</summary>
    private static void AddVarintField(List<byte> target, int field, ulong value)
    {
        AddVarint(target, (ulong)(field << 3));
        AddVarint(target, value);
    }

    /// <summary>length-delimitedなmessage fieldを符号化する。</summary>
    private static void AddMessage(List<byte> target, int field, byte[] payload)
    {
        AddVarint(target, (ulong)((field << 3) | 2));
        AddVarint(target, (ulong)payload.Length);
        target.AddRange(payload);
    }

    /// <summary>little-endianの32bit float fieldを符号化する。</summary>
    private static void AddFloat(List<byte> target, int field, float value)
    {
        AddVarint(target, (ulong)((field << 3) | 5));
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits(value));
        target.AddRange(bytes);
    }

    /// <summary>little-endianの64bit double fieldを符号化する。</summary>
    private static void AddDouble(List<byte> target, int field, double value)
    {
        AddVarint(target, (ulong)((field << 3) | 1));
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(value));
        target.AddRange(bytes);
    }
}
