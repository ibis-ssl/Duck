using System.Runtime.CompilerServices;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tracker.Tests;

public sealed class OwnedRefereeReconfigurationTests
{
    private const string RunId = "owned-reconfiguration-test";
    private const string EngineId = "expected-engine";
    private const string GcOldId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string GcNewId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string RefOldId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string RefNewId = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    [Fact]
    public async Task ForeignOrWrongEnginePreflightExecutesNoLifecycleCommands()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { EngineId = "different-engine" };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(adapter.LifecycleCommands);
        Assert.Equal(OwnedEndpointPhase.Initial, state.Snapshot().Phase);
    }

    [Fact]
    public async Task WrongOwnerPreflightExecutesNoLifecycleCommands()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { ForeignGameControllerOwner = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(adapter.LifecycleCommands);
        Assert.Equal(OwnedEndpointPhase.Initial, state.Snapshot().Phase);
    }

    [Fact]
    public async Task SuccessfulReconfigurationStopsRefereeRestartsSourceThenStartsRefereeWithFreshUri()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee);
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(
            new[] { ("referee-driver", KnownResourceCommands.StopCommand), ("game-controller", KnownResourceCommands.RestartCommand), ("referee-driver", KnownResourceCommands.StartCommand) },
            adapter.LifecycleCommands);
        Assert.Equal("ws://10.0.0.21:8082/api/control", state.GetUriForEnvironment());
        Assert.Equal(OwnedEndpointPhase.Active, state.Snapshot().Phase);
        Assert.Equal(RefNewId, adapter.GetCurrent(referee)!.ContainerId);
    }

    [Fact]
    public async Task SuccessfulReconfigurationTracksAspireOpaqueResourceIds()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance, lifetime);
        var fake = new FakeAdapter(gc, referee, switchToOpaqueIdsOnRecreation: true);
        await PublishAsync(notifications, gc, fake.GetCurrent(gc)!);
        await PublishAsync(notifications, referee, fake.GetCurrent(referee)!);
        var aspireAdapter = new AspireOwnedReconfigurationAdapter(notifications, null!, null!, string.Empty);
        Assert.Equal(gc.Name, aspireAdapter.GetCurrent(gc)!.ResourceId);
        var adapter = new ResourceNotificationFakeAdapter(notifications, fake);
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("game-controller-opaque", adapter.GetCurrent(gc)!.ResourceId);
        Assert.Equal("referee-driver-opaque", adapter.GetCurrent(referee)!.ResourceId);
        Assert.Equal("ws://10.0.0.21:8082/api/control", state.GetUriForEnvironment());

        using var monitor = new OwnedSourceGenerationMonitor(
            adapter, coordinator, gc, NullLogger<OwnedSourceGenerationMonitor>.Instance);
        await monitor.StartAsync(CancellationToken.None);
        var unexpected = fake.PublishSource("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
        await PublishAsync(notifications, gc, unexpected);
        await fake.SecondStopCommandEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(OwnedEndpointPhase.Suspended, state.Snapshot().Phase);
        Assert.Equal(2, fake.LifecycleCommands.Count(command => command == ("referee-driver", KnownResourceCommands.StopCommand)));
        await monitor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AspireAdapterUsesCurrentOpaqueKeyInsteadOfRetainedDisplayNameSnapshot()
    {
        var resource = new ContainerResource("game-controller");
        using var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance, lifetime);
        var adapter = new AspireOwnedReconfigurationAdapter(notifications, null!, null!, string.Empty);
        using var watchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var watch = adapter.WatchAsync(watchTimeout.Token).GetAsyncEnumerator(watchTimeout.Token);
        await notifications.PublishUpdateAsync(resource, resource.Name,
            _ => Snapshot(KnownResourceStates.Running, GcOldId));
        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(resource.Name, adapter.GetCurrent(resource)!.ResourceId);

        await notifications.PublishUpdateAsync(resource, "game-controller-current-key",
            _ => Snapshot(KnownResourceStates.Running, GcNewId));
        Assert.True(await watch.MoveNextAsync());
        Assert.Null(adapter.GetCurrent(resource));

        await notifications.PublishUpdateAsync(resource, resource.Name,
            _ => Snapshot(KnownResourceStates.Finished, GcOldId));
        Assert.True(await watch.MoveNextAsync());
        Assert.Equal("game-controller-current-key", adapter.GetCurrent(resource)!.ResourceId);
        Assert.Equal(GcNewId, adapter.GetCurrent(resource)!.ContainerId);

        await notifications.PublishUpdateAsync(resource, resource.Name,
            _ => Snapshot(KnownResourceStates.Running, GcOldId));
        var staleMove = watch.MoveNextAsync().AsTask();
        await Assert.ThrowsAsync<TimeoutException>(async () => await staleMove.WaitAsync(TimeSpan.FromMilliseconds(100)));

        Assert.Equal("game-controller-current-key", adapter.GetCurrent(resource)!.ResourceId);
        Assert.Equal(GcNewId, adapter.GetCurrent(resource)!.ContainerId);
        watchTimeout.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await staleMove);
    }

    [Theory]
    [InlineData("game-controller", null)]
    [InlineData("game-controller", GcNewId)]
    [InlineData("game-controller-starting-key", null)]
    [InlineData("game-controller-starting-key", GcNewId)]
    public async Task AspireAdapterAdoptsRunningOpaqueKeyAfterIdlessInitialPlaceholder(
        string initialResourceId,
        string? candidateStartingContainerId)
    {
        var resource = new ContainerResource("game-controller");
        using var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance, lifetime);
        var adapter = new AspireOwnedReconfigurationAdapter(notifications, null!, null!, string.Empty);
        using var watchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var watch = adapter.WatchAsync(watchTimeout.Token).GetAsyncEnumerator(watchTimeout.Token);

        await notifications.PublishUpdateAsync(resource, initialResourceId,
            _ => Snapshot(KnownResourceStates.Starting, null));
        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(initialResourceId, adapter.GetCurrent(resource)!.ResourceId);
        Assert.Null(adapter.GetCurrent(resource)!.ContainerId);

        await notifications.PublishUpdateAsync(resource, "game-controller-running-key",
            _ => Snapshot(KnownResourceStates.Starting, candidateStartingContainerId));
        Assert.True(await watch.MoveNextAsync());
        Assert.Null(adapter.GetCurrent(resource));

        await notifications.PublishUpdateAsync(resource, "game-controller-running-key",
            _ => Snapshot(KnownResourceStates.Running, GcNewId));
        Assert.True(await watch.MoveNextAsync());
        var adopted = adapter.GetCurrent(resource);
        Assert.NotNull(adopted);
        Assert.Equal("game-controller-running-key", adopted.ResourceId);
        Assert.Equal(GcNewId, adopted.ContainerId);

        await notifications.PublishUpdateAsync(resource, initialResourceId,
            _ => Snapshot(KnownResourceStates.Starting, null));
        Assert.Equal("game-controller-running-key", adapter.GetCurrent(resource)!.ResourceId);
        Assert.Equal(GcNewId, adapter.GetCurrent(resource)!.ContainerId);

        await notifications.PublishUpdateAsync(resource, initialResourceId,
            _ => Snapshot(KnownResourceStates.Running, null));
        Assert.Equal("game-controller-running-key", adapter.GetCurrent(resource)!.ResourceId);
        Assert.Equal(GcNewId, adapter.GetCurrent(resource)!.ContainerId);

        var staleMove = watch.MoveNextAsync().AsTask();
        await Assert.ThrowsAsync<TimeoutException>(async () => await staleMove.WaitAsync(TimeSpan.FromMilliseconds(100)));

        var current = adapter.GetCurrent(resource);
        Assert.NotNull(current);
        Assert.Equal("game-controller-running-key", current.ResourceId);
        Assert.Equal(GcNewId, current.ContainerId);
        watchTimeout.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await staleMove);
    }

    private static Task PublishAsync(
        ResourceNotificationService notifications,
        IResource resource,
        OwnedResourceObservation observation) => notifications.PublishUpdateAsync(
        resource,
        observation.ResourceId,
        _ => new CustomResourceSnapshot
        {
            ResourceType = "container",
            State = new ResourceStateSnapshot(observation.State, "fixture"),
            Properties = observation.ContainerId is null
                ? []
                : [new ResourcePropertySnapshot("container.id", observation.ContainerId)],
        });

    private static CustomResourceSnapshot Snapshot(string state, string? containerId) => new()
    {
        ResourceType = "container",
        State = new ResourceStateSnapshot(state, "fixture"),
        Properties = containerId is null
            ? []
            : [new ResourcePropertySnapshot("container.id", containerId)],
    };

    [Fact]
    public async Task OwnedStoppedRefereeCanRetryWithoutAnUnnecessaryStop()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee, refereeInitiallyRunning: false);
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(
            new[] { ("game-controller", KnownResourceCommands.RestartCommand), ("referee-driver", KnownResourceCommands.StartCommand) },
            adapter.LifecycleCommands);
        Assert.Equal("ws://10.0.0.21:8082/api/control", state.GetUriForEnvironment());
    }

    [Fact]
    public async Task StopNotificationWithoutContainerIdCanContinueUsingPreviouslyVerifiedOwnership()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { RemoveRefereeIdOnStop = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("ws://10.0.0.21:8082/api/control", state.GetUriForEnvironment());
        Assert.Equal(RefNewId, adapter.GetCurrent(referee)!.ContainerId);
    }

    [Fact]
    public async Task InitiallyStoppedRefereeWithoutFullIdIsRejectedBeforeLifecycleCommands()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee, refereeInitiallyRunning: false, refereeInitiallyHasId: false);
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(adapter.LifecycleCommands);
        Assert.Equal(OwnedEndpointPhase.Initial, state.Snapshot().Phase);
    }

    [Fact]
    public async Task GameControllerRestartWaitsForRefereeStoppedNotificationAfterStopCommandReturns()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { DelayStopNotification = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var execution = coordinator.ExecuteAsync(CancellationToken.None);
        await adapter.StopCommandEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(30);
        Assert.DoesNotContain(adapter.LifecycleCommands, command => command == ("game-controller", KnownResourceCommands.RestartCommand));

        adapter.PublishReferee(KnownResourceStates.Stopping, RefOldId);
        await Task.Delay(30);
        Assert.DoesNotContain(adapter.LifecycleCommands, command => command == ("game-controller", KnownResourceCommands.RestartCommand));

        adapter.PublishReferee(KnownResourceStates.Finished, RefOldId);
        var result = await execution;

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains(adapter.LifecycleCommands, command => command == ("game-controller", KnownResourceCommands.RestartCommand));
    }

    [Fact]
    public async Task StopTimeoutSuspendsWithoutRestartingSourceOrRestoringDnsFallback()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { DelayStopNotification = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state, TimeSpan.FromMilliseconds(100));

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Canceled);
        Assert.Equal(OwnedEndpointPhase.Suspended, state.Snapshot().Phase);
        Assert.DoesNotContain(adapter.LifecycleCommands, command => command == ("game-controller", KnownResourceCommands.RestartCommand));
        Assert.DoesNotContain(adapter.LifecycleCommands, command => command == ("referee-driver", KnownResourceCommands.StartCommand));
        Assert.Throws<InvalidOperationException>(() => state.GetUriForEnvironment());
    }

    [Fact]
    public async Task CommandSuccessDoesNotFinishUntilRefereeRunningNotificationArrives()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { DelayStartNotification = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var execution = coordinator.ExecuteAsync(CancellationToken.None);
        await adapter.StartCommandEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(30);
        Assert.False(execution.IsCompleted);
        Assert.Equal(OwnedEndpointPhase.Active, state.Snapshot().Phase);

        adapter.PublishReferee(KnownResourceStates.Running, RefNewId);
        Assert.True((await execution).Success);
    }

    [Fact]
    public async Task SourceGenerationChangeDuringInspectDoesNotPublishOrStartDependent()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { ChangeSourceDuringNewInspect = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.DoesNotContain(adapter.LifecycleCommands, command => command == ("referee-driver", KnownResourceCommands.StartCommand));
        Assert.Equal(OwnedEndpointPhase.Suspended, state.Snapshot().Phase);
        Assert.Throws<InvalidOperationException>(() => state.GetUriForEnvironment());
    }

    [Fact]
    public async Task SourceGenerationChangeDuringRestartOwnershipInspectPreventsRestart()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { ChangeSourceDuringRestartOwnershipCheck = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.DoesNotContain(adapter.LifecycleCommands, command => command == ("game-controller", KnownResourceCommands.RestartCommand));
        Assert.Equal(OwnedEndpointPhase.Suspended, state.Snapshot().Phase);
        Assert.Throws<InvalidOperationException>(() => state.GetUriForEnvironment());
    }

    [Fact]
    public async Task SourceGenerationChangeDuringDependentOwnershipInspectPreventsRefereeStart()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { ChangeSourceDuringDependentStartOwnershipCheck = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.DoesNotContain(adapter.LifecycleCommands, command => command == ("referee-driver", KnownResourceCommands.StartCommand));
        Assert.Equal(OwnedEndpointPhase.Suspended, state.Snapshot().Phase);
        Assert.Throws<InvalidOperationException>(() => state.GetUriForEnvironment());
    }

    [Fact]
    public async Task RefereeDriverOnDifferentDcpNetworkIsSuspendedAndStopped()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { MismatchedRefereeNetwork = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(OwnedEndpointPhase.Suspended, state.Snapshot().Phase);
        Assert.Equal(KnownResourceStates.Finished, adapter.GetCurrent(referee)!.State);
        Assert.Throws<InvalidOperationException>(() => state.GetUriForEnvironment());
    }

    [Fact]
    public async Task StaleSourceNotificationIsIgnoredButCurrentUnexpectedGenerationSuspendsAndStopsOwnedReferee()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee);
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);
        Assert.True((await coordinator.ExecuteAsync(CancellationToken.None)).Success);

        var currentSource = adapter.GetCurrent(gc)!;
        await coordinator.HandleUnexpectedSourceObservationAsync(currentSource with { ContainerId = GcOldId });
        Assert.Equal(OwnedEndpointPhase.Active, state.Snapshot().Phase);
        Assert.Single(adapter.LifecycleCommands, command => command == ("referee-driver", KnownResourceCommands.StopCommand));

        var replacement = adapter.PublishSource("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
        await coordinator.HandleUnexpectedSourceObservationAsync(replacement);

        Assert.Equal(OwnedEndpointPhase.Suspended, state.Snapshot().Phase);
        Assert.Equal(KnownResourceStates.Finished, adapter.GetCurrent(referee)!.State);
        Assert.Equal(2, adapter.LifecycleCommands.Count(command => command == ("referee-driver", KnownResourceCommands.StopCommand)));
        Assert.Throws<InvalidOperationException>(() => state.GetUriForEnvironment());
    }

    [Fact]
    public async Task InitialMissingIdAndFirstRunningNotificationsKeepDnsFallback()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee);
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);

        await coordinator.HandleUnexpectedSourceObservationAsync(adapter.PublishSource(null, "Starting"));
        await coordinator.HandleUnexpectedSourceObservationAsync(adapter.PublishSource(GcOldId, KnownResourceStates.Running));

        Assert.Equal(OwnedEndpointPhase.Initial, state.Snapshot().Phase);
        Assert.Equal("ws://game-controller:8082/api/control", state.GetUriForEnvironment());
        Assert.Empty(adapter.LifecycleCommands);
    }

    [Fact]
    public async Task LateStartAfterCancellationIsObservedAndStopped()
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee) { DelayStartCompletion = true };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state);
        using var cancellation = new CancellationTokenSource();

        var execution = coordinator.ExecuteAsync(cancellation.Token);
        await adapter.StartCommandEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        var result = await execution;
        Assert.True(result.Canceled);

        var blockedRetry = await coordinator.ExecuteAsync(CancellationToken.None);
        Assert.False(blockedRetry.Success);
        Assert.Single(adapter.LifecycleCommands, command => command == ("referee-driver", KnownResourceCommands.StopCommand));

        adapter.AllowStartCompletion.TrySetResult();
        await adapter.StartCommandCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        for (var attempt = 0; attempt < 50 && adapter.GetCurrent(referee)!.State != KnownResourceStates.Finished; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.False(result.Success);
        Assert.Equal(OwnedEndpointPhase.Suspended, state.Snapshot().Phase);
        Assert.Equal(KnownResourceStates.Finished, adapter.GetCurrent(referee)!.State);
        Assert.Equal(2, adapter.LifecycleCommands.Count(command => command == ("referee-driver", KnownResourceCommands.StopCommand)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationOrTimeoutWhileStartNotificationIsDelayedKeepsNextCommandBlockedUntilLateContainerStops(bool cancel)
    {
        var gc = new ContainerResource("game-controller");
        var referee = new ContainerResource("referee-driver");
        var adapter = new FakeAdapter(gc, referee)
        {
            DelayStartNotification = true,
            DelayStopOnCommandNumber = 2,
        };
        var state = new OwnedRefereeEndpointState("ws://game-controller:8082/api/control");
        var coordinator = CreateCoordinator(adapter, gc, referee, state, TimeSpan.FromMilliseconds(250));
        using var cancellation = new CancellationTokenSource();

        var execution = coordinator.ExecuteAsync(cancellation.Token);
        await adapter.StartCommandCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        if (cancel)
        {
            cancellation.Cancel();
        }
        var result = await execution;
        Assert.Equal(cancel, result.Canceled);
        Assert.False(result.Success);

        var beforeLateRunning = await coordinator.ExecuteAsync(CancellationToken.None);
        Assert.False(beforeLateRunning.Success);
        Assert.Equal(1, adapter.LifecycleCommands.Count(command => command == ("game-controller", KnownResourceCommands.RestartCommand)));
        Assert.Equal(1, adapter.LifecycleCommands.Count(command => command == ("referee-driver", KnownResourceCommands.StartCommand)));

        adapter.PublishReferee(KnownResourceStates.Running, RefNewId);
        await adapter.LateStopCommandEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var beforeLateStop = await coordinator.ExecuteAsync(CancellationToken.None);
        Assert.False(beforeLateStop.Success);
        Assert.Equal(2, adapter.LifecycleCommands.Count(command => command == ("referee-driver", KnownResourceCommands.StopCommand)));
        Assert.Equal(1, adapter.LifecycleCommands.Count(command => command == ("game-controller", KnownResourceCommands.RestartCommand)));

        adapter.PublishReferee(KnownResourceStates.Stopping, RefNewId);
        Assert.False((await coordinator.ExecuteAsync(CancellationToken.None)).Success);
        adapter.PublishReferee(KnownResourceStates.Finished, RefNewId);
    }

    [Theory]
    [InlineData("duck-net", "duck-net", "10.0.0.21", true)]
    [InlineData("other-net", "duck-net", "10.0.0.21", false)]
    [InlineData("duck-net", "duck-net", "127.0.0.1", false)]
    [InlineData("duck-net", "duck-net", "not-an-ip", false)]
    [InlineData("duck-net", "duck-net", "2001:db8::1", false)]
    public void NetworkEndpointRequiresOneMatchingBridgeNetworkAndRoutableIpv4(
        string networkMode,
        string networkName,
        string address,
        bool expected)
    {
        var json = JsonSerializer.Serialize(new
        {
            HostConfig = new { NetworkMode = networkMode },
            NetworkSettings = new
            {
                Networks = new Dictionary<string, object>
                {
                    [networkName] = new { IPAddress = address },
                },
            },
        });

        Assert.Equal(expected, OwnedContainerInspection.TryReadNetworkEndpoint(json, out _));
    }

    [Fact]
    public void LegacyNetworkEndpointDoesNotAdoptStandardDcpNetworkModeForms()
    {
        foreach (var networkMode in new[] { "default", new string('b', 64) })
        {
            var json = JsonSerializer.Serialize(new
            {
                HostConfig = new { NetworkMode = networkMode },
                NetworkSettings = new
                {
                    Networks = new Dictionary<string, object>
                    {
                        ["duck-net"] = new { IPAddress = "10.0.0.21", NetworkID = new string('b', 64) },
                    },
                },
            });

            Assert.False(OwnedContainerInspection.TryReadNetworkEndpoint(json, out _));
        }
    }

    private static OwnedRefereeReconfigurationCoordinator CreateCoordinator(
        IOwnedReconfigurationAdapter adapter,
        IResource gc,
        IResource referee,
        OwnedRefereeEndpointState state,
        TimeSpan? timeout = null) =>
        new(adapter, gc, referee, state, RunId, EngineId, timeout ?? TimeSpan.FromSeconds(1));

    private sealed class FakeAdapter(
        ContainerResource gc,
        ContainerResource referee,
        bool refereeInitiallyRunning = true,
        bool refereeInitiallyHasId = true,
        bool useOpaqueResourceIds = false,
        bool switchToOpaqueIdsOnRecreation = false) : IOwnedReconfigurationAdapter
    {
        private readonly Dictionary<string, OwnedResourceObservation> current = new(StringComparer.Ordinal)
        {
            [gc.Name] = new(gc, useOpaqueResourceIds ? $"{gc.Name}-opaque" : gc.Name, KnownResourceStates.Running, GcOldId),
            [referee.Name] = new(
                referee,
                useOpaqueResourceIds ? $"{referee.Name}-opaque" : referee.Name,
                refereeInitiallyRunning ? KnownResourceStates.Running : KnownResourceStates.Finished,
                refereeInitiallyHasId ? RefOldId : null),
        };
        private readonly Dictionary<string, string> inspections = new(StringComparer.Ordinal);
        private int oldGameControllerInspectCount;
        private int oldRefereeInspectCount;
        private readonly Queue<OwnedResourceObservation> notifications = new();

        public string EngineId { get; init; } = OwnedRefereeReconfigurationTests.EngineId;
        public bool ForeignGameControllerOwner { get; init; }
        public bool ChangeSourceDuringNewInspect { get; init; }
        public bool ChangeSourceDuringRestartOwnershipCheck { get; init; }
        public bool ChangeSourceDuringDependentStartOwnershipCheck { get; init; }
        public bool MismatchedRefereeNetwork { get; init; }
        public bool RemoveRefereeIdOnStop { get; init; }
        public bool DelayStartCompletion { get; init; }
        public bool DelayStartNotification { get; set; }
        public bool DelayStopNotification { get; init; }
        public int? DelayStopOnCommandNumber { get; init; }
        public bool UseOpaqueResourceIds { get; } = useOpaqueResourceIds;
        public bool SwitchToOpaqueIdsOnRecreation { get; } = switchToOpaqueIdsOnRecreation;
        public TaskCompletionSource StartCommandEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StartCommandCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowStartCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StopCommandEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LateStopCommandEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondStopCommandEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<(string Resource, string Command)> LifecycleCommands { get; } = [];

        public OwnedResourceObservation? GetCurrent(IResource resource) =>
            current.TryGetValue(resource.Name, out var value) && ReferenceEquals(value.Resource, resource) ? value : null;

        public OwnedResourceObservation? GetCurrent(string resourceId) => current.GetValueOrDefault(resourceId);

        public async IAsyncEnumerable<OwnedResourceObservation> WatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (notifications.TryDequeue(out var observation))
                {
                    yield return observation;
                }
                else
                {
                    await Task.Delay(10, cancellationToken);
                }
            }
        }

        public async Task<bool> ExecuteLifecycleCommandAsync(IResource resource, string commandName, CancellationToken cancellationToken)
        {
            LifecycleCommands.Add((resource.Name, commandName));
            if (ReferenceEquals(resource, referee) && commandName == KnownResourceCommands.StopCommand)
            {
                var stopCount = LifecycleCommands.Count(command => command == (referee.Name, KnownResourceCommands.StopCommand));
                if (stopCount == 2)
                {
                    SecondStopCommandEntered.TrySetResult();
                }
                if (DelayStopNotification || stopCount == DelayStopOnCommandNumber)
                {
                    if (stopCount == DelayStopOnCommandNumber)
                    {
                        LateStopCommandEntered.TrySetResult();
                    }
                    else
                    {
                        StopCommandEntered.TrySetResult();
                    }
                }
                else
                {
                    Set(referee, KnownResourceStates.Finished, RemoveRefereeIdOnStop ? null : GetCurrent(referee)?.ContainerId ?? RefOldId);
                }
            }
            else if (ReferenceEquals(resource, gc) && commandName == KnownResourceCommands.RestartCommand)
            {
                Set(gc, KnownResourceStates.Running, GcNewId);
            }
            else if (ReferenceEquals(resource, referee) && commandName == KnownResourceCommands.StartCommand)
            {
                if (DelayStartCompletion)
                {
                    StartCommandEntered.TrySetResult();
                    await AllowStartCompletion.Task;
                }

                if (DelayStartNotification)
                {
                    StartCommandEntered.TrySetResult();
                }
                else
                {
                    Set(referee, KnownResourceStates.Running, RefNewId);
                }
                StartCommandCompleted.TrySetResult();
            }
            else
            {
                return false;
            }

            return true;
        }

        public Task<string> CaptureDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            if (arguments.Contains("info"))
            {
                return Task.FromResult(EngineId);
            }

            var id = arguments[^1];
            if (id == GcOldId)
            {
                oldGameControllerInspectCount++;
            }
            if (id == RefOldId)
            {
                oldRefereeInspectCount++;
            }

            if (!inspections.TryGetValue(id, out var json))
            {
                var observation = current.Values.SingleOrDefault(value => value.ContainerId == id);
                var role = observation?.Resource.Name == "game-controller" ? "game-controller" : "referee-driver";
                json = Inspection(
                    id,
                    role,
                    id == GcNewId ? "10.0.0.21" : "10.0.0.20",
                    ForeignGameControllerOwner && id == GcOldId ? "other-run" : null,
                    MismatchedRefereeNetwork && id == RefNewId ? "other-net" : "duck-net");
                inspections[id] = json;
            }

            if (ChangeSourceDuringRestartOwnershipCheck && id == GcOldId &&
                oldGameControllerInspectCount == 3 &&
                LifecycleCommands.Count(command => command == ("referee-driver", KnownResourceCommands.StopCommand)) == 1)
            {
                Set(gc, KnownResourceStates.Running, "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
            }

            if (ChangeSourceDuringDependentStartOwnershipCheck && id == RefOldId &&
                oldRefereeInspectCount == 3 &&
                LifecycleCommands.Count(command => command == ("game-controller", KnownResourceCommands.RestartCommand)) == 1)
            {
                Set(gc, KnownResourceStates.Running, "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
            }

            if (ChangeSourceDuringNewInspect && id == GcNewId)
            {
                Set(gc, KnownResourceStates.Running, "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
            }

            return Task.FromResult(json);
        }

        private void Set(ContainerResource resource, string state, string? id)
        {
            var resourceId = ResourceIdFor(resource, id);
            var observation = new OwnedResourceObservation(resource, resourceId, state, id);
            current[resource.Name] = observation;
            notifications.Enqueue(observation);
        }

        private string ResourceIdFor(ContainerResource resource, string? containerId)
        {
            if (UseOpaqueResourceIds)
            {
                return $"{resource.Name}-opaque";
            }

            if (!SwitchToOpaqueIdsOnRecreation || containerId == GcOldId || containerId == RefOldId)
            {
                return resource.Name;
            }

            return containerId == GcNewId || containerId == RefNewId
                ? $"{resource.Name}-opaque"
                : $"{resource.Name}-opaque-next";
        }

        public OwnedResourceObservation PublishSource(string? id, string? state = null)
        {
            Set(gc, state ?? KnownResourceStates.Running, id);
            return current[gc.Name];
        }

        public OwnedResourceObservation PublishReferee(string state, string? id)
        {
            Set(referee, state, id);
            return current[referee.Name];
        }

        private static string Inspection(
            string id,
            string role,
            string address,
            string? runIdOverride = null,
            string networkName = "duck-net")
        {
            var labels = new Dictionary<string, string>
            {
                [ContainerIdentityExecHandoff.RunIdLabel] = runIdOverride ?? RunId,
                [ContainerIdentityExecHandoff.RoleLabel] = role,
            };
            return JsonSerializer.Serialize(new
            {
                Id = id,
                State = new { Status = "running" },
                Config = new { Labels = labels },
                HostConfig = new { NetworkMode = networkName },
                NetworkSettings = new
                {
                    Networks = new Dictionary<string, object>
                    {
                        [networkName] = new { IPAddress = address },
                    },
                },
            });
        }
    }

    private sealed class ResourceNotificationFakeAdapter(
        ResourceNotificationService notifications,
        FakeAdapter commands) : IOwnedReconfigurationAdapter
    {
        private readonly AspireOwnedReconfigurationAdapter notificationAdapter =
            new(notifications, null!, null!, string.Empty);

        public OwnedResourceObservation? GetCurrent(IResource resource) => notificationAdapter.GetCurrent(resource);

        public OwnedResourceObservation? GetCurrent(string resourceId) => notificationAdapter.GetCurrent(resourceId);

        public IAsyncEnumerable<OwnedResourceObservation> WatchAsync(CancellationToken cancellationToken) =>
            notificationAdapter.WatchAsync(cancellationToken);

        public async Task<bool> ExecuteLifecycleCommandAsync(
            IResource resource,
            string commandName,
            CancellationToken cancellationToken)
        {
            if (commandName == KnownResourceCommands.RestartCommand)
            {
                var previous = commands.GetCurrent(resource);
                if (previous is not null)
                {
                    await PublishAsync(notifications, resource, previous with { State = KnownResourceStates.Finished });
                }
            }

            var succeeded = await commands.ExecuteLifecycleCommandAsync(resource, commandName, cancellationToken);
            var current = commands.GetCurrent(resource);
            if (succeeded && current is not null)
            {
                await PublishAsync(notifications, resource, current);
            }

            return succeeded;
        }

        public Task<string> CaptureDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
            commands.CaptureDockerAsync(arguments, cancellationToken);
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
}
