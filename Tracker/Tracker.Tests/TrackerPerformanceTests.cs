using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Tracker.Core;
using Tracker.Performance;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 固定再生の入力境界、引数検証、製品処理の件数照合を確認する。
/// </summary>
public sealed class TrackerPerformanceTests
{
    private const long Second = 1_000_000_000L;
    private const string Revision = "d215ec0c3b9daef978bf889c15addbe97623da59";

    /// <summary>
    /// 引数を省略した場合に 30 秒窓、7 パス、最低 5 秒の暖機が選ばれることを確認する。
    /// </summary>
    [Fact]
    public void Options_UseDocumentedDefaults()
    {
        var options = CliOptions.Parse(RequiredArguments());

        Assert.Equal(30, options.DurationSeconds);
        Assert.Equal(7, options.Iterations);
        Assert.Equal(5, options.WarmupSeconds);
        Assert.Null(options.Profile);
    }

    /// <summary>
    /// 不正な反復条件ではファイルを開く前に終了し、入力 JSON を出力しないことを確認する。
    /// </summary>
    [Theory]
    [InlineData("--duration-seconds", "0")]
    [InlineData("--duration-seconds", "-1")]
    [InlineData("--iterations", "0")]
    [InlineData("--iterations", "1.5")]
    [InlineData("--warmup-seconds", "4")]
    [InlineData("--warmup-seconds", "abc")]
    public void Cli_RejectsInvalidReplayArguments(string name, string value)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Performance.Program.Run([.. RequiredArguments(), name, value], output, error);

        Assert.Equal(1, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains(name, error.ToString());
    }

    /// <summary>
    /// 別種別の時刻を混ぜず、最初の type 4 から終了時刻の直前までの入力を選ぶことを確認する。
    /// </summary>
    [Fact]
    public void Reader_UsesHalfOpenWindowFromFirstVision()
    {
        var first = VisionPayload(1, 2, captureSeconds: 10);
        var last = VisionPayload(2, 3, captureSeconds: 40);
        using var archive = MakeArchive(1,
            (9 * Second, 3, [], 0),
            (10 * Second, 4, first, first.Length),
            (41 * Second, 3, [], 0),
            (40 * Second - 1, 4, last, last.Length),
            (40 * Second, 4, [], 1));

        var window = VisionLogReader.Read(archive, 30);

        Assert.True(window.ReachedWindowEnd);
        Assert.Equal(new[] { 10 * Second, 40 * Second - 1 },
            window.Inputs.Select(item => item.ReceiverTimestampNs));
        Assert.Equal(new uint[] { 2, 3 }, window.CameraDetectionCounts.Keys);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(archive.ToArray())), window.ArchiveSha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(first.Concat(last).ToArray())), window.PayloadSha256);
    }

    /// <summary>
    /// レコード終端の正常な EOF と、不完全な区間の終了状態を区別できることを確認する。
    /// </summary>
    [Fact]
    public void Reader_AcceptsEofAtARecordBoundary()
    {
        var payload = VisionPayload(1, 2, captureSeconds: 1);
        using var archive = MakeArchive(1, (Second, 4, payload, payload.Length));

        var window = VisionLogReader.Read(archive, 30);

        Assert.Single(window.Inputs);
        Assert.False(window.ReachedWindowEnd);
    }

    /// <summary>
    /// 対応外のログ版の設定や gzip 以外の入力を明示的に拒否することを確認する。
    /// </summary>
    [Fact]
    public void Reader_RejectsUnsupportedFormat()
    {
        using var wrongVersion = MakeArchive(2);
        using var notGzip = new MemoryStream("SSL_LOG_FILE"u8.ToArray());

        Assert.Throws<InvalidDataException>(() => VisionLogReader.Read(wrongVersion, 30));
        Assert.Throws<InvalidDataException>(() => VisionLogReader.Read(notGzip, 30));
    }

    /// <summary>
    /// 負、空の type 4、上限を超える長さで入力を確保しないことを確認する。
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(16777217)]
    public void Reader_RejectsInvalidPayloadLength(int length)
    {
        using var archive = MakeArchive(1, (Second, 4, [], length));

        Assert.Throws<InvalidDataException>(() => VisionLogReader.Read(archive, 30));
    }

    /// <summary>
    /// 指定されたペイロード長の途中で記録が切れた場合に、正常終了扱いにしないことを確認する。
    /// </summary>
    [Fact]
    public void Reader_RejectsTruncatedPayload()
    {
        using var archive = MakeArchive(1, (Second, 4, [1], 2));

        Assert.Throws<EndOfStreamException>(() => VisionLogReader.Read(archive, 30));
    }

    /// <summary>
    /// 選択した入力の受信時刻が逆行した記録を並べ替えずに拒否することを確認する。
    /// </summary>
    [Fact]
    public void Reader_RejectsRegressingReceiveTime()
    {
        var payload = VisionPayload(1, 2, captureSeconds: 1);
        using var archive = MakeArchive(1,
            (2 * Second, 4, payload, payload.Length),
            (Second, 4, payload, payload.Length));

        Assert.Throws<InvalidDataException>(() => VisionLogReader.Read(archive, 30));
    }

    /// <summary>
    /// 選択できる type 4 が無い記録を拒否することを確認する。
    /// </summary>
    [Fact]
    public void Reader_RejectsArchiveWithoutVision()
    {
        using var archive = MakeArchive(1, (Second, 3, [1], 1));

        Assert.Throws<InvalidDataException>(() => VisionLogReader.Read(archive, 30));
    }

    /// <summary>
    /// 同じカメラの最新値、周期境界上の受信、末尾の保留と配信バイト列を固定する。
    /// </summary>
    [Fact]
    public void Replay_ReplacesBeforeTickAndLeavesFinalDetectionPending()
    {
        var first = VisionPayload(1, 2, captureSeconds: 1);
        var replaced = VisionPayload(2, 2, captureSeconds: 1.001);
        var latest = VisionPayload(3, 2, captureSeconds: 1.010);
        using var archive = MakeArchive(1,
            (Second, 4, first, first.Length),
            (Second + 1_000_000, 4, replaced, replaced.Length),
            (Second + 10_000_000, 4, latest, latest.Length));
        var window = VisionLogReader.Read(archive, 30);
        var configuration = new ReplayConfiguration(
            new TrackerRuntimeResolvedOptions
            {
                Enabled = true,
                EngineSettings = new TrackerEngineSettings { MergeWindowNs = 0, ReorderWindowNs = 5_000_000 },
            },
            IntervalMilliseconds: 10,
            Sha256: "");

        var measurement = ReplayRunner.Run(window, configuration, hashOutput: false);
        var quality1 = ReplayRunner.Run(window, configuration, hashOutput: true);
        var quality2 = ReplayRunner.Run(window, configuration, hashOutput: true);

        Assert.Equal(2, measurement.Counts.Cycles);
        Assert.Equal(2, measurement.Counts.EngineUpdates);
        Assert.Equal(1, measurement.Counts.LatestDetectionReplacements);
        Assert.Equal(0, measurement.Counts.LateDrops);
        Assert.Equal(1, measurement.Counts.CommittedDetections);
        Assert.Equal(1, measurement.Counts.CommittedFrames);
        Assert.Equal(1, measurement.Counts.PendingDetections);
        Assert.Equal(1, measurement.Counts.OutputPackets);
        Assert.Equal(10, measurement.VirtualCommitLagMilliseconds.P50);
        Assert.Null(measurement.WireSha256);
        Assert.NotNull(quality1.WireSha256);
        Assert.Equal(quality1.WireSha256, quality2.WireSha256);
        Assert.Equal(measurement.Counts, quality1.Counts);
        Assert.Equal(quality1.Counts, quality2.Counts);
    }

    /// <summary>
    /// 正常な記録でも確定・配信が 0 件なら比較を拒否し、件数と照合結果を診断に残すことを確認する。
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(2, 1, 1)]
    public void Cli_ReplayReportsWhetherTrackedOutputCanBeCompared(
        int detections, int expectedPendingDetections, int expectedOutputPackets)
    {
        var first = detections == 0
            ? TrackerContractTestData.CreateGeometryPacket().ToByteArray()
            : VisionPayload(1, 2, captureSeconds: 1);
        var records = new List<(long Time, int Type, byte[] Payload, int DeclaredLength)>
        {
            (Second, 4, first, first.Length),
        };
        if (detections == 2)
        {
            var second = VisionPayload(2, 2, captureSeconds: 1.016);
            records.Add((Second + 16_000_000, 4, second, second.Length));
        }

        records.Add((2 * Second, 4, first, first.Length));
        using var archive = MakeArchive(1, [.. records]);
        var directory = Directory.CreateTempSubdirectory("duck-tracker-performance-");
        var archivePath = Path.Combine(directory.FullName, "input.log.gz");
        var settingsPath = Path.Combine(directory.FullName, "appsettings.json");
        try
        {
            File.WriteAllBytes(archivePath, archive.ToArray());
            File.WriteAllText(settingsPath, """
                {
                  "Tracker": {
                    "Profiles": {
                      "default": {
                        "Engine": {
                          "ReorderWindowNs": 5000000,
                          "MergeWindowNs": 0
                        }
                      }
                    }
                  },
                  "RuntimeHost": { "OperationLoopIntervalMilliseconds": 16 }
                }
                """);
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = Performance.Program.Run(
                ["--archive", archivePath, "--settings", settingsPath, "--source-revision", Revision,
                 "--duration-seconds", "1", "--iterations", "1"],
                output, error, new WarmupClock());

            var eligible = expectedOutputPackets > 0;
            Assert.Equal(eligible ? 0 : 1, exitCode);
            using var report = JsonDocument.Parse(output.ToString());
            var root = report.RootElement;
            Assert.Equal(eligible, root.GetProperty("comparisonEligible").GetBoolean());
            Assert.Equal(3, root.GetProperty("warmup").GetProperty("passes").GetInt32());
            var input = root.GetProperty("input");
            Assert.True(input.GetProperty("reachedWindowEnd").GetBoolean());
            Assert.Equal(detections == 0 ? 0 : 1, input.GetProperty("cameraCount").GetInt32());
            var quality = root.GetProperty("quality");
            Assert.True(quality.GetProperty("consistentCounts").GetBoolean());
            Assert.True(quality.GetProperty("deterministicWireOutput").GetBoolean());
            foreach (var run in quality.GetProperty("runs").EnumerateArray())
            {
                var counts = run.GetProperty("counts");
                Assert.Equal(expectedOutputPackets, counts.GetProperty("committedFrames").GetInt32());
                Assert.Equal(expectedOutputPackets, counts.GetProperty("outputPackets").GetInt32());
                Assert.Equal(expectedPendingDetections, counts.GetProperty("pendingDetections").GetInt32());
            }

            if (eligible)
            {
                Assert.Empty(error.ToString());
            }
            else
            {
                Assert.Contains("比較に使用できません", error.ToString());
            }
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(archivePath);
            directory.Delete();
        }
    }

    private static string[] RequiredArguments() =>
        ["--archive", "input.log.gz", "--settings", "appsettings.json", "--source-revision", Revision];

    /// <summary>
    /// 記録の受信時刻とは別に、確定条件の検証に必要な検出時刻を設定する。
    /// </summary>
    private static byte[] VisionPayload(uint frame, uint camera, double captureSeconds) =>
        new SSL_WrapperPacket
        {
            Detection = new SSL_DetectionFrame
            {
                CameraId = camera,
                FrameNumber = frame,
                TCapture = captureSeconds,
                TSent = captureSeconds,
            },
        }.ToByteArray();

    /// <summary>
    /// 実ファイルに依存せず、正常例と不正例の両方に必要なヘッダーとペイロードを作る。
    /// </summary>
    private static MemoryStream MakeArchive(
        int version,
        params (long Time, int Type, byte[] Payload, int DeclaredLength)[] records)
    {
        var archive = new MemoryStream();
        using (var gzip = new GZipStream(archive, CompressionLevel.Fastest, leaveOpen: true))
        {
            Span<byte> header = stackalloc byte[16];
            "SSL_LOG_FILE"u8.CopyTo(header);
            BinaryPrimitives.WriteInt32BigEndian(header[12..], version);
            gzip.Write(header);

            foreach (var (time, type, payload, declaredLength) in records)
            {
                BinaryPrimitives.WriteInt64BigEndian(header, time);
                BinaryPrimitives.WriteInt32BigEndian(header[8..], type);
                BinaryPrimitives.WriteInt32BigEndian(header[12..], declaredLength);
                gzip.Write(header);
                gzip.Write(payload);
            }
        }

        archive.Position = 0;
        return archive;
    }

    /// <summary>
    /// 実時間を待たずに暖機の最低時間へ到達させる単調時計。
    /// </summary>
    private sealed class WarmupClock : TimeProvider
    {
        private long seconds;

        /// <summary>
        /// 時計の値を秒単位で返すため、1 秒あたりの刻みは 1 とする。
        /// </summary>
        public override long TimestampFrequency => 1;

        /// <summary>
        /// 呼び出しごとに時計を 5 秒進める。暖機の最低回数は実際の再生で満たす。
        /// </summary>
        public override long GetTimestamp() => seconds += 5;
    }
}
