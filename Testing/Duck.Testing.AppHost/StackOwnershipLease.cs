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
    /// Acquires the host-local stack ownership marker and holds it until this lease is disposed.
    /// </summary>
    public static StackOwnershipLease Acquire(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            return new StackOwnershipLease(new FileStream(
                fullPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None));
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"Another Duck Aspire stack already owns '{fullPath}'.",
                exception);
        }
    }

    public void Dispose()
    {
        stream.Dispose();
    }
}
