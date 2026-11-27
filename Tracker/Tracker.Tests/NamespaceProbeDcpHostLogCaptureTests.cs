using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tracker.Tests;

public sealed class NamespaceProbeDcpHostLogCaptureTests
{
    private const string OwnerLogical = "namespace-probe-owner";
    private const string OwnerPhysical = "duck-namespace-probe-owner-run_01";
    private const string FollowerLogical = "namespace-probe-follower";
    private const string FollowerPhysical = "duck-namespace-probe-follower-run_01";

    [Fact]
    public void LoggerFactoryCapturesOnlyTargetErrorsAndKnownFlushDebugTemplates()
    {
        var capture = CreateCapture();
        using var services = CreateServices(capture);
        var factory = services.GetRequiredService<ILoggerFactory>();
        var dcpLogger = factory.CreateLogger("Aspire.Hosting.Dcp.ContainerCreator");
        var otherLogger = factory.CreateLogger("Other.Component");

        dcpLogger.LogError("Container {ResourceName} could not be created", OwnerPhysical);
        dcpLogger.LogCritical("Container {ResourceName} failed critically", OwnerLogical);
        dcpLogger.LogError("container " + FollowerPhysical + " failed without a structured resource name");
        dcpLogger.LogError("Container {ResourceName} failed for " + OwnerPhysical, "unrelated-resource");
        dcpLogger.LogError("container " + OwnerPhysical + "-suffix failed");
        dcpLogger.LogError("container " + OwnerPhysical + ".foreign failed");
        dcpLogger.LogWarning("Container {ResourceName} warned", OwnerLogical);
        otherLogger.LogError("Container {ResourceName} failed", OwnerLogical);
        dcpLogger.LogDebug("Current log flush for {ResourceName} timed out after {Timeout}.", OwnerPhysical, TimeSpan.FromSeconds(2));
        dcpLogger.LogDebug("Current log flush for {ResourceName} ended because the resource was deleted.", FollowerLogical);
        dcpLogger.LogDebug("An unrelated operation timed out for {ResourceName}.", OwnerLogical);

        var entries = capture.Snapshot();
        Assert.Equal(5, entries.Count);
        Assert.Equal(3, entries.Count(entry => entry.Resource == "owner"));
        Assert.Equal(2, entries.Count(entry => entry.Resource == "follower"));
        Assert.Contains(entries, entry => entry.Level == LogLevel.Error && entry.Resource == "owner" && entry.Category == "Aspire.Hosting.Dcp.ContainerCreator");
        Assert.Contains(entries, entry => entry.Level == LogLevel.Critical && entry.Resource == "owner");
        Assert.Contains(entries, entry => entry.Level == LogLevel.Error && entry.Resource == "follower");
        Assert.Contains(entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("timed out after", StringComparison.Ordinal));
        Assert.Contains(entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("resource was deleted", StringComparison.Ordinal));
    }

    [Fact]
    public void StructuredResourceNameTakesPrecedenceAndAmbiguousNamesAreRejected()
    {
        var capture = CreateCapture();
        using var services = CreateServices(capture);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Aspire.Hosting.Dcp.ContainerCreator");
        var conflictingState = new KeyValuePair<string, object?>[]
        {
            new("ResourceName", OwnerLogical),
            new("ResourceName", FollowerLogical),
            new("{OriginalFormat}", "{ResourceName} failed"),
        };

        logger.LogError("Container {ResourceName} failed for " + OwnerLogical, "unrelated-resource");
        logger.Log(LogLevel.Error, new EventId(1), conflictingState, null, (state, _) => string.Join(' ', state.Select(pair => pair.Value)) + " " + OwnerLogical);
        logger.LogError("resource=" + OwnerPhysical + "suffix failed");

        Assert.Empty(capture.Snapshot());
    }

    [Fact]
    public void CombinedMessageAndExceptionAreLimitedAndAliasesShareTheResourceQuota()
    {
        var capture = CreateCapture();
        using var services = CreateServices(capture);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Aspire.Hosting.Dcp.ContainerCreator");
        var exception = new InvalidOperationException(new string('e', 1500));

        for (var index = 0; index < NamespaceProbeDcpHostLogCapture.MaximumEntriesPerResource + 1; index++)
        {
            var resourceName = index % 2 == 0 ? OwnerLogical : OwnerPhysical;
            var message = new string('m', 1000) + " " + resourceName;
            logger.LogError(exception, message);
        }

        var entries = capture.Snapshot();
        Assert.Equal(NamespaceProbeDcpHostLogCapture.MaximumEntriesPerResource, entries.Count);
        Assert.All(entries, entry => Assert.True(entry.Message.Length + (entry.ExceptionMessage?.Length ?? 0) <= NamespaceProbeDcpHostLogCapture.MaximumEntryCharacters));
        var summary = capture.Summary();
        Assert.Equal(1, (int)summary.GetType().GetProperty("ownerDropped")!.GetValue(summary)!);
    }

    private static NamespaceProbeDcpHostLogCapture CreateCapture() =>
        new([OwnerLogical, OwnerPhysical], [FollowerLogical, FollowerPhysical]);

    private static ServiceProvider CreateServices(NamespaceProbeDcpHostLogCapture capture) =>
        new ServiceCollection().AddLogging(capture.Configure).BuildServiceProvider();
}