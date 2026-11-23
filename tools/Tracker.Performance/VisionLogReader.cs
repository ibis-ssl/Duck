using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Tracker.Performance;

/// <summary>
/// gzip 圧縮された SSL ログのヘッダーとレコードを読み、選択した入力を固定する。
/// </summary>
internal static class VisionLogReader
{
    private const int VisionMessageType = 4;
    private const int MaximumPayloadBytes = 16 * 1024 * 1024;

    /// <summary>
    /// 圧縮ファイル全体を識別し、最初の type 4 から指定秒数未満の入力を取り出す。
    /// ファイル先頭に位置するシーク可能なストリームを借り、終了時に閉じない。
    /// </summary>
    internal static VisionWindow Read(Stream archive, int durationSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationSeconds);
        var archiveHash = Convert.ToHexString(SHA256.HashData(archive));
        archive.Position = 0;
        using var stream = new GZipStream(archive, CompressionMode.Decompress, leaveOpen: true);
        using var payloadHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> header = stackalloc byte[16];
        stream.ReadExactly(header);
        if (!header[..12].SequenceEqual("SSL_LOG_FILE"u8) || BinaryPrimitives.ReadInt32BigEndian(header[12..]) != 1)
        {
            throw new InvalidDataException("gzip 圧縮された SSL_LOG_FILE v1 が必要です。");
        }

        var inputs = new List<VisionInput>();
        var cameras = new SortedDictionary<uint, int>();
        var geometryCount = 0;
        var maximumRobots = 0;
        var maximumBalls = 0;
        var durationNs = durationSeconds * 1_000_000_000L;
        long? firstTimestamp = null;
        long previousTimestamp = -1;
        var reachedWindowEnd = false;
        while (TryReadHeader(stream, header))
        {
            var type = BinaryPrimitives.ReadInt32BigEndian(header[8..]);
            var timestamp = BinaryPrimitives.ReadInt64BigEndian(header);
            if (type == VisionMessageType)
            {
                if (timestamp < 0 || timestamp < previousTimestamp)
                {
                    throw new InvalidDataException("SSL-Vision の受信時刻が負、または記録順で逆行しています。");
                }

                previousTimestamp = timestamp;
                if (firstTimestamp is long start && timestamp - start >= durationNs)
                {
                    reachedWindowEnd = true;
                    break;
                }
            }

            var length = BinaryPrimitives.ReadInt32BigEndian(header[12..]);
            if (length < 0 || length > MaximumPayloadBytes || (type == VisionMessageType && length == 0))
            {
                throw new InvalidDataException("SSL ログレコードのデータ長が不正です。");
            }

            if (type != VisionMessageType)
            {
                SkipPayload(stream, length);
                continue;
            }

            firstTimestamp ??= timestamp;
            var payload = new byte[length];
            stream.ReadExactly(payload);
            var packet = SSL_WrapperPacket.Parser.ParseFrom(payload);
            inputs.Add(new VisionInput(timestamp, payload));
            payloadHash.AppendData(payload);
            if (packet.Detection is { } detection)
            {
                var cameraId = detection.CameraId;
                cameras[cameraId] = cameras.GetValueOrDefault(cameraId) + 1;
                maximumRobots = Math.Max(maximumRobots, detection.RobotsBlue.Count + detection.RobotsYellow.Count);
                maximumBalls = Math.Max(maximumBalls, detection.Balls.Count);
            }

            if (packet.Geometry is not null)
            {
                geometryCount++;
            }
        }

        if (inputs.Count == 0)
        {
            throw new InvalidDataException("SSL-Vision（type 4）の入力がありません。");
        }

        return new VisionWindow(
            inputs,
            archiveHash,
            Convert.ToHexString(payloadHash.GetHashAndReset()),
            cameras,
            geometryCount,
            maximumRobots,
            maximumBalls,
            reachedWindowEnd);
    }

    /// <summary>
    /// レコード境界の EOF と、途中で切れたヘッダーを区別する。
    /// </summary>
    private static bool TryReadHeader(Stream stream, Span<byte> header)
    {
        var first = stream.ReadByte();
        if (first < 0)
        {
            return false;
        }

        header[0] = (byte)first;
        stream.ReadExactly(header[1..]);
        return true;
    }

    /// <summary>
    /// 選択対象外のレコードも区間内の切り詰めを検出し、ペイロードを保持しない。
    /// </summary>
    private static void SkipPayload(Stream stream, int remaining)
    {
        Span<byte> buffer = stackalloc byte[8192];
        while (remaining > 0)
        {
            var count = Math.Min(remaining, buffer.Length);
            stream.ReadExactly(buffer[..count]);
            remaining -= count;
        }
    }
}
