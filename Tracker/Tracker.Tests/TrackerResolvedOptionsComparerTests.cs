using Tracker.Core;
using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

/// <summary>送信元 NIC のみが変わる設定も変更として扱うことを確認します。</summary>
public sealed class TrackerResolvedOptionsComparerTests
{
    /// <summary>RuntimeHost の送信元 NIC の変更を再適用対象として検出します。</summary>
    [Fact]
    public void RuntimeOptionsComparerDetectsInterfaceNameOnlyChange()
    {
        var left = new TrackerRuntimeResolvedOptions
        {
            PublisherOptions = new TrackerPublisherOptions { InterfaceName = "Ethernet A" },
        };
        var right = new TrackerRuntimeResolvedOptions
        {
            PublisherOptions = new TrackerPublisherOptions { InterfaceName = "Ethernet B" },
        };

        Assert.False(TrackerRuntimeResolvedOptionsComparer.AreResolvedOptionsEquivalent(left, right));
    }

    /// <summary>DebugHost の送信元 NIC の変更を再適用対象として検出します。</summary>
    [Fact]
    public void DebugHostOptionsComparerDetectsInterfaceNameOnlyChange()
    {
        var left = new TrackerResolvedOptions
        {
            PublisherOptions = new TrackerPublisherOptions { InterfaceName = "Ethernet A" },
        };
        var right = new TrackerResolvedOptions
        {
            PublisherOptions = new TrackerPublisherOptions { InterfaceName = "Ethernet B" },
        };

        Assert.False(TrackerResolvedOptionsComparer.AreResolvedOptionsEquivalent(left, right));
    }
}
