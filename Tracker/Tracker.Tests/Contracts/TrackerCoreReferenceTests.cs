using Tracker.Core;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: Tracker.Tests から Tracker.Core assembly を参照できることを検証する。
/// </summary>
public class TrackerCoreReferenceTests
{
    /// <summary>
    /// 何を確認しているか: Tracker.Core の marker 型を通じて参照 assembly 名が期待どおりであることを確認する。
    /// </summary>
    [Fact]
    public void TrackerTestsProject_CanReferenceTrackerCoreAssembly()
    {
        Assert.Equal("Tracker.Core", typeof(TrackerCoreAssemblyMarker).Assembly.GetName().Name);
    }
}
