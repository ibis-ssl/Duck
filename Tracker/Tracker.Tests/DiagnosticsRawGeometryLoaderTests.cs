using System.Net;
using System.Text.Json;
using Google.Protobuf;
using Tracker.DebugHost.Components.Pages;
using Tracker.DebugHost.Vision;

namespace Tracker.Tests;

/// <summary>
/// Diagnostics replay が raw SSL-Vision packet capture の geometry を field 描画に使う contract を固定する。
/// </summary>
public class DiagnosticsRawGeometryLoaderTests
{
    /// <summary>
    /// 何を確認しているか: diagnostics sidecar log から raw packet capture を辿り、固定値ではなく capture 内 geometry を読むこと。
    /// </summary>
    [Fact]
    public void Load_WhenMetadataPointsToPacketCapture_ReadsRawGeometry()
    {
        var fixture = CreateCaptureFixture();
        try
        {
            WriteGeometryPacket(fixture.PacketPath, fixture.StartedAt, fieldLength: 12345, fieldWidth: 6789);
            WriteMetadata(fixture);
            File.WriteAllText(fixture.DiagnosticsLogPath, "");

            var index = DiagnosticsRawGeometryLoader.Load(fixture.DiagnosticsLogPath);
            var geometry = index.Select(fixture.StartedAt);

            Assert.Null(index.Error);
            Assert.NotNull(geometry);
            Assert.Equal(12345, geometry.Field.FieldLength);
            Assert.Equal(6789, geometry.Field.FieldWidth);
        }
        finally
        {
            Directory.Delete(fixture.RootDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: 選択時刻以前の最新 raw geometry を使い、あとから来た別サイズ field に早く切り替えないこと。
    /// </summary>
    [Fact]
    public void Select_UsesLatestRawGeometryAtOrBeforeSelectedTimestamp()
    {
        var firstAt = new DateTimeOffset(2026, 5, 14, 13, 0, 0, TimeSpan.Zero);
        var secondAt = firstAt.AddSeconds(10);
        var index = new DiagnosticsRawGeometryIndex(
            [
                new DiagnosticsRawGeometryRecord(firstAt, CreateGeometry(fieldLength: 9000, fieldWidth: 6000)),
                new DiagnosticsRawGeometryRecord(secondAt, CreateGeometry(fieldLength: 12000, fieldWidth: 9000)),
            ],
            Error: null);

        var beforeSecond = index.Select(firstAt.AddSeconds(5));
        var afterSecond = index.Select(secondAt.AddSeconds(1));

        Assert.NotNull(beforeSecond);
        Assert.NotNull(afterSecond);
        Assert.Equal(9000, beforeSecond.Field.FieldLength);
        Assert.Equal(12000, afterSecond.Field.FieldLength);
    }

    private static CaptureFixture CreateCaptureFixture()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"diagnostics-geometry-{Guid.NewGuid():N}");
        var sessionFolder = "session-1";
        var sessionDirectory = Path.Combine(rootDirectory, sessionFolder);
        Directory.CreateDirectory(sessionDirectory);
        return new CaptureFixture(
            rootDirectory,
            sessionFolder,
            Path.Combine(sessionDirectory, "session-1.jsonl.gz"),
            Path.Combine(sessionDirectory, "session-1.metadata.json"),
            Path.Combine(sessionDirectory, "session-1.tracker-diagnostics.log"),
            new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero));
    }

    private static void WriteMetadata(CaptureFixture fixture)
    {
        File.WriteAllText(
            fixture.MetadataPath,
            JsonSerializer.Serialize(new
            {
                fixture.SessionFolder,
                PacketPath = Path.Combine(fixture.SessionFolder, Path.GetFileName(fixture.PacketPath)),
            }));
    }

    private static void WriteGeometryPacket(
        string packetPath,
        DateTimeOffset receivedAt,
        int fieldLength,
        int fieldWidth)
    {
        using var writer = VisionPacketCaptureFile.CreateWriter(packetPath);
        var packet = new SSL_WrapperPacket
        {
            Geometry = CreateGeometry(fieldLength, fieldWidth),
        };
        VisionPacketCaptureFile.WriteRecord(
            writer,
            receivedAt,
            new IPEndPoint(IPAddress.Loopback, 10020),
            packet.ToByteArray());
    }

    private static SSL_GeometryData CreateGeometry(int fieldLength, int fieldWidth)
    {
        return new SSL_GeometryData
        {
            Field = new SSL_GeometryFieldSize
            {
                FieldLength = fieldLength,
                FieldWidth = fieldWidth,
                GoalWidth = 1000,
                GoalDepth = 180,
                BoundaryWidth = 300,
                BoundaryWidthGoalLine = 300,
                PenaltyAreaDepth = 1000,
                PenaltyAreaWidth = 2000,
                CenterCircleRadius = 500,
                LineThickness = 10,
            },
        };
    }

    private sealed record CaptureFixture(
        string RootDirectory,
        string SessionFolder,
        string PacketPath,
        string MetadataPath,
        string DiagnosticsLogPath,
        DateTimeOffset StartedAt);
}
