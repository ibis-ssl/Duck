using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public abstract class TrackerEngineContractTestBase : IClassFixture<TrackerContractFixture>
{
    protected TrackerEngineContractTestBase(TrackerContractFixture fixture)
    {
        Fixture = fixture;
    }

    protected TrackerContractFixture Fixture { get; }
}
