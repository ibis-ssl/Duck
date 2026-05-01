using Tracker.Core;

namespace Tracker.Tests;

public class TrackerCoreReferenceTests
{
    [Fact]
    public void TrackerTestsProject_CanReferenceTrackerCoreAssembly()
    {
        Assert.Equal("Tracker.Core", typeof(TrackerCoreAssemblyMarker).Assembly.GetName().Name);
    }
}
