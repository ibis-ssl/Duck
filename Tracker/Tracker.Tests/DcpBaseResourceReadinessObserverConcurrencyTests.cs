using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tracker.Tests;

public sealed class DcpBaseResourceReadinessObserverConcurrencyTests
{
    [Fact]
    public async Task ConsumesNewResourceKeyWhilePreviousOwnershipInspectionIsPending()
    {
        using var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance,
            lifetime);
        var runId = "observer-concurrency-run";
        var stackId = "observer-concurrency-stack";
        var resources = new (IResource Resource, string Role)[]
        {
            (new ContainerResource("simulator", "example/simulator:fixed"), "simulator"),
            (new ContainerResource("game-controller", "example/controller:fixed"), "game-controller"),
            (new ContainerResource("cm4-sim", "example/cm4:fixed"), "cm4-sim"),
            (new ContainerResource("crane", "example/crane:fixed"), "crane"),
        };
        var registry = new DcpBaseResourceReadinessRegistry(
            resources.Select(item => (item.Resource, item.Role, runId)),
            stackId);
        var eventConsumer = new DcpBaseResourceReadinessEventConsumer(registry);
        var runner = new ObserverDockerRunner(runId, stackId);
        var ownershipConsumer = new DcpBaseResourceOwnershipEventConsumer(
            registry,
            runner,
            "desktop-linux",
            "expected-engine");
        var roles = new Dictionary<IResource, string>(ReferenceEqualityComparer.Instance);
        foreach (var (resource, role) in resources)
        {
            roles.Add(resource, role);
        }
        using var observer = new DcpBaseResourceReadinessObserver(notifications, eventConsumer, ownershipConsumer, roles);

        await observer.StartAsync(CancellationToken.None);
        try
        {
            foreach (var (resource, role) in resources)
            {
                await notifications.PublishUpdateAsync(resource, $"{role}-key-a", _ => Snapshot("Running", ContainerId(role)));
            }

            await runner.FirstInspectStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await notifications.PublishUpdateAsync(resources[0].Resource, "simulator-key-b", _ => Snapshot("Running", new string('f', 64)));

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            var newKeyObserved = false;
            while (DateTime.UtcNow < deadline)
            {
                var candidate = registry.GetCandidates().Single(item => item.Resource == resources[0].Resource);
                if (candidate.ResourceKeyAmbiguous)
                {
                    newKeyObserved = true;
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }

            Assert.True(newKeyObserved, "The observer did not consume the new resource key while the prior Docker inspect was pending.");
            var heldCandidate = registry.GetCandidates().Single(item => item.Resource == resources[0].Resource);
            Assert.True(heldCandidate.ResourceKeyAmbiguous);
            Assert.Null(heldCandidate.ContainerId);
            Assert.False(registry.Get(resources[0].Resource)!.OwnershipVerified);
        }
        finally
        {
            runner.ReleaseFirstInspect.TrySetResult();
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await observer.StopAsync(stopTimeout.Token);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task StartsOnlyTheOwnedCurrentCm4SessionAndReapsItAfterDcpStopAndExecExit(int failedStartAttempt)
    {
        using var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance,
            lifetime);
        var runId = "observer-cm4-session-run";
        var stackId = "observer-cm4-session-stack";
        var resources = new (IResource Resource, string Role)[]
        {
            (new ContainerResource("simulator", "example/simulator:fixed"), "simulator"),
            (new ContainerResource("game-controller", "example/controller:fixed"), "game-controller"),
            (new ContainerResource("cm4-sim", "example/cm4:fixed"), "cm4-sim"),
            (new ContainerResource("crane", "example/crane:fixed"), "crane"),
        };
        var registry = new DcpBaseResourceReadinessRegistry(
            resources.Select(item => (item.Resource, item.Role, runId)),
            stackId);
        var runner = new ObserverDockerRunner(runId, stackId);
        runner.FailForegroundStartAt = failedStartAttempt;
        var ownershipConsumer = new DcpBaseResourceOwnershipEventConsumer(
            registry,
            runner,
            "desktop-linux",
            "expected-engine");
        var cm4Resource = resources.Single(item => item.Role == "cm4-sim").Resource;
        var targetController = new OwnedCm4TransportTargetController(
            runner,
            "desktop-linux",
            "expected-engine",
            runId,
            GetCurrentOwnedPair);
        var sessions = new OwnedCm4TransportServerSessionManager(
            registry,
            runner,
            cm4Resource,
            "desktop-linux",
            "expected-engine",
            runId,
            targetController);
        var roles = new Dictionary<IResource, string>(ReferenceEqualityComparer.Instance);
        foreach (var (resource, role) in resources) roles.Add(resource, role);
        var observer = new DcpBaseResourceReadinessObserver(
            notifications,
            new DcpBaseResourceReadinessEventConsumer(registry),
            ownershipConsumer,
            roles,
            sessions);

        OwnedCm4TransportIdentityPair? GetCurrentOwnedPair()
        {
            var simulator = registry.GetCandidates().SingleOrDefault(item => item.Role == "simulator");
            var cm4 = registry.GetCandidates().SingleOrDefault(item => item.Role == "cm4-sim");
            if (!IsVerified(simulator) || !IsVerified(cm4)) return null;
            return new OwnedCm4TransportIdentityPair(ToIdentity(cm4!), ToIdentity(simulator!));

            OwnedCm4TransportIdentity ToIdentity(DcpBaseResourceIdentityCandidate candidate) => new(
                candidate.RunId,
                "expected-engine",
                candidate.ResourceName,
                candidate.ResourceId,
                candidate.ContainerId!,
                candidate.Generation);

            static bool IsVerified(DcpBaseResourceIdentityCandidate? candidate) =>
                candidate is { IdentityVerified: true, NetworkVerified: true, OwnershipVerified: true } &&
                !candidate.ResourceKeyAmbiguous &&
                candidate.State == "Running" &&
                OwnedContainerInspection.IsFullId(candidate.ContainerId);
        }

        await observer.StartAsync(CancellationToken.None);
        var observerStopped = false;
        try
        {
            foreach (var (resource, role) in resources)
            {
                await notifications.PublishUpdateAsync(resource, $"{role}-key-a", _ => Snapshot("Running", ContainerId(role)));
            }
            await runner.FirstInspectStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            runner.ReleaseFirstInspect.TrySetResult();
            await WaitUntilAsync(() => registry.GetCandidates().All(item => item.OwnershipVerified));
            await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Running", ContainerId("cm4-sim")));

            if (failedStartAttempt == 1)
            {
                await runner.ForegroundStartAttempted.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.Empty(runner.ForegroundSessions);
                Assert.Equal(0, runner.StdioCalls);
                Assert.True(registry.Get(cm4Resource)!.OwnershipVerified);
                return;
            }

            try
            {
                await runner.ForegroundSessionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch (TimeoutException exception)
            {
                var candidates = string.Join("; ", registry.GetCandidates().Select(item =>
                    $"{item.Role}:{item.State}:{item.ContainerId}:{item.IdentityVerified}/{item.NetworkVerified}/{item.OwnershipVerified}:{item.ObservedNetworkAddress}"));
                var ownership = await ownershipConsumer.VerifyCurrentAsync(
                    resourceId => notifications.TryGetCurrentState(resourceId, out var state) ? state : null,
                    CancellationToken.None);
                throw new Xunit.Sdk.XunitException($"No owned CM4 session started. Inspects={runner.InspectCount}; ownership={ownership.Disposition}:{ownership.Reason}; candidates={candidates}; cause={exception.Message}");
            }
            var firstSession = Assert.Single(runner.ForegroundSessions);
            Assert.Equal(new string('3', 64), firstSession.Arguments[firstSession.Arguments.IndexOf("--cm4-container-id") + 1]);
            var firstGeneration = long.Parse(firstSession.Arguments[firstSession.Arguments.IndexOf("--cm4-generation") + 1], System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(firstGeneration > 0);
            Assert.Contains("--server", firstSession.Arguments);
            await runner.TargetApplied.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await WaitUntilAsync(() => targetController.Snapshot().Phase == Cm4TransportTargetPhase.Applied);
            Assert.Equal(Cm4TransportTargetPhase.Applied, targetController.Snapshot().Phase);
            Assert.Equal(1, runner.StdioCalls);
            Assert.False(registry.Get(cm4Resource)!.IsReady);

            var releaseSignals = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var terminalNotification = Task.Run(async () =>
            {
                await releaseSignals.Task.ConfigureAwait(false);
                await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Stopped", ContainerId("cm4-sim")));
            });
            var childCompletion = Task.Run(async () =>
            {
                await releaseSignals.Task.ConfigureAwait(false);
                firstSession.Session.CompleteExit();
            });
            releaseSignals.TrySetResult();
            await Task.WhenAll(terminalNotification, childCompletion);
            await notifications.PublishUpdateAsync(resources[0].Resource, "simulator-key-a", _ => Snapshot("Running", ContainerId("simulator")));
            await firstSession.Session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Running", new string('5', 64)));

            if (failedStartAttempt == 2)
            {
                await WaitUntilAsync(() => runner.ForegroundStartAttempts == 2);
                Assert.Single(runner.ForegroundSessions);
                Assert.Equal(1, runner.StdioCalls);
                Assert.Equal(Cm4TransportTargetPhase.Initial, targetController.Snapshot().Phase);
                return;
            }

            await runner.ForegroundSessionCountReached(2).WaitAsync(TimeSpan.FromSeconds(3));
            var replacementSession = runner.ForegroundSessions[1];
            Assert.Equal(new string('5', 64), replacementSession.Arguments[replacementSession.Arguments.IndexOf("--cm4-container-id") + 1]);
            Assert.True(long.Parse(replacementSession.Arguments[replacementSession.Arguments.IndexOf("--cm4-generation") + 1], System.Globalization.CultureInfo.InvariantCulture) > firstGeneration);
            await runner.SecondTargetApplied.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(2, runner.StdioCalls);
            Assert.Equal(Cm4TransportTargetPhase.Applied, targetController.Snapshot().Phase);
            Assert.Equal(new string('5', 64), targetController.Snapshot().Cm4ContainerId);

            await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Stopped", new string('5', 64)));
            await WaitUntilAsync(() => registry.GetCandidates().Single(item => ReferenceEquals(item.Resource, cm4Resource)).State == "Stopped");
            replacementSession.Session.CompleteExit();
            using (var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                await observer.StopAsync(stopTimeout.Token);
            }
            observerStopped = true;
            await replacementSession.Session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        finally
        {
            if (!observerStopped)
            {
                using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await observer.StopAsync(stopTimeout.Token);
            }
        }
    }

    [Fact]
    public async Task DoesNotStartReplacementWhilePriorOwnedExecSessionHasNotExited()
    {
        using var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance,
            lifetime);
        var runId = "observer-cm4-session-run";
        var stackId = "observer-cm4-session-stack";
        var resources = new (IResource Resource, string Role)[]
        {
            (new ContainerResource("simulator", "example/simulator:fixed"), "simulator"),
            (new ContainerResource("game-controller", "example/controller:fixed"), "game-controller"),
            (new ContainerResource("cm4-sim", "example/cm4:fixed"), "cm4-sim"),
            (new ContainerResource("crane", "example/crane:fixed"), "crane"),
        };
        var registry = new DcpBaseResourceReadinessRegistry(
            resources.Select(item => (item.Resource, item.Role, runId)),
            stackId);
        var runner = new ObserverDockerRunner(runId, stackId);
        var ownershipConsumer = new DcpBaseResourceOwnershipEventConsumer(registry, runner, "desktop-linux", "expected-engine");
        var cm4Resource = resources.Single(item => item.Role == "cm4-sim").Resource;
        var sessions = new OwnedCm4TransportServerSessionManager(registry, runner, cm4Resource, "desktop-linux", "expected-engine", runId);
        var roles = new Dictionary<IResource, string>(ReferenceEqualityComparer.Instance);
        foreach (var (resource, role) in resources) roles.Add(resource, role);
        var observer = new DcpBaseResourceReadinessObserver(
            notifications,
            new DcpBaseResourceReadinessEventConsumer(registry),
            ownershipConsumer,
            roles,
            sessions);

        await observer.StartAsync(CancellationToken.None);
        var observerStopped = false;
        try
        {
            foreach (var (resource, role) in resources)
            {
                await notifications.PublishUpdateAsync(resource, $"{role}-key-a", _ => Snapshot("Running", ContainerId(role)));
            }
            await runner.FirstInspectStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            runner.ReleaseFirstInspect.TrySetResult();
            await WaitUntilAsync(() => registry.GetCandidates().All(item => item.OwnershipVerified));
            await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Running", ContainerId("cm4-sim")));
            try
            {
                await runner.ForegroundSessionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch (TimeoutException exception)
            {
                var candidates = string.Join("; ", registry.GetCandidates().Select(item =>
                    $"{item.Role}:{item.State}:{item.ContainerId}:{item.IdentityVerified}/{item.NetworkVerified}/{item.OwnershipVerified}:{item.ObservedNetworkAddress}"));
                var ownership = await ownershipConsumer.VerifyCurrentAsync(
                    resourceId => notifications.TryGetCurrentState(resourceId, out var state) ? state : null,
                    CancellationToken.None);
                throw new Xunit.Sdk.XunitException($"No owned CM4 session started. Inspects={runner.InspectCount}; ownership={ownership.Disposition}:{ownership.Reason}; candidates={candidates}; cause={exception.Message}");
            }

            await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Stopped", ContainerId("cm4-sim")));
            await WaitUntilAsync(() => registry.GetCandidates().Single(item => ReferenceEquals(item.Resource, cm4Resource)).State == "Stopped");
            await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Running", new string('5', 64)));
            await notifications.PublishUpdateAsync(resources[0].Resource, "simulator-key-a", _ => Snapshot("Running", ContainerId("simulator")));

            await WaitUntilAsync(() =>
            {
                var current = registry.GetCandidates().Single(item => ReferenceEquals(item.Resource, cm4Resource));
                return current.ContainerId == new string('5', 64) && current.OwnershipVerified;
            });
            Assert.Single(runner.ForegroundSessions);
            Assert.False(runner.ForegroundSessions[0].Session.HasExited);

            runner.ForegroundSessions[0].Session.CompleteExit();
            await notifications.PublishUpdateAsync(resources[0].Resource, "simulator-key-a", _ => Snapshot("Running", ContainerId("simulator")));
            try
            {
                await runner.ForegroundSessionCountReached(2).WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch (TimeoutException exception)
            {
                var current = registry.GetCandidates().Single(item => ReferenceEquals(item.Resource, cm4Resource));
                throw new Xunit.Sdk.XunitException($"Replacement session did not start. oldExited={runner.ForegroundSessions[0].Session.HasExited}; oldDisposed={runner.ForegroundSessions[0].Session.Disposed.Task.IsCompleted}; candidate={current.State}:{current.ContainerId}:{current.IdentityVerified}/{current.OwnershipVerified}; observer={observer.ExecuteTask?.Status}; cause={exception.Message}");
            }
            using (var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                await observer.StopAsync(stopTimeout.Token);
            }
            observerStopped = true;
            Assert.False(runner.ForegroundSessions[1].Session.HasExited);
            Assert.False(runner.ForegroundSessions[1].Session.Disposed.Task.IsCompleted);
            runner.ForegroundSessions[1].Session.CompleteExit();
        }
        finally
        {
            if (!observerStopped)
            {
                using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await observer.StopAsync(stopTimeout.Token);
            }
        }
    }

    [Fact]
    public async Task DoesNotTreatDifferentCurrentContainerAsTheStoppedOwnedSession()
    {
        using var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance,
            lifetime);
        var runId = "observer-cm4-identity-replacement-run";
        var stackId = "observer-cm4-identity-replacement-stack";
        var resources = new (IResource Resource, string Role)[]
        {
            (new ContainerResource("simulator", "example/simulator:fixed"), "simulator"),
            (new ContainerResource("game-controller", "example/controller:fixed"), "game-controller"),
            (new ContainerResource("cm4-sim", "example/cm4:fixed"), "cm4-sim"),
            (new ContainerResource("crane", "example/crane:fixed"), "crane"),
        };
        var registry = new DcpBaseResourceReadinessRegistry(
            resources.Select(item => (item.Resource, item.Role, runId)),
            stackId);
        var runner = new ObserverDockerRunner(runId, stackId);
        var ownershipConsumer = new DcpBaseResourceOwnershipEventConsumer(registry, runner, "desktop-linux", "expected-engine");
        var cm4Resource = resources.Single(item => item.Role == "cm4-sim").Resource;
        var sessions = new OwnedCm4TransportServerSessionManager(registry, runner, cm4Resource, "desktop-linux", "expected-engine", runId);
        var roles = new Dictionary<IResource, string>(ReferenceEqualityComparer.Instance);
        foreach (var (resource, role) in resources) roles.Add(resource, role);
        var observer = new DcpBaseResourceReadinessObserver(
            notifications,
            new DcpBaseResourceReadinessEventConsumer(registry),
            ownershipConsumer,
            roles,
            sessions);

        await observer.StartAsync(CancellationToken.None);
        try
        {
            foreach (var (resource, role) in resources)
            {
                await notifications.PublishUpdateAsync(resource, $"{role}-key-a", _ => Snapshot("Running", ContainerId(role)));
            }
            await runner.FirstInspectStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            runner.ReleaseFirstInspect.TrySetResult();
            await WaitUntilAsync(() => registry.GetCandidates().All(item => item.OwnershipVerified));
            await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Running", ContainerId("cm4-sim")));
            await runner.ForegroundSessionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var original = Assert.Single(runner.ForegroundSessions);
            var originalId = new string('3', 64);

            var replacedId = new string('5', 64);
            await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Stopped", replacedId));
            Assert.True(notifications.TryGetCurrentState("cm4-sim-key-a", out var terminalNotification));
            Assert.Equal(replacedId, OwnedResourceObservationProjection.TryProject(terminalNotification!)?.ContainerId);
            await WaitUntilAsync(() =>
            {
                var current = registry.GetCandidates().Single(item => ReferenceEquals(item.Resource, cm4Resource));
                return current.ResourceId == "cm4-sim-key-a" && current.ContainerId is null && current.State == "Stopped";
            });
            original.Session.CompleteExit();
            await notifications.PublishUpdateAsync(cm4Resource, "cm4-sim-key-a", _ => Snapshot("Running", new string('6', 64)));
            await WaitUntilAsync(() =>
            {
                var current = registry.GetCandidates().Single(item => ReferenceEquals(item.Resource, cm4Resource));
                return current.ContainerId == new string('6', 64) && current.State == "Running" && current.OwnershipVerified;
            });

            Assert.Equal(originalId, original.Arguments[original.Arguments.IndexOf("--cm4-container-id") + 1]);
            Assert.Single(runner.ForegroundSessions);
            Assert.False(original.Session.Disposed.Task.IsCompleted);
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await observer.StopAsync(stopTimeout.Token);
        }
    }

    private static string ContainerId(string role) => role switch
    {
        "simulator" => new string('1', 64),
        "game-controller" => new string('2', 64),
        "cm4-sim" => new string('3', 64),
        _ => new string('4', 64),
    };

    private static CustomResourceSnapshot Snapshot(string state, string? containerId) => new()
    {
        ResourceType = "container",
        State = new ResourceStateSnapshot(state, "fixture"),
        Properties = containerId is null ? [] : [new ResourcePropertySnapshot("container.id", containerId)],
    };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        Assert.True(condition(), "The expected DCP ownership state was not observed before the test deadline.");
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();
        public CancellationToken ApplicationStarted => started.Token;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => stopped.Token;
        public void StopApplication() => stopping.Cancel();
        public void Dispose()
        {
            started.Dispose();
            stopping.Dispose();
            stopped.Dispose();
        }
    }

    private sealed class ObserverDockerRunner(string runId, string stackId) : IDockerStdioCommandRunner, IDockerCliForegroundSessionRunner
    {
        private int inspectCount;
        private int foregroundStartAttempts;

        internal TaskCompletionSource FirstInspectStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseFirstInspect { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ForegroundStartAttempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ForegroundSessionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource TargetApplied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondTargetApplied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal List<(string[] Arguments, ControlledForegroundSession Session)> ForegroundSessions { get; } = [];
        private int stdioCalls;
        internal int StdioCalls => Volatile.Read(ref stdioCalls);
        internal int InspectCount => Volatile.Read(ref inspectCount);
        internal int ForegroundStartAttempts => Volatile.Read(ref foregroundStartAttempts);
        internal int FailForegroundStartAt { get; set; }

        internal Task ForegroundSessionCountReached(int count) => Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (ForegroundSessions.Count >= count) return;
                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }
            throw new TimeoutException($"Expected {count} foreground sessions.");
        });

        public IDockerCliForegroundSession StartForegroundSession(IReadOnlyList<string> arguments)
        {
            var attempt = Interlocked.Increment(ref foregroundStartAttempts);
            ForegroundStartAttempted.TrySetResult();
            if (attempt == FailForegroundStartAt)
            {
                throw new IOException("Controlled foreground start failure.");
            }

            var session = new ControlledForegroundSession();
            lock (ForegroundSessions)
            {
                ForegroundSessions.Add((arguments.ToArray(), session));
                ForegroundSessionStarted.TrySetResult();
            }
            return session;
        }

        public async Task<DockerStdioCommandResult> CaptureBoundedAsync(
            IReadOnlyList<string> arguments,
            int maximumOutputCharacters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (arguments.Contains("info", StringComparer.Ordinal))
            {
                return new DockerStdioCommandResult(0, "expected-engine", string.Empty);
            }

            var formatIndex = Array.IndexOf(arguments.ToArray(), "--format");
            if (formatIndex >= 0 && formatIndex + 1 < arguments.Count &&
                arguments[formatIndex + 1].Contains("{{.Id}}", StringComparison.Ordinal))
            {
                var inspectedId = arguments[^1];
                var inspectedRole = inspectedId[0] switch
                {
                    '1' => "simulator",
                    '2' => "game-controller",
                    '3' or '5' or '6' => "cm4-sim",
                    _ => "crane",
                };
                var address = inspectedRole == "simulator" ? "172.28.0.11" : "172.28.0.10";
                return new DockerStdioCommandResult(
                    0,
                    string.Join('\n',
                        inspectedId,
                        "running",
                        "aspire-default-network",
                        runId,
                        inspectedRole,
                        $"aspire-default-network\t{new string('a', 64)}\t{address}"),
                    string.Empty);
            }

            var currentInspect = Interlocked.Increment(ref inspectCount);
            if (currentInspect == 1)
            {
                FirstInspectStarted.TrySetResult();
                await ReleaseFirstInspect.Task.WaitAsync(cancellationToken);
            }

            var containerId = arguments[^1];
            var role = containerId[0] switch
            {
                '1' => "simulator",
                '2' => "game-controller",
                '3' => "cm4-sim",
                '5' => "cm4-sim",
                '6' => "cm4-sim",
                _ => "crane",
            };
            return new DockerStdioCommandResult(0, InspectJson(containerId, role, runId, stackId), string.Empty);
        }

        public Task<DockerStdioCommandResult> ExecuteWithStdioAsync(
            IReadOnlyList<string> arguments,
            string standardInput,
            int maximumOutputCharacters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var callNumber = Interlocked.Increment(ref stdioCalls);
            var request = JsonSerializer.Deserialize<Cm4TransportTargetMessage>(
                standardInput.TrimEnd('\r', '\n'),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (request?.Type == "applyTarget")
            {
                if (callNumber == 1) TargetApplied.TrySetResult();
                if (callNumber == 2) SecondTargetApplied.TrySetResult();
            }
            var acknowledgement = request is null
                ? string.Empty
                : JsonSerializer.Serialize(request with { Type = "applied" }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
            return Task.FromResult(new DockerStdioCommandResult(0, acknowledgement, string.Empty));
        }

        public Task<string> CaptureAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ContainerIdentityControlExecResult> RelayAsync(
            IReadOnlyList<string> arguments,
            ResourceDiagnosticIdentity identity,
            string roleName,
            string? expectedBootId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        internal sealed class ControlledForegroundSession : IDockerCliForegroundSession
        {
            private readonly TaskCompletionSource<int> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool HasExited => completion.Task.IsCompleted;
            public Task<int> Completion => completion.Task;
            internal void CompleteExit(int exitCode = 0) => completion.TrySetResult(exitCode);
            public ValueTask DisposeAsync()
            {
                if (!HasExited) throw new InvalidOperationException("The owned foreground process has not exited.");
                Disposed.TrySetResult();
                return ValueTask.CompletedTask;
            }
        }

        private static string InspectJson(string containerId, string role, string runId, string stackId) =>
            JsonSerializer.Serialize(new
            {
                Id = containerId,
                State = new { Status = "running" },
                Config = new
                {
                    Labels = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ContainerIdentityExecHandoff.RunIdLabel] = runId,
                        [ContainerIdentityExecHandoff.RoleLabel] = role,
                        [DockerContainerSpec.StackLabel] = stackId,
                        [DockerContainerSpec.ResourceLabel] = role,
                        [DockerContainerSpec.RunLabel] = runId,
                    },
                },
                HostConfig = new { NetworkMode = "aspire-default-network" },
                NetworkSettings = new
                {
                    Networks = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["aspire-default-network"] = new
                        {
                            IPAddress = role == "simulator" ? "172.28.0.11" : "172.28.0.10",
                            NetworkID = new string('a', 64),
                        },
                    },
                },
            });
    }
}
