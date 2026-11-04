using System.Buffers.Binary;
using System.IO.Compression;
using System.Threading;

namespace Duck.Testing.AppHost;

internal sealed record SslLogDecodedInputSummary(
    int RefereeRecords,
    int VisionDetectionRecords,
    int VisionTrackerRecords,
    DateTimeOffset? LastRefereeAtUtc,
    DateTimeOffset? LastVisionDetectionAtUtc,
    DateTimeOffset? LastVisionTrackerAtUtc,
    long DecodedPayloadBytes,
    bool ReachedDecompressedEof,
    string? ReadFailure)
{
    internal static SslLogDecodedInputSummary Empty { get; } =
        new(0, 0, 0, null, null, null, 0, false, null);

    internal bool TryGetFreshEvidenceTime(
        DateTimeOffset containerStartedAtUtc,
        DateTimeOffset now,
        TimeSpan freshnessWindow,
        out DateTimeOffset observedAtUtc)
    {
        observedAtUtc = default;
        if (RefereeRecords == 0 || VisionDetectionRecords == 0 || VisionTrackerRecords == 0 ||
            LastRefereeAtUtc is not { } referee ||
            LastVisionDetectionAtUtc is not { } vision ||
            LastVisionTrackerAtUtc is not { } tracker)
            return false;

        if (!IsCurrentAndFresh(referee, containerStartedAtUtc, now, freshnessWindow) ||
            !IsCurrentAndFresh(vision, containerStartedAtUtc, now, freshnessWindow) ||
            !IsCurrentAndFresh(tracker, containerStartedAtUtc, now, freshnessWindow))
            return false;

        observedAtUtc = referee <= vision && referee <= tracker
            ? referee
            : vision <= tracker ? vision : tracker;
        return true;
    }

    private static bool IsCurrentAndFresh(
        DateTimeOffset recordAtUtc,
        DateTimeOffset containerStartedAtUtc,
        DateTimeOffset now,
        TimeSpan freshnessWindow) =>
        recordAtUtc >= containerStartedAtUtc &&
        recordAtUtc <= now + TimeSpan.FromSeconds(1) &&
        now - recordAtUtc <= freshnessWindow;
}

/// <summary>Reads complete SSL-log format-v1 records and can publish incremental progress.</summary>
internal static class SslLogRecordReader
{
    private const int RefereeMessageType = 3;
    private const int VisionMessageType = 4;
    private const int VisionTrackerMessageType = 5;
    private const int MaximumPayloadBytes = 16 * 1024 * 1024;
    private static readonly byte[] Header = "SSL_LOG_FILE"u8.ToArray();

    internal static SslLogDecodedInputSummary Read(Stream compressedArchive) =>
        ReadCore(compressedArchive, null, CancellationToken.None);

    internal static SslLogDecodedInputSummary ReadAppending(
        Stream compressedArchive,
        Action<SslLogDecodedInputSummary> publishProgress,
        CancellationToken cancellationToken) =>
        ReadCore(compressedArchive, publishProgress, cancellationToken);

    private static SslLogDecodedInputSummary ReadCore(
        Stream compressedArchive,
        Action<SslLogDecodedInputSummary>? publishProgress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compressedArchive);
        cancellationToken.ThrowIfCancellationRequested();
        var refereeCount = 0;
        var visionCount = 0;
        var trackerCount = 0;
        var payloadBytes = 0L;
        DateTimeOffset? lastReferee = null;
        DateTimeOffset? lastVisionDetection = null;
        DateTimeOffset? lastTracker = null;
        var reachedDecompressedEof = false;
        string? readFailure = null;

        using var gzip = new GZipStream(compressedArchive, CompressionMode.Decompress, leaveOpen: true);
        try
        {
            var header = ReadExactly(gzip, Header.Length);
            if (!header.AsSpan().SequenceEqual(Header))
                return BuildSummary("SSL_LOG_FILE header is missing.");
            if (ReadInt32BigEndian(gzip) != 1)
                return BuildSummary("Only SSL log format version 1 is supported.");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var timestampPrefix = gzip.ReadByte();
                if (timestampPrefix < 0)
                {
                    reachedDecompressedEof = true;
                    break;
                }

                var timestampBytes = new byte[sizeof(long)];
                timestampBytes[0] = (byte)timestampPrefix;
                ReadExactly(gzip, timestampBytes.AsSpan(1));
                var receiverTimestampNs = BinaryPrimitives.ReadInt64BigEndian(timestampBytes);
                var messageType = ReadInt32BigEndian(gzip);
                var payloadLength = ReadInt32BigEndian(gzip);
                if (payloadLength <= 0 || payloadLength > MaximumPayloadBytes)
                {
                    readFailure = $"Record payload length {payloadLength} is invalid.";
                    break;
                }

                var payload = ReadExactly(gzip, payloadLength);
                var recordAtUtc = TryConvertUnixNanoseconds(receiverTimestampNs);
                if (recordAtUtc is null)
                {
                    readFailure = "A record has an invalid receiver timestamp.";
                    break;
                }

                payloadBytes += payloadLength;
                var evidenceChanged = false;
                try
                {
                    switch (messageType)
                    {
                        case RefereeMessageType:
                        {
                            var packet = global::Referee.Parser.ParseFrom(payload);
                            if (!packet.HasCommand || !packet.HasCommandCounter)
                                break;
                            refereeCount++;
                            lastReferee = Max(lastReferee, recordAtUtc.Value);
                            evidenceChanged = true;
                            break;
                        }
                        case VisionMessageType:
                        {
                            var packet = global::SSL_WrapperPacket.Parser.ParseFrom(payload);
                            if (packet.Detection is null)
                                break;
                            visionCount++;
                            lastVisionDetection = Max(lastVisionDetection, recordAtUtc.Value);
                            evidenceChanged = true;
                            break;
                        }
                        case VisionTrackerMessageType:
                        {
                            var packet = global::TrackerWrapperPacket.Parser.ParseFrom(payload);
                            if (packet.TrackedFrame is null)
                                break;
                            trackerCount++;
                            lastTracker = Max(lastTracker, recordAtUtc.Value);
                            evidenceChanged = true;
                            break;
                        }
                    }
                }
                catch (Google.Protobuf.InvalidProtocolBufferException)
                {
                    // Invalid payloads do not count as positive evidence; continue with later records.
                }

                if (evidenceChanged)
                    publishProgress?.Invoke(BuildSummary(readFailure));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is EndOfStreamException or InvalidDataException or IOException)
        {
            // Offline archives may end with a partial record; the live append stream waits for more bytes instead.
            readFailure = exception is EndOfStreamException
                ? "The archive ended in a partial record."
                : "The gzip stream ended before a clean footer.";
        }

        return BuildSummary(readFailure);

        SslLogDecodedInputSummary BuildSummary(string? failure) => new(
            refereeCount,
            visionCount,
            trackerCount,
            lastReferee,
            lastVisionDetection,
            lastTracker,
            payloadBytes,
            reachedDecompressedEof,
            failure);
    }

    private static int ReadInt32BigEndian(Stream stream) =>
        BinaryPrimitives.ReadInt32BigEndian(ReadExactly(stream, sizeof(int)));

    private static byte[] ReadExactly(Stream stream, int length)
    {
        var buffer = new byte[length];
        ReadExactly(stream, buffer);
        return buffer;
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer[offset..]);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }

    private static DateTimeOffset? TryConvertUnixNanoseconds(long timestampNanoseconds)
    {
        try
        {
            return DateTimeOffset.UnixEpoch.AddTicks(timestampNanoseconds / 100);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static DateTimeOffset Max(DateTimeOffset? current, DateTimeOffset candidate) =>
        current is { } existing && existing > candidate ? existing : candidate;
}

/// <summary>Waits at the current file end so a single gzip reader can consume later appended bytes.</summary>
internal sealed class AppendWaitFileReadStream(
    FileStream file,
    string path,
    CancellationToken cancellationToken) : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => file.Length;
    public override long Position { get => file.Position; set => throw new NotSupportedException(); }
    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = file.Read(buffer);
            if (read > 0)
                return read;

            if (!File.Exists(path))
                throw new IOException("The recorder archive path was removed while reading.");
            if (file.Position > file.Length)
                throw new IOException("The recorder archive was truncated while reading.");

            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
        }
    }

    public override int ReadByte()
    {
        Span<byte> one = stackalloc byte[1];
        return Read(one) == 0 ? -1 : one[0];
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
