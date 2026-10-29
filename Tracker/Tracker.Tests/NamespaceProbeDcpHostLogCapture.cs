using Microsoft.Extensions.Logging;

namespace Tracker.Tests;

internal sealed class NamespaceProbeDcpHostLogCapture
{
    private const string DcpCategory = "Aspire.Hosting.Dcp";
    private const string TimedOutTemplate = "Current log flush for {ResourceName} timed out after {Timeout}.";
    private const string DeletedTemplate = "Current log flush for {ResourceName} ended because the resource was deleted.";
    private readonly object gate = new();
    private readonly HashSet<string> ownerNames;
    private readonly HashSet<string> followerNames;
    private readonly List<Entry> ownerEntries = [];
    private readonly List<Entry> followerEntries = [];
    private int ownerDropped;
    private int followerDropped;

    internal const int MaximumEntriesPerResource = 32;
    internal const int MaximumEntryCharacters = 2048;

    internal NamespaceProbeDcpHostLogCapture(IEnumerable<string> ownerResourceNames, IEnumerable<string> followerResourceNames)
    {
        ownerNames = CreateNames(ownerResourceNames);
        followerNames = CreateNames(followerResourceNames);
        if (ownerNames.Overlaps(followerNames))
        {
            throw new ArgumentException("所有者と従属資源の名前は重複できません。");
        }
    }

    internal void Configure(ILoggingBuilder logging)
    {
        logging.AddProvider(new NamespaceProbeDcpHostLogProvider(this));
        logging.AddFilter<NamespaceProbeDcpHostLogProvider>(NamespaceProbeDcpHostLogProvider.IsEnabledForCapture);
    }

    internal IReadOnlyList<Entry> Snapshot()
    {
        lock (gate)
        {
            return ownerEntries.Concat(followerEntries).ToArray();
        }
    }

    internal object Summary()
    {
        lock (gate)
        {
            return new
            {
                ownerEntries = ownerEntries.Count,
                followerEntries = followerEntries.Count,
                ownerDropped,
                followerDropped,
            };
        }
    }

    internal void Capture(string category, LogLevel level, string message, Exception? exception, object? state)
    {
        if (!NamespaceProbeDcpHostLogProvider.IsEnabledForCapture(category, level) ||
            !TryResolveResource(state, message, out var role, out var resourceName))
        {
            return;
        }

        if (level == LogLevel.Debug && !IsKnownFlushDebug(state, message, resourceName))
        {
            return;
        }

        var exceptionMessage = exception?.Message;
        if (exceptionMessage is { Length: > MaximumEntryCharacters })
        {
            exceptionMessage = exceptionMessage[..MaximumEntryCharacters];
        }
        var messageLimit = MaximumEntryCharacters - (exceptionMessage?.Length ?? 0);
        var boundedMessage = message.Length <= messageLimit ? message : message[..messageLimit];
        var entry = new Entry(role, category, level, boundedMessage, exceptionMessage);

        lock (gate)
        {
            var entries = role == "owner" ? ownerEntries : followerEntries;
            if (entries.Count >= MaximumEntriesPerResource)
            {
                if (role == "owner")
                {
                    ownerDropped++;
                }
                else
                {
                    followerDropped++;
                }
                return;
            }

            entries.Add(entry);
        }
    }

    private bool TryResolveResource(object? state, string message, out string role, out string resourceName)
    {
        var structuredCount = 0;
        string? structuredName = null;
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            foreach (var pair in values)
            {
                if (string.Equals(pair.Key, "ResourceName", StringComparison.Ordinal))
                {
                    structuredCount++;
                    structuredName = pair.Value as string;
                }
            }
        }

        if (structuredCount > 0)
        {
            if (structuredCount != 1 || structuredName is null)
            {
                role = string.Empty;
                resourceName = string.Empty;
                return false;
            }

            if (ownerNames.Contains(structuredName))
            {
                role = "owner";
                resourceName = structuredName;
                return true;
            }
            if (followerNames.Contains(structuredName))
            {
                role = "follower";
                resourceName = structuredName;
                return true;
            }

            role = string.Empty;
            resourceName = string.Empty;
            return false;
        }

        var matches = ownerNames.Where(name => ContainsExactToken(message, name)).Select(name => (Role: "owner", Name: name))
            .Concat(followerNames.Where(name => ContainsExactToken(message, name)).Select(name => (Role: "follower", Name: name)))
            .Distinct()
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            role = string.Empty;
            resourceName = string.Empty;
            return false;
        }

        role = matches[0].Role;
        resourceName = matches[0].Name;
        return true;
    }

    private static bool IsKnownFlushDebug(object? state, string message, string resourceName)
    {
        var originalFormat = GetStructuredValue(state, "{OriginalFormat}");
        if (string.Equals(originalFormat, TimedOutTemplate, StringComparison.Ordinal))
        {
            return message.StartsWith($"Current log flush for {resourceName} timed out after ", StringComparison.Ordinal) && message.EndsWith(".", StringComparison.Ordinal);
        }
        if (string.Equals(originalFormat, DeletedTemplate, StringComparison.Ordinal))
        {
            return string.Equals(message, $"Current log flush for {resourceName} ended because the resource was deleted.", StringComparison.Ordinal);
        }
        return false;
    }

    private static string? GetStructuredValue(object? state, string key)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
        {
            return null;
        }

        string? result = null;
        var count = 0;
        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, key, StringComparison.Ordinal))
            {
                count++;
                result = pair.Value as string;
            }
        }
        return count == 1 ? result : null;
    }

    private static bool ContainsExactToken(string message, string token)
    {
        var start = 0;
        while ((start = message.IndexOf(token, start, StringComparison.Ordinal)) >= 0)
        {
            var end = start + token.Length;
            var leftBoundary = start == 0 || !IsTokenCharacter(message[start - 1]);
            var rightBoundary = end == message.Length || !IsTokenCharacter(message[end]);
            if (leftBoundary && rightBoundary)
            {
                return true;
            }
            start = end;
        }
        return false;
    }

    private static bool IsTokenCharacter(char value) => char.IsLetterOrDigit(value) || value is '_' or '-' or '.';

    private static HashSet<string> CreateNames(IEnumerable<string> names)
    {
        var result = names.Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.Ordinal);
        if (result.Count == 0)
        {
            throw new ArgumentException("対象資源名を1つ以上指定してください。", nameof(names));
        }
        return result;
    }

    internal sealed record Entry(string Resource, string Category, LogLevel Level, string Message, string? ExceptionMessage);
}

internal sealed class NamespaceProbeDcpHostLogProvider(NamespaceProbeDcpHostLogCapture capture) : ILoggerProvider
{
    internal static bool IsEnabledForCapture(string? category, LogLevel level) =>
        category is not null &&
        (category == "Aspire.Hosting.Dcp" || category.StartsWith("Aspire.Hosting.Dcp.", StringComparison.Ordinal)) &&
        (level == LogLevel.Debug || level is LogLevel.Error or LogLevel.Critical);

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(capture, categoryName);

    public void Dispose()
    {
    }

    private sealed class CaptureLogger(NamespaceProbeDcpHostLogCapture capture, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => IsEnabledForCapture(category, logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                capture.Capture(category, logLevel, formatter(state, exception), exception, state);
            }
        }
    }
}