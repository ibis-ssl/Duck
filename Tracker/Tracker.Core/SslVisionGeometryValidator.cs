using Google.Protobuf;

namespace Tracker.Core;

/// <summary>Validates serialized SSL-Vision geometry payloads using the repository's generated protobuf type.</summary>
public static class SslVisionGeometryValidator
{
    public static byte[]? ExtractWrapperPacketGeometry(ReadOnlySpan<byte> payload)
    {
        try
        {
            return SSL_WrapperPacket.Parser.ParseFrom(payload.ToArray()).Geometry?.ToByteArray();
        }
        catch (InvalidProtocolBufferException exception)
        {
            throw new InvalidDataException("SSL-Vision wrapper packet protobuf payload is invalid.", exception);
        }
    }

    public static void Validate(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new InvalidDataException("SSL-Vision geometry protobuf payload is empty.");
        }

        try
        {
            _ = SSL_GeometryData.Parser.ParseFrom(payload.ToArray());
        }
        catch (InvalidProtocolBufferException exception)
        {
            throw new InvalidDataException("SSL-Vision geometry protobuf payload is invalid.", exception);
        }
    }
}
