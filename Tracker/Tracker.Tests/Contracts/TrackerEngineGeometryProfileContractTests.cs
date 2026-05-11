using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TrackerEngine の geometry snapshot、geometry reset、profile switch contract を検証する。
/// </summary>
public class TrackerEngineGeometryProfileContractTests : TrackerEngineContractTestBase, IClassFixture<TrackerContractFixture>
{
    public TrackerEngineGeometryProfileContractTests(TrackerContractFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// 何を確認しているか: geometry packet の表示用 field / goal 情報が geometry snapshot に保持されることを確認する。
    /// </summary>
    [Fact]
    public void Update_PreservesDisplayGeometryInGeometrySnapshot()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        // tracked field 表示で defense area や center circle が消えないよう、描画用 geometry 寸法を保持することを確認する。
        var geometryPacket = TrackerContractTestData.CreateGeometryPacket(
            fieldLength: 12000,
            fieldWidth: 9000,
            boundaryWidth: 300,
            boundaryWidthGoalLine: 350,
            penaltyAreaDepth: 1200,
            penaltyAreaWidth: 2400,
            centerCircleRadius: 600,
            lineThickness: 12);
        geometryPacket.Geometry.Field.FieldLines.Add(
            new SSL_FieldLineSegment
            {
                Name = "LeftPenaltyStretch",
                P1 = new Vector2f { X = -4800, Y = -1200 },
                P2 = new Vector2f { X = -4800, Y = 1200 },
                Thickness = 12,
                Type = SSL_FieldShapeType.LeftPenaltyStretch,
            });
        geometryPacket.Geometry.Field.FieldArcs.Add(
            new SSL_FieldCircularArc
            {
                Name = "CenterCircle",
                Center = new Vector2f { X = 0, Y = 0 },
                Radius = 600,
                A1 = 0,
                A2 = MathF.PI,
                Thickness = 12,
                Type = SSL_FieldShapeType.CenterCircle,
            });

        _ = engine.Update(packet: geometryPacket, settings: settings);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        Assert.NotNull(committedFrame.GeometrySnapshot);
        Assert.Equal(350, committedFrame.GeometrySnapshot!.BoundaryWidthGoalLineMm);
        Assert.Equal(1200, committedFrame.GeometrySnapshot.PenaltyAreaDepthMm);
        Assert.Equal(2400, committedFrame.GeometrySnapshot.PenaltyAreaWidthMm);
        Assert.Equal(600, committedFrame.GeometrySnapshot.CenterCircleRadiusMm);
        Assert.Equal(12, committedFrame.GeometrySnapshot.LineThicknessMm);
        var fieldLine = Assert.Single(committedFrame.GeometrySnapshot.FieldLines);
        Assert.Equal("LeftPenaltyStretch", fieldLine.Name);
        Assert.Equal(-4800, fieldLine.P1XMm);
        Assert.Equal(1200, fieldLine.P2YMm);
        Assert.Equal(SSL_FieldShapeType.LeftPenaltyStretch, fieldLine.Type);
        var fieldArc = Assert.Single(committedFrame.GeometrySnapshot.FieldArcs);
        Assert.Equal("CenterCircle", fieldArc.Name);
        Assert.Equal(600, fieldArc.RadiusMm);
        Assert.Equal(SSL_FieldShapeType.CenterCircle, fieldArc.Type);
    }

    /// <summary>
    /// 何を確認しているか: field geometry の大きな変更で geometry reset が発行され、旧 generation の pending frame が破棄されることを確認する。
    /// </summary>
    [Fact]
    public void Update_EmitsGeometryResetAndDropsPendingFramesFromOldGeometryGeneration()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
            reorderWindowNs: 100_000_000,
            mergeWindowNs: 20_000_000,
            geometryResetFieldLengthThresholdMm: 100,
            geometryResetFieldWidthThresholdMm: 100);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateGeometryPacket(fieldLength: 9000, fieldWidth: 6000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var resetResult = engine.Update(
            packet: new SSL_WrapperPacket
            {
                Geometry = TrackerContractTestData.CreateGeometryPacket(fieldLength: 9400, fieldWidth: 6400).Geometry,
                Detection = TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: 20,
                    cameraId: 1,
                    balls: [TrackerContractTestData.CreateBall(x: 200)],
                    captureTimeSeconds: 2.0).Detection,
            },
            settings: settings);

        Assert.Contains(resetResult.EmittedEvents, emitted => emitted.Kind == TrackerEventKind.GeometryReset);
        Assert.Single(resetResult.CommittedFrames);
        Assert.Equal(2_000_000_000L, resetResult.CommittedFrames[0].DataTimestampNs);
        Assert.Equal(
            [TrackerEventKind.GeometryReset, TrackerEventKind.WorldFrameCommitted],
            resetResult.EmittedEvents.Select(emitted => emitted.Kind).Take(2));
    }

    /// <summary>
    /// 何を確認しているか: goal geometry の変更だけでも geometry reset として扱われることを確認する。
    /// </summary>
    [Fact]
    public void Update_EmitsGeometryResetWhenGoalGeometryChanges()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
            reorderWindowNs: 100_000_000,
            mergeWindowNs: 20_000_000,
            geometryResetFieldLengthThresholdMm: 500,
            geometryResetFieldWidthThresholdMm: 500);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateGeometryPacket(
                fieldLength: 9000,
                fieldWidth: 6000,
                goalWidth: 1800,
                goalDepth: 180),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var resetResult = engine.Update(
            packet: new SSL_WrapperPacket
            {
                Geometry = TrackerContractTestData.CreateGeometryPacket(
                    fieldLength: 9000,
                    fieldWidth: 6000,
                    goalWidth: 2000,
                    goalDepth: 240).Geometry,
                Detection = TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: 20,
                    cameraId: 1,
                    balls: [TrackerContractTestData.CreateBall(x: 200)],
                    captureTimeSeconds: 2.0).Detection,
            },
            settings: settings);

        Assert.Contains(resetResult.EmittedEvents, emitted => emitted.Kind == TrackerEventKind.GeometryReset);
        Assert.Single(resetResult.CommittedFrames);
        Assert.Equal(2_000_000_000L, resetResult.CommittedFrames[0].DataTimestampNs);
    }

    /// <summary>
    /// 何を確認しているか: packet を伴わない profile switch では ProfileSwitched だけが発行され、frame commit が混ざらないことを確認する。
    /// </summary>
    [Fact]
    public void Update_WithControlOnlyProfileSwitch_EmitsOnlyProfileSwitched()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(profileName: "default");
        var switchedSettings = Fixture.CreateSettings(profileName: "fast");

        var result = engine.Update(
            packet: null,
            settings: settings,
            profileSwitchRequest: Fixture.CreateProfileSwitchRequest(
                requestVersion: 2,
                profileName: "fast",
                resolvedBaseSettings: switchedSettings));

        Assert.Empty(result.CommittedFrames);
        Assert.Equal([TrackerEventKind.ProfileSwitched], result.EmittedEvents.Select(emitted => emitted.Kind));
        Assert.Equal("fast", result.EmittedEvents[0].ProfileName);
    }

    /// <summary>
    /// 何を確認しているか: profile switch と frame commit が同じ result に入る場合、profile 適用 event が先に並ぶことを確認する。
    /// </summary>
    [Fact]
    public void Update_OrdersProfileSwitchBeforeWorldFrameCommitted_WhenSwitchAndFrameShareAResult()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0);
        var switchedSettings = Fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings,
            profileSwitchRequest: Fixture.CreateProfileSwitchRequest(
                requestVersion: 3,
                profileName: "fast",
                resolvedBaseSettings: switchedSettings));

        Assert.Equal(
            [TrackerEventKind.ProfileSwitched, TrackerEventKind.WorldFrameCommitted],
            result.EmittedEvents.Select(emitted => emitted.Kind).Take(2));
        Assert.Equal("fast", Assert.Single(result.CommittedFrames).Metadata.ProfileName);
    }

    /// <summary>
    /// 何を確認しているか: profile switch を挟んでも出力 frame number が連続し、番号が巻き戻らないことを確認する。
    /// </summary>
    [Fact]
    public void Update_PreservesFrameNumberContinuityAcrossProfileSwitch()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0);
        var switchedSettings = Fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0);

        var firstResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var switchResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.0),
            settings: settings,
            profileSwitchRequest: Fixture.CreateProfileSwitchRequest(
                requestVersion: 3,
                profileName: "fast",
                resolvedBaseSettings: switchedSettings));

        var firstCommittedFrame = Assert.Single(firstResult.CommittedFrames);
        var switchedCommittedFrame = Assert.Single(switchResult.CommittedFrames);

        Assert.Equal(firstCommittedFrame.FrameNumber + 1, switchedCommittedFrame.FrameNumber);
        Assert.Equal(
            [TrackerEventKind.ProfileSwitched, TrackerEventKind.WorldFrameCommitted],
            switchResult.EmittedEvents.Select(emitted => emitted.Kind).Take(2));
        Assert.Equal("fast", switchedCommittedFrame.Metadata.ProfileName);
    }

    /// <summary>
    /// 何を確認しているか: profile switch 時に旧 profile の buffered detection が消え、新 profile の frame に混ざらないことを確認する。
    /// </summary>
    [Fact]
    public void Update_ProfileSwitchClearsPendingBufferedDetectionsFromOldProfile()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(profileName: "default", reorderWindowNs: 1_500_000_000, mergeWindowNs: 20_000_000);
        var switchedSettings = Fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var switchResult = engine.Update(
            packet: null,
            settings: settings,
            profileSwitchRequest: Fixture.CreateProfileSwitchRequest(
                requestVersion: 4,
                profileName: "fast",
                resolvedBaseSettings: switchedSettings));

        Assert.Empty(switchResult.CommittedFrames);
        Assert.Equal([TrackerEventKind.ProfileSwitched], switchResult.EmittedEvents.Select(emitted => emitted.Kind));

        var frameResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.0),
            settings: switchedSettings);

        var committedFrame = Assert.Single(frameResult.CommittedFrames);
        Assert.Equal(2_000_000_000L, committedFrame.DataTimestampNs);
        Assert.Equal("fast", committedFrame.Metadata.ProfileName);
    }
}
