namespace Duck.Testing.Diagnostics;

/// <summary>
/// 呼出側が期待する資源の起動世代を識別する値。
/// コンテナの探索や所有証明は行わないため、期待する識別子は
/// 信頼できる資源の所有者から呼出側が取得する。
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
