using Aspire.Hosting.ApplicationModel;

namespace Duck.Testing.AppHost;

internal sealed record RegisteredContainerRuntimeTarget(string ResourceId, string RoleName, string ContainerId);

internal static class RegisteredContainerEventProjection
{
    private const string ContainerIdPropertyName = "container.id";

    /// <summary>
    /// 登録資源に対応する最新観測から、実行中コンテナの完全IDを候補として取り出します。
    /// </summary>
    internal static bool TryProject(
        IResource registeredResource,
        string registeredResourceId,
        string expectedRoleName,
        ResourceEvent observation,
        ResourceEvent currentState,
        out RegisteredContainerRuntimeTarget? target)
    {
        target = null;

        if (string.IsNullOrWhiteSpace(registeredResourceId) ||
            string.IsNullOrWhiteSpace(expectedRoleName) ||
            !ReferenceEquals(observation.Resource, registeredResource) ||
            !ReferenceEquals(currentState.Resource, registeredResource) ||
            !string.Equals(registeredResource.Name, expectedRoleName, StringComparison.Ordinal) ||
            !string.Equals(observation.ResourceId, registeredResourceId, StringComparison.Ordinal) ||
            !string.Equals(currentState.ResourceId, registeredResourceId, StringComparison.Ordinal) ||
            !IsRunning(observation) ||
            !IsRunning(currentState) ||
            !TryReadContainerId(observation.Snapshot, out var observedContainerId) ||
            !TryReadContainerId(currentState.Snapshot, out var currentContainerId) ||
            !string.Equals(observedContainerId, currentContainerId, StringComparison.Ordinal))
        {
            return false;
        }

        target = new RegisteredContainerRuntimeTarget(
            registeredResourceId,
            expectedRoleName,
            currentContainerId);
        return true;
    }

    private static bool IsRunning(ResourceEvent resourceEvent) =>
        string.Equals(
            resourceEvent.Snapshot.State?.Text,
            KnownResourceStates.Running,
            StringComparison.Ordinal);

    private static bool TryReadContainerId(CustomResourceSnapshot snapshot, out string containerId)
    {
        containerId = string.Empty;
        var properties = snapshot.Properties;
        if (properties.IsDefaultOrEmpty)
        {
            return false;
        }

        string? found = null;
        foreach (var property in properties)
        {
            if (!string.Equals(property.Name, ContainerIdPropertyName, StringComparison.Ordinal))
            {
                continue;
            }

            if (found is not null || property.Value is not string value || !IsFullContainerId(value))
            {
                return false;
            }

            found = value;
        }

        if (found is null)
        {
            return false;
        }

        containerId = found;
        return true;
    }

    private static bool IsFullContainerId(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f') ||
                  (character >= 'A' && character <= 'F')))
            {
                return false;
            }
        }

        return true;
    }
}

