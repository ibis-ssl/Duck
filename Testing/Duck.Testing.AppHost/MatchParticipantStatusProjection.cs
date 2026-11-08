using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.Diagnostics;
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
    string? ServiceEvidenceReason = null,
    string? TrackerOutputNetworkId = null,
    string? TrackerOutputNetworkAddress = null,
    ServiceReceiveSnapshot? MatchedTrackerReceiveSource = null)
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
                    ServiceEvidenceObservedAtUtc is { } recorderObservedAt &&
                    DateTimeOffset.UtcNow - recorderObservedAt <= TimeSpan.FromSeconds(15) &&
                    recorderObservedAt <= DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1) &&
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
    IReadOnlyList<MatchParticipantResourceStatus> Resources,
    IReadOnlyList<ServiceReceiveSnapshot>? DuckTrackerReceiveSources = null,
    DateTimeOffset? DuckTrackerDiagnosticsObservedAtUtc = null,
    string? DuckContainerId = null,
    string? DuckNetworkId = null,
    string? DuckNetworkAddress = null,
    IReadOnlyList<ServiceReceiveSnapshot>? DuckSelfDecodedTrackerSources = null)
{
    public bool IsReady => Resources.Count > 0 && Resources.All(resource => resource.IsReady);
    public string AppHostOperatingSystem => OperatingSystem.IsLinux() ? "linux" :
        OperatingSystem.IsWindows() ? "windows" : "other";
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
    IResource duckResource,
    EndpointReference duckVisionEndpoint,
    string path,
    string recorderOutputDirectory,
    string runId,
    string dockerExecutable,
    string stackId,
    TimeProvider clock) : BackgroundService, IHostedLifecycleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient VisionHealthClient = new(new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(2),
    }) { Timeout = TimeSpan.FromSeconds(2) };
    private static readonly TimeSpan RecorderIdentityValidationInterval = TimeSpan.FromSeconds(1);
    private readonly MatchParticipantResourceIdMap resourceIds = new(resources);
    private readonly DockerCliCommandRunner docker = new(dockerExecutable);
    private readonly Dictionary<string, (string Key, long Count)> trackerBaselines = new(StringComparer.Ordinal);
    private readonly object recorderSessionGate = new();
    private readonly SemaphoreSlim recorderSessionTransition = new(1, 1);
    private RecorderReaderSession? recorderSession;
    private int recorderSessionsStopping;
    private readonly ConcurrentDictionary<IResource, (VerifiedContainerLogEvidence Identity, DateTimeOffset Cursor)> logCursors =
        new(ReferenceEqualityComparer.Instance);
    private readonly MatchParticipantLogEvidenceTracker logEvidence = new(
        resources.Where(resource => MatchParticipantLogEvidenceTracker.RequiredMarkers(resource.Name).Count > 0).ToArray(),
        runId,
        clock);
    private int observerStarted;

    private sealed record RecorderArchiveCollection(
        VerifiedContainerLogEvidence Identity,
        FileInfo Archive,
        SslLogDecodedInputSummary Summary,
        string? Failure);

    private sealed class RecorderReaderSession(
        string runId,
        VerifiedContainerLogEvidence identity,
        string archivePath,
        CancellationToken stoppingToken)
    {
        private readonly CancellationTokenSource cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        private Task? readerTask;
        private SslLogDecodedInputSummary latest = SslLogDecodedInputSummary.Empty;
        private string? failure;

        internal string RunId { get; } = runId;
        internal VerifiedContainerLogEvidence Identity { get; } = identity;
        internal string ArchivePath { get; } = archivePath;
        internal DateTimeOffset LastIdentityValidationUtc { get; set; }
        internal SslLogDecodedInputSummary Latest => Volatile.Read(ref latest);
        internal string? Failure => Volatile.Read(ref failure);

        internal bool Matches(
            string expectedRunId,
            VerifiedContainerLogEvidence expectedIdentity,
            string expectedPath) =>
            string.Equals(RunId, expectedRunId, StringComparison.Ordinal) &&
            RecorderGenerationMatches(Identity, expectedIdentity) &&
            string.Equals(ArchivePath, expectedPath, StringComparison.OrdinalIgnoreCase);

        internal void Start()
        {
            readerTask = Task.Run(Run, CancellationToken.None);
        }

        internal Task Completion => readerTask ?? Task.CompletedTask;

        internal void Cancel()
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The reader exited before the owning projection observed the identity change.
            }
        }

        private void Run()
        {
            var token = cancellation.Token;
            try
            {
                using var file = new FileStream(ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var appendStream = new AppendWaitFileReadStream(file, ArchivePath, token);
                var result = SslLogRecordReader.ReadAppending(
                    appendStream,
                    progress => Volatile.Write(ref latest, progress),
                    token);
                Volatile.Write(ref latest, result);
                Volatile.Write(ref failure, result.ReadFailure ?? "Recorder input stream ended unexpectedly.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Expected on resource replacement or AppHost shutdown.
            }
            catch (Exception exception)
            {
                Volatile.Write(ref failure, exception.Message);
            }
            finally
            {
                cancellation.Dispose();
            }
        }
    }

    public Task StartingAsync(CancellationToken cancellationToken) => StartObserverAsync(cancellationToken);
    public override Task StartAsync(CancellationToken cancellationToken) => StartObserverAsync(cancellationToken);
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref recorderSessionsStopping, 1);
        await ResetRecorderSessionAsync().ConfigureAwait(false);
    }
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref recorderSessionsStopping, 1);
        await ResetRecorderSessionAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

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
        var idObserver = ObserveResourceIdsAsync(stoppingToken);
        IReadOnlyList<ServiceReceiveSnapshot>? duckTrackerReceiveSources = null;
        DateTimeOffset? duckTrackerDiagnosticsObservedAtUtc = null;
        string? duckContainerId = null;
        string? duckNetworkId = null;
        string? duckNetworkAddress = null;

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
                string? trackerOutputNetworkId = null;
                string? trackerOutputNetworkAddress = null;
                ServiceReceiveSnapshot? matchedTrackerReceiveSource = null;
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
                    if (resource.Name is "tigers-blue" or "autoref-tigers" && target is not null)
                    {
                        var trackerEvidence = await CollectNativeTrackerOutputAsync(
                            resource, target, stoppingToken).ConfigureAwait(false);
                        (duckTrackerReceiveSources, duckTrackerDiagnosticsObservedAtUtc) =
                            PreserveTrackerDiagnostics(
                                duckTrackerReceiveSources, duckTrackerDiagnosticsObservedAtUtc, trackerEvidence);
                        matchedTrackerReceiveSource = trackerEvidence.MatchedSource;
                        trackerOutputNetworkId = trackerEvidence.ParticipantNetworkId;
                        trackerOutputNetworkAddress = trackerEvidence.ParticipantNetworkAddress;
                        if (trackerEvidence.DuckContainerId is not null)
                        {
                            duckContainerId = trackerEvidence.DuckContainerId;
                            duckNetworkId = trackerEvidence.DuckNetworkId;
                            duckNetworkAddress = trackerEvidence.DuckNetworkAddress;
                        }
                        serviceEvidenceReady = trackerEvidence.Ready;
                        serviceEvidenceObservedAtUtc = trackerEvidence.ObservedAtUtc;
                        serviceEvidenceReason = trackerEvidence.Reason;
                    }
                    else
                    {
                        serviceEvidenceReady = false;
                        serviceEvidenceObservedAtUtc = currentEvidence.MatchedMarkers.Count > 0
                            ? currentEvidence.ObservedAtUtc
                            : null;
                        var partial = string.Join(",", currentEvidence.MatchedMarkers.Order(StringComparer.Ordinal));
                        serviceEvidenceReason = resource.Name switch
                        {
                            "autoref-tigers" when evidence is null => "Full-ID inspect/log collection failed or generation/network changed; supplemental evidence was rejected.",
                            "autoref-tigers" => $"Target-side decoded processing evidence is unavailable; supplemental startup markers observed: {(partial.Length == 0 ? "none" : partial)}.",
                            _ => "Participant diagnostics unavailable.",
                        };
                    }
                }
                else if (resource.Name == "ssl-log-recorder" && target is { State: "Running" })
                {
                    var collection = await CollectCurrentRecorderRecordsAsync(resource, target, stoppingToken)
                        .ConfigureAwait(false);
                    if (collection is not null)
                    {
                        archiveBytes = collection.Archive.Length;
                        archiveLastWrite = new DateTimeOffset(collection.Archive.LastWriteTimeUtc, TimeSpan.Zero);
                        var observedAtUtc = default(DateTimeOffset);
                        serviceEvidenceReady = collection.Failure is null && collection.Summary.TryGetFreshEvidenceTime(
                            collection.Identity.StartedAtUtc, clock.GetUtcNow(), TimeSpan.FromSeconds(15), out observedAtUtc);
                        serviceEvidenceObservedAtUtc = serviceEvidenceReady ? observedAtUtc : null;
                        serviceEvidenceReason = DescribeRecorderEvidence(
                            collection.Summary, serviceEvidenceReady, collection.Failure);
                    }
                    else
                    {
                        await ResetRecorderSessionAsync().ConfigureAwait(false);
                        serviceEvidenceReason = "Recorder identity, current-run archive, or complete input records could not be verified.";
                    }
                }
                else if (resource.Name == "ssl-log-recorder")
                {
                    await ResetRecorderSessionAsync().ConfigureAwait(false);
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
                    serviceEvidenceReason,
                    trackerOutputNetworkId,
                    trackerOutputNetworkAddress,
                    matchedTrackerReceiveSource));
            }

            var duckSelfDecodedTrackerSources = duckNetworkAddress is not null && duckTrackerReceiveSources is not null
                ? SelectCurrentDecodedSourcesFromAddress(
                    duckTrackerReceiveSources, duckNetworkAddress, runId, duckResource.Name, duckContainerId)
                : null;
            var content = JsonSerializer.Serialize(
                new MatchParticipantStatusSnapshot(
                    clock.GetUtcNow(), statuses, duckTrackerReceiveSources, duckTrackerDiagnosticsObservedAtUtc,
                    duckContainerId, duckNetworkId, duckNetworkAddress, duckSelfDecodedTrackerSources),
                JsonOptions);
            await WriteStatusSnapshotAsync(path, content, stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(250), clock, stoppingToken).ConfigureAwait(false);
        }
    }

    internal static async Task WriteStatusSnapshotAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(temporaryPath, path, overwrite: true);
                return;
            }
            catch (Exception exception) when (OperatingSystem.IsWindows() && attempt < 4 &&
                (exception is UnauthorizedAccessException ||
                 exception is IOException && (exception.HResult & 0xffff) is 32 or 33))
            {
                // A Windows reader can briefly deny deletion while reading the previous snapshot.
                // Keep that complete snapshot and retry only the atomic replacement for at most 400ms.
                // Persistent denial still fails; cancellation and readiness freshness remain effective.
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ObserveResourceIdsAsync(CancellationToken stoppingToken)
    {
        await foreach (var observation in notifications.WatchAsync(stoppingToken).ConfigureAwait(false))
        {
            var previous = resourceIds.GetCurrent(observation.Resource);
            resourceIds.Observe(observation, notifications);
            var current = resourceIds.GetCurrent(observation.Resource);
            if (observation.Resource.Name == "ssl-log-recorder" &&
                (previous?.ResourceId != current?.ResourceId ||
                 previous?.ContainerId != current?.ContainerId ||
                 current?.State != "Running"))
                await ResetRecorderSessionAsync().ConfigureAwait(false);
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

    private async Task<RecorderArchiveCollection?> CollectCurrentRecorderRecordsAsync(
        IResource resource,
        MatchParticipantCurrentTarget target,
        CancellationToken cancellationToken)
    {
        if (!RegisteredContainerEventProjection.HasFullContainerId(target.ContainerId))
        {
            await ResetRecorderSessionAsync().ConfigureAwait(false);
            return null;
        }

        try
        {
            var now = clock.GetUtcNow();
            var archive = GetFreshRecorderArchive(recorderOutputDirectory, now);
            if (archive is null)
            {
                await ResetRecorderSessionAsync().ConfigureAwait(false);
                return null;
            }

            var session = GetRecorderSession();
            var needsValidation = session is null ||
                now - session.LastIdentityValidationUtc >= RecorderIdentityValidationInterval;
            VerifiedContainerLogEvidence identity;
            if (needsValidation)
            {
                var beforeJson = await docker.CaptureAsync(
                    DockerContainerSpec.CreateInspectArguments(target.ContainerId), cancellationToken).ConfigureAwait(false);
                if (!VerifiedContainerLogEvidence.TryParse(
                        beforeJson, target, runId, stackId, resource.Name, out var before) || before is null)
                {
                    await ResetRecorderSessionAsync().ConfigureAwait(false);
                    return null;
                }

                if (session is null || !session.Matches(runId, before, archive.FullName))
                {
                    await ResetRecorderSessionAsync().ConfigureAwait(false);
                    session = await StartRecorderSessionAsync(before, archive.FullName, cancellationToken)
                        .ConfigureAwait(false);
                    if (session is null)
                        return null;
                }

                var afterJson = await docker.CaptureAsync(
                    DockerContainerSpec.CreateInspectArguments(target.ContainerId), cancellationToken).ConfigureAwait(false);
                if (!VerifiedContainerLogEvidence.TryParse(
                        afterJson, target, runId, stackId, resource.Name, out var after) ||
                    after is null || !RecorderGenerationMatches(before, after))
                {
                    await ResetRecorderSessionAsync().ConfigureAwait(false);
                    return null;
                }

                identity = after;
                session.LastIdentityValidationUtc = now;
            }
            else
            {
                identity = session!.Identity;
            }

            var currentTarget = resourceIds.GetCurrent(resource);
            if (currentTarget is null || currentTarget.ResourceId != target.ResourceId ||
                currentTarget.ContainerId != target.ContainerId || currentTarget.State != "Running")
            {
                await ResetRecorderSessionAsync().ConfigureAwait(false);
                return null;
            }

            if (session is null || !session.Matches(runId, identity, archive.FullName))
            {
                await ResetRecorderSessionAsync().ConfigureAwait(false);
                return null;
            }

            return new(identity, archive, session.Latest, session.Failure);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
                                          JsonException or FormatException or UnauthorizedAccessException)
        {
            await ResetRecorderSessionAsync().ConfigureAwait(false);
            return null;
        }
    }

    private static bool RecorderGenerationMatches(
        VerifiedContainerLogEvidence left,
        VerifiedContainerLogEvidence right) =>
        left.ContainerId == right.ContainerId && left.CreatedAtUtc == right.CreatedAtUtc &&
        left.StartedAtUtc == right.StartedAtUtc && left.RestartCount == right.RestartCount;

    private RecorderReaderSession? GetRecorderSession()
    {
        lock (recorderSessionGate)
            return recorderSession;
    }

    private async Task<RecorderReaderSession?> StartRecorderSessionAsync(
        VerifiedContainerLogEvidence identity,
        string archivePath,
        CancellationToken stoppingToken)
    {
        await recorderSessionTransition.WaitAsync(stoppingToken).ConfigureAwait(false);
        try
        {
            await ResetRecorderSessionCoreAsync().ConfigureAwait(false);
            if (Volatile.Read(ref recorderSessionsStopping) != 0)
                return null;

            var session = new RecorderReaderSession(runId, identity, archivePath, stoppingToken);
            lock (recorderSessionGate)
            {
                session.Start();
                recorderSession = session;
            }
            return session;
        }
        finally
        {
            recorderSessionTransition.Release();
        }
    }

    private async Task ResetRecorderSessionAsync()
    {
        await recorderSessionTransition.WaitAsync().ConfigureAwait(false);
        try
        {
            await ResetRecorderSessionCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            recorderSessionTransition.Release();
        }
    }

    private async Task ResetRecorderSessionCoreAsync()
    {
        RecorderReaderSession? previous;
        lock (recorderSessionGate)
        {
            previous = recorderSession;
            recorderSession = null;
        }
        if (previous is null)
            return;

        previous.Cancel();
        await previous.Completion.ConfigureAwait(false);
    }

    private async Task<TigersTrackerOutputResult> CollectNativeTrackerOutputAsync(
        IResource participantResource,
        MatchParticipantCurrentTarget participantTarget,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CollectNativeTrackerOutputCoreAsync(participantResource, participantTarget, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return TigersTrackerOutputResult.NotReady("Duck vision-health request exceeded its 2-second observation budget.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or
                                          JsonException or FormatException or UnauthorizedAccessException)
        {
            return TigersTrackerOutputResult.NotReady($"Current Duck tracker-output observation failed: {exception.Message}");
        }
    }

    private async Task<TigersTrackerOutputResult> CollectNativeTrackerOutputCoreAsync(
        IResource participantResource,
        MatchParticipantCurrentTarget participantTarget,
        CancellationToken cancellationToken)
    {
        var participantBeforeJson = await docker.CaptureAsync(
            DockerContainerSpec.CreateInspectArguments(participantTarget.ContainerId), cancellationToken).ConfigureAwait(false);
        if (!VerifiedContainerLogEvidence.TryParse(
                participantBeforeJson, participantTarget, runId, stackId, participantResource.Name, out var participantBefore) || participantBefore is null)
            return TigersTrackerOutputResult.NotReady($"{participantResource.Name} current full-ID/run/network inspection failed.");

        var duckTarget = resourceIds.GetCurrent(duckResource);
        if (duckTarget is null || duckTarget.State != "Running")
            return TigersTrackerOutputResult.NotReady("Duck current container identity is unavailable.");
        var duckBeforeJson = await docker.CaptureAsync(
            DockerContainerSpec.CreateInspectArguments(duckTarget.ContainerId), cancellationToken).ConfigureAwait(false);
        if (!VerifiedContainerLogEvidence.TryParse(
                duckBeforeJson, duckTarget, runId, stackId, duckResource.Name, out var duckBefore) || duckBefore is null)
            return TigersTrackerOutputResult.NotReady("Duck current full-ID/run/network inspection failed.");
        if (!string.Equals(participantBefore.NetworkId, duckBefore.NetworkId, StringComparison.Ordinal) ||
            !IPAddress.TryParse(participantBefore.NetworkAddress, out var participantAddress))
            return TigersTrackerOutputResult.NotReady(
                $"{participantResource.Name} and Duck are not verified on one current network.",
                duckContainerId: duckBefore.ContainerId,
                duckNetworkId: duckBefore.NetworkId,
                duckNetworkAddress: duckBefore.NetworkAddress);

        var endpointValue = await duckVisionEndpoint.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (!Uri.TryCreate(endpointValue?.ToString(), UriKind.Absolute, out var healthUri))
            return TigersTrackerOutputResult.NotReady("Duck vision-health endpoint is not allocated.");
        healthUri = new Uri(healthUri, "/health/vision");

        var requestStarted = clock.GetUtcNow();
        using var response = await VisionHealthClient.GetAsync(healthUri, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var participantAfterJson = await docker.CaptureAsync(
            DockerContainerSpec.CreateInspectArguments(participantTarget.ContainerId), cancellationToken).ConfigureAwait(false);
        var duckAfterJson = await docker.CaptureAsync(
            DockerContainerSpec.CreateInspectArguments(duckTarget.ContainerId), cancellationToken).ConfigureAwait(false);
        if (!VerifiedContainerLogEvidence.TryParse(
                participantAfterJson, participantTarget, runId, stackId, participantResource.Name, out var participantAfter) ||
            participantAfter is null || !participantBefore.IsSameGeneration(participantAfter) ||
            !VerifiedContainerLogEvidence.TryParse(
                duckAfterJson, duckTarget, runId, stackId, duckResource.Name, out var duckAfter) ||
            duckAfter is null || !duckBefore.IsSameGeneration(duckAfter))
            return TigersTrackerOutputResult.NotReady($"{participantResource.Name} or Duck generation/network changed during tracker-output observation.");

        var currentParticipant = resourceIds.GetCurrent(participantResource);
        var currentDuck = resourceIds.GetCurrent(duckResource);
        if (currentParticipant is null || currentParticipant.ResourceId != participantTarget.ResourceId ||
            currentParticipant.ContainerId != participantTarget.ContainerId || currentParticipant.State != "Running" ||
            currentDuck is null || currentDuck.ResourceId != duckTarget.ResourceId ||
            currentDuck.ContainerId != duckTarget.ContainerId || currentDuck.State != "Running")
            return TigersTrackerOutputResult.NotReady("AppHost resource identities changed during tracker-output observation.");

        var decisionTime = clock.GetUtcNow();

        VisionHealthTrackerResponse? trackerResponse;
        try
        {
            trackerResponse = JsonSerializer.Deserialize<VisionHealthTrackerResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return TigersTrackerOutputResult.NotReady("Duck vision-health response did not contain tracker diagnostics.");
        }

        var trackerReceiveSources = trackerResponse?.TrackerDiagnostics;
        if (trackerReceiveSources is null)
            return TigersTrackerOutputResult.NotReady("Duck vision-health response did not contain tracker diagnostics.");
        var trackerDiagnosticsObservedAtUtc = clock.GetUtcNow();

        if (!NativeTrackerOutputReadiness.TrySelectFreshCurrentSource(
                trackerReceiveSources,
                runId,
                duckTarget.ResourceId,
                duckTarget.ContainerId,
                participantAddress,
                decisionTime - requestStarted,
                decisionTime,
                out var snapshot,
                out var trackerReason) || snapshot is null)
            return TigersTrackerOutputResult.NotReady(
                trackerReason, trackerReceiveSources, trackerDiagnosticsObservedAtUtc,
                duckBefore.ContainerId, duckBefore.NetworkId, duckBefore.NetworkAddress,
                null, participantBefore.NetworkId, participantBefore.NetworkAddress);
        var baselineKey = string.Join('\u001f', runId, participantBefore.ContainerId, participantBefore.StartedAtUtc,
            participantBefore.RestartCount, participantBefore.NetworkId, participantBefore.NetworkAddress,
            duckBefore.ContainerId, duckBefore.StartedAtUtc, duckBefore.RestartCount, duckBefore.NetworkId,
            snapshot.Identity!.Generation, snapshot.ReceiverEpoch, snapshot.SourceKey, snapshot.SourceUuid,
            snapshot.RemoteEndpoint);
        if (trackerBaselines.TryGetValue(participantResource.Name, out var previous) &&
            string.Equals(previous.Key, baselineKey, StringComparison.Ordinal) &&
            snapshot.SuccessfullyDecodedCount > previous.Count)
        {
            trackerBaselines[participantResource.Name] = (baselineKey, snapshot.SuccessfullyDecodedCount);
            return new(true, snapshot.LastReceivedAt,
                $"Duck observed fresh tracker output from the current {participantResource.Name} full-ID/run/network generation; this establishes tracker-output preparation only.",
                trackerReceiveSources,
                trackerDiagnosticsObservedAtUtc,
                duckBefore.ContainerId,
                duckBefore.NetworkId,
                duckBefore.NetworkAddress,
                snapshot,
                participantBefore.NetworkId,
                participantBefore.NetworkAddress);
        }

        trackerBaselines[participantResource.Name] = (baselineKey, snapshot.SuccessfullyDecodedCount);
        return TigersTrackerOutputResult.NotReady(
            $"A fresh current {participantResource.Name} tracker count baseline is recorded; waiting for a new decoded packet.",
            trackerReceiveSources,
            trackerDiagnosticsObservedAtUtc,
            duckBefore.ContainerId,
            duckBefore.NetworkId,
            duckBefore.NetworkAddress,
            snapshot,
            participantBefore.NetworkId,
            participantBefore.NetworkAddress);
    }

    private sealed record VisionHealthTrackerResponse(IReadOnlyList<ServiceReceiveSnapshot>? TrackerDiagnostics);

    internal sealed record TigersTrackerOutputResult(
        bool Ready,
        DateTimeOffset? ObservedAtUtc,
        string Reason,
        IReadOnlyList<ServiceReceiveSnapshot>? TrackerReceiveSources = null,
        DateTimeOffset? TrackerDiagnosticsObservedAtUtc = null,
        string? DuckContainerId = null,
        string? DuckNetworkId = null,
        string? DuckNetworkAddress = null,
        ServiceReceiveSnapshot? MatchedSource = null,
        string? ParticipantNetworkId = null,
        string? ParticipantNetworkAddress = null)
    {
        internal static TigersTrackerOutputResult NotReady(
            string reason,
            IReadOnlyList<ServiceReceiveSnapshot>? trackerReceiveSources = null,
            DateTimeOffset? trackerDiagnosticsObservedAtUtc = null,
            string? duckContainerId = null,
            string? duckNetworkId = null,
            string? duckNetworkAddress = null,
            ServiceReceiveSnapshot? matchedSource = null,
            string? participantNetworkId = null,
            string? participantNetworkAddress = null) =>
            new(false, null, reason, trackerReceiveSources, trackerDiagnosticsObservedAtUtc,
                duckContainerId, duckNetworkId, duckNetworkAddress, matchedSource,
                participantNetworkId, participantNetworkAddress);
    }

    internal static (IReadOnlyList<ServiceReceiveSnapshot>? Sources, DateTimeOffset? ObservedAtUtc)
        PreserveTrackerDiagnostics(
            IReadOnlyList<ServiceReceiveSnapshot>? currentSources,
            DateTimeOffset? currentObservedAtUtc,
            TigersTrackerOutputResult trackerEvidence) =>
        trackerEvidence.TrackerReceiveSources is null
            ? (currentSources, currentObservedAtUtc)
            : (trackerEvidence.TrackerReceiveSources, trackerEvidence.TrackerDiagnosticsObservedAtUtc);

    internal static IReadOnlyList<ServiceReceiveSnapshot> SelectCurrentDecodedSourcesFromAddress(
        IReadOnlyList<ServiceReceiveSnapshot> sources,
        string address,
        string expectedRunId,
        string expectedResourceId,
        string? expectedContainerId)
    {
        if (!IPAddress.TryParse(address, out var expectedAddress) ||
            string.IsNullOrWhiteSpace(expectedRunId) || string.IsNullOrWhiteSpace(expectedResourceId) ||
            !RegisteredContainerEventProjection.HasFullContainerId(expectedContainerId))
            return [];

        return sources.Where(source =>
                source.SuccessfullyDecodedCount > 0 &&
                source.DecodeStatus == DiagnosticDecodeStatus.Succeeded &&
                source.ReceiverEpoch > 0 &&
                source.Identity is { } identity &&
                string.Equals(identity.RunId, expectedRunId, StringComparison.Ordinal) &&
                string.Equals(identity.ResourceId, expectedResourceId, StringComparison.Ordinal) &&
                string.Equals(identity.ContainerId, expectedContainerId, StringComparison.Ordinal) &&
                identity.Generation > 0 &&
                source.RemoteEndpoint is { } remoteEndpoint &&
                IPEndPoint.TryParse(remoteEndpoint, out var endpoint) &&
                endpoint.Address.Equals(expectedAddress))
            .ToArray();
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

    private static FileInfo? GetFreshRecorderArchive(
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
            ? latest
            : null;
    }

    private static string DescribeRecorderEvidence(
        SslLogDecodedInputSummary summary,
        bool ready,
        string? sessionFailure)
    {
        var status = ready ? "fresh" : "missing or stale";
        var failure = summary.ReadFailure is null ? string.Empty : $" tail={summary.ReadFailure}";
        var readerFailure = sessionFailure is null ? string.Empty : $" reader={sessionFailure}";
        return $"Recorder decoded {status} records for current run: referee={summary.RefereeRecords}, " +
            $"vision-detection={summary.VisionDetectionRecords}, tracker={summary.VisionTrackerRecords}; " +
            $"decompressed-eof={summary.ReachedDecompressedEof}.{failure}{readerFailure}";
    }
}
