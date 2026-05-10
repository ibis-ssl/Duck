namespace Tracker.Server.Vision;

public sealed class VisionReceiverRuntimeOptionsStore : IDisposable
{
    private readonly object gate = new();
    private VisionReceiverResolvedOptions currentOptions;
    private CancellationTokenSource changeSource = new();

    public VisionReceiverRuntimeOptionsStore(VisionReceiverResolvedOptions initialOptions)
    {
        currentOptions = initialOptions;
    }

    public VisionReceiverRuntimeOptionsSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new VisionReceiverRuntimeOptionsSnapshot(currentOptions, changeSource.Token);
        }
    }

    public void ApplyConfiguration(VisionReceiverResolvedOptions nextOptions)
    {
        ArgumentNullException.ThrowIfNull(nextOptions);

        CancellationTokenSource? previousSource = null;

        lock (gate)
        {
            if (currentOptions == nextOptions)
            {
                return;
            }

            currentOptions = nextOptions;
            previousSource = changeSource;
            changeSource = new CancellationTokenSource();
        }

        previousSource.Cancel();
        previousSource.Dispose();
    }

    public void Dispose()
    {
        CancellationTokenSource? source;

        lock (gate)
        {
            source = changeSource;
            changeSource = new CancellationTokenSource();
        }

        source.Cancel();
        source.Dispose();
    }
}

public sealed record VisionReceiverRuntimeOptionsSnapshot(
    VisionReceiverResolvedOptions Options,
    CancellationToken ChangeToken);
