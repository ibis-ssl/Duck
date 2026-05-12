using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tracker.Core;

namespace Tracker.CaptureReplay;

/// <summary>
/// jsonl.gz 形式の vision packet capture を読み、schema version と payload を復元する。
/// </summary>
internal static class VisionPacketCaptureReader
{
    private const int SchemaVersion = 1;

    /// <summary>
    /// capture file の各行を既存 schema version の record として順序通り列挙する。
    /// </summary>
    public static IEnumerable<VisionPacketCaptureRecord> ReadRecords(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<CaptureRecordDto>(line)
                ?? throw new InvalidDataException("Vision packet capture record is empty.");
            if (record.SchemaVersion != SchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported vision packet capture schema version '{record.SchemaVersion}'.");
            }

            yield return new VisionPacketCaptureRecord(
                DateTimeOffset.Parse(record.ReceivedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                record.RemoteEndpoint,
                Convert.FromBase64String(record.PayloadBase64));
        }
    }

    private sealed class CaptureRecordDto
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("receivedAt")]
        public string ReceivedAt { get; set; } = "";

        [JsonPropertyName("remoteEndpoint")]
        public string? RemoteEndpoint { get; set; }

        [JsonPropertyName("payloadBase64")]
        public string PayloadBase64 { get; set; } = "";
    }
}

/// <summary>
/// capture 1 行分の受信時刻、送信元、SSL_WrapperPacket payload を保持する。
/// </summary>
internal sealed record VisionPacketCaptureRecord(
    DateTimeOffset ReceivedAt,
    string? RemoteEndpoint,
    byte[] Payload)
{
    /// <summary>
    /// 保存された binary payload を SSL_WrapperPacket として parse する。
    /// </summary>
    public SSL_WrapperPacket ParsePacket()
    {
        return SSL_WrapperPacket.Parser.ParseFrom(Payload);
    }
}
