using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TrackerEngine contract test 群が共有 fixture を同じ入口で利用できることを支える。
/// </summary>
public abstract class TrackerEngineContractTestBase : IClassFixture<TrackerContractFixture>
{
    protected TrackerEngineContractTestBase(TrackerContractFixture fixture)
    {
        Fixture = fixture;
    }

    protected TrackerContractFixture Fixture { get; }
}
