using Aspire.Hosting.ApplicationModel;

namespace Duck.Testing.AppHost;

internal sealed record RegisteredContainerRuntimeTarget(string ResourceId, string RoleName, string ContainerId);

internal static class RegisteredContainerEventProjection
{
    internal static bool TryProject(IResource registeredResource, string registeredResourceId, string expectedRoleName, ResourceEvent observation, ResourceEvent currentState, out RegisteredContainerRuntimeTarget? target)
    {
        target = null;
        return false;
    }
}
