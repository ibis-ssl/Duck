using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class MatchParticipantStatusProjectionTests
{
    [Fact]
    public void ParticipantStatusUsesObservedInstanceResourceIdAndHasNoNameFallback()
    {
        var resource = new ContainerResource("tigers-blue", "tigers-image");
        var ids = new MatchParticipantResourceIdMap([resource]);

        Assert.Null(ids.GetCurrentId(resource));

        var observation = new ResourceEvent(resource, "opaque-tigers-instance-key", new CustomResourceSnapshot
        {
            ResourceType = "container",
            State = new ResourceStateSnapshot(KnownResourceStates.Running, "fixture"),
            Properties = [],
        });
        Assert.True(ids.Observe(observation));
        Assert.Equal("opaque-tigers-instance-key", ids.GetCurrentId(resource));

        var unrelated = new ContainerResource("other", "other-image");
        Assert.False(ids.Observe(new ResourceEvent(unrelated, "other-instance-key", new CustomResourceSnapshot
        {
            ResourceType = "container",
            Properties = [],
        })));
        Assert.Null(ids.GetCurrentId(unrelated));
    }
}
