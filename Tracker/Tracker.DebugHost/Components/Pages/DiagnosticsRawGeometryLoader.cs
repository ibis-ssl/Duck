using System.Text.Json;
using Google.Protobuf;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Components.Pages;

/// <summary>
/// capture sidecar diagnostics log に対応する raw SSL-Vision packet capture から geometry を読み込む。
/// </summary>
internal static class DiagnosticsRawGeometryLoader
{
    /// <summary>
    /// diagnostics log path から対応する packet capture を解決し、raw geometry の時系列索引を作る。
    /// </summary>
    public static DiagnosticsRawGeometryIndex Load(string? selectedLogPath)
    {
        if (selectedLogPath is null)
        {
            return DiagnosticsRawGeometryIndex.Empty;
        }

        var metadataPath = ResolveMetadataPath(selectedLogPath);
        if (metadataPath is null)
        {
            return DiagnosticsRawGeometryIndex.WithError(
                "Capture metadata is available only for capture sidecar diagnostics logs.");
        }

        if (!File.Exists(metadataPath))
        {
            return DiagnosticsRawGeometryIndex.WithError(
                "Capture metadata file was not found for this diagnostics log.");
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<CaptureMetadata>(File.ReadAllText(metadataPath))
                ?? throw new InvalidDataException("Capture metadata is empty.");
            var packetPath = ResolvePacketPath(metadataPath, metadata);
            if (!File.Exists(packetPath))
            {
                return DiagnosticsRawGeometryIndex.WithError(
                    "Raw SSL-Vision packet capture file was not found for this diagnostics log.");
            }

            var geometryRecords = VisionPacketCaptureFile.ReadRecords(packetPath)
                .Select(record => new
                {
                    record.ReceivedAt,
                    Geometry = record.ParsePacket().Geometry,
                })
                .Where(record => record.Geometry?.Field is not null)
                .Select(record => new DiagnosticsRawGeometryRecord(record.ReceivedAt, record.Geometry))
                .ToArray();

            return geometryRecords.Length == 0
                ? DiagnosticsRawGeometryIndex.WithError(
                    "Raw SSL-Vision geometry was not found for this diagnostics log.")
                : new DiagnosticsRawGeometryIndex(geometryRecords, Error: null);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or InvalidProtocolBufferException)
        {
            return DiagnosticsRawGeometryIndex.WithError($"Raw SSL-Vision geometry could not be read: {ex.Message}");
        }
    }

    private static string? ResolveMetadataPath(string diagnosticsLogPath)
    {
        const string diagnosticsSuffix = ".tracker-diagnostics.log";
        const string metadataSuffix = ".metadata.json";

        return diagnosticsLogPath.EndsWith(diagnosticsSuffix, StringComparison.Ordinal)
            ? string.Concat(diagnosticsLogPath.AsSpan(0, diagnosticsLogPath.Length - diagnosticsSuffix.Length), metadataSuffix)
            : null;
    }

    private static string ResolvePacketPath(string metadataPath, CaptureMetadata metadata)
    {
        if (Path.IsPathRooted(metadata.PacketPath))
        {
            return metadata.PacketPath;
        }

        var metadataDirectory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidDataException("Capture metadata directory could not be resolved.");
        var captureDirectory = string.Equals(
            Path.GetFileName(metadataDirectory),
            metadata.SessionFolder,
            StringComparison.Ordinal)
            ? Path.GetDirectoryName(metadataDirectory) ?? metadataDirectory
            : metadataDirectory;
        return Path.Combine(captureDirectory, metadata.PacketPath);
    }

    private sealed class CaptureMetadata
    {
        public string SessionFolder { get; set; } = "";

        public string PacketPath { get; set; } = "";
    }
}

/// <summary>
/// raw SSL-Vision geometry packet の時系列索引。
/// </summary>
internal sealed record DiagnosticsRawGeometryIndex(
    IReadOnlyList<DiagnosticsRawGeometryRecord> Records,
    string? Error)
{
    /// <summary>
    /// 未選択時に使う空の索引。
    /// </summary>
    public static DiagnosticsRawGeometryIndex Empty { get; } = new([], Error: null);

    /// <summary>
    /// 読み込み失敗理由を持つ索引を作る。
    /// </summary>
    public static DiagnosticsRawGeometryIndex WithError(string error)
    {
        return new DiagnosticsRawGeometryIndex([], error);
    }

    /// <summary>
    /// 選択時刻以前の最新 raw geometry を返す。先行 geometry がない場合のみ最初の raw geometry を使う。
    /// </summary>
    public SSL_GeometryData? Select(DateTimeOffset? selectedReceivedAt)
    {
        if (Records.Count == 0)
        {
            return null;
        }

        if (selectedReceivedAt is null)
        {
            return Records[^1].Geometry;
        }

        for (var index = Records.Count - 1; index >= 0; index--)
        {
            if (Records[index].ReceivedAt <= selectedReceivedAt.Value)
            {
                return Records[index].Geometry;
            }
        }

        return Records[0].Geometry;
    }
}

/// <summary>
/// raw SSL-Vision geometry packet と受信時刻。
/// </summary>
internal sealed record DiagnosticsRawGeometryRecord(
    DateTimeOffset ReceivedAt,
    SSL_GeometryData Geometry);
