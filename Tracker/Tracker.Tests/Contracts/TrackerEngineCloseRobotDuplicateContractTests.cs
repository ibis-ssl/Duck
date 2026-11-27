using System.Linq.Expressions;
using System.Reflection;
using Tracker.Core;
using Tracker.Tests.Contracts;
using Xunit.Abstractions;
using static Tracker.Tests.Contracts.TrackerContractTestData;

namespace Tracker.Tests;

/// <summary>
/// 同一検出内の近接重複判定で維持する追跡結果と、判定一回ごとの割り当てを検証する。
/// </summary>
public sealed class TrackerEngineCloseRobotDuplicateContractTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;
    private readonly ITestOutputHelper output;

    public TrackerEngineCloseRobotDuplicateContractTests(TrackerContractFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    /// <summary>
    /// 同一カメラ・同一チームの別 ID は、平面距離が 135 mm 未満の場合だけ除外されることを確認する。
    /// </summary>
    [Theory]
    [InlineData(134.999f, 0f, true)]
    [InlineData(135f, 0f, false)]
    [InlineData(135.001f, 0f, false)]
    [InlineData(-135f, 0f, false)]
    [InlineData(81f, 108f, false)]
    public void Update_FiltersDifferentIdsOnlyWithin135Mm(float xMm, float yMm, bool isDuplicate)
    {
        var candidate = CreateRobot(robotId: 2, x: xMm, confidence: 0.9f);
        candidate.Y = yMm;
        var frame = TrackSingleDetection([CreateRobot(robotId: 1), candidate]);

        uint[] expectedIds = isDuplicate ? [1u] : [1u, 2u];
        Assert.Equal(expectedIds, frame.Robots.Select(robot => robot.RobotId).Order());
    }

    /// <summary>
    /// 入力順を変えても、近接する別 ID のうち信頼度が高い観測を残し、同率では小さい ID を残すことを確認する。
    /// </summary>
    [Theory]
    [InlineData(false, false, 2u)]
    [InlineData(false, true, 2u)]
    [InlineData(true, false, 1u)]
    [InlineData(true, true, 1u)]
    public void Update_PrioritizesConfidenceThenLowerId(bool sameConfidence, bool reverseInput, uint expectedId)
    {
        var first = CreateRobot(robotId: 2);
        var second = CreateRobot(robotId: 1, x: 1, confidence: sameConfidence ? 1f : 0.9f);
        var frame = TrackSingleDetection(reverseInput ? [second, first] : [first, second]);

        Assert.Equal(expectedId, Assert.Single(frame.Robots).RobotId);
    }

    /// <summary>
    /// 同じ ID の近接観測が重複判定を通過し、信頼度降順の処理で後から登録した座標が使われることを確認する。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Update_RetainsLastProcessedObservationOfSameId(bool reverseInput)
    {
        var first = CreateRobot(robotId: 1);
        var second = CreateRobot(robotId: 1, x: 2, confidence: 0.9f);
        var frame = TrackSingleDetection(reverseInput ? [second, first] : [first, second]);

        var robot = Assert.Single(frame.Robots);
        Assert.Equal(1u, robot.RobotId);
        Assert.Equal(2d, robot.XMm, precision: 6);
    }

    /// <summary>
    /// 同じ確定フレームに近接観測が入っても、カメラまたはチームが違えば別 ID の観測が残ることを確認する。
    /// </summary>
    [Fact]
    public void Update_PreservesNearbyRobotsAcrossCamerasAndTeams()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);
        _ = engine.Update(CreateDetectionPacket(
            frameNumber: 1,
            cameraId: 1,
            robotsYellow: [CreateRobot(robotId: 1)],
            robotsBlue: [CreateRobot(robotId: 2, x: 1)]), settings);
        _ = engine.Update(CreateDetectionPacket(
            frameNumber: 2,
            cameraId: 2,
            robotsYellow: [CreateRobot(robotId: 3, x: 2)],
            captureTimeSeconds: 1.010), settings);

        var result = engine.Update(CreateDetectionPacket(frameNumber: 3, captureTimeSeconds: 2.0), settings);

        var frame = Assert.Single(result.CommittedFrames);
        Assert.Equal(
            [(1u, TrackerTeam.Yellow), (2u, TrackerTeam.Blue), (3u, TrackerTeam.Yellow)],
            frame.Robots.OrderBy(robot => robot.RobotId).Select(robot => (robot.RobotId, robot.Team)));
    }

    /// <summary>
    /// 対象外の登録済み観測が前後どちらにあっても、同一カメラ・同一チームの近接する別 ID を見つけることを確認する。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HasCloseRobotObservationWithDifferentId_FindsMatchInEitherInsertionOrder(bool reverseInsertion)
    {
        (uint Camera, TrackerTeam Team, SSL_DetectionRobot Robot)[] observations =
        [
            (1u, TrackerTeam.Yellow, CreateRobot(robotId: 2)),
            (0u, TrackerTeam.Blue, CreateRobot(robotId: 3)),
            (0u, TrackerTeam.Yellow, CreateRobot(robotId: 4, x: 200)),
            (0u, TrackerTeam.Yellow, CreateRobot(robotId: 5)),
        ];
        if (reverseInsertion)
        {
            Array.Reverse(observations);
        }

        var isDuplicate = CreateDuplicateCheck(observations);

        Assert.True(isDuplicate(CreateRobot(robotId: 6)));
        Assert.False(isDuplicate(CreateRobot(robotId: 5)));
    }

    /// <summary>
    /// 観測の登録やデリゲートの準備を除き、連続した近接重複判定が呼び出しごとの割り当てを生まないことを確認する。
    /// </summary>
    [Fact]
    public void HasCloseRobotObservationWithDifferentId_DoesNotAllocatePerCall()
    {
        const int iterations = 10_000;
        const long allocationBudgetBytes = 4_096;
        var isDuplicate = CreateDuplicateCheck([(0u, TrackerTeam.Yellow, CreateRobot(robotId: 1))]);
        var near = CreateRobot(robotId: 2);
        var far = CreateRobot(robotId: 2, x: 200);

        _ = CountDuplicates(isDuplicate, near, far, iterations);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var duplicateCount = CountDuplicates(isDuplicate, near, far, iterations);
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        output.WriteLine($"近接重複判定 {2 * iterations:N0} 回の割り当ては {allocatedBytes:N0} バイト。");
        Assert.Equal(iterations, duplicateCount);
        Assert.InRange(allocatedBytes, 0, allocationBudgetBytes);
    }

    private TrackerFrame TrackSingleDetection(SSL_DetectionRobot[] robots)
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);
        var result = engine.Update(CreateDetectionPacket(robotsYellow: robots), settings);
        return Assert.Single(result.CommittedFrames);
    }

    private static Func<SSL_DetectionRobot, bool> CreateDuplicateCheck(
        (uint Camera, TrackerTeam Team, SSL_DetectionRobot Robot)[] observations)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var engineType = typeof(TrackerEngine);
        var addObservation = engineType.GetMethod("AddRobotObservation", flags)!;
        var checkDuplicate = engineType.GetMethod("HasCloseRobotObservationWithDifferentId", flags)!;
        var dictionaryType = addObservation.GetParameters()[0].ParameterType;
        var dictionary = Activator.CreateInstance(dictionaryType)!;

        foreach (var observation in observations)
        {
            addObservation.Invoke(null, [dictionary, observation.Team, observation.Robot, observation.Camera, 0L]);
        }

        var candidate = Expression.Parameter(typeof(SSL_DetectionRobot));
        var check = Expression.Call(
            checkDuplicate,
            Expression.Constant(dictionary, dictionaryType),
            Expression.Constant(TrackerTeam.Yellow),
            candidate,
            Expression.Constant(0u));
        return Expression.Lambda<Func<SSL_DetectionRobot, bool>>(check, candidate).Compile();
    }

    private static int CountDuplicates(
        Func<SSL_DetectionRobot, bool> isDuplicate,
        SSL_DetectionRobot near,
        SSL_DetectionRobot far,
        int iterations)
    {
        var duplicateCount = 0;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            duplicateCount += isDuplicate(near) ? 1 : 0;
            duplicateCount += isDuplicate(far) ? 1 : 0;
        }

        return duplicateCount;
    }
}
