namespace Duck.Testing.AppHost;

/// <summary>Tracks one Game Controller generation's initial referee command and fresh known-command stream.</summary>
public sealed class RefereeReadinessState(int expectedInitialCommand, TimeSpan? freshnessWindow = null)
{
    private readonly TimeSpan freshnessWindow = freshnessWindow ?? TimeSpan.FromSeconds(3);
    private readonly object gate = new();
    private DateTimeOffset lastKnownCommandAt;
    private bool lastCommandKnown;
    private bool initialCommandObserved;
    private int? currentCommand;

    public bool InitialCommandObserved
    {
        get
        {
            lock (gate)
            {
                return initialCommandObserved;
            }
        }
    }

    public int? CurrentCommand
    {
        get
        {
            lock (gate)
            {
                return currentCommand;
            }
        }
    }

    public void Observe(int command, DateTimeOffset receivedAt)
    {
        lock (gate)
        {
            currentCommand = command;
            lastCommandKnown = command is >= 0 and <= 17;
            if (!lastCommandKnown)
            {
                return;
            }

            lastKnownCommandAt = receivedAt;
            if (!initialCommandObserved && command == expectedInitialCommand)
            {
                initialCommandObserved = true;
            }
        }
    }

    public bool IsHealthy(DateTimeOffset now)
    {
        lock (gate)
        {
            return initialCommandObserved &&
                lastCommandKnown &&
                currentCommand is >= 0 and <= 17 &&
                now - lastKnownCommandAt <= freshnessWindow;
        }
    }
}
