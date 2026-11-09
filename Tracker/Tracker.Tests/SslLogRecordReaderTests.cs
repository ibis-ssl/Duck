using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json;
using Google.Protobuf;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class SslLogRecordReaderTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReadsNullSimulatorResponseDatagramLengthAsUnavailable()
    {
        using var result = JsonDocument.Parse("""{"destinationPort":10300,"datagramBytes":null}""");

        Assert.Equal(10300, MatchResultJsonReader.ReadInt32(result.RootElement, "destinationPort"));
        Assert.Null(MatchResultJsonReader.ReadInt32(result.RootElement, "datagramBytes"));
        Assert.Null(MatchResultJsonReader.ReadInt32(result.RootElement, "missing"));
    }

    [Fact]
    public void CorrelatesSimulatorSetupResponseWithFirstVisionBallAfterTheSingleSetupSend()
    {
        var setupStartedAt = StartedAt.AddSeconds(2);
        var setupCompletedAt = StartedAt.AddSeconds(2.5);
        var firstVisionAfterSetup = StartedAt.AddSeconds(3);
        var firstVisionBallAfterSetup = StartedAt.AddSeconds(4);
        var archive = CreateArchive(
            Record(4, StartedAt.AddSeconds(2), VisionPacket(new global::SSL_DetectionBall { X = 9, Y = 9 }).ToByteArray()),
            Record(4, firstVisionAfterSetup, VisionPacket().ToByteArray()),
            Record(4, firstVisionBallAfterSetup, VisionPacket(new global::SSL_DetectionBall
            {
                X = 0.3f,
                Y = -0.2f,
                Z = 0.04f,
                Confidence = 0.95f,
            }).ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream, new(
            setupStartedAt,
            setupCompletedAt,
            "no_response_observed",
            "172.18.0.5",
            10300,
            null,
            null,
            null));

        var correlation = Assert.IsType<InitialSetupVisionCorrelation>(report.InitialSetupVisionCorrelation);
        Assert.Equal("vision_ball_observed_after_setup", correlation.Status);
        Assert.Equal("no_response_observed", correlation.Setup.ResponseStatus);
        Assert.Equal("172.18.0.5", correlation.Setup.DestinationIp);
        Assert.Equal(10300, correlation.Setup.DestinationPort);
        Assert.Equal(firstVisionAfterSetup, correlation.FirstVisionFrameAfterSetup?.ReceivedAtUtc);
        Assert.Empty(correlation.FirstVisionFrameAfterSetup!.Balls);
        Assert.Equal(firstVisionBallAfterSetup, correlation.FirstVisionFrameWithBallAfterSetup?.ReceivedAtUtc);
        var ball = Assert.Single(correlation.FirstVisionFrameWithBallAfterSetup!.Balls);
        Assert.Equal(0.3f, ball.X);
        Assert.Equal(-0.2f, ball.Y);
        Assert.Equal(0.04f, ball.Z);
        Assert.Equal(0.95f, ball.Confidence);
    }

    [Fact]
    public void ReportsLastRefereePacketAndLastVisionBallForEndOfMatchDiagnosis()
    {
        var counter42At = StartedAt.AddSeconds(42);
        var finalVisionAt = StartedAt.AddSeconds(43);
        var finalRefereeAt = StartedAt.AddSeconds(44);
        var archive = CreateArchive(
            Record(3, counter42At, new global::Referee
            {
                PacketTimestamp = 1,
                Command = global::Referee.Types.Command.BallPlacementYellow,
                CommandCounter = 42,
                Stage = global::Referee.Types.Stage.NormalFirstHalf,
                NextCommand = global::Referee.Types.Command.NormalStart,
                DesignatedPosition = new global::Referee.Types.Point { X = -2500, Y = 934 },
            }.ToByteArray()),
            Record(3, StartedAt.AddSeconds(42.2), new global::Referee
            {
                PacketTimestamp = 2,
                Command = global::Referee.Types.Command.BallPlacementYellow,
                CommandCounter = 42,
                Stage = global::Referee.Types.Stage.NormalFirstHalf,
                NextCommand = global::Referee.Types.Command.NormalStart,
                DesignatedPosition = new global::Referee.Types.Point { X = -2500, Y = 934 },
            }.ToByteArray()),
            Record(4, StartedAt.AddSeconds(42.5), VisionPacket(new global::SSL_DetectionBall
            {
                X = -2.5f,
                Y = 0.934f,
                Confidence = 0.9f,
            }).ToByteArray()),
            Record(4, finalVisionAt, VisionPacket(new global::SSL_DetectionBall
            {
                X = -2.5f,
                Y = 0.934f,
                Confidence = 0.95f,
            }).ToByteArray()),
            Record(3, finalRefereeAt, RefereePacket(global::Referee.Types.Command.Stop, 43).ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.Equal(finalRefereeAt, report.LastRefereePacket?.ReceivedAtUtc);
        Assert.Equal(43u, report.LastRefereePacket?.CommandCounter);
        Assert.Equal("Stop", report.LastRefereePacket?.Command);
        Assert.Equal(finalVisionAt, report.LastVisionFrameWithBall?.ReceivedAtUtc);
        Assert.Equal(-2.5f, Assert.Single(report.LastVisionFrameWithBall!.Balls).X);
        Assert.Equal(new uint[] { 42, 43 }, report.RefereeTimelineAfterCounter42!.Select(packet => packet.CommandCounter));
        Assert.Equal(finalVisionAt, report.BallTraceAfterCounter42!.Last().ReceivedAtUtc);
    }

    [Fact]
    public void RefereeDiagnosticCoordinatesAreNullWhenNonFiniteAndReportRemainsJsonSerializable()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), new global::Referee
            {
                PacketTimestamp = 1,
                Command = global::Referee.Types.Command.BallPlacementYellow,
                CommandCounter = 42,
                Stage = global::Referee.Types.Stage.NormalSecondHalf,
                DesignatedPosition = new global::Referee.Types.Point { X = float.NaN, Y = float.PositiveInfinity },
            }.ToByteArray()),
            Record(3, StartedAt.AddSeconds(2), new global::Referee
            {
                PacketTimestamp = 2,
                Command = global::Referee.Types.Command.BallPlacementYellow,
                CommandCounter = 43,
                Stage = global::Referee.Types.Stage.NormalSecondHalf,
                DesignatedPosition = new global::Referee.Types.Point { X = float.NegativeInfinity, Y = 1 },
            }.ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.Null(report.LastRefereePacket?.DesignatedPositionXMillimeters);
        Assert.Equal(1f, report.LastRefereePacket?.DesignatedPositionYMillimeters);
        Assert.True(report.RecordStreamComplete);
        Assert.False(report.Passed);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.Equal(JsonValueKind.Null,
            json.RootElement.GetProperty("LastRefereePacket").GetProperty("DesignatedPositionXMillimeters").ValueKind);
    }

    [Fact]
    public void RefereePointWithMissingRequiredCoordinateIsNotReportedAsZero()
    {
        var validPlacement = new global::Referee
        {
            PacketTimestamp = 1,
            Command = global::Referee.Types.Command.BallPlacementYellow,
            CommandCounter = 42,
            Stage = global::Referee.Types.Stage.NormalSecondHalf,
            DesignatedPosition = new global::Referee.Types.Point { X = -2500, Y = 934 },
        }.ToByteArray();
        var missingCoordinate = new global::Referee
        {
            PacketTimestamp = 2,
            Command = global::Referee.Types.Command.BallPlacementYellow,
            CommandCounter = 43,
            Stage = global::Referee.Types.Stage.NormalSecondHalf,
        }.ToByteArray().Concat(new byte[] { 0x4a, 0x00 }).ToArray();
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), validPlacement),
            Record(3, StartedAt.AddSeconds(2), missingCoordinate));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.True(report.RecordStreamComplete);
        Assert.Equal(43u, report.LastRefereePacket?.CommandCounter);
        Assert.Null(report.LastRefereePacket?.DesignatedPositionXMillimeters);
        Assert.Null(report.LastRefereePacket?.DesignatedPositionYMillimeters);
        JsonDocument.Parse(JsonSerializer.Serialize(report)).Dispose();
    }

    [Fact]
    public void Counter42BallTraceDoesNotReusePreCounterBallWhenNoLaterBallExists()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.Stop, 41).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), VisionPacket(new global::SSL_DetectionBall { X = 1, Y = 2 }).ToByteArray()),
            Record(3, StartedAt.AddSeconds(3), RefereePacket(global::Referee.Types.Command.BallPlacementYellow, 42).ToByteArray()),
            Record(4, StartedAt.AddSeconds(4), VisionPacket().ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.Equal(StartedAt.AddSeconds(2), report.LastVisionFrameWithBall?.ReceivedAtUtc);
        Assert.Empty(report.BallTraceAfterCounter42!);
    }

    [Theory]
    [InlineData(256, false)]
    [InlineData(257, true)]
    [InlineData(300, true)]
    public void Counter42DiagnosticsStayBoundedAndRetainLatestEvidence(int evidenceCount, bool expectTruncated)
    {
        var records = new List<(int Type, DateTimeOffset AtUtc, byte[] Payload)>();
        for (var index = 0; index < evidenceCount; index++)
        {
            var at = StartedAt.AddSeconds(index * 11);
            records.Add(Record(3, at, new global::Referee
            {
                PacketTimestamp = (ulong)index + 1,
                Command = global::Referee.Types.Command.Stop,
                CommandCounter = 42,
                Stage = global::Referee.Types.Stage.NormalSecondHalf,
                Blue = new global::Referee.Types.TeamInfo { Score = (uint)index },
            }.ToByteArray()));
            records.Add(Record(4, at.AddSeconds(1), VisionPacket(
                new global::SSL_DetectionBall { X = index, Y = 0 }).ToByteArray()));
        }

        using var stream = new MemoryStream(CreateArchive([.. records]), writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.Equal(256, report.RefereeTimelineAfterCounter42!.Count);
        Assert.Equal(expectTruncated, report.RefereeTimelineAfterCounter42Truncated);
        Assert.Equal((uint)(evidenceCount - 1), report.RefereeTimelineAfterCounter42[^1].BlueScore);
        Assert.Equal(256, report.BallTraceAfterCounter42!.Count);
        Assert.Equal(expectTruncated, report.BallTraceAfterCounter42Truncated);
        Assert.Equal(StartedAt.AddSeconds((evidenceCount - 1) * 11 + 1),
            report.BallTraceAfterCounter42[^1].ReceivedAtUtc);
    }

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
    public void MotionAcceptanceRequiresBothTeamsToMoveInTheSameActiveCommandWindow()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, 7).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), MotionPacket(0, 0, 10, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(3), MotionPacket(20, 0, 10, 0).ToByteArray()),
            Record(3, StartedAt.AddSeconds(4), RefereePacket(global::Referee.Types.Command.ForceStart, 8).ToByteArray()),
            Record(4, StartedAt.AddSeconds(5), MotionPacket(20, 0, 10, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(6), MotionPacket(20, 0, 30, 0).ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.True(report.RecordStreamComplete);
        Assert.False(report.Passed);
        Assert.Equal(2, report.ActiveWindows.Count);
        Assert.True(report.ActiveWindows[0].CraneMoved);
        Assert.False(report.ActiveWindows[0].TigersMoved);
        Assert.False(report.ActiveWindows[1].CraneMoved);
        Assert.True(report.ActiveWindows[1].TigersMoved);
    }

    [Fact]
    public void MotionAcceptancePassesWhenBothTeamsMoveInOneActiveWindow()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, 7).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), AddUnknownWrapperField(MotionPacket(0, 0, 10, 0).ToByteArray())),
            Record(4, StartedAt.AddSeconds(3), AddUnknownWrapperField(MotionPacket(20, 0, 30, 0).ToByteArray())));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.True(report.RecordStreamComplete);
        Assert.True(report.Passed);
        Assert.True(report.ActiveWindows.Single().CraneMoved);
        Assert.True(report.ActiveWindows.Single().TigersMoved);
    }

    [Fact]
    public void MotionAcceptanceRejectsSplitDetectionFieldsThatWouldMergeIntoBothTeamsMoving()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, 7).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), SplitMotionPackets(0, 10)),
            Record(4, StartedAt.AddSeconds(3), SplitMotionPackets(20, 30)));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.True(report.RecordStreamComplete);
        Assert.False(report.Passed);
        Assert.Equal(2, report.VisionRecords);
        Assert.False(report.ActiveWindows.Single().CraneMoved);
        Assert.False(report.ActiveWindows.Single().TigersMoved);
    }

    [Fact]
    public void MotionAcceptanceAcceptsForwardCounterWrapAndDoesNotReopenStaleCommands()
    {
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, uint.MaxValue).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), MotionPacket(0, 0, 10, 0).ToByteArray()),
            Record(3, StartedAt.AddSeconds(3), RefereePacket(global::Referee.Types.Command.NormalStart, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(4), MotionPacket(0, 0, 10, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(5), MotionPacket(20, 0, 30, 0).ToByteArray()),
            Record(3, StartedAt.AddSeconds(6), RefereePacket(global::Referee.Types.Command.Stop, 1).ToByteArray()),
            Record(3, StartedAt.AddSeconds(7), RefereePacket(global::Referee.Types.Command.NormalStart, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(8), MotionPacket(20, 0, 30, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(9), MotionPacket(40, 0, 50, 0).ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.True(report.RecordStreamComplete);
        Assert.True(report.Passed);
        Assert.Equal(new uint[] { uint.MaxValue, 0 }, report.ActiveWindows.Take(2).Select(window => window.CommandCounter));
        Assert.Equal(2, report.ActiveWindows.Count);
        Assert.True(report.ActiveWindows[1].CraneMoved);
        Assert.True(report.ActiveWindows[1].TigersMoved);
    }

    [Fact]
    public void MalformedVisionWrapperDoesNotProvideMotionEvidence()
    {
        var validPrefix = MotionPacket(0, 0, 10, 0).ToByteArray();
        var malformed = validPrefix.Concat(new byte[] { 0xff }).ToArray();
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, 7).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), malformed),
            Record(4, StartedAt.AddSeconds(3), malformed));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.True(report.RecordStreamComplete);
        Assert.False(report.Passed);
        Assert.Equal(0, report.ActiveWindows.Single().CraneMovingRobotCount);
        Assert.Equal(0, report.ActiveWindows.Single().TigersMovingRobotCount);
    }

    [Fact]
    public void MissingRobotCoordinatesCannotBeTreatedAsMovementToTheOrigin()
    {
        var first = new global::SSL_DetectionFrame();
        first.RobotsYellow.Add(new global::SSL_DetectionRobot { RobotId = 1, X = 200, Y = 300 });
        first.RobotsBlue.Add(new global::SSL_DetectionRobot { RobotId = 2, X = -200, Y = -300 });
        var missingCoordinates = new global::SSL_DetectionFrame();
        missingCoordinates.RobotsYellow.Add(new global::SSL_DetectionRobot { RobotId = 1 });
        missingCoordinates.RobotsBlue.Add(new global::SSL_DetectionRobot { RobotId = 2 });
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, 7).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), new global::SSL_WrapperPacket { Detection = first }.ToByteArray()),
            Record(4, StartedAt.AddSeconds(3), new global::SSL_WrapperPacket { Detection = missingCoordinates }.ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.True(report.RecordStreamComplete);
        Assert.False(report.Passed);
        Assert.False(report.ActiveWindows.Single().CraneMoved);
        Assert.False(report.ActiveWindows.Single().TigersMoved);
    }

    [Fact]
    public void PartialRecordTailRetainsMotionEvidenceButCannotPassAcceptance()
    {
        var archive = CreateArchiveWithTrailingData(
            [0x01, 0x02],
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, 7).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), MotionPacket(0, 0, 10, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(3), MotionPacket(20, 0, 30, 0).ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.False(report.RecordStreamComplete);
        Assert.False(report.Passed);
        Assert.True(report.PartialEvidencePreserved);
        Assert.True(report.ActiveWindows.Single().CraneMoved);
        Assert.True(report.ActiveWindows.Single().TigersMoved);
    }

    [Fact]
    public void MissingGzipFooterCannotPassEvenWhenBothTeamsMoved()
    {
        var complete = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, 7).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), MotionPacket(0, 0, 10, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(3), MotionPacket(20, 0, 30, 0).ToByteArray()));
        var truncated = complete[..^8];

        using var stream = new MemoryStream(truncated, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.False(report.RecordStreamComplete);
        Assert.False(report.Passed);
        Assert.True(report.PartialEvidencePreserved);
        Assert.True(report.ActiveWindows.Single().CraneMoved);
        Assert.True(report.ActiveWindows.Single().TigersMoved);
    }

    [Fact]
    public void TrailingBytesAndCopiedFooterCannotPassAsACompleteGzipMember()
    {
        var complete = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, 7).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), MotionPacket(0, 0, 10, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(3), MotionPacket(20, 0, 30, 0).ToByteArray()));
        var trailing = new byte[complete.Length + 2 + 8];
        complete.CopyTo(trailing, 0);
        trailing[complete.Length] = 0x55;
        trailing[complete.Length + 1] = 0x66;
        complete.AsSpan(complete.Length - 8).CopyTo(trailing.AsSpan(complete.Length + 2));

        using var stream = new MemoryStream(trailing, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.False(report.RecordStreamComplete);
        Assert.False(report.Passed);
        Assert.True(report.ActiveWindows.Single().CraneMoved);
        Assert.True(report.ActiveWindows.Single().TigersMoved);
    }

    [Fact]
    public void RefereeCounterOutsideUInt32RangeCannotOpenAnActiveWindow()
    {
        // Referee command=NORMAL_START (field 4), command_counter=0x1_0000_0000 (field 5).
        byte[] outOfRangeReferee = [0x20, 0x02, 0x28, 0x80, 0x80, 0x80, 0x80, 0x10];
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), outOfRangeReferee),
            Record(4, StartedAt.AddSeconds(2), MotionPacket(0, 0, 10, 0).ToByteArray()),
            Record(4, StartedAt.AddSeconds(3), MotionPacket(20, 0, 30, 0).ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.True(report.RecordStreamComplete);
        Assert.False(report.Passed);
        Assert.Empty(report.ActiveWindows);
    }

    [Fact]
    public void MotionAcceptanceRejectsDuplicateRobotIdsWithinAFrame()
    {
        var duplicateFrame = new global::SSL_DetectionFrame();
        duplicateFrame.RobotsYellow.Add(new global::SSL_DetectionRobot { RobotId = 1, X = 0, Y = 0 });
        duplicateFrame.RobotsYellow.Add(new global::SSL_DetectionRobot { RobotId = 1, X = 1, Y = 0 });
        duplicateFrame.RobotsBlue.Add(new global::SSL_DetectionRobot { RobotId = 2, X = 0, Y = 0 });
        var archive = CreateArchive(
            Record(3, StartedAt.AddSeconds(1), RefereePacket(global::Referee.Types.Command.NormalStart, 7).ToByteArray()),
            Record(4, StartedAt.AddSeconds(2), new global::SSL_WrapperPacket { Detection = duplicateFrame }.ToByteArray()));

        using var stream = new MemoryStream(archive, writable: false);
        var report = SslLogMotionAcceptanceVerifier.Read(stream);

        Assert.True(report.RecordStreamComplete);
        Assert.False(report.Passed);
        Assert.False(report.ActiveWindows.Single().CraneMoved);
        Assert.False(report.ActiveWindows.Single().TigersMoved);
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

    private static global::Referee RefereePacket(
        global::Referee.Types.Command command = global::Referee.Types.Command.Stop,
        uint counter = 1) => new()
    {
        PacketTimestamp = 1,
        Command = command,
        CommandCounter = counter,
    };

    private static global::SSL_WrapperPacket MotionPacket(float yellowX, float yellowY, float blueX, float blueY)
    {
        var frame = new global::SSL_DetectionFrame();
        frame.RobotsYellow.Add(new global::SSL_DetectionRobot { RobotId = 1, X = yellowX, Y = yellowY });
        frame.RobotsBlue.Add(new global::SSL_DetectionRobot { RobotId = 2, X = blueX, Y = blueY });
        return new global::SSL_WrapperPacket { Detection = frame };
    }

    private static byte[] SplitMotionPackets(float yellowX, float blueX)
    {
        var yellowFrame = new global::SSL_DetectionFrame();
        yellowFrame.RobotsYellow.Add(new global::SSL_DetectionRobot { RobotId = 1, X = yellowX, Y = 0 });
        var blueFrame = new global::SSL_DetectionFrame();
        blueFrame.RobotsBlue.Add(new global::SSL_DetectionRobot { RobotId = 2, X = blueX, Y = 0 });

        return new global::SSL_WrapperPacket { Detection = yellowFrame }.ToByteArray()
            .Concat(new global::SSL_WrapperPacket { Detection = blueFrame }.ToByteArray())
            .ToArray();
    }

    private static byte[] AddUnknownWrapperField(byte[] packet) =>
        [.. packet, 0xa0, 0x06, 0x07];

    private static global::SSL_WrapperPacket VisionPacket(params global::SSL_DetectionBall[] balls)
    {
        var frame = new global::SSL_DetectionFrame
        {
            FrameNumber = 1,
            TCapture = 1,
            TSent = 1,
            CameraId = 0,
        };
        frame.Balls.AddRange(balls);
        return new global::SSL_WrapperPacket { Detection = frame };
    }

    private static global::TrackerWrapperPacket TrackerPacket() => new()
    {
        Uuid = "test-source",
        TrackedFrame = new global::TrackedFrame { FrameNumber = 1, Timestamp = 1 },
    };
}
