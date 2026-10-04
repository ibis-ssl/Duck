namespace Duck.Testing.AppHost;

/// <summary>
/// Represents ownership of the host-local Duck Aspire stack.
/// </summary>
public sealed class StackOwnershipLease : IDisposable
{
    private readonly FileStream stream;

    private StackOwnershipLease(FileStream stream)
    {
        this.stream = stream;
    }

    /// <summary>
    /// Opens an ownership marker without exclusive acquisition.
    /// </summary>
    public static StackOwnershipLease Acquire(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        return new StackOwnershipLease(new FileStream(
            fullPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite));
    }

    public void Dispose()
    {
        stream.Dispose();
    }
}
