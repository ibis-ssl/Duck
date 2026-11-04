using System.Buffers.Binary;
using System.IO.Compression;
using Google.Protobuf;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class SslLogRecordReaderTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReadsAndDecodesFreshRefereeVisionAndTrackerRecords()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket().ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), VisionPacket().ToByteArray()),
            Record(5, StartedAt.AddSeconds(3), TrackerPacket().ToByteArray()));

        var summary = Read(archive);

        Assert.True(summary.ReachedDecompressedEof);
        Assert.Equal(1, summary.RefereeRecords);
        Assert.Equal(1, summary.VisionDetectionRecords);
        Assert.Equal(1, summary.VisionTrackerRecords);
        Assert.True(summary.DecodedPayloadBytes > 0);
        Assert.True(summary.TryGetFreshEvidenceTime(
            StartedAt,
            StartedAt.AddSeconds(4),
            TimeSpan.FromSeconds(15),
            out var observedAtUtc));
        Assert.Equal(StartedAt.AddSeconds(1), observedAtUtc);
    }

    [Fact]
    public void DoesNotCountGeometryOnlyOrUndecodableRecordsAsFreshDetectionEvidence()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket().ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), new global::SSL_WrapperPacket
            {
                Geometry = new global::SSL_GeometryData(),
            }.ToByteArray()),
            Record(5, StartedAt.AddSeconds(3), TrackerPacket().ToByteArray()),
            Record(4, StartedAt.AddSeconds(4), [0xff]));

        var summary = Read(archive);

        Assert.True(summary.ReachedDecompressedEof);
        Assert.Equal(0, summary.VisionDetectionRecords);
        Assert.False(summary.TryGetFreshEvidenceTime(
            StartedAt,
            StartedAt.AddSeconds(5),
            TimeSpan.FromSeconds(15),
            out _));
    }

    [Fact]
    public void RequiresAllThreeInputTypesToBeFreshWithinCurrentContainerGeneration()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket().ToByteArray()),
            Record(4, StartedAt.AddSeconds(-1), VisionPacket().ToByteArray()),
            Record(5, StartedAt.AddSeconds(3), TrackerPacket().ToByteArray()));

        var summary = Read(archive);

        Assert.False(summary.TryGetFreshEvidenceTime(
            StartedAt,
            StartedAt.AddSeconds(4),
            TimeSpan.FromSeconds(15),
            out _));
        Assert.False(summary.TryGetFreshEvidenceTime(
            StartedAt,
            StartedAt.AddSeconds(40),
            TimeSpan.FromSeconds(15),
            out _));
    }

    [Fact]
    public void KeepsCompleteRecordsBeforeAnIncompleteSslRecordTail()
    {
        var completeArchive = CreateArchiveWithTrailingData(
            [0x01, 0x02],
            Record(3, StartedAt.AddSeconds(1), RefereePacket().ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), VisionPacket().ToByteArray()),
            Record(5, StartedAt.AddSeconds(3), TrackerPacket().ToByteArray()));

        var summary = Read(completeArchive);

        Assert.False(summary.ReachedDecompressedEof);
        Assert.Contains("partial record", summary.ReadFailure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, summary.RefereeRecords);
        Assert.Equal(1, summary.VisionDetectionRecords);
        Assert.Equal(1, summary.VisionTrackerRecords);
        Assert.True(summary.TryGetFreshEvidenceTime(
            StartedAt,
            StartedAt.AddSeconds(4),
            TimeSpan.FromSeconds(15),
            out _));
    }

    [Fact]
    public async Task LiveReaderCompletesSplitRecordAfterAppendAndWaitsForMore()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket().ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), VisionPacket().ToByteArray()),
            Record(5, StartedAt.AddSeconds(3), TrackerPacket().ToByteArray()));
        var path = Path.Combine(Path.GetTempPath(), $"duck-ssl-log-{Guid.NewGuid():N}.log.gz");
        var splitOffset = 0;
        for (var candidate = 1; candidate < archive.Length - 8; candidate++)
        {
            var prefix = Read(archive[..candidate]);
            if (prefix.RefereeRecords == 1 && prefix.VisionDetectionRecords == 1 &&
                prefix.VisionTrackerRecords == 0 && prefix.ReadFailure is not null)
            {
                splitOffset = candidate;
                break;
            }
        }
        Assert.True(splitOffset > 0, "The fixture must split the compressed archive within the final tracker record.");

        var partialArchive = archive[..splitOffset];
        await File.WriteAllBytesAsync(path, partialArchive);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var latest = SslLogDecodedInputSummary.Empty;

        try
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var appendStream = new AppendWaitFileReadStream(file, path, cancellation.Token);
            var reader = Task.Run(() => SslLogRecordReader.ReadAppending(
                appendStream, summary => Volatile.Write(ref latest, summary), cancellation.Token));

            await WaitForSummaryConditionAsync(
                () => Volatile.Read(ref latest),
                summary => summary.RefereeRecords == 1 && summary.VisionDetectionRecords == 1 &&
                    summary.VisionTrackerRecords == 0,
                TimeSpan.FromSeconds(3));
            Assert.Equal(1, Volatile.Read(ref latest).RefereeRecords);
            Assert.Equal(1, Volatile.Read(ref latest).VisionDetectionRecords);
            Assert.Equal(0, Volatile.Read(ref latest).VisionTrackerRecords);

            await using (var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                await writer.WriteAsync(archive[splitOffset..]);
                await writer.FlushAsync();
            }

            await WaitForSummaryConditionAsync(
                () => Volatile.Read(ref latest),
                summary => summary.VisionTrackerRecords == 1,
                TimeSpan.FromSeconds(3));
            Assert.False(reader.IsCompleted, "The append reader must wait at the current file end after decoding the appended record.");
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SslLogDecodedInputSummary Read(byte[] archive)
    {
        using var stream = new MemoryStream(archive, writable: false);
        return SslLogRecordReader.Read(stream);
    }

    private static async Task WaitForSummaryConditionAsync(
        Func<SslLogDecodedInputSummary> getSummary,
        Func<SslLogDecodedInputSummary, bool> condition,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var summary = getSummary();
            if (condition(summary))
                return;
            await Task.Delay(25);
        }
        throw new TimeoutException("The live SSL reader did not publish a complete record from each required type.");
    }

    private static byte[] CreateArchive(params (int Type, DateTimeOffset AtUtc, byte[] Payload)[] records)
        => CreateArchiveWithTrailingData([], records);

    private static byte[] CreateArchiveWithTrailingData(
        byte[] trailingData,
        params (int Type, DateTimeOffset AtUtc, byte[] Payload)[] records)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write("SSL_LOG_FILE"u8);
            WriteInt32(gzip, 1);
            foreach (var record in records)
            {
                WriteInt64(gzip, (record.AtUtc - DateTimeOffset.UnixEpoch).Ticks * 100);
                WriteInt32(gzip, record.Type);
                WriteInt32(gzip, record.Payload.Length);
                gzip.Write(record.Payload);
            }
            gzip.Write(trailingData);
        }
        return output.ToArray();
    }

    private static (int Type, DateTimeOffset AtUtc, byte[] Payload) Record(
        int type,
        DateTimeOffset atUtc,
        byte[] payload) => (type, atUtc, payload);

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static global::Referee RefereePacket() => new()
    {
        PacketTimestamp = 1,
        Command = global::Referee.Types.Command.Stop,
        CommandCounter = 1,
    };

    private static global::SSL_WrapperPacket VisionPacket() => new()
    {
        Detection = new global::SSL_DetectionFrame
        {
            FrameNumber = 1,
            TCapture = 1,
            TSent = 1,
            CameraId = 0,
        },
    };

    private static global::TrackerWrapperPacket TrackerPacket() => new()
    {
        Uuid = "test-source",
        TrackedFrame = new global::TrackedFrame { FrameNumber = 1, Timestamp = 1 },
    };
}
