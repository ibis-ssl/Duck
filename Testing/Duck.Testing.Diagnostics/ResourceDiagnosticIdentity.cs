namespace Duck.Testing.Diagnostics;

/// <summary>
/// Identifies the resource incarnation expected by the caller. This value does not
/// discover a container or prove ownership; callers must obtain the expected identity
/// from their trusted resource owner.
/// </summary>
public sealed record ResourceDiagnosticIdentity
{
    public ResourceDiagnosticIdentity(string runId, string resourceId, string containerId, long generation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        if (containerId.Length != 64 || !containerId.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("A complete 64-character container ID is required.", nameof(containerId));
        }
        if (generation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        RunId = runId;
        ResourceId = resourceId;
        ContainerId = containerId;
        Generation = generation;
    }

    public string RunId { get; }
    public string ResourceId { get; }
    public string ContainerId { get; }
    public long Generation { get; }
}
