using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;

namespace Tracker.Tests;

public class TrackerRenderSnapshotLogReaderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void ReadFrame_ReturnsSnapshotMatchingDiagnosticsFrame()
    {
        // 何を確認しているか: diagnostics log に対応する render snapshot から指定 frame の描画状態を復元できることを確認する。
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-render-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(captureDirectory);
        var diagnosticsLogPath = Path.Combine(captureDirectory, "ssl-vision-packets-test.tracker-diagnostics.log");
        var renderSnapshotPath = Path.Combine(captureDirectory, "ssl-vision-packets-test.render-snapshots.jsonl.gz");

        try
        {
            File.WriteAllText(diagnosticsLogPath, "");
            WriteRenderSnapshot(
                renderSnapshotPath,
                new TrackerRenderSnapshotRecord(
                    SchemaVersion: 1,
                    ReceivedAt: new DateTimeOffset(2026, 5, 10, 20, 0, 0, TimeSpan.Zero),
                    Frame: CreateFrame(frameNumber: 42)));
            var reader = CreateReader(captureDirectory);

            var result = reader.ReadFrame(diagnosticsLogPath, "42");

            Assert.Null(result.Error);
            Assert.NotNull(result.Snapshot);
            Assert.Equal((uint)42, result.Snapshot.Frame.FrameNumber);
            Assert.Single(result.Snapshot.Frame.Robots);
            Assert.Single(result.Snapshot.Frame.SourceDetections[0].RobotsYellow);
        }
        finally
        {
            if (Directory.Exists(captureDirectory))
            {
                Directory.Delete(captureDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReadIndex_ReturnsSnapshotsByFrameForRepeatedScrubbing()
    {
        // 何を確認しているか: scrubber が繰り返し参照できるように、frame number keyed index が cache されることを確認する。
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-render-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(captureDirectory);
        var diagnosticsLogPath = Path.Combine(captureDirectory, "ssl-vision-packets-test.tracker-diagnostics.log");
        var renderSnapshotPath = Path.Combine(captureDirectory, "ssl-vision-packets-test.render-snapshots.jsonl.gz");

        try
        {
            File.WriteAllText(diagnosticsLogPath, "");
            WriteRenderSnapshots(
                renderSnapshotPath,
                new TrackerRenderSnapshotRecord(
                    SchemaVersion: 1,
                    ReceivedAt: new DateTimeOffset(2026, 5, 10, 20, 0, 0, TimeSpan.Zero),
                    Frame: CreateFrame(frameNumber: 41)),
                new TrackerRenderSnapshotRecord(
                    SchemaVersion: 1,
                    ReceivedAt: new DateTimeOffset(2026, 5, 10, 20, 0, 1, TimeSpan.Zero),
                    Frame: CreateFrame(frameNumber: 42)));
            var reader = CreateReader(captureDirectory);

            var firstResult = reader.ReadIndex(diagnosticsLogPath);
            var secondResult = reader.ReadIndex(diagnosticsLogPath);

            Assert.Null(firstResult.Error);
            Assert.NotNull(firstResult.Index);
            Assert.Same(firstResult.Index, secondResult.Index);
            Assert.Equal((uint)41, firstResult.Index.SnapshotsByFrame[41].Frame.FrameNumber);
            Assert.Equal((uint)42, firstResult.Index.SnapshotsByFrame[42].Frame.FrameNumber);
        }
        finally
        {
            if (Directory.Exists(captureDirectory))
            {
                Directory.Delete(captureDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReadFrame_RejectsDiagnosticsLogOutsideList()
    {
        // 何を確認しているか: packet capture 一覧外の diagnostics log から render snapshot を読ませないことを確認する。
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-render-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(captureDirectory);
        var unlistedDiagnosticsLogPath = Path.Combine(Path.GetTempPath(), $"tracker-diagnostics-unlisted-{Guid.NewGuid():N}.log");

        try
        {
            File.WriteAllText(unlistedDiagnosticsLogPath, "");
            var reader = CreateReader(captureDirectory);

            var result = reader.ReadFrame(unlistedDiagnosticsLogPath, "42");

            Assert.Null(result.Snapshot);
            Assert.NotNull(result.Error);
        }
        finally
        {
            if (Directory.Exists(captureDirectory))
            {
                Directory.Delete(captureDirectory, recursive: true);
            }

            File.Delete(unlistedDiagnosticsLogPath);
        }
    }

    [Fact]
    public void ReadFrame_ReturnsErrorForCorruptRenderSnapshot()
    {
        // 何を確認しているか: gzip として壊れた render snapshot では snapshot を返さず error を返すことを確認する。
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-render-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(captureDirectory);
        var diagnosticsLogPath = Path.Combine(captureDirectory, "ssl-vision-packets-test.tracker-diagnostics.log");
        var renderSnapshotPath = Path.Combine(captureDirectory, "ssl-vision-packets-test.render-snapshots.jsonl.gz");

        try
        {
            File.WriteAllText(diagnosticsLogPath, "");
            File.WriteAllText(renderSnapshotPath, "not gzip");
            var reader = CreateReader(captureDirectory);

            var result = reader.ReadFrame(diagnosticsLogPath, "42");

            Assert.Null(result.Snapshot);
            Assert.NotNull(result.Error);
        }
        finally
        {
            if (Directory.Exists(captureDirectory))
            {
                Directory.Delete(captureDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReadFrame_ReturnsErrorForRenderSnapshotMissingFrame()
    {
        // 何を確認しているか: frame payload が欠落した render snapshot 行では snapshot を返さず error を返すことを確認する。
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-render-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(captureDirectory);
        var diagnosticsLogPath = Path.Combine(captureDirectory, "ssl-vision-packets-test.tracker-diagnostics.log");
        var renderSnapshotPath = Path.Combine(captureDirectory, "ssl-vision-packets-test.render-snapshots.jsonl.gz");

        try
        {
            File.WriteAllText(diagnosticsLogPath, "");
            WriteRenderSnapshotLine(renderSnapshotPath, """{"schemaVersion":1,"receivedAt":"2026-05-10T20:00:00Z"}""");
            var reader = CreateReader(captureDirectory);

            var result = reader.ReadFrame(diagnosticsLogPath, "42");

            Assert.Null(result.Snapshot);
            Assert.NotNull(result.Error);
        }
        finally
        {
            if (Directory.Exists(captureDirectory))
            {
                Directory.Delete(captureDirectory, recursive: true);
            }
        }
    }

    private static TrackerRenderSnapshotLogReader CreateReader(string captureDirectory)
    {
        var diagnosticsLogReader = new TrackerDiagnosticsLogReader(
            Options.Create(new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions
                {
                    DirectoryPath = captureDirectory,
                },
            }),
            new TrackerDiagnosticsOptions());
        return new TrackerRenderSnapshotLogReader(diagnosticsLogReader);
    }

    private static void WriteRenderSnapshot(string path, TrackerRenderSnapshotRecord record)
    {
        WriteRenderSnapshots(path, record);
    }

    private static void WriteRenderSnapshots(string path, params TrackerRenderSnapshotRecord[] records)
    {
        using var fileStream = File.Create(path);
        using var gzipStream = new GZipStream(fileStream, CompressionLevel.SmallestSize);
        using var writer = new StreamWriter(gzipStream);
        foreach (var record in records)
        {
            writer.WriteLine(JsonSerializer.Serialize(record, JsonOptions));
        }
    }

    private static void WriteRenderSnapshotLine(string path, string line)
    {
        using var fileStream = File.Create(path);
        using var gzipStream = new GZipStream(fileStream, CompressionLevel.SmallestSize);
        using var writer = new StreamWriter(gzipStream);
        writer.WriteLine(line);
    }

    private static TrackerFrame CreateFrame(uint frameNumber)
    {
        return new TrackerFrame
        {
            FrameNumber = frameNumber,
            DataTimestampNs = 1_000_000_000,
            GeometrySnapshot = new TrackerGeometrySnapshot
            {
                FieldLengthMm = 12000,
                FieldWidthMm = 9000,
                GoalWidthMm = 1000,
                GoalDepthMm = 180,
                BoundaryWidthMm = 300,
                BoundaryWidthGoalLineMm = 300,
                PenaltyAreaDepthMm = 1000,
                PenaltyAreaWidthMm = 2000,
                CenterCircleRadiusMm = 500,
                LineThicknessMm = 10,
            },
            Balls =
            [
                new TrackedBallState
                {
                    InternalTrackId = 1,
                    XMm = 100,
                    YMm = 50,
                    Visibility = 1,
                },
            ],
            Robots =
            [
                new TrackedRobotState
                {
                    Team = TrackerTeam.Yellow,
                    RobotId = 4,
                    XMm = 80,
                    YMm = 120,
                    OrientationRad = 0.5,
                    Visibility = 1,
                },
            ],
            Metadata = new TrackerFrameMetadata
            {
                ProfileName = "sim",
            },
            SourceDetections =
            [
                new TrackerSourceDetectionFrame
                {
                    SourceFrameNumber = frameNumber,
                    CameraId = 1,
                    Balls =
                    [
                        new SSL_DetectionBall
                        {
                            Confidence = 1,
                            X = 100,
                            Y = 50,
                        },
                    ],
                    RobotsYellow =
                    [
                        new SSL_DetectionRobot
                        {
                            Confidence = 1,
                            RobotId = 4,
                            X = 80,
                            Y = 120,
                            Orientation = 0.5f,
                        },
                    ],
                },
            ],
        };
    }
}
