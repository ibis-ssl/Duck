using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;

namespace Tracker.Server.Vision;

public sealed record VisionPacketCaptureRecord(
    DateTimeOffset ReceivedAt,
    string? RemoteEndpoint,
    byte[] Payload)
{
    public SSL_WrapperPacket ParsePacket()
    {
        return SSL_WrapperPacket.Parser.ParseFrom(Payload);
    }
}

internal static class VisionPacketCaptureFile
{
    private const int SchemaVersion = 1;

    public static StreamWriter CreateWriter(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var gzipStream = new GZipStream(fileStream, CompressionLevel.SmallestSize);
        return new StreamWriter(gzipStream);
    }

    public static void WriteRecord(
        TextWriter writer,
        DateTimeOffset receivedAt,
        EndPoint remoteEndpoint,
        ReadOnlySpan<byte> payload)
    {
        var payloadBase64 = Convert.ToBase64String(payload);
        writer.Write('{');
        writer.Write("\"schemaVersion\":");
        writer.Write(SchemaVersion.ToString(CultureInfo.InvariantCulture));
        writer.Write(",\"receivedAt\":\"");
        writer.Write(receivedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.Write("\",\"remoteEndpoint\":");
        writer.Write(JsonSerializer.Serialize(remoteEndpoint.ToString()));
        writer.Write(",\"payloadBase64\":\"");
        writer.Write(payloadBase64);
        writer.WriteLine("\"}");
    }

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

    public static string BuildCapturePath(VisionPacketCaptureOptions options, DateTimeOffset startedAt)
    {
        return BuildCapturePaths(options, startedAt).PacketPath;
    }

    public static VisionPacketCapturePaths BuildCapturePaths(VisionPacketCaptureOptions options, DateTimeOffset startedAt)
    {
        var timestamp = startedAt.UtcDateTime.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
        var basePath = Path.Combine(
            ResolveDirectoryPath(options.DirectoryPath),
            $"{options.FilePrefix}-{timestamp}-{Guid.NewGuid():N}");
        return new VisionPacketCapturePaths(
            PacketPath: $"{basePath}.jsonl.gz",
            MetadataPath: $"{basePath}.metadata.json",
            DiagnosticsLogPath: $"{basePath}.tracker-diagnostics.log",
            RenderSnapshotPath: $"{basePath}.render-snapshots.jsonl.gz");
    }

    internal static string ResolveDirectoryPath(string directoryPath)
    {
        return Path.IsPathRooted(directoryPath)
            ? directoryPath
            : Path.Combine(AppContext.BaseDirectory, directoryPath);
    }

    public sealed record VisionPacketCapturePaths(
        string PacketPath,
        string MetadataPath,
        string DiagnosticsLogPath,
        string RenderSnapshotPath);

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
