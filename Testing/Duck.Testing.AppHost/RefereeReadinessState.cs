namespace Duck.Testing.AppHost;

/// <summary>Tracks one Game Controller generation's initial command, team identity, and fresh command stream.</summary>
public sealed class RefereeReadinessState(
    int expectedInitialCommand,
    TimeSpan? freshnessWindow = null,
    string? expectedYellowTeamName = null)
{
    private readonly TimeSpan freshnessWindow = freshnessWindow ?? TimeSpan.FromSeconds(3);
    private readonly object gate = new();
    private DateTimeOffset lastKnownCommandAt;
    private bool lastCommandKnown;
    private bool initialCommandObserved;
    private int? currentCommand;
    private bool expectedYellowTeamNameObserved;
    private string? currentYellowTeamName;

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

    public string? CurrentYellowTeamName
    {
        get
        {
            lock (gate)
            {
                return currentYellowTeamName;
            }
        }
    }

    public bool HasExpectedYellowTeamName
    {
        get
        {
            lock (gate)
            {
                return expectedYellowTeamName is null || expectedYellowTeamNameObserved;
            }
        }
    }

    public void Observe(int command, DateTimeOffset receivedAt, string? yellowTeamName = null)
    {
        lock (gate)
        {
            currentCommand = command;
            currentYellowTeamName = yellowTeamName;
            expectedYellowTeamNameObserved = expectedYellowTeamName is null ||
                string.Equals(expectedYellowTeamName, yellowTeamName, StringComparison.Ordinal);
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

    public bool IsCommandHealthy(DateTimeOffset now)
    {
        lock (gate)
        {
            return initialCommandObserved &&
                lastCommandKnown &&
                currentCommand is >= 0 and <= 17 &&
                now - lastKnownCommandAt <= freshnessWindow;
        }
    }

    public bool IsHealthy(DateTimeOffset now)
    {
        lock (gate)
        {
            return initialCommandObserved &&
                lastCommandKnown &&
                currentCommand is >= 0 and <= 17 &&
                now - lastKnownCommandAt <= freshnessWindow &&
                expectedYellowTeamNameObserved;
        }
    }
}
