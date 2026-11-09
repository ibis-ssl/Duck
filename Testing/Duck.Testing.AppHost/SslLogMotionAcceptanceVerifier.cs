using System.Buffers.Binary;
using System.IO.Compression;

namespace Duck.Testing.AppHost;

internal sealed record SslLogMotionAcceptanceReport(
    bool Passed,
    bool RecordStreamComplete,
    int RecordCount,
    int RefereeRecords,
    int VisionRecords,
    bool PartialEvidencePreserved,
    IReadOnlyList<SslLogMotionWindow> ActiveWindows,
    string? Reason,
    InitialSetupVisionCorrelation? InitialSetupVisionCorrelation = null,
    SslLogRefereePacketSnapshot? LastRefereePacket = null,
    SslLogVisionFrameWithBalls? LastVisionFrameWithBall = null,
    IReadOnlyList<SslLogRefereePacketSnapshot>? RefereeTimelineAfterCounter42 = null,
    IReadOnlyList<SslLogVisionFrameWithBalls>? BallTraceAfterCounter42 = null);

internal sealed record InitialSimulatorSetupEvidence(
    DateTimeOffset? SendStartedAtUtc,
    DateTimeOffset? SendCompletedAtUtc,
    string? ResponseStatus,
    string? DestinationIp,
    int? DestinationPort,
    string? RemoteEndpoint,
    int? ResponseDatagramBytes,
    DateTimeOffset? ResponseReceivedAtUtc);

internal sealed record SslLogVisionBall(float X, float Y, float Z, float Confidence);

internal sealed record SslLogVisionFrameWithBalls(
    DateTimeOffset ReceivedAtUtc,
    uint FrameNumber,
    uint CameraId,
    IReadOnlyList<SslLogVisionBall> Balls);

internal sealed record SslLogRefereePacketSnapshot(
    DateTimeOffset ReceivedAtUtc,
    string Command,
    uint CommandCounter,
    string? Stage,
    string? NextCommand,
    uint? BlueScore,
    uint? YellowScore,
    float? DesignatedPositionXMillimeters,
    float? DesignatedPositionYMillimeters,
    bool? BlueTeamOnPositiveHalf);

internal sealed record InitialSetupVisionCorrelation(
    string Status,
    InitialSimulatorSetupEvidence Setup,
    SslLogVisionFrameWithBalls? FirstVisionFrameAfterSetup,
    SslLogVisionFrameWithBalls? FirstVisionFrameWithBallAfterSetup);

internal sealed record SslLogMotionWindow(
    uint CommandCounter,
    int Command,
    int VisionPackets,
    int CraneMovingRobotCount,
    int TigersMovingRobotCount)
{
    internal bool CraneMoved => CraneMovingRobotCount > 0;
    internal bool TigersMoved => TigersMovingRobotCount > 0;
}

/// <summary>Verifies that both participating teams move within the same active referee command window.</summary>
internal static class SslLogMotionAcceptanceVerifier
{
    // ssl-proto's referee command numbers: NORMAL_START, FORCE_START, and direct/indirect free kicks.
    private static readonly HashSet<int> ActiveCommands = [2, 3, 8, 9, 10, 11];
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    internal static SslLogMotionAcceptanceReport Read(
        Stream archive,
        InitialSimulatorSetupEvidence? initialSetupEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(archive);
        using var compressed = new MemoryStream();
        archive.CopyTo(compressed);
        var bytes = compressed.ToArray();
        compressed.Position = 0;
        var records = 0;
        var refereeRecords = 0;
        var visionRecords = 0;
        uint? lastCounter = null;
        (uint Counter, int Command)? activeKey = null;
        Window? activeWindow = null;
        var windows = new List<Window>();
        SslLogVisionFrameWithBalls? firstVisionFrameAfterSetup = null;
        SslLogVisionFrameWithBalls? firstVisionFrameWithBallAfterSetup = null;
        SslLogRefereePacketSnapshot? lastRefereePacket = null;
        SslLogVisionFrameWithBalls? lastVisionFrameWithBall = null;
        SslLogRefereePacketSnapshot? lastRefereeTimelineEntry = null;
        DateTimeOffset? counter42ObservedAtUtc = null;
        DateTimeOffset? lastBallTraceSampleAtUtc = null;
        var refereeTimelineAfterCounter42 = new List<SslLogRefereePacketSnapshot>();
        var ballTraceAfterCounter42 = new List<SslLogVisionFrameWithBalls>();

        var summary = SslLogRecordReader.ReadMotionEvidence(compressed, (recordAtUtc, type, referee, frame, payload) =>
        {
            records++;
            if (type == 3 && referee is { HasCommand: true, HasCommandCounter: true } packet &&
                HasUInt32RefereeCounter(payload.Span))
            {
                var designatedPosition = packet.DesignatedPosition;
                lastRefereePacket = new(
                    recordAtUtc,
                    packet.Command.ToString(),
                    packet.CommandCounter,
                    packet.HasStage ? packet.Stage.ToString() : null,
                    packet.HasNextCommand ? packet.NextCommand.ToString() : null,
                    packet.Blue is null ? null : packet.Blue.Score,
                    packet.Yellow is null ? null : packet.Yellow.Score,
                    designatedPosition?.X,
                    designatedPosition?.Y,
                    packet.HasBlueTeamOnPositiveHalf ? packet.BlueTeamOnPositiveHalf : null);

                if (packet.CommandCounter >= 42)
                {
                    counter42ObservedAtUtc ??= recordAtUtc;
                    if (!SameRefereeState(lastRefereeTimelineEntry, lastRefereePacket))
                    {
                        refereeTimelineAfterCounter42.Add(lastRefereePacket);
                        lastRefereeTimelineEntry = lastRefereePacket;
                    }
                }
            }

            if (type == 4 && frame is not null &&
                (initialSetupEvidence?.SendCompletedAtUtc is not { } setupSentAtUtc || recordAtUtc >= setupSentAtUtc))
            {
                var balls = frame.Balls
                    .Where(ball => ball.HasX && ball.HasY && float.IsFinite(ball.X) && float.IsFinite(ball.Y))
                    .Select(ball => new SslLogVisionBall(
                        ball.X,
                        ball.Y,
                        ball.HasZ && float.IsFinite(ball.Z) ? ball.Z : 0,
                        ball.HasConfidence && float.IsFinite(ball.Confidence) ? ball.Confidence : 0))
                    .ToArray();
                var sample = new SslLogVisionFrameWithBalls(recordAtUtc, frame.FrameNumber, frame.CameraId, balls);
                if (balls.Length > 0)
                {
                    lastVisionFrameWithBall = sample;
                    if (counter42ObservedAtUtc is not null &&
                        (lastBallTraceSampleAtUtc is null || recordAtUtc - lastBallTraceSampleAtUtc >= TimeSpan.FromSeconds(10)))
                    {
                        ballTraceAfterCounter42.Add(sample);
                        lastBallTraceSampleAtUtc = recordAtUtc;
                    }
                    if (initialSetupEvidence?.SendCompletedAtUtc is not null)
                    {
                        firstVisionFrameAfterSetup ??= sample;
                        firstVisionFrameWithBallAfterSetup ??= sample;
                    }
                }

                if (initialSetupEvidence?.SendCompletedAtUtc is not null)
                    firstVisionFrameAfterSetup ??= sample;
            }

            if (type == 3)
            {
                refereeRecords++;
                if (referee is null || !referee.HasCommand || !referee.HasCommandCounter ||
                    !HasUInt32RefereeCounter(payload.Span))
                {
                    activeKey = null;
                    activeWindow = null;
                    return;
                }

                var counter = referee.CommandCounter;
                var command = (int)referee.Command;
                if (lastCounter is { } previous)
                {
                    if (counter == previous)
                    {
                        if (activeKey != (counter, command) || activeWindow is null)
                        {
                            activeKey = null;
                            activeWindow = null;
                        }
                        return;
                    }

                    var distance = unchecked(counter - previous);
                    if (distance >= 0x80000000)
                    {
                        activeKey = null;
                        activeWindow = null;
                        return;
                    }
                }

                lastCounter = counter;
                if (!ActiveCommands.Contains(command))
                {
                    activeKey = null;
                    activeWindow = null;
                    return;
                }

                activeKey = (counter, command);
                activeWindow = new Window(counter, command);
                windows.Add(activeWindow);
                return;
            }

            if (type != 4)
                return;
            visionRecords++;
            if (frame is null || activeWindow is null)
                return;
            activeWindow.VisionPackets++;
            Observe(frame.RobotsYellow, activeWindow.Crane);
            Observe(frame.RobotsBlue, activeWindow.Tigers);
        });

        var gzipFooterValid = HasValidGzipFooter(bytes);
        var complete = summary.ReachedDecompressedEof && summary.ReadFailure is null && gzipFooterValid;
        var reportedWindows = windows.Select(window => window.ToReport()).ToArray();
        var passed = complete && reportedWindows.Any(window => window.CraneMoved && window.TigersMoved);
        var reason = !complete
            ? summary.ReadFailure ?? (!gzipFooterValid
                ? "The gzip member footer checksum or uncompressed size is missing or invalid."
                : "The SSL log record stream did not reach a clean decompressed EOF.")
            : passed ? null : "No active referee command window contained position changes for both teams.";
        var setupCorrelation = initialSetupEvidence is null
            ? null
            : new InitialSetupVisionCorrelation(
                initialSetupEvidence.SendCompletedAtUtc is null
                    ? "setup_send_time_unavailable"
                    : firstVisionFrameWithBallAfterSetup is not null
                        ? "vision_ball_observed_after_setup"
                        : firstVisionFrameAfterSetup is not null
                            ? "vision_frame_without_ball_after_setup"
                            : "no_vision_frame_observed_after_setup",
                initialSetupEvidence,
                firstVisionFrameAfterSetup,
                firstVisionFrameWithBallAfterSetup);
        if (counter42ObservedAtUtc is not null && lastVisionFrameWithBall is not null &&
            (ballTraceAfterCounter42.Count == 0 ||
             ballTraceAfterCounter42[^1].ReceivedAtUtc != lastVisionFrameWithBall.ReceivedAtUtc))
            ballTraceAfterCounter42.Add(lastVisionFrameWithBall);
        return new(passed, complete, records, refereeRecords, visionRecords, records > 0, reportedWindows, reason,
            setupCorrelation, lastRefereePacket, lastVisionFrameWithBall,
            refereeTimelineAfterCounter42, ballTraceAfterCounter42);
    }

    private static bool SameRefereeState(
        SslLogRefereePacketSnapshot? previous,
        SslLogRefereePacketSnapshot current) =>
        previous is not null &&
        previous.Command == current.Command &&
        previous.CommandCounter == current.CommandCounter &&
        previous.Stage == current.Stage &&
        previous.NextCommand == current.NextCommand &&
        previous.BlueScore == current.BlueScore &&
        previous.YellowScore == current.YellowScore &&
        previous.DesignatedPositionXMillimeters == current.DesignatedPositionXMillimeters &&
        previous.DesignatedPositionYMillimeters == current.DesignatedPositionYMillimeters &&
        previous.BlueTeamOnPositiveHalf == current.BlueTeamOnPositiveHalf;

    // Protobuf's uint32 parser truncates an oversized varint, so validate the raw field too.
    private static bool HasUInt32RefereeCounter(ReadOnlySpan<byte> payload)
    {
        var offset = 0;
        var found = false;
        while (offset < payload.Length)
        {
            if (!TryReadVarint(payload, ref offset, out var tag) || tag == 0)
                return false;
            var field = (int)(tag >> 3);
            var wire = (int)(tag & 7);
            if (field == 5)
            {
                if (wire != 0 || !TryReadVarint(payload, ref offset, out var value) || value > uint.MaxValue)
                    return false;
                found = true;
                continue;
            }

            switch (wire)
            {
                case 0:
                    if (!TryReadVarint(payload, ref offset, out _)) return false;
                    break;
                case 1:
                    if (payload.Length - offset < 8) return false;
                    offset += 8;
                    break;
                case 2:
                    if (!TryReadVarint(payload, ref offset, out var length) || length > (ulong)(payload.Length - offset)) return false;
                    offset += (int)length;
                    break;
                case 5:
                    if (payload.Length - offset < 4) return false;
                    offset += 4;
                    break;
                default:
                    return false;
            }
        }
        return found;
    }

    private static bool TryReadVarint(ReadOnlySpan<byte> payload, ref int offset, out ulong value)
    {
        value = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (offset >= payload.Length)
                return false;
            var current = payload[offset++];
            if (shift == 63 && current > 1)
                return false;
            value |= (ulong)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
                return true;
        }
        return false;
    }

    private static bool HasValidGzipFooter(byte[] compressed)
    {
        if (compressed.Length < 18)
            return false;

        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(compressed.AsSpan(compressed.Length - 8, 4));
        var expectedSize = BinaryPrimitives.ReadUInt32LittleEndian(compressed.AsSpan(compressed.Length - 4, 4));
        uint crc = uint.MaxValue;
        uint size = 0;
        try
        {
            using var input = new MemoryStream(compressed, writable: false);
            using var exactInput = new SingleByteReadStream(input);
            using var gzip = new GZipStream(exactInput, CompressionMode.Decompress, leaveOpen: true);
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                size = unchecked(size + (uint)read);
                for (var index = 0; index < read; index++)
                    crc = Crc32Table[(crc ^ buffer[index]) & 0xff] ^ (crc >> 8);
            }
            if (exactInput.BytesRead != compressed.Length)
                return false;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return false;
        }

        return ~crc == expectedCrc && size == expectedSize;
    }

    private sealed class SingleByteReadStream(Stream inner) : Stream
    {
        internal long BytesRead => inner.Position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => count == 0 ? 0 : inner.Read(buffer, offset, 1);
        public override int Read(Span<byte> buffer) => buffer.IsEmpty ? 0 : inner.Read(buffer[..1]);
        public override int ReadByte() => inner.ReadByte();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint value = 0; value < table.Length; value++)
        {
            var crc = value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
            table[value] = crc;
        }
        return table;
    }

    private static void Observe(IEnumerable<global::SSL_DetectionRobot> robots, Dictionary<uint, PositionState> observed)
    {
        var framePositions = new Dictionary<uint, (int Count, float X, float Y)>();
        foreach (var robot in robots)
        {
            if (!robot.HasRobotId || !robot.HasX || !robot.HasY ||
                !float.IsFinite(robot.X) || !float.IsFinite(robot.Y))
                continue;
            if (framePositions.TryGetValue(robot.RobotId, out var current))
                framePositions[robot.RobotId] = (current.Count + 1, robot.X, robot.Y);
            else
                framePositions.Add(robot.RobotId, (1, robot.X, robot.Y));
        }

        foreach (var (id, position) in framePositions)
        {
            if (position.Count != 1)
                continue;
            if (observed.TryGetValue(id, out var prior))
            {
                if (prior.X != position.X || prior.Y != position.Y)
                    prior.Moved = true;
                prior.X = position.X;
                prior.Y = position.Y;
            }
            else
            {
                observed.Add(id, new PositionState(position.X, position.Y));
            }
        }
    }

    private sealed class Window(uint counter, int command)
    {
        internal int VisionPackets { get; set; }
        internal Dictionary<uint, PositionState> Crane { get; } = [];
        internal Dictionary<uint, PositionState> Tigers { get; } = [];
        internal SslLogMotionWindow ToReport() => new(
            counter, command, VisionPackets,
            Crane.Values.Count(robot => robot.Moved),
            Tigers.Values.Count(robot => robot.Moved));
    }

    private sealed class PositionState(float x, float y)
    {
        internal float X { get; set; } = x;
        internal float Y { get; set; } = y;
        internal bool Moved { get; set; }
    }
}
