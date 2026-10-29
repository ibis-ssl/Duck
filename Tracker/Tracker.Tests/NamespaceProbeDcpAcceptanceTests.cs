using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Tracker.Tests;

public sealed class NamespaceProbeDcpAcceptanceTests(ITestOutputHelper output)
{
    private static readonly TimeSpan ProbeDeadline = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OperationBudget = TimeSpan.FromSeconds(45);
    private const string ExpectedContext = "desktop-linux";
    internal const string GateVariableName = "DUCK_I81_NAMESPACE_DCP_PROBE_ENABLED";
    private const string EngineVariable = "DUCK_I81_NAMESPACE_DCP_EXPECTED_ENGINE_ID";
    private const string RunIdVariable = "DUCK_I81_NAMESPACE_DCP_RUN_ID";

    [Theory]
    [InlineData(null)]
    public void MissingNativeProbeGateDoesNotEnableTheFixture(string? value)
    {
        Assert.False(NamespaceProbeTestSettings.Parse(value, null, null).Enabled);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("enabled-ish")]
    public void NativeProbeGateRejectsPresentValuesOtherThanTrue(string value)
    {
        Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeTestSettings.Parse(value, null, null));
    }

    [Theory]
    [InlineData(null, "run_01")]
    [InlineData("engine", null)]
    [InlineData("engine", "bad/run")]
    public void EnabledNativeProbeRequiresAnExactTargetAndSafeRunId(
        string? engineId,
        string? runId)
    {
        Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeTestSettings.Parse("true", engineId, runId));
    }

    [Fact]
    public void EnabledNativeProbeAcceptsOnlyTheApprovedContextAndExplicitIdentity()
    {
        var settings = NamespaceProbeTestSettings.Parse("true", "expected-engine", "run_01");

        Assert.True(settings.Enabled);
        Assert.Equal(ExpectedContext, settings.Context);
        Assert.Equal("expected-engine", settings.ExpectedEngineId);
        Assert.Equal("run_01", settings.RunId);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(44, 16)]
    [InlineData(45, 15)]
    [InlineData(60, 0)]
    public void CleanupBudgetReservesOnlyTheRemainingProbeDeadline(int elapsedSeconds, int expectedSeconds)
    {
        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            NamespaceProbeCleanupBudget.Remaining(TimeSpan.FromSeconds(elapsedSeconds)));
    }

    [Fact]
    public async Task ReadOnlyPreflightRejectsAnEffectiveEngineMismatchBeforeInspectingImagesOrContainers()
    {
        var docker = new FakeReadOnlyDockerCli(
            (true, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["info", "--format", "{{.ID}}"], "other-engine"),
            (false, ["context", "show"], "desktop-linux"));
        var settings = NamespaceProbeTestSettings.Parse("true", "expected-engine", "run_01");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VerifyReadOnlyPreflightAsync(docker, settings, null, null, CancellationToken.None));

        Assert.DoesNotContain(docker.Calls, call => call.Arguments.Contains("image", StringComparer.Ordinal));
        Assert.DoesNotContain(docker.Calls, call => call.Arguments.Contains("ps", StringComparer.Ordinal));
        Assert.All(docker.Calls, call => Assert.True(FakeReadOnlyDockerCli.IsReadOnly(call.Arguments)));
    }

    [Fact]
    public async Task ReadOnlyPreflightRejectsMissingPinnedCacheBeforeCheckingRunIdCollision()
    {
        var docker = new FakeReadOnlyDockerCli(
            (true, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["context", "show"], "desktop-linux"),
            (true, ["image", "inspect", "--format", "{{json .RepoDigests}}", ProbeTestArgument.Any], ""));
        var settings = NamespaceProbeTestSettings.Parse("true", "expected-engine", "run_01");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VerifyReadOnlyPreflightAsync(docker, settings, null, null, CancellationToken.None));

        Assert.DoesNotContain(docker.Calls, call => call.Arguments.Contains("ps", StringComparer.Ordinal));
        Assert.All(docker.Calls, call => Assert.True(FakeReadOnlyDockerCli.IsReadOnly(call.Arguments)));
    }

    [Fact]
    public async Task ReadOnlyPreflightRejectsRunIdCollisionWithoutAnyMutationCommand()
    {
        var docker = new FakeReadOnlyDockerCli(
            (true, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["context", "show"], "desktop-linux"),
            (true, ["image", "inspect", "--format", "{{json .RepoDigests}}", ProbeTestArgument.Any], $"[{System.Text.Json.JsonSerializer.Serialize($"{NamespaceProbeModelFactory.AspNetImage}@sha256:{NamespaceProbeModelFactory.AspNetImageSha256}")}]"),
            (true, NamespaceProbeCleanupInventory.BuildRunIdInventoryArguments("run_01"), "existing-id"));
        var settings = NamespaceProbeTestSettings.Parse("true", "expected-engine", "run_01");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VerifyReadOnlyPreflightAsync(docker, settings, null, null, CancellationToken.None));

        Assert.All(docker.Calls, call => Assert.True(FakeReadOnlyDockerCli.IsReadOnly(call.Arguments)));
    }

    [Theory]
    [InlineData("unix:///somewhere", null)]
    [InlineData(null, "default")]
    public void DockerEnvironmentOverridesAreRejectedWithoutChangingTheEnvironment(string? dockerHost, string? dockerContext)
    {
        Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeDockerSelection.ValidateOverrides(ExpectedContext, dockerHost, dockerContext));
    }

    [Fact]
    public async Task CleanupDoesNotStopOrDisposeWhenTheApprovedEngineCheckFails()
    {
        var stopCalls = 0;
        var disposeCalls = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var result = await NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
            _ => Task.FromException(new InvalidOperationException("wrong engine")),
            _ =>
            {
                stopCalls++;
                return Task.CompletedTask;
            },
            _ =>
            {
                disposeCalls++;
                return Task.CompletedTask;
            },
            timeout.Token);

        Assert.False(result.EngineVerified);
        Assert.IsType<InvalidOperationException>(result.EngineCheckFailure);
        Assert.Equal(0, stopCalls);
        Assert.Equal(0, disposeCalls);
    }

    [Fact]
    public async Task CleanupDeadlineExpiryDoesNotInvokeReadOrLifecycleCallbacks()
    {
        var verifyCalls = 0;
        var stopCalls = 0;
        var disposeCalls = 0;
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        var result = await NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
            _ =>
            {
                verifyCalls++;
                return Task.CompletedTask;
            },
            _ =>
            {
                stopCalls++;
                return Task.CompletedTask;
            },
            _ =>
            {
                disposeCalls++;
                return Task.CompletedTask;
            },
            timeout.Token);

        Assert.False(result.EngineVerified);
        Assert.IsAssignableFrom<OperationCanceledException>(result.EngineCheckFailure);
        Assert.Equal(0, verifyCalls);
        Assert.Equal(0, stopCalls);
        Assert.Equal(0, disposeCalls);
    }

    [Fact]
    public async Task NotificationMonitorKeepsOtherResourceEventsSeenBeforeTheRequestedOwner()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var owner = builder.AddContainer("monitor-owner", "example/image").Resource;
        var follower = builder.AddContainer("monitor-follower", "example/image").Resource;
        var followerId = new string('b', 64);
        var ownerId = new string('a', 64);
        var adapter = new SequenceOwnedReconfigurationAdapter(
        [
            new OwnedResourceObservation(follower, "follower-key", KnownResourceStates.Running, followerId),
            new OwnedResourceObservation(owner, "owner-key", KnownResourceStates.Running, ownerId),
        ]);
        await using var monitor = new NamespaceProbeNotificationMonitor(adapter);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var ownerObservation = await monitor.WaitForRunningAsync(owner, null, timeout.Token);
        var followerObservation = await monitor.WaitForRunningAsync(follower, null, timeout.Token);

        Assert.Equal(ownerId, ownerObservation.ContainerId);
        Assert.Equal(followerId, followerObservation.ContainerId);
    }

    [Fact]
    public async Task NotificationMonitorRejectsThePriorGenerationAndReturnsTheNextFullId()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var owner = builder.AddContainer("monitor-owner", "example/image").Resource;
        var previousId = new string('a', 64);
        var nextId = new string('b', 64);
        var adapter = new SequenceOwnedReconfigurationAdapter(
        [
            new OwnedResourceObservation(owner, "owner-key", KnownResourceStates.Running, previousId),
            new OwnedResourceObservation(owner, "owner-key", KnownResourceStates.Stopping, previousId),
            new OwnedResourceObservation(owner, "owner-next-key", KnownResourceStates.Running, nextId),
        ]);
        await using var monitor = new NamespaceProbeNotificationMonitor(adapter);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var observation = await monitor.WaitForRunningAsync(owner, previousId, timeout.Token);

        Assert.Equal(nextId, observation.ContainerId);
        Assert.Equal("owner-next-key", observation.ResourceId);
    }

    [Fact]
    public async Task NotificationMonitorRejectsAnUnexpectedOwnerGenerationBeforeTheFollowerRestartEvenWithinLimit()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var owner = builder.AddContainer("monitor-owner", "example/image").Resource;
        var follower = builder.AddContainer("monitor-follower", "example/image").Resource;
        var ownerInitialId = new string('a', 64);
        var sequence = new[]
        {
            new OwnedResourceObservation(owner, "owner-initial", KnownResourceStates.Running, ownerInitialId),
            new OwnedResourceObservation(follower, "follower-initial", KnownResourceStates.Running, new string('b', 64)),
            new OwnedResourceObservation(owner, "owner-approved-restart", KnownResourceStates.Running, new string('c', 64)),
            new OwnedResourceObservation(owner, "owner-unexpected-extra", KnownResourceStates.Running, new string('d', 64)),
        };
        var inner = new SequenceOwnedReconfigurationAdapter(sequence);
        var gated = new GatedOwnedReconfigurationAdapter(inner, eventCount: sequence.Length);
        await using var monitor = new NamespaceProbeNotificationMonitor(gated, plannedRecreationOrder: [owner, follower]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await gated.WaitUntilQueuedAsync(0, timeout.Token);
        gated.Continue();
        await gated.WaitUntilQueuedAsync(1, timeout.Token);

        var ownerRestartCalls = 0;
        await monitor.ExecuteLifecycleCommandWithinGenerationLimitAsync(
            owner,
            KnownResourceCommands.RestartCommand,
            _ =>
            {
                ownerRestartCalls++;
                gated.Continue();
                return Task.CompletedTask;
            },
            timeout.Token);
        await gated.WaitUntilQueuedAsync(2, timeout.Token);
        _ = await monitor.WaitForRunningAsync(owner, ownerInitialId, timeout.Token);
        gated.Continue();
        await gated.WaitUntilQueuedAsync(3, timeout.Token);

        var followerRestartCalls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            monitor.ExecuteLifecycleCommandWithinGenerationLimitAsync(
                follower,
                KnownResourceCommands.StartCommand,
                _ =>
                {
                    followerRestartCalls++;
                    return Task.CompletedTask;
                },
                timeout.Token));

        Assert.Equal(1, ownerRestartCalls);
        Assert.Equal(4, monitor.ObservedGenerations.Select(observation => observation.ContainerId).Distinct().Count());
        Assert.Equal(0, followerRestartCalls);
    }

    [Fact]
    public async Task NotificationMonitorRejectsAnotherOwnerRestartAtThreePlannedGenerations()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var owner = builder.AddContainer("monitor-owner", "example/image").Resource;
        var follower = builder.AddContainer("monitor-follower", "example/image").Resource;
        var ownerInitialId = new string('a', 64);
        var sequence = new[]
        {
            new OwnedResourceObservation(owner, "owner-initial", KnownResourceStates.Running, ownerInitialId),
            new OwnedResourceObservation(follower, "follower-initial", KnownResourceStates.Running, new string('b', 64)),
            new OwnedResourceObservation(owner, "owner-approved-restart", KnownResourceStates.Running, new string('c', 64)),
        };
        var inner = new SequenceOwnedReconfigurationAdapter(sequence);
        var gated = new GatedOwnedReconfigurationAdapter(inner, eventCount: sequence.Length);
        await using var monitor = new NamespaceProbeNotificationMonitor(gated, plannedRecreationOrder: [owner, follower]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await gated.WaitUntilQueuedAsync(0, timeout.Token);
        gated.Continue();
        await gated.WaitUntilQueuedAsync(1, timeout.Token);

        var ownerRestartCalls = 0;
        await monitor.ExecuteLifecycleCommandWithinGenerationLimitAsync(
            owner,
            KnownResourceCommands.RestartCommand,
            _ =>
            {
                ownerRestartCalls++;
                gated.Continue();
                return Task.CompletedTask;
            },
            timeout.Token);
        await gated.WaitUntilQueuedAsync(2, timeout.Token);
        _ = await monitor.WaitForRunningAsync(owner, ownerInitialId, timeout.Token);

        var unexpectedOwnerRestartCalls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            monitor.ExecuteLifecycleCommandWithinGenerationLimitAsync(
                owner,
                KnownResourceCommands.RestartCommand,
                _ =>
                {
                    unexpectedOwnerRestartCalls++;
                    return Task.CompletedTask;
                },
                timeout.Token));

        Assert.Equal(1, ownerRestartCalls);
        Assert.Equal(3, monitor.ObservedGenerations.Select(observation => observation.ContainerId).Distinct().Count());
        Assert.Equal(0, unexpectedOwnerRestartCalls);
    }

    [Fact]
    public async Task NotificationMonitorAllowsThePlannedOwnerThenFollowerRecreationOrder()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var owner = builder.AddContainer("monitor-owner", "example/image").Resource;
        var follower = builder.AddContainer("monitor-follower", "example/image").Resource;
        var ownerInitialId = new string('a', 64);
        var followerInitialId = new string('b', 64);
        var ownerNextId = new string('c', 64);
        var followerNextId = new string('d', 64);
        var sequence = new[]
        {
            new OwnedResourceObservation(owner, "owner-initial", KnownResourceStates.Running, ownerInitialId),
            new OwnedResourceObservation(follower, "follower-initial", KnownResourceStates.Running, followerInitialId),
            new OwnedResourceObservation(owner, "owner-recreated", KnownResourceStates.Running, ownerNextId),
            new OwnedResourceObservation(follower, "follower-recreated", KnownResourceStates.Running, followerNextId),
        };
        var inner = new SequenceOwnedReconfigurationAdapter(sequence);
        var gated = new GatedOwnedReconfigurationAdapter(inner, eventCount: sequence.Length);
        await using var monitor = new NamespaceProbeNotificationMonitor(gated, plannedRecreationOrder: [owner, follower]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await gated.WaitUntilQueuedAsync(0, timeout.Token);
        gated.Continue();
        await gated.WaitUntilQueuedAsync(1, timeout.Token);

        var followerStopCalls = 0;
        await monitor.ExecuteLifecycleCommandWithinGenerationLimitAsync(
            follower,
            KnownResourceCommands.StopCommand,
            _ =>
            {
                followerStopCalls++;
                return Task.CompletedTask;
            },
            timeout.Token);

        var ownerRestartCalls = 0;
        await monitor.ExecuteLifecycleCommandWithinGenerationLimitAsync(
            owner,
            KnownResourceCommands.RestartCommand,
            _ =>
            {
                ownerRestartCalls++;
                gated.Continue();
                return Task.CompletedTask;
            },
            timeout.Token);
        _ = await monitor.WaitForRunningAsync(owner, ownerInitialId, timeout.Token);

        var followerStartCalls = 0;
        await monitor.ExecuteLifecycleCommandWithinGenerationLimitAsync(
            follower,
            KnownResourceCommands.StartCommand,
            _ =>
            {
                followerStartCalls++;
                gated.Continue();
                return Task.CompletedTask;
            },
            timeout.Token);
        _ = await monitor.WaitForRunningAsync(follower, followerInitialId, timeout.Token);

        Assert.Contains(monitor.ObservedGenerations, observation => observation.ContainerId == ownerNextId);
        Assert.Contains(monitor.ObservedGenerations, observation => observation.ContainerId == followerNextId);
        Assert.Equal(4, monitor.ObservedGenerations.Select(observation => observation.ContainerId).Distinct().Count());
        Assert.Equal(1, followerStopCalls);
        Assert.Equal(1, ownerRestartCalls);
        Assert.Equal(1, followerStartCalls);
    }

    [Fact]
    public async Task NotificationMonitorRecordsAllGenerationsAndBlocksAnotherCommandAfterTheFourGenerationLimit()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var owner = builder.AddContainer("monitor-owner", "example/image").Resource;
        var sequence = Enumerable.Range(0, 5)
            .Select(index => new OwnedResourceObservation(
                owner,
                $"owner-key-{index}",
                KnownResourceStates.Running,
                index.ToString("x", System.Globalization.CultureInfo.InvariantCulture).PadLeft(64, '0')))
            .ToArray();
        var inner = new SequenceOwnedReconfigurationAdapter(sequence);
        var gated = new GatedOwnedReconfigurationAdapter(inner, eventCount: sequence.Length);
        await using var monitor = new NamespaceProbeNotificationMonitor(gated);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        for (var index = 0; index < sequence.Length; index++)
        {
            await gated.WaitUntilQueuedAsync(index, timeout.Token);
            gated.Continue();
        }

        var commandCalls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            monitor.ExecuteLifecycleCommandWithinGenerationLimitAsync(
                _ =>
                {
                    commandCalls++;
                    return Task.CompletedTask;
                },
                timeout.Token));

        Assert.Equal(5, monitor.ObservedGenerations.Count);
        Assert.Equal(5, monitor.ObservedGenerations.Select(observation => observation.ContainerId).Distinct().Count());
        Assert.Equal(0, commandCalls);
    }

    [Fact]
    public async Task NotificationMonitorRejectsQueuedRunningObservationWhenAspireCurrentIsStopped()
    {
        var resource = new ContainerResource("monitor-current-owner");
        using var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance, lifetime);
        var adapter = new AspireOwnedReconfigurationAdapter(notifications, null!, null!, string.Empty);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var containerId = new string('a', 64);

        await notifications.PublishUpdateAsync(resource, "owner-key",
            _ => Snapshot(KnownResourceStates.Running, containerId));
        var oldRunning = Assert.IsType<OwnedResourceObservation>(adapter.GetCurrent(resource));
        await notifications.PublishUpdateAsync(resource, "owner-key",
            _ => Snapshot(KnownResourceStates.Stopping, containerId));
        var currentStopped = Assert.IsType<OwnedResourceObservation>(adapter.GetCurrent(resource));
        var queuedAdapter = new QueuedCurrentOwnedReconfigurationAdapter(adapter, [oldRunning, currentStopped]);
        var gatedAdapter = new GatedOwnedReconfigurationAdapter(queuedAdapter, eventCount: 2);
        await using var monitor = new NamespaceProbeNotificationMonitor(gatedAdapter);

        await gatedAdapter.WaitUntilQueuedAsync(0, timeout.Token);
        gatedAdapter.Continue();
        await gatedAdapter.WaitUntilQueuedAsync(1, timeout.Token);
        gatedAdapter.Continue();

        var current = adapter.GetCurrent(resource);
        Assert.Equal(KnownResourceStates.Stopping, current?.State);
        Assert.Equal(containerId, current?.ContainerId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            monitor.WaitForRunningAsync(resource, null, timeout.Token));
    }

    [Fact]
    public async Task NotificationMonitorRejectsQueuedTerminalObservationWhenAspireCurrentIsNextGeneration()
    {
        var resource = new ContainerResource("monitor-current-owner");
        using var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance, lifetime);
        var adapter = new AspireOwnedReconfigurationAdapter(notifications, null!, null!, string.Empty);
        using var watchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var actualWatch = adapter.WatchAsync(watchTimeout.Token).GetAsyncEnumerator(watchTimeout.Token);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var oldId = new string('a', 64);
        var nextId = new string('b', 64);

        await notifications.PublishUpdateAsync(resource, "owner-key",
            _ => Snapshot(KnownResourceStates.Running, oldId));
        Assert.True(await actualWatch.MoveNextAsync());
        var oldRunning = Assert.IsType<OwnedResourceObservation>(adapter.GetCurrent(resource));
        await notifications.PublishUpdateAsync(resource, "owner-key",
            _ => Snapshot(KnownResourceStates.Finished, oldId));
        Assert.True(await actualWatch.MoveNextAsync());
        var oldStopped = Assert.IsType<OwnedResourceObservation>(adapter.GetCurrent(resource));
        await notifications.PublishUpdateAsync(resource, "owner-next-key",
            _ => Snapshot(KnownResourceStates.Running, nextId));
        Assert.True(await actualWatch.MoveNextAsync());
        var nextRunning = Assert.IsType<OwnedResourceObservation>(adapter.GetCurrent(resource));
        var queuedAdapter = new QueuedCurrentOwnedReconfigurationAdapter(adapter, [oldRunning, oldStopped, nextRunning]);
        var gatedAdapter = new GatedOwnedReconfigurationAdapter(queuedAdapter, eventCount: 3);
        await using var monitor = new NamespaceProbeNotificationMonitor(gatedAdapter);

        await gatedAdapter.WaitUntilQueuedAsync(0, timeout.Token);
        gatedAdapter.Continue();
        await gatedAdapter.WaitUntilQueuedAsync(1, timeout.Token);
        gatedAdapter.Continue();
        await gatedAdapter.WaitUntilQueuedAsync(2, timeout.Token);
        gatedAdapter.Continue();

        var current = adapter.GetCurrent(resource);
        Assert.Equal("owner-next-key", current?.ResourceId);
        Assert.Equal(KnownResourceStates.Running, current?.State);
        Assert.Equal(nextId, current?.ContainerId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            monitor.WaitForStoppedAsync(resource, oldId, timeout.Token));
    }

    [Fact]
    public async Task CleanupCancellationDuringStopReturnsFailureWithoutStartingDispose()
    {
        using var deadline = new CancellationTokenSource();
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeCalls = 0;

        var cleanup = NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
            _ => Task.CompletedTask,
            async token =>
            {
                stopStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
            _ =>
            {
                disposeCalls++;
                return Task.CompletedTask;
            },
            deadline.Token);

        await stopStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        deadline.Cancel();
        var result = await cleanup;

        Assert.True(result.EngineVerified);
        Assert.IsAssignableFrom<OperationCanceledException>(result.LifecycleFailure);
        Assert.Equal("stop", result.FailedStage);
        Assert.Equal(0, disposeCalls);
    }

    [Fact]
    public async Task CleanupChecksCancellationAfterStopCallbackCompletesBeforeStartingDispose()
    {
        using var deadline = new CancellationTokenSource();
        var disposeCalls = 0;

        var result = await NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
            _ => Task.CompletedTask,
            _ =>
            {
                deadline.Cancel();
                return Task.CompletedTask;
            },
            _ =>
            {
                disposeCalls++;
                return Task.CompletedTask;
            },
            deadline.Token);

        Assert.True(result.EngineVerified);
        Assert.IsAssignableFrom<OperationCanceledException>(result.LifecycleFailure);
        Assert.Equal("dispose", result.FailedStage);
        Assert.Equal(0, disposeCalls);
    }

    [Fact]
    public async Task CleanupStopFailureReturnsFailureWithoutStartingDispose()
    {
        var disposeCalls = 0;

        var result = await NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
            _ => Task.CompletedTask,
            _ => Task.FromException(new IOException("stop failed")),
            _ =>
            {
                disposeCalls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(result.EngineVerified);
        Assert.IsType<IOException>(result.LifecycleFailure);
        Assert.Equal("stop", result.FailedStage);
        Assert.Equal(0, disposeCalls);
    }

    [Fact]
    public async Task CleanupDisposeFailureIsReturnedAfterTheStopCompletes()
    {
        var stopCalls = 0;

        var result = await NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
            _ => Task.CompletedTask,
            _ =>
            {
                stopCalls++;
                return Task.CompletedTask;
            },
            _ => Task.FromException(new IOException("dispose failed")),
            CancellationToken.None);

        Assert.True(result.EngineVerified);
        Assert.IsType<IOException>(result.LifecycleFailure);
        Assert.Equal("dispose", result.FailedStage);
        Assert.Equal(1, stopCalls);
    }

    [Fact]
    public async Task OwnerPortBindingValidationRejectsAnyAdditionalPublishedPort()
    {
        var docker = new FakeReadOnlyDockerCli((true,
            ["inspect", "--format", "{{json .}}", "owner-id"],
            """{"HostConfig":{"PortBindings":{"39110/tcp":[{"HostIp":"127.0.0.1","HostPort":"40000"}],"1234/tcp":[{"HostIp":"0.0.0.0","HostPort":"1234"}]}}}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AssertOwnerEndpointIsLoopbackOnlyAsync(docker, "owner-id", CancellationToken.None));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"HostConfig":{"PortBindings":null}}""")]
    [InlineData("""{"HostConfig":{"PortBindings":[]}}""")]
    public async Task OwnerPortBindingValidationRejectsMissingOrUnknownShape(string inspectJson)
    {
        var docker = new FakeReadOnlyDockerCli((true,
            ["inspect", "--format", "{{json .}}", "owner-id"], inspectJson));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AssertOwnerEndpointIsLoopbackOnlyAsync(docker, "owner-id", CancellationToken.None));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"HostConfig":{"PortBindings":null}}""")]
    [InlineData("""{"HostConfig":{"PortBindings":[]}}""")]
    public async Task FollowerPortBindingValidationRejectsMissingOrUnknownShape(string inspectJson)
    {
        var docker = new FakeReadOnlyDockerCli((true,
            ["inspect", "--format", "{{json .}}", "follower-id"], inspectJson));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AssertFollowerHasNoPublishedHostPortsAsync(docker, "follower-id", CancellationToken.None));
    }

    [Fact]
    public async Task RunIdInventoryRequestsNonTruncatedContainerIds()
    {
        var docker = new FakeReadOnlyDockerCli(
            (true, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["context", "show"], "desktop-linux"),
            (true, ["image", "inspect", "--format", "{{json .RepoDigests}}", ProbeTestArgument.Any],
                $"[{System.Text.Json.JsonSerializer.Serialize($"{NamespaceProbeModelFactory.AspNetImage}@sha256:{NamespaceProbeModelFactory.AspNetImageSha256}")}]"),
            (true, NamespaceProbeCleanupInventory.BuildRunIdInventoryArguments("run_01"), "\r\n"));
        var settings = NamespaceProbeTestSettings.Parse("true", "expected-engine", "run_01");

        await VerifyReadOnlyPreflightAsync(docker, settings, null, null, CancellationToken.None);

        Assert.Contains("--no-trunc", docker.Calls.Last().Arguments);
    }

    [Fact]
    public void RemainingOwnedContainerCannotBeConfirmedWithoutAReadableState()
    {
        var id = new string('a', 64);
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            Id = id,
            Config = new
            {
                Labels = new Dictionary<string, string>
                {
                    [ContainerIdentityExecHandoff.RunIdLabel] = "run_01",
                    [ContainerIdentityExecHandoff.RoleLabel] = NamespaceProbeModelFactory.OwnerRole,
                },
            },
        });

        Assert.False(HasOwnedProbeLabels(json, id, "run_01"));
    }

    [Fact]
    public void CleanupInspectionRecordsFullIdRoleAndStateAndRejectsShortInventoryIds()
    {
        var id = new string('a', 64);
        var json = CreateInspectJson(id, "run_01", NamespaceProbeModelFactory.OwnerRole, "exited");

        var exact = NamespaceProbeCleanupInventory.ParseCandidate(id,
            new ReadOnlyDockerResult(0, json, string.Empty), "run_01");
        var truncated = NamespaceProbeCleanupInventory.ParseCandidate(id[..12],
            new ReadOnlyDockerResult(0, json, string.Empty), "run_01");

        Assert.Equal("owned", exact.Classification);
        Assert.Equal(id, exact.ContainerId);
        Assert.Equal(NamespaceProbeModelFactory.OwnerRole, exact.Role);
        Assert.Equal("exited", exact.State);
        Assert.Equal("truncated-inventory-id", truncated.Classification);
        Assert.Equal(id, truncated.ContainerId);
    }

    [Fact]
    public void CleanupInspectionDistinguishesForeignLabelsMissingObjectsAndInspectFailures()
    {
        var id = new string('b', 64);
        var foreign = NamespaceProbeCleanupInventory.ParseCandidate(id,
            new ReadOnlyDockerResult(0, CreateInspectJson(id, "other-run", NamespaceProbeModelFactory.OwnerRole, "running"), string.Empty),
            "run_01");
        var disappeared = NamespaceProbeCleanupInventory.ParseCandidate(id,
            new ReadOnlyDockerResult(1, string.Empty, "Error: No such object: " + id), "run_01");
        var failed = NamespaceProbeCleanupInventory.ParseCandidate(id,
            new ReadOnlyDockerResult(1, string.Empty, "permission denied"), "run_01");

        Assert.Equal("foreign-or-unowned", foreign.Classification);
        Assert.Equal("disappeared", disappeared.Classification);
        Assert.Equal("inspect-failed", failed.Classification);
    }

    [Fact]
    public async Task CleanupVerificationWaitsForAnExitedOwnedContainerToDisappear()
    {
        var id = new string('a', 64);
        var inventoryArguments = NamespaceProbeCleanupInventory.BuildRunIdInventoryArguments("run_01");
        var docker = new FakeReadOnlyDockerCli(
            (true, inventoryArguments, id),
            (true, ["inspect", "--format", "{{json .}}", id],
                CreateInspectJson(id, "run_01", NamespaceProbeModelFactory.OwnerRole, "exited")),
            (true, inventoryArguments, "\r\n"));
        var inventoryCount = 0;

        await NamespaceProbeCleanupInventory.VerifyNoOwnedResourcesAsync(
            docker,
            "run_01",
            _ => Task.CompletedTask,
            (_, _) => inventoryCount++,
            (_, _, _) => { },
            CancellationToken.None,
            TimeSpan.Zero);

        Assert.Equal(2, inventoryCount);
        Assert.Equal(3, docker.Calls.Count);
    }

    [Fact]
    public async Task CleanupVerificationDoesNotSucceedWhileOwnedContainerRemains()
    {
        var id = new string('a', 64);
        var inventoryArguments = NamespaceProbeCleanupInventory.BuildRunIdInventoryArguments("run_01");
        var docker = new FakeReadOnlyDockerCli(
            (true, inventoryArguments, id),
            (true, ["inspect", "--format", "{{json .}}", id],
                CreateInspectJson(id, "run_01", NamespaceProbeModelFactory.OwnerRole, "exited")),
            (true, inventoryArguments, id),
            (true, ["inspect", "--format", "{{json .}}", id],
                CreateInspectJson(id, "run_01", NamespaceProbeModelFactory.OwnerRole, "exited")));
        using var cancellation = new CancellationTokenSource();
        var candidateCount = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            NamespaceProbeCleanupInventory.VerifyNoOwnedResourcesAsync(
                docker,
                "run_01",
                _ => Task.CompletedTask,
                (_, _) => { },
                (_, _, _) =>
                {
                    if (++candidateCount == 2)
                    {
                        cancellation.Cancel();
                    }
                },
                cancellation.Token,
                TimeSpan.Zero));

        Assert.Equal(2, candidateCount);
        Assert.Equal(4, docker.Calls.Count);
    }

    [Fact]
    public async Task ResourceLogCaptureCancellationReturnsWhenAReaderDoesNotStop()
    {
        using var stop = new CancellationTokenSource();
        var blockedReader = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var stopped = await NamespaceProbeResourceLogCapture.TryStopAsync(
            stop,
            [blockedReader.Task],
            TimeSpan.FromMilliseconds(25)).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(stopped);
        Assert.True(stop.IsCancellationRequested);
        blockedReader.SetResult();
        await blockedReader.Task;
    }

    private static string CreateInspectJson(string id, string runId, string role, string state) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            Id = id,
            Config = new
            {
                Labels = new Dictionary<string, string>
                {
                    [ContainerIdentityExecHandoff.RunIdLabel] = runId,
                    [ContainerIdentityExecHandoff.RoleLabel] = role,
                },
            },
            State = new { Status = state },
        });

    [NativeNamespaceProbeFact]
    public async Task NativeNamespaceProbeRecreatesOnlyItsTwoOwnedContainersOnce()
    {
        var settings = NamespaceProbeTestSettings.Parse(
            Environment.GetEnvironmentVariable(GateVariableName),
            Environment.GetEnvironmentVariable(EngineVariable),
            Environment.GetEnvironmentVariable(RunIdVariable));
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("The native probe fact was not skipped even though its gate is absent.");
        }

        await RunNativeProbeAsync(settings);
    }

    private async Task RunNativeProbeAsync(NamespaceProbeTestSettings settings)
    {
        var stopwatch = Stopwatch.StartNew();
        using var operationDeadline = new CancellationTokenSource(OperationBudget);
        var cancellationToken = operationDeadline.Token;
        var ownedContainerIds = new HashSet<string>(StringComparer.Ordinal);
        DistributedApplication? application = null;
        StackOwnershipLease? ownershipLease = null;
        ResourceNotificationService? notifications = null;
        ResourceCommandService? commands = null;
        NamespaceProbeNotificationMonitor? monitor = null;
        CancellationTokenSource? resourceLogCaptureStop = null;
        Task[] resourceLogCaptureTasks = [];
        var resourceErrorLogs = new ConcurrentQueue<CapturedResourceErrorLog>();
        var resourceStateObservations = new ConcurrentQueue<CapturedResourceState>();
        var resourceStateObservationCounts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        Exception? operationFailure = null;
        Exception? cleanupFailure = null;
        var dockerExecutable = GetDockerExecutablePath();
        var publishDirectory = GetExistingPublishDirectory();

        try
        {
            var docker = new ReadOnlyDockerCli(dockerExecutable, settings.Context);
            Record("preflight.started", new { context = settings.Context, runId = settings.RunId });
            await VerifyReadOnlyPreflightAsync(
                docker,
                settings,
                Environment.GetEnvironmentVariable("DOCKER_HOST"),
                Environment.GetEnvironmentVariable("DOCKER_CONTEXT"),
                cancellationToken).ConfigureAwait(false);
            Record("preflight.passed", new { engineId = settings.ExpectedEngineId, imageDigest = NamespaceProbeModelFactory.AspNetImageSha256 });

            ownershipLease = StackOwnershipLease.Acquire(
                Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock"));

            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = [],
                AssemblyName = typeof(Projects.Duck_Testing_AppHost).Assembly.GetName().Name,
                ProjectDirectory = Projects.Duck_Testing_AppHost.ProjectPath,
                DisableDashboard = true,
            });
            var model = await NamespaceProbeModelFactory.AddResourcesAndProjectAsync(
                builder,
                settings.RunId,
                publishDirectory,
                cancellationToken).ConfigureAwait(false);
            Assert.Equal(2, model.Resources.Count);

            var owner = Assert.IsType<ContainerResource>(Assert.Single(
                builder.Resources,
                resource => resource.Name == NamespaceProbeModelFactory.OwnerResourceName));
            var follower = Assert.IsType<ContainerResource>(Assert.Single(
                builder.Resources,
                resource => resource.Name == NamespaceProbeModelFactory.FollowerResourceName));
            application = builder.Build();
            notifications = application.Services.GetRequiredService<ResourceNotificationService>();
            commands = application.Services.GetRequiredService<ResourceCommandService>();
            var resourceLogger = application.Services.GetRequiredService<ResourceLoggerService>();
            var identityAdapter = new AspireOwnedReconfigurationAdapter(
                notifications,
                commands,
                new DockerCliCommandRunner(dockerExecutable),
                settings.Context);
            monitor = new NamespaceProbeNotificationMonitor(identityAdapter, observation =>
                Record("generation.observed", new
                {
                    resource = observation.Resource.Name,
                    resourceId = observation.ResourceId,
                    containerId = observation.ContainerId,
                    state = observation.State,
                }),
                [owner, follower],
                observation =>
                {
                    var count = resourceStateObservationCounts.AddOrUpdate(observation.Resource.Name, 1, (_, current) => current + 1);
                    if (count <= 64)
                    {
                        resourceStateObservations.Enqueue(new CapturedResourceState(
                            observation.Resource.Name,
                            observation.ResourceId,
                            observation.State));
                    }
                });
            resourceLogCaptureStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            resourceLogCaptureTasks =
            [
                CaptureResourceErrorLogsAsync(resourceLogger, owner, resourceLogCaptureStop.Token, resourceErrorLogs),
                CaptureResourceErrorLogsAsync(resourceLogger, follower, resourceLogCaptureStop.Token, resourceErrorLogs),
            ];
            Record("resource.error-log-capture.started", new { resources = 2, errorsOnly = true, maxLinesPerResource = 32, maxCharactersPerLine = 2048 });

            Record("apphost.starting", new { resources = 2, dashboardDisabled = true });
            await application.StartAsync(cancellationToken).ConfigureAwait(false);
            Record("apphost.started", new { });

            var ownerInitial = await monitor.WaitForRunningAsync(owner, null, cancellationToken).ConfigureAwait(false);
            ownedContainerIds.Add(ownerInitial.ContainerId!);
            Record("resource.running", new { role = NamespaceProbeModelFactory.OwnerRole, resourceId = ownerInitial.ResourceId, containerId = ownerInitial.ContainerId });
            await AssertOwnedRunningAsync(docker, settings, ownerInitial, NamespaceProbeModelFactory.OwnerRole, cancellationToken)
                .ConfigureAwait(false);
            await AssertOwnerEndpointIsLoopbackOnlyAsync(docker, ownerInitial.ContainerId!, cancellationToken).ConfigureAwait(false);

            var followerInitial = await monitor.WaitForRunningAsync(follower, null, cancellationToken).ConfigureAwait(false);
            ownedContainerIds.Add(followerInitial.ContainerId!);
            Record("resource.running", new { role = NamespaceProbeModelFactory.FollowerRole, resourceId = followerInitial.ResourceId, containerId = followerInitial.ContainerId });
            await AssertOwnedRunningAsync(docker, settings, followerInitial, NamespaceProbeModelFactory.FollowerRole, cancellationToken)
                .ConfigureAwait(false);
            await AssertFollowerSharesCurrentOwnerNamespaceAsync(
                docker,
                followerInitial.ContainerId!,
                ownerInitial.ContainerId!,
                $"duck-namespace-probe-owner-{settings.RunId}",
                cancellationToken).ConfigureAwait(false);
            await AssertFollowerHasNoPublishedHostPortsAsync(docker, followerInitial.ContainerId!, cancellationToken)
                .ConfigureAwait(false);

            Record("follower.stop.started", new { containerId = followerInitial.ContainerId });
            await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
            await ExecuteResourceCommandAsync(monitor, commands, follower, KnownResourceCommands.StopCommand, cancellationToken)
                .ConfigureAwait(false);
            Record("follower.stop.accepted", new { });
            var followerStopped = await monitor.WaitForStoppedAsync(
                follower,
                followerInitial.ContainerId!,
                cancellationToken).ConfigureAwait(false);
            Record("follower.stopped", new { resourceId = followerStopped.ResourceId, containerId = followerStopped.ContainerId, state = followerStopped.State });
            if (!string.Equals(followerStopped.ContainerId, followerInitial.ContainerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The follower did not reach a terminal state for its originally owned container ID.");
            }

            Record("owner.restart.started", new { containerId = ownerInitial.ContainerId });
            await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
            await ExecuteResourceCommandAsync(monitor, commands, owner, KnownResourceCommands.RestartCommand, cancellationToken)
                .ConfigureAwait(false);
            Record("owner.restart.accepted", new { });
            var ownerRestarted = await monitor.WaitForRunningAsync(
                owner,
                ownerInitial.ContainerId,
                cancellationToken).ConfigureAwait(false);
            ownedContainerIds.Add(ownerRestarted.ContainerId!);
            Record("resource.running", new { role = NamespaceProbeModelFactory.OwnerRole, resourceId = ownerRestarted.ResourceId, containerId = ownerRestarted.ContainerId });
            await AssertOwnedRunningAsync(docker, settings, ownerRestarted, NamespaceProbeModelFactory.OwnerRole, cancellationToken)
                .ConfigureAwait(false);
            await AssertOwnerEndpointIsLoopbackOnlyAsync(docker, ownerRestarted.ContainerId!, cancellationToken).ConfigureAwait(false);
            await VerifyOwnerTcpConnectionAsync(docker, ownerRestarted.ContainerId!, cancellationToken).ConfigureAwait(false);

            Record("follower.start.started", new { previousContainerId = followerInitial.ContainerId, ownerContainerId = ownerRestarted.ContainerId });
            await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
            await ExecuteResourceCommandAsync(monitor, commands, follower, KnownResourceCommands.StartCommand, cancellationToken)
                .ConfigureAwait(false);
            Record("follower.start.accepted", new { });
            var followerRestarted = await monitor.WaitForRunningAsync(
                follower,
                followerInitial.ContainerId,
                cancellationToken).ConfigureAwait(false);
            ownedContainerIds.Add(followerRestarted.ContainerId!);
            Record("resource.running", new { role = NamespaceProbeModelFactory.FollowerRole, resourceId = followerRestarted.ResourceId, containerId = followerRestarted.ContainerId });
            await AssertOwnedRunningAsync(docker, settings, followerRestarted, NamespaceProbeModelFactory.FollowerRole, cancellationToken)
                .ConfigureAwait(false);
            await AssertFollowerSharesCurrentOwnerNamespaceAsync(
                docker,
                followerRestarted.ContainerId!,
                ownerRestarted.ContainerId!,
                $"duck-namespace-probe-owner-{settings.RunId}",
                cancellationToken).ConfigureAwait(false);
            await AssertFollowerHasNoPublishedHostPortsAsync(docker, followerRestarted.ContainerId!, cancellationToken)
                .ConfigureAwait(false);

            Assert.Equal(4, ownedContainerIds.Count);
        }
        catch (Exception exception)
        {
            operationFailure = exception;
            Record("operation.failed", new { exceptionType = exception.GetType().FullName, message = exception.Message, elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
        }
        finally
        {
            var remaining = NamespaceProbeCleanupBudget.Remaining(stopwatch.Elapsed);
            if (remaining <= TimeSpan.Zero)
            {
                cleanupFailure ??= new TimeoutException("The 60-second probe deadline expired before DCP cleanup could be confirmed.");
            }
            else
            {
                using var cleanupDeadline = new CancellationTokenSource(remaining);
                Record("cleanup.started", new { remainingMilliseconds = (long)remaining.TotalMilliseconds, observedContainerGenerations = monitor?.ObservedGenerations.Count ?? ownedContainerIds.Count });

                if (monitor is not null)
                {
                    try
                    {
                        await monitor.DisposeAsync().AsTask().WaitAsync(cleanupDeadline.Token).ConfigureAwait(false);
                        Record("monitor.stopped", new { });
                    }
                    catch (Exception exception)
                    {
                        cleanupFailure ??= exception;
                        Record("monitor.stop-failed", new { exceptionType = exception.GetType().FullName, message = exception.Message });
                    }
                }

                Record("generation.ledger.final", new
                {
                    observedContainerGenerations = monitor?.ObservedGenerations.Select(observation => new
                    {
                        resource = observation.Resource.Name,
                        resourceId = observation.ResourceId,
                        containerId = observation.ContainerId,
                        state = observation.State,
                    }).ToArray() ?? [],
                });
                while (resourceStateObservations.TryDequeue(out var stateObservation))
                {
                    Record("resource.state", stateObservation);
                }
                Record("resource.state-capture.summary", new { counts = resourceStateObservationCounts });

                if (cleanupDeadline.IsCancellationRequested)
                {
                    cleanupFailure ??= new TimeoutException("The cleanup deadline expired before the approved Engine check and DCP cleanup.");
                    Record("cleanup.not-started-deadline", new { elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                }
                else if (application is not null)
                {
                    var cleanupResult = await NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
                        token => AssertEngineIdentityAsync(
                            new ReadOnlyDockerCli(dockerExecutable, settings.Context),
                            settings,
                            token),
                        async token =>
                        {
                            try
                            {
                                await application.StopAsync(token).WaitAsync(token).ConfigureAwait(false);
                                Record("apphost.stopped", new { });
                            }
                            catch (Exception exception)
                            {
                                cleanupFailure ??= exception;
                                Record("apphost.stop-failed", new { exceptionType = exception.GetType().FullName, message = exception.Message });
                                throw;
                            }
                        },
                        async token =>
                        {
                            try
                            {
                                await application.DisposeAsync().AsTask().WaitAsync(token).ConfigureAwait(false);
                                Record("apphost.disposed", new { });
                            }
                            catch (Exception exception)
                            {
                                cleanupFailure ??= exception;
                                Record("apphost.dispose-failed", new { exceptionType = exception.GetType().FullName, message = exception.Message });
                                throw;
                            }
                        },
                        cleanupDeadline.Token).ConfigureAwait(false);

                    if (cleanupResult.LifecycleFailure is not null)
                    {
                        cleanupFailure ??= cleanupResult.LifecycleFailure;
                        Record("cleanup.lifecycle-failed", new
                        {
                            stage = cleanupResult.FailedStage,
                            exceptionType = cleanupResult.LifecycleFailure.GetType().FullName,
                            message = cleanupResult.LifecycleFailure.Message,
                            elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                        });
                    }

                    if (cleanupResult.EngineVerified && !cleanupDeadline.IsCancellationRequested)
                    {
                        try
                        {
                            var docker = new ReadOnlyDockerCli(dockerExecutable, settings.Context);
                            await NamespaceProbeCleanupInventory.VerifyNoOwnedResourcesAsync(
                                docker,
                                settings.RunId,
                                token => AssertEngineIdentityAsync(docker, settings, token),
                                (exitCode, remainingIds) => Record("cleanup.inventory", new
                                {
                                    exitCode,
                                    remainingContainerIds = remainingIds,
                                    observedContainerGenerations = monitor?.ObservedGenerations.Count ?? ownedContainerIds.Count,
                                }),
                                (requestedId, inspect, evidence) =>
                                {
                                    Record("cleanup.inventory-candidate", new
                                    {
                                        requestedId,
                                        containerId = evidence.ContainerId,
                                        role = evidence.Role,
                                        state = evidence.State,
                                        classification = evidence.Classification,
                                        exitCode = inspect.ExitCode,
                                    });
                                    if (evidence.Classification == "owned")
                                    {
                                        Record("cleanup.remaining-owned-resource", new
                                        {
                                            containerId = evidence.ContainerId,
                                            role = evidence.Role,
                                            state = evidence.State,
                                        });
                                    }
                                    else if (evidence.Classification == "inspect-failed")
                                    {
                                        Record("cleanup.candidate-inspect-failed", new { requestedId, exitCode = inspect.ExitCode });
                                    }
                                },
                                cleanupDeadline.Token,
                                TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

                            Record("cleanup.verified", new { remainingOwnedResources = 0 });
                        }
                        catch (Exception exception)
                        {
                            cleanupFailure ??= exception;
                            Record("cleanup.verification-failed", new { exceptionType = exception.GetType().FullName, message = exception.Message, elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                        }
                    }
                    else if (!cleanupResult.EngineVerified)
                    {
                        cleanupFailure ??= cleanupResult.EngineCheckFailure ?? new InvalidOperationException("The approved Engine/context changed before cleanup.");
                        Record("cleanup.skipped-engine-mismatch", new { exceptionType = cleanupResult.EngineCheckFailure?.GetType().FullName, message = cleanupResult.EngineCheckFailure?.Message, elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                    }
                    else
                    {
                        cleanupFailure ??= new TimeoutException("The cleanup deadline expired before owned-resource inventory verification.");
                        Record("cleanup.inventory-not-verified", new { elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                    }
                }
                else if (monitor is not null)
                {
                    cleanupFailure ??= new TimeoutException("The cleanup deadline expired before owned-resource inventory verification.");
                    Record("cleanup.inventory-not-verified", new { elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                }

            }

            if (resourceLogCaptureStop is not null)
            {
                try
                {
                    var maxWait = remaining > TimeSpan.Zero
                        ? TimeSpan.FromMilliseconds(Math.Min(remaining.TotalMilliseconds, 250))
                        : TimeSpan.Zero;
                    if (await NamespaceProbeResourceLogCapture.TryStopAsync(
                        resourceLogCaptureStop,
                        resourceLogCaptureTasks,
                        maxWait).ConfigureAwait(false))
                    {
                        Record("resource.error-log-capture.stopped", new { });
                    }
                    else
                    {
                        Record("resource.error-log-capture.stop-failed", new { exceptionType = nameof(TimeoutException), message = "Resource error log capture did not stop within its bounded wait." });
                    }
                }
                catch (Exception exception)
                {
                    Record("resource.error-log-capture.stop-failed", new { exceptionType = exception.GetType().FullName, message = exception.Message });
                }
                while (resourceErrorLogs.TryDequeue(out var capturedLog))
                {
                    Record("resource.error-log", capturedLog);
                }
                resourceLogCaptureStop.Dispose();
                resourceLogCaptureStop = null;
            }

            ownershipLease?.Dispose();
            var observedGenerationCount = monitor?.ObservedGenerations.Count ?? ownedContainerIds.Count;
            Record("probe.finished", new { elapsedMilliseconds = stopwatch.ElapsedMilliseconds, observedContainerGenerations = observedGenerationCount, cleanupVerified = cleanupFailure is null });
        }

        if (operationFailure is not null || cleanupFailure is not null)
        {
            throw new InvalidOperationException(
                $"The bounded native namespace probe did not complete cleanly. Owned container generations observed: {monitor?.ObservedGenerations.Count ?? ownedContainerIds.Count}; elapsed: {stopwatch.Elapsed}. Cleanup must be reviewed before any retry.",
                operationFailure ?? cleanupFailure);
        }
    }

    internal async Task VerifyReadOnlyPreflightAsync(
        IReadOnlyDockerCli docker,
        NamespaceProbeTestSettings settings,
        string? dockerHostOverride,
        string? dockerContextOverride,
        CancellationToken cancellationToken)
    {
        var engineId = await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
        NamespaceProbeDockerSelection.ValidateOverrides(settings.Context, dockerHostOverride, dockerContextOverride);
        Record("preflight.engine-identity", new { engineId, context = settings.Context });

        var imageReference = $"{NamespaceProbeModelFactory.AspNetImage}@sha256:{NamespaceProbeModelFactory.AspNetImageSha256}";
        var image = await docker.RunAsync(
            ["image", "inspect", "--format", "{{json .RepoDigests}}", imageReference],
            cancellationToken).ConfigureAwait(false);
        if (image.ExitCode != 0 || !image.StandardOutput.Contains(imageReference, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The exact pinned ASP.NET image digest is not available in the local cache.");
        }
        Record("preflight.cached-image", new { exitCode = image.ExitCode, imageReference });

        var collisions = await docker.RunAsync(
            NamespaceProbeCleanupInventory.BuildRunIdInventoryArguments(settings.RunId),
            cancellationToken).ConfigureAwait(false);
        if (collisions.ExitCode != 0 || !string.IsNullOrWhiteSpace(collisions.StandardOutput))
        {
            throw new InvalidOperationException("The probe run ID is already present or its owner-label preflight failed.");
        }
        Record("preflight.run-id-collision-check", new { exitCode = collisions.ExitCode, matchedContainers = 0 });
    }

    private static async Task ExecuteResourceCommandAsync(
        NamespaceProbeNotificationMonitor monitor,
        ResourceCommandService commands,
        IResource resource,
        string command,
        CancellationToken cancellationToken)
    {
        await monitor.ExecuteLifecycleCommandWithinGenerationLimitAsync(resource, command, async token =>
        {
            var result = await commands.ExecuteCommandAsync(resource, command, token).ConfigureAwait(false);
            if (!result.Success)
            {
                throw new InvalidOperationException($"The DCP resource command '{command}' failed for the selected probe resource.");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task AssertOwnedRunningAsync(
        IReadOnlyDockerCli docker,
        NamespaceProbeTestSettings settings,
        OwnedResourceObservation observation,
        string role,
        CancellationToken cancellationToken)
    {
        if (!OwnedContainerInspection.IsFullId(observation.ContainerId))
        {
            throw new InvalidOperationException("The current probe resource did not expose a full container ID.");
        }

        var inspection = await docker.RunAsync(
            ["inspect", "--format", "{{json .}}", observation.ContainerId!],
            cancellationToken).ConfigureAwait(false);
        if (inspection.ExitCode != 0 ||
            !OwnedContainerInspection.MatchesOwner(inspection.StandardOutput, observation.ContainerId!, settings.RunId, role, requireRunning: true))
        {
            throw new InvalidOperationException("The inspected container was not the running resource owned by this probe run.");
        }

        var expectedImage = $"{NamespaceProbeModelFactory.AspNetImage}@sha256:{NamespaceProbeModelFactory.AspNetImageSha256}";
        using (var document = System.Text.Json.JsonDocument.Parse(inspection.StandardOutput))
        {
            if (!document.RootElement.TryGetProperty("Config", out var config) ||
                !config.TryGetProperty("Image", out var image) ||
                !string.Equals(image.GetString(), expectedImage, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The running owned resource did not use the exact pinned image reference.");
            }
        }
        Record("container.inspect-verified", new { role, containerId = observation.ContainerId, runId = settings.RunId, image = expectedImage });

        await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> AssertEngineIdentityAsync(
        IReadOnlyDockerCli docker,
        NamespaceProbeTestSettings settings,
        CancellationToken cancellationToken)
    {
        var selected = await docker.RunAsync(["info", "--format", "{{.ID}}"], cancellationToken).ConfigureAwait(false);
        var effective = await docker.RunDefaultAsync(["info", "--format", "{{.ID}}"], cancellationToken).ConfigureAwait(false);
        var context = await docker.RunDefaultAsync(["context", "show"], cancellationToken).ConfigureAwait(false);
        if (selected.ExitCode != 0 || effective.ExitCode != 0 || context.ExitCode != 0 ||
            !string.Equals(selected.StandardOutput.Trim(), settings.ExpectedEngineId, StringComparison.Ordinal) ||
            !string.Equals(effective.StandardOutput.Trim(), settings.ExpectedEngineId, StringComparison.Ordinal) ||
            !string.Equals(context.StandardOutput.Trim(), settings.Context, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The explicit and effective Docker selections no longer identify the approved Engine and context.");
        }

        return selected.StandardOutput.Trim();
    }

    private async Task AssertOwnerEndpointIsLoopbackOnlyAsync(
        IReadOnlyDockerCli docker,
        string containerId,
        CancellationToken cancellationToken)
    {
        using var document = await InspectJsonAsync(docker, containerId, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (!root.TryGetProperty("HostConfig", out var hostConfig) ||
            !hostConfig.TryGetProperty("PortBindings", out var bindings) ||
            bindings.ValueKind != System.Text.Json.JsonValueKind.Object ||
            bindings.EnumerateObject().Count() != 1 ||
            !bindings.TryGetProperty($"{NamespaceProbeModelFactory.OwnerPort}/tcp", out var entries) ||
            entries.ValueKind != System.Text.Json.JsonValueKind.Array || entries.GetArrayLength() != 1)
        {
            throw new InvalidOperationException("The owner container did not have exactly one dynamically published TCP endpoint.");
        }

        var binding = entries[0];
        if (binding.ValueKind != System.Text.Json.JsonValueKind.Object ||
            binding.EnumerateObject().Count() != 2 ||
            !binding.TryGetProperty("HostIp", out var hostIpElement) ||
            !binding.TryGetProperty("HostPort", out var hostPortElement) ||
            hostIpElement.ValueKind != System.Text.Json.JsonValueKind.String ||
            hostPortElement.ValueKind != System.Text.Json.JsonValueKind.String ||
            hostIpElement.GetString() != "127.0.0.1" ||
            !int.TryParse(hostPortElement.GetString(), out var hostPort) ||
            hostPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("The owner endpoint was not bound to a dynamic localhost TCP port.");
        }

        Record("owner.endpoint-inspected", new { containerId, hostIp = "127.0.0.1", hostPort, targetPort = NamespaceProbeModelFactory.OwnerPort, protocol = "TCP" });
    }

    private async Task AssertFollowerSharesCurrentOwnerNamespaceAsync(
        IReadOnlyDockerCli docker,
        string followerId,
        string currentOwnerId,
        string ownerContainerName,
        CancellationToken cancellationToken)
    {
        var ownerNameInspection = await docker.RunAsync(["inspect", "--format", "{{.Id}}", ownerContainerName], cancellationToken)
            .ConfigureAwait(false);
        if (ownerNameInspection.ExitCode != 0 ||
            !string.Equals(ownerNameInspection.StandardOutput.Trim(), currentOwnerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The stable owner name did not resolve to the currently observed owner full ID.");
        }

        using var document = await InspectJsonAsync(docker, followerId, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("HostConfig", out var hostConfig) ||
            !hostConfig.TryGetProperty("NetworkMode", out var modeElement) ||
            modeElement.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            throw new InvalidOperationException("The follower container did not report its network mode.");
        }

        var mode = modeElement.GetString();
        if (!string.Equals(mode, $"container:{currentOwnerId}", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mode, $"container:{ownerContainerName}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The follower was not attached to the current owner container network namespace.");
        }

        var ownerJson = await docker.RunAsync(["inspect", "--format", "{{json .NetworkSettings.Networks}}", currentOwnerId], cancellationToken)
            .ConfigureAwait(false);
        var followerNetworks = document.RootElement.GetProperty("NetworkSettings").GetProperty("Networks");
        if (ownerJson.ExitCode != 0 || followerNetworks.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            throw new InvalidOperationException("The owner/follower network settings could not be read for this generation.");
        }

        Record("follower.network-namespace-verified", new
        {
            followerContainerId = followerId,
            ownerContainerId = currentOwnerId,
            ownerName = ownerContainerName,
            networkMode = mode,
            ownerNetworks = ownerJson.StandardOutput,
            followerNetworks = followerNetworks.ToString(),
        });
    }

    private static async Task AssertFollowerHasNoPublishedHostPortsAsync(
        IReadOnlyDockerCli docker,
        string containerId,
        CancellationToken cancellationToken)
    {
        using var document = await InspectJsonAsync(docker, containerId, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("HostConfig", out var hostConfig) ||
            hostConfig.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !hostConfig.TryGetProperty("PortBindings", out var bindings) ||
            bindings.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            throw new InvalidOperationException("The follower host port bindings could not be verified.");
        }

        if (bindings.EnumerateObject().Any())
        {
            throw new InvalidOperationException("The follower unexpectedly published a host port.");
        }
    }

    private static async Task<System.Text.Json.JsonDocument> InspectJsonAsync(
        IReadOnlyDockerCli docker,
        string containerId,
        CancellationToken cancellationToken)
    {
        var result = await docker.RunAsync(["inspect", "--format", "{{json .}}", containerId], cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new IOException("The owned probe container could not be inspected.");
        }

        return System.Text.Json.JsonDocument.Parse(result.StandardOutput);
    }

    private async Task VerifyOwnerTcpConnectionAsync(
        IReadOnlyDockerCli docker,
        string containerId,
        CancellationToken cancellationToken)
    {
        using var document = await InspectJsonAsync(docker, containerId, cancellationToken).ConfigureAwait(false);
        var bindings = document.RootElement.GetProperty("HostConfig").GetProperty("PortBindings");
        var hostPort = int.Parse(bindings.GetProperty($"{NamespaceProbeModelFactory.OwnerPort}/tcp")[0].GetProperty("HostPort").GetString()!);
        var deadline = Stopwatch.StartNew();
        Exception? lastFailure = null;
        while (deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync(System.Net.IPAddress.Loopback, hostPort, cancellationToken).ConfigureAwait(false);
                Record("owner.localhost-tcp-connected", new { containerId, hostIp = "127.0.0.1", hostPort, protocol = "TCP", applicationProtocolTested = false });
                return;
            }
            catch (System.Net.Sockets.SocketException exception)
            {
                lastFailure = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException("A localhost TCP connection to the dynamically published owner endpoint was not established.", lastFailure);
    }

    private static CustomResourceSnapshot Snapshot(string state, string? containerId) => new()
    {
        ResourceType = "container",
        State = new ResourceStateSnapshot(state, "fixture"),
        Properties = containerId is null
            ? []
            : [new ResourcePropertySnapshot("container.id", containerId)],
    };

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

    private void Record(string stage, object details) => output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        timestampUtc = DateTimeOffset.UtcNow,
        stage,
        details,
    }));

    private static async Task CaptureResourceErrorLogsAsync(
        ResourceLoggerService resourceLogger,
        IResource resource,
        CancellationToken cancellationToken,
        ConcurrentQueue<CapturedResourceErrorLog> capturedLogs)
    {
        var capturedForResource = 0;
        try
        {
            await foreach (var batch in resourceLogger.WatchAsync(resource)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                foreach (var line in batch)
                {
                    if (!line.IsErrorMessage || capturedForResource >= 32)
                    {
                        continue;
                    }

                    var content = line.Content.Length <= 2048 ? line.Content : line.Content[..2048];
                    capturedLogs.Enqueue(new CapturedResourceErrorLog(resource.Name, line.LineNumber, content));
                    capturedForResource++;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private sealed record CapturedResourceErrorLog(string Resource, int LineNumber, string Content);
    private sealed record CapturedResourceState(string Resource, string ResourceId, string State);

    private static class NamespaceProbeResourceLogCapture
    {
        internal static async Task<bool> TryStopAsync(
            CancellationTokenSource stop,
            IReadOnlyList<Task> readers,
            TimeSpan maxWait)
        {
            ArgumentNullException.ThrowIfNull(stop);
            ArgumentNullException.ThrowIfNull(readers);
            if (maxWait < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWait));
            }

            stop.Cancel();
            try
            {
                await Task.WhenAll(readers).WaitAsync(maxWait).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }
    }

    private static bool HasOwnedProbeLabels(string json, string containerId, string runId)
        => NamespaceProbeCleanupInventory.ParseCandidate(
            containerId,
            new ReadOnlyDockerResult(0, json, string.Empty),
            runId).Classification == "owned";

    private static string GetDockerExecutablePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = Path.Combine(localAppData, "Programs", "DockerDesktop", "resources", "bin", "docker.exe");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The existing Docker Desktop CLI executable was not found at its installed path.");
        }

        return path;
    }

    private static string GetExistingPublishDirectory()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(
            repositoryRoot,
            "Testing", "Duck.Testing.RefereeDriver.Host", "bin", "Release", "net10.0", "publish");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Testing", "Duck.Testing.AppHost", "Duck.Testing.AppHost.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root could not be located from the test output directory.");
    }
}

internal sealed class GatedOwnedReconfigurationAdapter(IOwnedReconfigurationAdapter inner, int eventCount)
    : IOwnedReconfigurationAdapter
{
    private readonly TaskCompletionSource[] queued = Enumerable.Range(0, eventCount)
        .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
        .ToArray();
    private readonly Channel<bool> continueChannel = Channel.CreateUnbounded<bool>();

    public OwnedResourceObservation? GetCurrent(IResource resource) => inner.GetCurrent(resource);
    public OwnedResourceObservation? GetCurrent(string resourceId) => inner.GetCurrent(resourceId);

    public async IAsyncEnumerable<OwnedResourceObservation> WatchAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var watch = inner.WatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        for (var index = 0; index < queued.Length; index++)
        {
            if (!await watch.MoveNextAsync().ConfigureAwait(false))
            {
                yield break;
            }

            yield return watch.Current;
            queued[index].TrySetResult();
            if (index < queued.Length - 1)
            {
                await continueChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task<bool> ExecuteLifecycleCommandAsync(
        IResource resource,
        string commandName,
        CancellationToken cancellationToken) =>
        inner.ExecuteLifecycleCommandAsync(resource, commandName, cancellationToken);

    public Task<string> CaptureDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        inner.CaptureDockerAsync(arguments, cancellationToken);

    internal Task WaitUntilQueuedAsync(int index, CancellationToken cancellationToken) =>
        queued[index].Task.WaitAsync(cancellationToken);

    internal void Continue() => continueChannel.Writer.TryWrite(true);
}

internal sealed class QueuedCurrentOwnedReconfigurationAdapter(
    IOwnedReconfigurationAdapter currentAdapter,
    IReadOnlyList<OwnedResourceObservation> queuedObservations) : IOwnedReconfigurationAdapter
{
    public OwnedResourceObservation? GetCurrent(IResource resource) => currentAdapter.GetCurrent(resource);
    public OwnedResourceObservation? GetCurrent(string resourceId) => currentAdapter.GetCurrent(resourceId);

    public async IAsyncEnumerable<OwnedResourceObservation> WatchAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var observation in queuedObservations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return observation;
            await Task.Yield();
        }
    }

    public Task<bool> ExecuteLifecycleCommandAsync(
        IResource resource,
        string commandName,
        CancellationToken cancellationToken) =>
        currentAdapter.ExecuteLifecycleCommandAsync(resource, commandName, cancellationToken);

    public Task<string> CaptureDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        currentAdapter.CaptureDockerAsync(arguments, cancellationToken);
}

internal sealed record NamespaceProbeTestSettings(
    bool Enabled,
    string Context,
    string ExpectedEngineId,
    string RunId)
{
    internal static NamespaceProbeTestSettings Parse(string? enabledValue, string? expectedEngineId, string? runId)
    {
        if (enabledValue is null)
        {
            return new NamespaceProbeTestSettings(false, string.Empty, string.Empty, string.Empty);
        }

        if (!bool.TryParse(enabledValue, out var enabled) || !enabled)
        {
            throw new InvalidOperationException($"{NamespaceProbeDcpAcceptanceTests.GateVariableName} must be true when specified.");
        }

        if (string.IsNullOrWhiteSpace(expectedEngineId) ||
            string.IsNullOrWhiteSpace(runId))
        {
            throw new InvalidOperationException("The native probe requires its exact Docker context, expected engine identity, and run ID.");
        }

        return new NamespaceProbeTestSettings(
            true,
            "desktop-linux",
            expectedEngineId,
            NamespaceProbeModelFactory.ValidateRunId(runId));
    }
}

public sealed class NativeNamespaceProbeFactAttribute : FactAttribute
{
    public NativeNamespaceProbeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(NamespaceProbeDcpAcceptanceTests.GateVariableName) is null)
        {
            Skip = $"Set {NamespaceProbeDcpAcceptanceTests.GateVariableName}=true and provide the required target values to run this native probe.";
        }
    }
}

internal sealed class NamespaceProbeNotificationMonitor : IAsyncDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly IOwnedReconfigurationAdapter adapter;
    private readonly Action<OwnedResourceObservation>? observationRecorded;
    private readonly IResource[]? plannedRecreationOrder;
    private readonly Action<OwnedResourceObservation>? stateObserved;
    private readonly object generationGate = new();
    private readonly Dictionary<IResource, Dictionary<string, OwnedResourceObservation>> generations = [];
    private InvalidOperationException? generationLimitFailure;
    private InvalidOperationException? generationPlanFailure;
    private IResource? reservedRecreationResource;
    private int nextPlannedRecreation;
    private readonly Channel<OwnedResourceObservation> observations = Channel.CreateUnbounded<OwnedResourceObservation>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly Task pump;

    internal NamespaceProbeNotificationMonitor(
        IOwnedReconfigurationAdapter adapter,
        Action<OwnedResourceObservation>? observationRecorded = null,
        IReadOnlyList<IResource>? plannedRecreationOrder = null,
        Action<OwnedResourceObservation>? stateObserved = null)
    {
        this.adapter = adapter;
        this.observationRecorded = observationRecorded;
        this.plannedRecreationOrder = plannedRecreationOrder?.ToArray();
        this.stateObserved = stateObserved;
        if (this.plannedRecreationOrder is { Length: not 2 } ||
            this.plannedRecreationOrder is { Length: 2 } plan && ReferenceEquals(plan[0], plan[1]))
        {
            throw new ArgumentException("The namespace probe plan requires one recreation for each distinct resource.", nameof(plannedRecreationOrder));
        }
        pump = Task.Run(async () =>
        {
            try
            {
                await foreach (var observation in adapter.WatchAsync(stop.Token).ConfigureAwait(false))
                {
                    stateObserved?.Invoke(observation);
                    RecordGeneration(observation);
                    await observations.Writer.WriteAsync(observation, stop.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }
            finally
            {
                observations.Writer.TryComplete();
            }
        });
    }

    internal IReadOnlyList<OwnedResourceObservation> ObservedGenerations
    {
        get
        {
            lock (generationGate)
            {
                return generations.Values.SelectMany(byId => byId.Values).ToArray();
            }
        }
    }

    internal void EnsureWithinGenerationLimit()
    {
        foreach (var resource in ObservedGenerations.Select(observation => observation.Resource).Distinct())
        {
            var current = adapter.GetCurrent(resource);
            if (current is not null)
            {
                RecordGeneration(current);
            }
        }

        lock (generationGate)
        {
            if (generationLimitFailure is not null)
            {
                throw generationLimitFailure;
            }

            if (generationPlanFailure is not null)
            {
                throw generationPlanFailure;
            }
        }
    }

    internal async Task ExecuteLifecycleCommandWithinGenerationLimitAsync(
        Func<CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        EnsureWithinGenerationLimit();
        await execute(cancellationToken).ConfigureAwait(false);
        EnsureWithinGenerationLimit();
    }

    internal async Task ExecuteLifecycleCommandWithinGenerationLimitAsync(
        IResource resource,
        string commandName,
        Func<CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        EnsureWithinGenerationLimit();
        if (plannedRecreationOrder is not null &&
            (commandName == KnownResourceCommands.StartCommand || commandName == KnownResourceCommands.RestartCommand))
        {
            ReserveExpectedRecreation(resource);
        }

        await execute(cancellationToken).ConfigureAwait(false);
        EnsureWithinGenerationLimit();
    }

    private void ReserveExpectedRecreation(IResource resource)
    {
        lock (generationGate)
        {
            if (generationLimitFailure is not null)
            {
                throw generationLimitFailure;
            }

            if (generationPlanFailure is not null)
            {
                throw generationPlanFailure;
            }

            if (plannedRecreationOrder is null ||
                reservedRecreationResource is not null ||
                nextPlannedRecreation >= plannedRecreationOrder.Length ||
                !ReferenceEquals(plannedRecreationOrder[nextPlannedRecreation], resource))
            {
                generationPlanFailure ??= new InvalidOperationException(
                    "The lifecycle command would recreate a resource outside the namespace probe's planned order.");
                throw generationPlanFailure;
            }

            if (generations.Values.Sum(byId => byId.Count) >= 4)
            {
                generationLimitFailure ??= new InvalidOperationException(
                    "The namespace probe has no remaining approved container-generation slot.");
                throw generationLimitFailure;
            }

            reservedRecreationResource = resource;
        }
    }

    private void RecordGeneration(OwnedResourceObservation observation)
    {
        if (!OwnedContainerInspection.IsFullId(observation.ContainerId))
        {
            return;
        }

        lock (generationGate)
        {
            if (!generations.TryGetValue(observation.Resource, out var byId))
            {
                byId = new Dictionary<string, OwnedResourceObservation>(StringComparer.OrdinalIgnoreCase);
                generations.Add(observation.Resource, byId);
            }

            var isNewGeneration = !byId.ContainsKey(observation.ContainerId!);
            if (isNewGeneration && plannedRecreationOrder is not null)
            {
                if (byId.Count == 0)
                {
                    if (reservedRecreationResource is not null)
                    {
                        generationPlanFailure ??= new InvalidOperationException(
                            "An initial resource generation arrived while a different recreation was reserved.");
                    }
                }
                else if (reservedRecreationResource is null ||
                    !ReferenceEquals(reservedRecreationResource, observation.Resource) ||
                    nextPlannedRecreation >= plannedRecreationOrder.Length ||
                    !ReferenceEquals(plannedRecreationOrder[nextPlannedRecreation], observation.Resource))
                {
                    generationPlanFailure ??= new InvalidOperationException(
                        "The namespace probe observed a container generation outside its reserved resource order.");
                }
                else
                {
                    reservedRecreationResource = null;
                    nextPlannedRecreation++;
                }
            }

            byId[observation.ContainerId!] = observation;
            if (generations.Values.Sum(values => values.Count) > 4)
            {
                generationLimitFailure ??= new InvalidOperationException(
                    "The namespace probe observed more than its four approved container generations.");
            }
        }

        observationRecorded?.Invoke(observation);
    }

    internal async Task<OwnedResourceObservation> WaitForRunningAsync(
        IResource resource,
        string? previousContainerId,
        CancellationToken cancellationToken)
    {
        EnsureWithinGenerationLimit();
        var current = adapter.GetCurrent(resource);
        if (current is not null)
        {
            RecordGeneration(current);
            EnsureWithinGenerationLimit();
        }
        if (current is not null && IsNewRunningContainer(current, previousContainerId))
        {
            return current;
        }

        await foreach (var observation in observations.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            EnsureWithinGenerationLimit();
            if (!ReferenceEquals(observation.Resource, resource))
            {
                continue;
            }

            current = GetExactCurrentObservation(observation);
            EnsureWithinGenerationLimit();
            if (current is not null && IsNewRunningContainer(current, previousContainerId))
            {
                return current;
            }
        }

        throw new InvalidOperationException($"No current running container identity was observed for resource '{resource.Name}'.");
    }

    internal async Task<OwnedResourceObservation> WaitForStoppedAsync(
        IResource resource,
        string expectedContainerId,
        CancellationToken cancellationToken)
    {
        EnsureWithinGenerationLimit();
        var current = adapter.GetCurrent(resource);
        if (current is not null)
        {
            RecordGeneration(current);
            EnsureWithinGenerationLimit();
        }
        if (current is not null && IsExpectedStopped(current, expectedContainerId))
        {
            return current;
        }

        await foreach (var observation in observations.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            EnsureWithinGenerationLimit();
            if (!ReferenceEquals(observation.Resource, resource))
            {
                continue;
            }

            current = GetExactCurrentObservation(observation);
            EnsureWithinGenerationLimit();
            if (current is not null && IsExpectedStopped(current, expectedContainerId))
            {
                return current;
            }
        }

        throw new InvalidOperationException($"No terminal observation for the owned container was observed for resource '{resource.Name}'.");
    }

    private OwnedResourceObservation? GetExactCurrentObservation(OwnedResourceObservation notification)
    {
        var current = adapter.GetCurrent(notification.Resource);
        return current is not null && ReferenceEquals(current.Resource, notification.Resource) &&
            string.Equals(current.ResourceId, notification.ResourceId, StringComparison.Ordinal) &&
            string.Equals(current.State, notification.State, StringComparison.Ordinal) &&
            string.Equals(current.ContainerId, notification.ContainerId, StringComparison.Ordinal)
            ? current
            : null;
    }

    private static bool IsNewRunningContainer(OwnedResourceObservation observation, string? previousContainerId) =>
        string.Equals(observation.State, KnownResourceStates.Running, StringComparison.Ordinal) &&
        OwnedContainerInspection.IsFullId(observation.ContainerId) &&
        !string.Equals(observation.ContainerId, previousContainerId, StringComparison.Ordinal);

    private static bool IsExpectedStopped(OwnedResourceObservation observation, string expectedContainerId) =>
        observation.IsStopped && string.Equals(observation.ContainerId, expectedContainerId, StringComparison.Ordinal);

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        await pump.ConfigureAwait(false);
        stop.Dispose();
    }
}

internal sealed class SequenceOwnedReconfigurationAdapter(IReadOnlyList<OwnedResourceObservation> sequence)
    : IOwnedReconfigurationAdapter
{
    private readonly ConcurrentDictionary<IResource, OwnedResourceObservation> current = new();

    public OwnedResourceObservation? GetCurrent(IResource resource) => current.GetValueOrDefault(resource);

    public OwnedResourceObservation? GetCurrent(string resourceId) =>
        current.Values.LastOrDefault(observation => string.Equals(observation.ResourceId, resourceId, StringComparison.Ordinal));

    public async IAsyncEnumerable<OwnedResourceObservation> WatchAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var observation in sequence)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current[observation.Resource] = observation;
            yield return observation;
            await Task.Yield();
        }
    }

    public Task<bool> ExecuteLifecycleCommandAsync(IResource resource, string commandName, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<string> CaptureDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed record ReadOnlyDockerResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed record NamespaceProbeCleanupCandidateEvidence(
    string Classification,
    string? ContainerId = null,
    string? Role = null,
    string? State = null);

internal static class NamespaceProbeCleanupInventory
{
    internal static string[] BuildRunIdInventoryArguments(string runId) =>
    [
        "ps", "--all", "--quiet", "--no-trunc",
        "--filter", $"label={ContainerIdentityExecHandoff.RunIdLabel}={runId}",
    ];

    internal static NamespaceProbeCleanupCandidateEvidence ParseCandidate(
        string inventoryId,
        ReadOnlyDockerResult inspect,
        string runId)
    {
        if (inspect.ExitCode != 0)
        {
            var error = inspect.StandardError;
            return error.Contains("No such object", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("No such container", StringComparison.OrdinalIgnoreCase)
                ? new NamespaceProbeCleanupCandidateEvidence("disappeared")
                : new NamespaceProbeCleanupCandidateEvidence("inspect-failed");
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(inspect.StandardOutput);
            var root = document.RootElement;
            if (!root.TryGetProperty("Id", out var idElement) ||
                idElement.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                return new NamespaceProbeCleanupCandidateEvidence("missing-full-id");
            }

            var id = idElement.GetString();
            if (!OwnedContainerInspection.IsFullId(id))
            {
                return new NamespaceProbeCleanupCandidateEvidence("invalid-full-id", id);
            }

            if (!OwnedContainerInspection.IsFullId(inventoryId))
            {
                return new NamespaceProbeCleanupCandidateEvidence("truncated-inventory-id", id);
            }

            if (!string.Equals(id, inventoryId, StringComparison.OrdinalIgnoreCase))
            {
                return new NamespaceProbeCleanupCandidateEvidence("id-mismatch", id);
            }

            if (!root.TryGetProperty("Config", out var config) ||
                !config.TryGetProperty("Labels", out var labels) ||
                labels.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !labels.TryGetProperty(ContainerIdentityExecHandoff.RunIdLabel, out var actualRunIdElement) ||
                actualRunIdElement.ValueKind != System.Text.Json.JsonValueKind.String ||
                !labels.TryGetProperty(ContainerIdentityExecHandoff.RoleLabel, out var roleElement) ||
                roleElement.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                return new NamespaceProbeCleanupCandidateEvidence("missing-owner-labels", id);
            }

            var actualRunId = actualRunIdElement.GetString();
            var role = roleElement.GetString();
            if (!string.Equals(actualRunId, runId, StringComparison.Ordinal) ||
                role is not (NamespaceProbeModelFactory.OwnerRole or NamespaceProbeModelFactory.FollowerRole))
            {
                return new NamespaceProbeCleanupCandidateEvidence("foreign-or-unowned", id, role);
            }

            if (!root.TryGetProperty("State", out var stateElement) ||
                !stateElement.TryGetProperty("Status", out var statusElement) ||
                statusElement.ValueKind != System.Text.Json.JsonValueKind.String ||
                string.IsNullOrWhiteSpace(statusElement.GetString()))
            {
                return new NamespaceProbeCleanupCandidateEvidence("missing-state", id, role);
            }

            return new NamespaceProbeCleanupCandidateEvidence("owned", id, role, statusElement.GetString());
        }
        catch (System.Text.Json.JsonException)
        {
            return new NamespaceProbeCleanupCandidateEvidence("malformed-inspection");
        }
        catch (InvalidOperationException)
        {
            return new NamespaceProbeCleanupCandidateEvidence("malformed-inspection");
        }
    }

    internal static async Task VerifyNoOwnedResourcesAsync(
        IReadOnlyDockerCli docker,
        string runId,
        Func<CancellationToken, Task> verifyEngine,
        Action<int, IReadOnlyList<string>> inventoryObserved,
        Action<string, ReadOnlyDockerResult, NamespaceProbeCleanupCandidateEvidence> candidateObserved,
        CancellationToken cancellationToken,
        TimeSpan pollInterval)
    {
        ArgumentNullException.ThrowIfNull(docker);
        ArgumentNullException.ThrowIfNull(verifyEngine);
        ArgumentNullException.ThrowIfNull(inventoryObserved);
        ArgumentNullException.ThrowIfNull(candidateObserved);
        if (pollInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }

        while (true)
        {
            await verifyEngine(cancellationToken).ConfigureAwait(false);
            var inventory = await docker.RunAsync(BuildRunIdInventoryArguments(runId), cancellationToken).ConfigureAwait(false);
            if (inventory.ExitCode != 0)
            {
                throw new IOException("The run-label inventory could not verify DCP cleanup.");
            }

            var remainingIds = inventory.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            inventoryObserved(inventory.ExitCode, remainingIds);
            if (remainingIds.Length == 0)
            {
                return;
            }

            var remainingOwnedCount = 0;
            var unverifiedCandidates = false;
            foreach (var remainingId in remainingIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var inspect = await docker.RunAsync(["inspect", "--format", "{{json .}}", remainingId], cancellationToken)
                    .ConfigureAwait(false);
                var evidence = ParseCandidate(remainingId, inspect, runId);
                candidateObserved(remainingId, inspect, evidence);
                if (evidence.Classification == "owned")
                {
                    remainingOwnedCount++;
                }
                else if (evidence.Classification is not "disappeared")
                {
                    unverifiedCandidates = true;
                }
            }

            if (unverifiedCandidates)
            {
                throw new InvalidOperationException("DCP cleanup left unverified run-labeled resources; no manual removal was attempted.");
            }

            if (remainingOwnedCount > 0 || remainingIds.Length > 0)
            {
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

internal interface IReadOnlyDockerCli
{
    Task<ReadOnlyDockerResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    Task<ReadOnlyDockerResult> RunDefaultAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

internal sealed class ReadOnlyDockerCli(string executablePath, string context) : IReadOnlyDockerCli
{
    public async Task<ReadOnlyDockerResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        await RunCoreAsync(arguments, includeContext: true, cancellationToken).ConfigureAwait(false);

    public async Task<ReadOnlyDockerResult> RunDefaultAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        await RunCoreAsync(arguments, includeContext: false, cancellationToken).ConfigureAwait(false);

    private async Task<ReadOnlyDockerResult> RunCoreAsync(
        IReadOnlyList<string> arguments,
        bool includeContext,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (includeContext)
        {
            startInfo.ArgumentList.Add("--context");
            startInfo.ArgumentList.Add(context);
        }
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new IOException("The read-only Docker metadata command did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(8), cancellationToken)
            .ConfigureAwait(false);
        return new ReadOnlyDockerResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }
}

internal sealed class FakeReadOnlyDockerCli(params (bool ExplicitContext, string[] ExpectedArguments, string Output)[] responses)
    : IReadOnlyDockerCli
{
    private readonly Queue<(bool ExplicitContext, string[] ExpectedArguments, string Output)> queue = new(responses);
    internal List<(bool ExplicitContext, IReadOnlyList<string> Arguments)> Calls { get; } = [];

    public Task<ReadOnlyDockerResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        RespondAsync(true, arguments, cancellationToken);

    public Task<ReadOnlyDockerResult> RunDefaultAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        RespondAsync(false, arguments, cancellationToken);

    private Task<ReadOnlyDockerResult> RespondAsync(
        bool explicitContext,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add((explicitContext, arguments.ToArray()));
        if (queue.Count == 0)
        {
            throw new InvalidOperationException("The fake read-only Docker plan received an unexpected command.");
        }

        var expected = queue.Dequeue();
        if (expected.ExplicitContext != explicitContext || !ArgumentsMatch(expected.ExpectedArguments, arguments))
        {
            throw new InvalidOperationException("The fake read-only Docker plan received an unexpected command shape.");
        }

        return Task.FromResult(new ReadOnlyDockerResult(
            expected.Output.Length == 0 ? 1 : 0,
            expected.Output,
            expected.Output.Length == 0 ? "planned read-only command failure" : string.Empty));
    }

    private static bool ArgumentsMatch(IReadOnlyList<string> expected, IReadOnlyList<string> actual) =>
        expected.Count == actual.Count && expected.Zip(actual).All(pair =>
            pair.First == ProbeTestArgument.Any || string.Equals(pair.First, pair.Second, StringComparison.Ordinal));

    internal static bool IsReadOnly(IReadOnlyList<string> arguments) =>
        arguments.Count > 0 &&
        arguments[0] is "info" or "context" or "image" or "ps" or "inspect";
}

internal static class ProbeTestArgument
{
    internal const string Any = "<any>";
}

internal static class NamespaceProbeDockerSelection
{
    internal static void ValidateOverrides(string expectedContext, string? dockerHost, string? dockerContext)
    {
        if (!string.IsNullOrWhiteSpace(dockerHost) ||
            (!string.IsNullOrWhiteSpace(dockerContext) &&
             !string.Equals(dockerContext, expectedContext, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Docker environment overrides did not prove the same approved Engine and context.");
        }
    }
}

internal static class NamespaceProbeCleanupBudget
{
    internal static TimeSpan Remaining(TimeSpan elapsed)
    {
        var deadline = TimeSpan.FromSeconds(60);
        return elapsed >= deadline ? TimeSpan.Zero : deadline - elapsed;
    }
}

internal sealed record NamespaceProbeCleanupResult(
    bool EngineVerified,
    Exception? EngineCheckFailure,
    Exception? LifecycleFailure = null,
    string? FailedStage = null);

internal static class NamespaceProbeCleanup
{
    internal static async Task<NamespaceProbeCleanupResult> RunOnlyAfterEngineCheckAsync(
        Func<CancellationToken, Task> verifyEngine,
        Func<CancellationToken, Task> stopApplication,
        Func<CancellationToken, Task> disposeApplication,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await verifyEngine(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return new NamespaceProbeCleanupResult(false, exception);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await stopApplication(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return new NamespaceProbeCleanupResult(true, null, exception, "stop");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await disposeApplication(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return new NamespaceProbeCleanupResult(true, null, exception, "dispose");
        }

        return new NamespaceProbeCleanupResult(true, null);
    }
}
