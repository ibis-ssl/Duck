using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class RegisteredContainerEventProjectionTests
{
    private const string ResourceId = "debug-host";
    private const string Role = "DebugHost";
    private const string OldId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string NewId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void ProjectsFullIdSeparatelyFromResourceKeyAndRole()
    {
        var resource = new ContainerResource(Role, "example/image");
        var observed = Event(resource, ResourceId, "Running", NewId);
        var current = Event(resource, ResourceId, "Running", NewId);
        var accepted = RegisteredContainerEventProjection.TryProject(resource, ResourceId, Role, observed, current, out var target);
        Assert.True(accepted);
        Assert.Equal(ResourceId, target!.ResourceId);
        Assert.Equal(Role, target.RoleName);
        Assert.Equal(NewId, target.ContainerId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeg")]
    public void RejectsMissingOrMalformedContainerId(string? id)
    {
        var resource = new ContainerResource(Role, "example/image");
        AssertRejected(resource, Event(resource, ResourceId, "Running", id), Event(resource, ResourceId, "Running", id));
    }

    [Fact]
    public void RejectsDuplicateContainerIdProperties()
    {
        var resource = new ContainerResource(Role, "example/image");
        AssertRejected(resource, Event(resource, ResourceId, "Running", NewId, NewId), Event(resource, ResourceId, "Running", NewId));
    }

    [Fact]
    public void RejectsTerminalOrMismatchedCurrentState()
    {
        var resource = new ContainerResource(Role, "example/image");
        AssertRejected(resource, Event(resource, ResourceId, "Stopped", NewId), Event(resource, ResourceId, "Stopped", NewId));
        AssertRejected(resource, Event(resource, ResourceId, "Failed", NewId), Event(resource, ResourceId, "Failed", NewId));
        AssertRejected(resource, Event(resource, ResourceId, "Running", NewId), Event(resource, ResourceId, "Stopped", NewId));
    }

    [Fact]
    public void RejectsDifferentResourceKeyOrResourceObject()
    {
        var resource = new ContainerResource(Role, "example/image");
        var other = new ContainerResource(Role, "example/image");
        AssertRejected(resource, Event(resource, "other-resource", "Running", NewId), Event(resource, ResourceId, "Running", NewId));
        AssertRejected(resource, Event(other, ResourceId, "Running", NewId), Event(resource, ResourceId, "Running", NewId));
    }

    [Fact]
    public void RejectsStaleIdAndAcceptsReplacementOnlyWhenCurrentStateAgrees()
    {
        var resource = new ContainerResource(Role, "example/image");
        AssertRejected(resource, Event(resource, ResourceId, "Running", OldId), Event(resource, ResourceId, "Running", NewId));
        var accepted = RegisteredContainerEventProjection.TryProject(
            resource, ResourceId, Role,
            Event(resource, ResourceId, "Running", NewId),
            Event(resource, ResourceId, "Running", NewId),
            out var target);
        Assert.True(accepted);
        Assert.Equal(NewId, target!.ContainerId);
    }

    [Fact]
    public void RejectsNonStringId()
    {
        var resource = new ContainerResource(Role, "example/image");
        AssertRejected(resource, Event(resource, ResourceId, "Running", 17), Event(resource, ResourceId, "Running", 17));
    }

    private static void AssertRejected(IResource resource, ResourceEvent observed, ResourceEvent current)
    {
        var accepted = RegisteredContainerEventProjection.TryProject(resource, ResourceId, Role, observed, current, out var target);
        Assert.False(accepted);
        Assert.Null(target);
    }

    private static ResourceEvent Event(IResource resource, string id, string state, object? containerId, params object?[] duplicateIds)
    {
        var properties = new List<ResourcePropertySnapshot>();
        if (containerId is not null)
        {
            properties.Add(new ResourcePropertySnapshot("container.id", containerId));
        }
        properties.AddRange(duplicateIds.Select(value => new ResourcePropertySnapshot("container.id", value)));
        return new ResourceEvent(resource, id, new CustomResourceSnapshot
        {
            ResourceType = "container",
            State = new ResourceStateSnapshot(state, "fixture"),
            Properties = [.. properties],
        });
    }
}
