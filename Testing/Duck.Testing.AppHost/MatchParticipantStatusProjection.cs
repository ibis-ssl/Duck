using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Duck.Testing.AppHost;

internal sealed record MatchParticipantResourceStatus(
    string ResourceId,
    string? State,
    string? HealthStatus,
    string? CurrentResourceId,
    string? ContainerId,
    string RunId,
    bool ServiceEvidenceReady,
    DateTimeOffset? ServiceEvidenceObservedAtUtc,
    long? RecorderArchiveBytes,
    DateTimeOffset? RecorderArchiveLastWriteUtc,
    string? ServiceEvidenceReason = null)
{
    public bool IsReady
    {
        get
        {
            if (!string.Equals(State, "Running", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(CurrentResourceId) ||
                string.IsNullOrWhiteSpace(RunId) ||
                string.Equals(HealthStatus, "Unhealthy", StringComparison.OrdinalIgnoreCase) ||
                !RegisteredContainerEventProjection.HasFullContainerId(ContainerId))
            {
                return false;
            }

            return ResourceId switch
            {
                "tigers-blue" or "autoref-tigers" => ServiceEvidenceReady &&
                    ServiceEvidenceObservedAtUtc is not null && ServiceEvidenceObservedAtUtc <= DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1),
                "ssl-log-recorder" => ServiceEvidenceReady &&
                    RecorderArchiveBytes is > 0 &&
                    RecorderArchiveLastWriteUtc is { } lastWrite &&
                    DateTimeOffset.UtcNow - lastWrite <= TimeSpan.FromSeconds(15) &&
                    lastWrite <= DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1),
                _ => string.Equals(HealthStatus, "Healthy", StringComparison.OrdinalIgnoreCase),
            };
        }
    }
}

internal sealed record MatchParticipantStatusSnapshot(
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<MatchParticipantResourceStatus> Resources)
{
    public bool IsReady => Resources.Count > 0 && Resources.All(resource => resource.IsReady);
}

internal sealed record VerifiedContainerLogEvidence(
    string ContainerId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset StartedAtUtc,
    int RestartCount,
    string NetworkName,
    string NetworkId,
    string NetworkAddress)
{
    internal bool IsSameGeneration(VerifiedContainerLogEvidence other) =>
        ContainerId == other.ContainerId && CreatedAtUtc == other.CreatedAtUtc &&
        StartedAtUtc == other.StartedAtUtc && RestartCount == other.RestartCount &&
        NetworkName == other.NetworkName && NetworkId == other.NetworkId && NetworkAddress == other.NetworkAddress;

    internal static bool TryParse(
        string json,
        MatchParticipantCurrentTarget expected,
        string runId,
        string stackId,
        string resourceName,
        out VerifiedContainerLogEvidence? evidence)
    {
        evidence = null;
        if (expected.State != "Running" ||
            !RegisteredContainerEventProjection.HasFullContainerId(expected.ContainerId) ||
            string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(stackId) ||
            string.IsNullOrWhiteSpace(resourceName))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var id = root.GetProperty("Id").GetString();
            var createdText = root.GetProperty("Created").GetString();
            var stateElement = root.GetProperty("State");
            var state = stateElement.GetProperty("Status").GetString();
            var startedAtText = stateElement.GetProperty("StartedAt").GetString();
            var restartCount = root.GetProperty("RestartCount").GetInt32();
            var labels = root.GetProperty("Config").GetProperty("Labels");
            if (!string.Equals(id, expected.ContainerId, StringComparison.Ordinal) ||
                state != "running" ||
                !LabelMatches(labels, DockerContainerSpec.StackLabel, stackId) ||
                !LabelMatches(labels, DockerContainerSpec.ResourceLabel, resourceName) ||
                !LabelMatches(labels, DockerContainerSpec.RunLabel, runId))
                return false;

            if (!DateTimeOffset.TryParse(createdText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var created) ||
                !DateTimeOffset.TryParse(startedAtText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var startedAt) ||
                !OwnedContainerInspection.TryReadStandardDcpNetworkEndpoint(json, out var endpoint) || endpoint is null)
                return false;

            evidence = new(id!, created, startedAt, restartCount, endpoint.NetworkName, endpoint.NetworkId!, endpoint.Address.ToString());
            return true;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool LabelMatches(JsonElement labels, string key, string expected) =>
        labels.ValueKind == JsonValueKind.Object &&
        labels.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);
}

internal sealed record MatchParticipantCurrentTarget(
    string ResourceId,
    string ContainerId,
    string State,
    DateTimeOffset? StartedAtUtc = null,
    int RestartCount = 0);

internal sealed class MatchParticipantResourceIdMap
{
    private readonly object gate = new();
    private readonly Dictionary<IResource, MatchParticipantCurrentTarget?> targets = new(ReferenceEqualityComparer.Instance);

    internal MatchParticipantResourceIdMap(IEnumerable<IResource> resources)
    {
        foreach (var resource in resources)
            targets.Add(resource, null);
    }

    internal bool Observe(ResourceEvent observation, ResourceNotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(notifications);
        lock (gate)
        {
            if (!targets.ContainsKey(observation.Resource))
                return false;

            MatchParticipantCurrentTarget? target = null;
            if (notifications.TryGetCurrentState(observation.ResourceId, out var currentState) &&
                currentState is not null &&
                RegisteredContainerEventProjection.TryProject(
                    observation.Resource,
                    observation.ResourceId,
                    observation.Resource.Name,
                    observation,
                    currentState,
                    out var projected) &&
                projected is not null)
            {
                target = new MatchParticipantCurrentTarget(
                    projected.ResourceId,
                    projected.ContainerId,
                    currentState.Snapshot.State?.Text ?? "Unknown");
            }

            targets[observation.Resource] = target;
            return true;
        }
    }

    internal MatchParticipantCurrentTarget? GetCurrent(IResource resource)
    {
        lock (gate)
            return targets.TryGetValue(resource, out var target) ? target : null;
    }
}

internal sealed record MatchParticipantServiceEvidence(
    string ResourceId,
    string ContainerId,
    string RunId,
    DateTimeOffset ObservedAtUtc,
    IReadOnlySet<string> MatchedMarkers,
    bool IsReady,
    string Reason);

internal sealed class RecorderOutputGrowthTracker
{
    private string? containerId;
    private string? currentRunId;
    private long previousBytes;
    private bool hasPreviousObservation;

    internal bool Observe(
        MatchParticipantCurrentTarget? target,
        string runId,
        long? bytes,
        DateTimeOffset? lastWriteUtc,
        DateTimeOffset now)
    {
        if (target is not { State: "Running" } ||
            !RegisteredContainerEventProjection.HasFullContainerId(target.ContainerId) ||
            string.IsNullOrWhiteSpace(runId))
        {
            Reset();
            return false;
        }

        if (!string.Equals(containerId, target.ContainerId, StringComparison.Ordinal) ||
            !string.Equals(currentRunId, runId, StringComparison.Ordinal))
        {
            containerId = target.ContainerId;
            currentRunId = runId;
            previousBytes = 0;
            hasPreviousObservation = false;
        }

        var isFresh = lastWriteUtc is { } modified &&
            now - modified <= TimeSpan.FromSeconds(15) && modified <= now + TimeSpan.FromSeconds(1);
        var grew = bytes is > 0 && hasPreviousObservation && bytes.Value > previousBytes;
        previousBytes = bytes.GetValueOrDefault();
        hasPreviousObservation = bytes.HasValue;
        return isFresh && grew;
    }

    private void Reset()
    {
        containerId = null;
        currentRunId = null;
        previousBytes = 0;
        hasPreviousObservation = false;
    }
}

internal sealed class MatchParticipantLogEvidenceTracker
{
    internal static readonly TimeSpan EvidenceLifetime = TimeSpan.FromMinutes(2);
    private sealed class Entry
    {
        internal MatchParticipantCurrentTarget? Target;
        internal readonly HashSet<string> Markers = new(StringComparer.Ordinal);
        internal DateTimeOffset? ObservedAtUtc;
        internal DateTimeOffset? GenerationBoundaryUtc;
    }

    private readonly object gate = new();
    private readonly Dictionary<IResource, Entry> entries = new(ReferenceEqualityComparer.Instance);
    private readonly string runId;
    private readonly TimeProvider clock;
    private readonly DateTimeOffset runStartedAtUtc;

    internal MatchParticipantLogEvidenceTracker(
        IReadOnlyList<IResource> resources,
        string runId,
        TimeProvider clock)
    {
        this.runId = runId;
        this.clock = clock;
        runStartedAtUtc = clock.GetUtcNow();
        foreach (var resource in resources)
            entries.Add(resource, new Entry { GenerationBoundaryUtc = runStartedAtUtc });
    }

    internal void SetCurrentTarget(
        IResource resource,
        MatchParticipantCurrentTarget? target,
        long? minimumLineNumber = null,
        DateTimeOffset? generationBoundaryUtc = null)
    {
        _ = minimumLineNumber;
        lock (gate)
        {
            if (!entries.TryGetValue(resource, out var entry))
                return;

            var identityChanged = entry.Target?.ResourceId != target?.ResourceId ||
                entry.Target?.ContainerId != target?.ContainerId ||
                entry.Target?.StartedAtUtc != target?.StartedAtUtc ||
                entry.Target?.RestartCount != target?.RestartCount;
            if (identityChanged)
            {
                entry.Markers.Clear();
                entry.ObservedAtUtc = null;
                if (generationBoundaryUtc is { } boundary)
                    entry.GenerationBoundaryUtc = boundary;
                else if (entry.Target is not null && target is not null)
                    entry.GenerationBoundaryUtc = clock.GetUtcNow();
                if (target is null && entry.Target is not null)
                    entry.GenerationBoundaryUtc ??= clock.GetUtcNow();
            }

            entry.Target = target;
            if (target is null || target.State is not "Running" and not "Starting")
            {
                entry.Markers.Clear();
                entry.ObservedAtUtc = null;
                return;
            }

        }
    }

    internal void ObserveLogLine(IResource resource, string content, long lineNumber)
    {
        ArgumentNullException.ThrowIfNull(content);
        lock (gate)
        {
            if (!entries.TryGetValue(resource, out var entry))
                return;

            if (entry.Target is null || entry.Target.State is not "Running" and not "Starting")
            {
                return;
            }

            if (!TryReadSourceTimestamp(content, out var sourceTimestamp))
                return;
            if (entry.GenerationBoundaryUtc is { } boundary && sourceTimestamp <= boundary)
                return;
            if (entry.ObservedAtUtc is { } lastObserved && sourceTimestamp <= lastObserved)
                return;

            ObserveLogLine(resource, entry, content, sourceTimestamp);
        }
    }

    internal static bool TryReadSourceTimestamp(string content, out DateTimeOffset timestamp)
    {
        var match = Regex.Match(content,
            @"^\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:[.,]\d{1,9})?(?:Z|[+-]\d{2}:\d{2})?");
        if (!match.Success || !DateTimeOffset.TryParse(
                match.Value.Replace(',', '.'),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out timestamp))
        {
            timestamp = default;
            return false;
        }
        return true;
    }

    internal MatchParticipantServiceEvidence GetEvidence(IResource resource)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(resource, out var entry) || entry.Target is not { State: "Running" } target)
            {
                return new(string.Empty, string.Empty, runId, clock.GetUtcNow(), new HashSet<string>(), false,
                    "No current running container identity was observed.");
            }

            var required = RequiredMarkers(resource.Name);
            var matched = new HashSet<string>(entry.Markers, StringComparer.Ordinal);
            var observedAt = entry.ObservedAtUtc ?? clock.GetUtcNow();
            var isFresh = clock.GetUtcNow() - observedAt <= EvidenceLifetime &&
                observedAt <= clock.GetUtcNow() + TimeSpan.FromSeconds(1);
            var ready = required.Count > 0 && required.All(matched.Contains) && isFresh &&
                RegisteredContainerEventProjection.HasFullContainerId(target.ContainerId);
            return new(target.ResourceId, target.ContainerId, runId, entry.ObservedAtUtc ?? clock.GetUtcNow(), matched, ready,
                ready ? "Pinned service log markers were observed recently for the current container identity."
                    : $"Required current-container log markers are missing: {string.Join(",", required.Where(marker => !matched.Contains(marker)))}");
        }
    }

    internal static IReadOnlyList<string> RequiredMarkers(string resourceName) => resourceName switch
    {
        "tigers-blue" => ["geometry-received", "tracker-publisher-started"],
        "autoref-tigers" => ["geometry-received", "active-mode", "registered-with-game-controller"],
        _ => [],
    };

    private void ObserveLogLine(IResource resource, Entry entry, string content, DateTimeOffset sourceTimestamp)
    {
        var required = RequiredMarkers(resource.Name);
        if (required.Count == 0)
            return;

        var observedAny = false;
        if (content.Contains("Received geometry from vision", StringComparison.OrdinalIgnoreCase))
        {
            entry.Markers.Add("geometry-received");
            observedAny = true;
        }
        if (resource.Name == "tigers-blue" &&
            content.Contains("Publishing vision tracking packets to 224.5.23.2:11010", StringComparison.OrdinalIgnoreCase))
        {
            entry.Markers.Add("tracker-publisher-started");
            observedAny = true;
        }
        if (resource.Name == "autoref-tigers" &&
            content.Contains("Changed AutoRef mode to ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            entry.Markers.Add("active-mode");
            observedAny = true;
        }
        if (resource.Name == "autoref-tigers" &&
            content.Contains("Successfully registered AutoRef", StringComparison.OrdinalIgnoreCase))
        {
            entry.Markers.Add("registered-with-game-controller");
            observedAny = true;
        }
        if (observedAny)
            entry.ObservedAtUtc = sourceTimestamp;
    }
}

/// <summary>Projects current AppHost participants and target-owned readiness evidence to the shared result mount.</summary>
internal sealed class MatchParticipantStatusProjection(
    ResourceNotificationService notifications,
    IReadOnlyList<IResource> resources,
    string path,
    string recorderOutputDirectory,
    string runId,
    string dockerExecutable,
    string stackId,
    TimeProvider clock) : BackgroundService, IHostedLifecycleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MatchParticipantResourceIdMap resourceIds = new(resources);
    private readonly RecorderOutputGrowthTracker recorderOutputGrowth = new();
    private readonly DockerCliCommandRunner docker = new(dockerExecutable);
    private readonly ConcurrentDictionary<IResource, (VerifiedContainerLogEvidence Identity, DateTimeOffset Cursor)> logCursors =
        new(ReferenceEqualityComparer.Instance);
    private readonly MatchParticipantLogEvidenceTracker logEvidence = new(
        resources.Where(resource => MatchParticipantLogEvidenceTracker.RequiredMarkers(resource.Name).Count > 0).ToArray(),
        runId,
        clock);
    private int observerStarted;

    public Task StartingAsync(CancellationToken cancellationToken) => StartObserverAsync(cancellationToken);
    public override Task StartAsync(CancellationToken cancellationToken) => StartObserverAsync(cancellationToken);
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private Task StartObserverAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref observerStarted, 1, 0) != 0)
            return Task.CompletedTask;
        try
        {
            return base.StartAsync(cancellationToken);
        }
        catch
        {
            Volatile.Write(ref observerStarted, 0);
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException(
            "Match participant status path must have a parent directory.");
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(recorderOutputDirectory);
        var temporaryPath = path + ".tmp";
        var idObserver = ObserveResourceIdsAsync(stoppingToken);

        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            if (idObserver.IsCompleted)
                await idObserver.ConfigureAwait(false);
            var statuses = new List<MatchParticipantResourceStatus>(resources.Count);
            foreach (var resource in resources)
            {
                var target = resourceIds.GetCurrent(resource);
                var currentState = target is not null && notifications.TryGetCurrentState(target.ResourceId, out var current)
                    ? current
                    : null;
                var currentSnapshot = currentState is not null && ReferenceEquals(currentState.Resource, resource) &&
                    currentState.ResourceId == target?.ResourceId &&
                    RegisteredContainerEventProjection.TryProject(
                        resource, target.ResourceId, resource.Name, currentState, currentState, out var currentTarget) &&
                    currentTarget?.ContainerId == target.ContainerId
                    ? currentState.Snapshot
                    : null;
                var serviceEvidenceReady = false;
                DateTimeOffset? serviceEvidenceObservedAtUtc = null;
                string? serviceEvidenceReason = null;
                long? archiveBytes = null;
                DateTimeOffset? archiveLastWrite = null;
                if (MatchParticipantLogEvidenceTracker.RequiredMarkers(resource.Name).Count > 0)
                {
                    var evidence = target is null ? null :
                        await CollectCurrentContainerLogsAsync(resource, target, stoppingToken).ConfigureAwait(false);
                    if (evidence is null)
                    {
                        logCursors.TryRemove(resource, out _);
                        logEvidence.SetCurrentTarget(resource, null);
                    }
                    else
                    {
                        logCursors[resource] = (evidence.Identity, evidence.Cursor);
                    }
                    var currentEvidence = logEvidence.GetEvidence(resource);
                    serviceEvidenceReady = false;
                    serviceEvidenceObservedAtUtc = currentEvidence.MatchedMarkers.Count > 0
                        ? currentEvidence.ObservedAtUtc
                        : null;
                    var partial = string.Join(",", currentEvidence.MatchedMarkers.Order(StringComparer.Ordinal));
                    serviceEvidenceReason = resource.Name switch
                    {
                        "tigers-blue" when evidence is null => "Full-ID inspect/log collection failed or generation/network changed; supplemental evidence was rejected.",
                        "autoref-tigers" when evidence is null => "Full-ID inspect/log collection failed or generation/network changed; supplemental evidence was rejected.",
                        "tigers-blue" => $"Target-side receive/decode diagnostics are unavailable; supplemental startup markers observed: {(partial.Length == 0 ? "none" : partial)}.",
                        "autoref-tigers" => $"Target-side decoded processing evidence is unavailable; supplemental startup markers observed: {(partial.Length == 0 ? "none" : partial)}.",
                        _ => "Participant diagnostics unavailable.",
                    };
                }
                else if (resource.Name == "ssl-log-recorder" && target is { State: "Running" })
                {
                    var archive = GetFreshRecorderArchive(recorderOutputDirectory, clock.GetUtcNow());
                    if (archive is not null)
                    {
                        archiveBytes = archive.Value.Bytes;
                        archiveLastWrite = archive.Value.LastWriteUtc;
                    }
                    _ = recorderOutputGrowth.Observe(target, runId, archiveBytes, archiveLastWrite, clock.GetUtcNow());
                    serviceEvidenceReady = false;
                    serviceEvidenceReason = "Complete live recorder records could not be decoded; archive growth is supplemental only.";
                }
                else if (resource.Name == "ssl-log-recorder")
                {
                    recorderOutputGrowth.Observe(target, runId, null, null, clock.GetUtcNow());
                    serviceEvidenceReason = "Recorder container identity is unavailable; no decoded record evidence was collected.";
                }

                statuses.Add(new MatchParticipantResourceStatus(
                    resource.Name,
                    currentSnapshot?.State?.Text,
                    currentSnapshot?.HealthStatus?.ToString(),
                    currentSnapshot is null ? null : target?.ResourceId,
                    currentSnapshot is null ? null : target?.ContainerId,
                    currentSnapshot is null ? string.Empty : runId,
                    serviceEvidenceReady,
                    serviceEvidenceObservedAtUtc,
                    archiveBytes,
                    archiveLastWrite,
                    serviceEvidenceReason));
            }

            var content = JsonSerializer.Serialize(
                new MatchParticipantStatusSnapshot(clock.GetUtcNow(), statuses), JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, content, stoppingToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
            await Task.Delay(TimeSpan.FromMilliseconds(250), clock, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ObserveResourceIdsAsync(CancellationToken stoppingToken)
    {
        await foreach (var observation in notifications.WatchAsync(stoppingToken).ConfigureAwait(false))
        {
            var previous = resourceIds.GetCurrent(observation.Resource);
            resourceIds.Observe(observation, notifications);
            var current = resourceIds.GetCurrent(observation.Resource);
            if (previous?.ResourceId != current?.ResourceId || previous?.ContainerId != current?.ContainerId)
            {
                if (current is null)
                {
                    logCursors.TryRemove(observation.Resource, out _);
                    logEvidence.SetCurrentTarget(observation.Resource, null);
                }
            }
        }
    }

    private async Task<ContainerLogCollection?> CollectCurrentContainerLogsAsync(
        IResource resource,
        MatchParticipantCurrentTarget target,
        CancellationToken cancellationToken)
    {
        if (!RegisteredContainerEventProjection.HasFullContainerId(target.ContainerId))
            return null;

        try
        {
            var beforeJson = await docker.CaptureAsync(
                DockerContainerSpec.CreateInspectArguments(target.ContainerId), cancellationToken).ConfigureAwait(false);
            if (!VerifiedContainerLogEvidence.TryParse(
                    beforeJson, target, runId, stackId, resource.Name, out var before) || before is null)
                return null;

            var cursor = logCursors.TryGetValue(resource, out var previous) && previous.Identity.IsSameGeneration(before)
                ? previous.Cursor
                : before.StartedAtUtc;
            var since = cursor > before.StartedAtUtc ? cursor.AddSeconds(-1) : before.StartedAtUtc;
            var logs = await docker.CaptureAsync(
                ["logs", "--timestamps", "--since", since.ToString("O", CultureInfo.InvariantCulture), target.ContainerId],
                cancellationToken).ConfigureAwait(false);
            var afterJson = await docker.CaptureAsync(
                DockerContainerSpec.CreateInspectArguments(target.ContainerId), cancellationToken).ConfigureAwait(false);
            if (!VerifiedContainerLogEvidence.TryParse(
                    afterJson, target, runId, stackId, resource.Name, out var after) ||
                after is null || !before.IsSameGeneration(after))
                return null;
            var observedTarget = resourceIds.GetCurrent(resource);
            if (observedTarget is null || observedTarget.ResourceId != target.ResourceId ||
                observedTarget.ContainerId != target.ContainerId || observedTarget.State != "Running")
                return null;

            var generationTarget = target with
            {
                StartedAtUtc = before.StartedAtUtc,
                RestartCount = before.RestartCount,
            };
            logEvidence.SetCurrentTarget(resource, generationTarget, generationBoundaryUtc: before.StartedAtUtc);
            var lineNumber = 0L;
            foreach (var line in logs.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (MatchParticipantLogEvidenceTracker.TryReadSourceTimestamp(line, out var sourceTimestamp) && sourceTimestamp > cursor)
                    cursor = sourceTimestamp;
                logEvidence.ObserveLogLine(resource, line, ++lineNumber);
            }
            return new(before, cursor);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or FormatException)
        {
            return null;
        }
    }

    private sealed record ContainerLogCollection(VerifiedContainerLogEvidence Identity, DateTimeOffset Cursor);

    private static (long Bytes, DateTimeOffset LastWriteUtc)? GetFreshRecorderArchive(
        string directory,
        DateTimeOffset now)
    {
        if (!Directory.Exists(directory))
            return null;
        var latest = Directory.EnumerateFiles(directory, "*.log.gz", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists && file.Length > 0)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
        if (latest is null)
            return null;
        var lastWrite = new DateTimeOffset(latest.LastWriteTimeUtc, TimeSpan.Zero);
        return now - lastWrite <= TimeSpan.FromSeconds(15) && lastWrite <= now + TimeSpan.FromSeconds(1)
            ? (latest.Length, lastWrite)
            : null;
    }
}
