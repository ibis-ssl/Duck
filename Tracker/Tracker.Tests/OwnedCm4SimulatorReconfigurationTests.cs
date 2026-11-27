using Duck.Testing.AppHost;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tracker.Tests;

public sealed class OwnedCm4SimulatorReconfigurationTests
{
    [Fact]
    public async Task DcpAdapterDoesNotDispatchRestartWhenCallerIsAlreadyCanceled()
    {
        var restartCalls = 0;
        var builder = DistributedApplication.CreateBuilder([]);
        var simulator = builder.AddContainer("simulator", "example/simulator")
            .WithCommand(
                KnownResourceCommands.RestartCommand,
                "Restart simulator",
                _ =>
                {
                    Interlocked.Increment(ref restartCalls);
                    return Task.FromResult(new ExecuteCommandResult { Success = true });
                })
            .Resource;
        await using var application = builder.Build();
        var adapter = new DcpOwnedCm4SimulatorReconfigurationAdapter(
            null!,
            null!,
            null!,
            application.Services.GetRequiredService<ResourceNotificationService>(),
            simulator,
            simulator,
            "engine-01");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.RestartSimulatorAsync(
                _ =>
                {
                    Interlocked.Increment(ref restartCalls);
                    return Task.FromResult(new ExecuteCommandResult { Success = true });
                },
                CreateCommandContext(cancellation.Token),
                cancellation.Token));
        Assert.Equal(0, restartCalls);
    }

    [Fact]
    public async Task RestartGuardCallsTheCapturedCommandOnceAndReturnsItsResult()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var simulator = builder.AddContainer("simulator", "example/simulator");
        var previous = CreatePair(1, "a");
        var replacement = CreatePair(2, "b");
        var adapter = new FakeAdapter(previous, replacement);
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var handler = new OwnedCm4SimulatorRestartCommandHandler(coordinator);
        var restartCalls = 0;
        var originalCommand = new ResourceCommandAnnotation(
            KnownResourceCommands.RestartCommand,
            "Restart simulator",
            _ => ResourceCommandState.Enabled,
            context =>
            {
                restartCalls++;
                Assert.Equal("simulator", context.ResourceName);
                Assert.NotNull(context.Arguments);
                adapter.Events.Add("original");
                return Task.FromResult(new ExecuteCommandResult { Success = true, Message = "restart result" });
            },
            "Restart the simulator",
            arguments: null,
            confirmationMessage: null,
            iconName: "ArrowCounterclockwise",
            iconVariant: IconVariant.Regular,
            isHighlighted: false);
        simulator.Resource.Annotations.Add(originalCommand);

        await using var application = builder.Build();
        handler.Install(simulator.Resource);
        handler.Install(simulator.Resource);
        var guardedCommand = Assert.Single(
            simulator.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            command => command.Name == KnownResourceCommands.RestartCommand);
        Assert.NotSame(originalCommand, guardedCommand);
        Assert.Same(originalCommand.UpdateState, guardedCommand.UpdateState);
        Assert.Equal(originalCommand.Visibility, guardedCommand.Visibility);

        var result = await application.Services.GetRequiredService<ResourceCommandService>()
            .ExecuteCommandAsync(simulator.Resource, KnownResourceCommands.RestartCommand, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("restart result", result.Message);
        Assert.Equal(1, restartCalls);
        Assert.Equal(["invalidate", "restart", "original", "wait-replacement", "apply"], adapter.Events);
        Assert.Equal(replacement, controller.LastAppliedPair);
    }

    [Fact]
    public async Task ReconfigurationInvalidatesBeforeRestartAndAppliesOnlyTheReplacement()
    {
        var previous = CreatePair(1, "a");
        var replacement = CreatePair(2, "b");
        var adapter = new FakeAdapter(previous, replacement);
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");

        var result = await ExecuteAsync(coordinator, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["invalidate", "restart", "wait-replacement", "apply"], adapter.Events);
        Assert.Equal(Cm4TransportTargetPhase.Applied, controller.Snapshot().Phase);
        Assert.Equal(replacement, controller.LastAppliedPair);
    }

    [Fact]
    public async Task Cm4RestartInvalidatesThenWaitsForBothOwnedReplacementsBeforeApplyingOnce()
    {
        var previous = CreatePair(4, "a");
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2),
            previous.Simulator);
        var afterSimulator = new OwnedCm4TransportIdentityPair(
            afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 5));
        var adapter = new FakeAdapter(previous, afterSimulator) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var cm4RestartCalls = 0;
        var simulatorRestartCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ =>
            {
                cm4RestartCalls++;
                adapter.Events.Add("restart-cm4");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            context =>
            {
                simulatorRestartCalls++;
                Assert.Equal("simulator", context.ResourceName);
                adapter.Events.Add("restart-simulator");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            });

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, cm4RestartCalls);
        Assert.Equal(1, simulatorRestartCalls);
        Assert.Equal(["invalidate", "restart-cm4", "wait-cm4", "restart-simulator", "wait-replacement", "apply"], adapter.Events);
        Assert.Equal(afterSimulator, controller.LastAppliedPair);
    }

    [Fact]
    public async Task StandardCm4RestartRunsCapturedCraneRestartAfterCurrentTargetApply()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var simulator = builder.AddContainer("simulator", "example/simulator");
        var cm4 = builder.AddContainer("cm4-sim", "example/cm4");
        var crane = builder.AddContainer("crane", "example/crane");
        var previous = CreatePair(4, "a");
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2),
            previous.Simulator);
        var afterSimulator = new OwnedCm4TransportIdentityPair(
            afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 5));
        var adapter = new FakeAdapter(previous, afterSimulator) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var handler = new OwnedCm4SimulatorRestartCommandHandler(coordinator);
        var cm4Calls = 0;
        var simulatorCalls = 0;
        var craneCalls = 0;
        CancellationToken? operationToken = null;
        AddRestartCommand(cm4.Resource, "Restart CM4", context =>
        {
            cm4Calls++;
            operationToken = context.CancellationToken;
            adapter.Events.Add("restart-cm4");
            return Task.FromResult(new ExecuteCommandResult { Success = true });
        });
        AddRestartCommand(simulator.Resource, "Restart simulator", context =>
        {
            simulatorCalls++;
            adapter.Events.Add("restart-simulator");
            return Task.FromResult(new ExecuteCommandResult { Success = true });
        });
        var craneCommand = AddRestartCommand(crane.Resource, "Restart Crane", context =>
        {
            craneCalls++;
            Assert.Equal("crane", context.ResourceName);
            Assert.Equal(operationToken, context.CancellationToken);
            adapter.Events.Add("restart-crane");
            return Task.FromResult(new ExecuteCommandResult { Success = true });
        });

        await using var application = builder.Build();
        handler.Install(simulator.Resource);
        handler.InstallCm4(cm4.Resource, simulator.Resource, crane.Resource);

        var result = await application.Services.GetRequiredService<ResourceCommandService>()
            .ExecuteCommandAsync(cm4.Resource, KnownResourceCommands.RestartCommand, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, cm4Calls);
        Assert.Equal(1, simulatorCalls);
        Assert.Equal(1, craneCalls);
        Assert.Same(craneCommand, Assert.Single(crane.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            command => command.Name == KnownResourceCommands.RestartCommand));
        Assert.Equal(["invalidate", "restart-cm4", "wait-cm4", "restart-simulator", "wait-replacement", "apply", "restart-crane"], adapter.Events);
    }

    [Theory]
    [InlineData("Stopped", false, false)]
    [InlineData("Starting", false, false)]
    [InlineData("Running", true, false)]
    [InlineData("Running", false, true)]
    public async Task DcpAdapterRequiresCurrentCraneNotificationToMatchOwnedCandidate(
        string currentState,
        bool expectedCurrent,
        bool replaceContainerId)
    {
        var ownership = new DcpBaseResourceOwnershipEventConsumerTests.OwnershipFixture();
        await ownership.VerifyAsync();
        var candidates = ownership.Registry.GetCandidates();
        var simulatorCandidate = Assert.Single(candidates, candidate => candidate.Role == "simulator");
        var cm4Candidate = Assert.Single(candidates, candidate => candidate.Role == "cm4-sim");
        var craneCandidate = Assert.Single(candidates, candidate => candidate.Role == "crane");
        Assert.True(craneCandidate.OwnershipVerified);

        var builder = DistributedApplication.CreateBuilder([]);
        await using var application = builder.Build();
        var notifications = application.Services.GetRequiredService<ResourceNotificationService>();
        var notifiedContainerId = replaceContainerId ? new string('f', 64) : craneCandidate.ContainerId!;
        await notifications.PublishUpdateAsync(craneCandidate.Resource, craneCandidate.ResourceId!, snapshot => snapshot with
        {
            ResourceType = "container",
            State = new ResourceStateSnapshot(currentState, "fixture"),
            Properties = [new ResourcePropertySnapshot("container.id", notifiedContainerId)],
        });

        var adapter = new DcpOwnedCm4SimulatorReconfigurationAdapter(
            ownership.Registry,
            ownership.OwnershipConsumer,
            new DcpBaseResourceReadinessEventConsumer(ownership.Registry),
            notifications,
            simulatorCandidate.Resource,
            cm4Candidate.Resource,
            "expected-engine",
            crane: craneCandidate.Resource);

        Assert.Equal(expectedCurrent, adapter.IsCurrentOwnedCrane());
        if (expectedCurrent)
        {
            return;
        }

        var controller = new FakeController([], adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var cm4Calls = 0;
        var simulatorCalls = 0;
        var craneCalls = 0;
        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ =>
            {
                cm4Calls++;
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            _ =>
            {
                simulatorCalls++;
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            _ =>
            {
                craneCalls++;
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            });

        Assert.False(result.Success);
        Assert.Equal(0, cm4Calls);
        Assert.Equal(0, simulatorCalls);
        Assert.Equal(0, craneCalls);
    }

    [Fact]
    public async Task Cm4RestartDoesNotDispatchCraneRestartWithoutCurrentOwnership()
    {
        var previous = CreatePair(4, "a");
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2),
            previous.Simulator);
        var replacement = new OwnedCm4TransportIdentityPair(
            afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 5));
        var adapter = new FakeAdapter(previous, replacement)
        {
            Cm4Replacement = afterCm4,
            CraneOwned = false,
        };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var craneCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ =>
            {
                adapter.Events.Add("restart-cm4");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            _ =>
            {
                adapter.Events.Add("restart-simulator");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            _ =>
            {
                craneCalls++;
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            });

        Assert.False(result.Success);
        Assert.Equal(0, craneCalls);
        Assert.Empty(adapter.Events);
    }

    [Fact]
    public async Task Cm4RestartCancellationAfterApplyDoesNotDispatchCraneRestart()
    {
        var previous = CreatePair(4, "a");
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2), previous.Simulator);
        var replacement = new OwnedCm4TransportIdentityPair(
            afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 5));
        using var cancellation = new CancellationTokenSource();
        var adapter = new FakeAdapter(previous, replacement) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair)
        {
            AfterApply = cancellation.Cancel,
        };
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var craneCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(cancellation.Token),
            _ => { adapter.Events.Add("restart-cm4"); return Task.FromResult(new ExecuteCommandResult { Success = true }); },
            _ => { adapter.Events.Add("restart-simulator"); return Task.FromResult(new ExecuteCommandResult { Success = true }); },
            _ => { craneCalls++; return Task.FromResult(new ExecuteCommandResult { Success = true }); });

        Assert.False(result.Success);
        Assert.True(result.Canceled);
        Assert.Equal(0, craneCalls);
        Assert.Equal("apply", adapter.Events[^1]);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task Cm4RestartDeadlineDuringApplyDoesNotDispatchCraneRestart()
    {
        var previous = CreatePair(4, "a");
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2), previous.Simulator);
        var replacement = new OwnedCm4TransportIdentityPair(
            afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 5));
        var adapter = new FakeAdapter(previous, replacement) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair) { BlockApply = true };
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(
            adapter, controller, "run-01", TimeSpan.FromMilliseconds(100));
        var craneCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ => { adapter.Events.Add("restart-cm4"); return Task.FromResult(new ExecuteCommandResult { Success = true }); },
            _ => { adapter.Events.Add("restart-simulator"); return Task.FromResult(new ExecuteCommandResult { Success = true }); },
            _ => { craneCalls++; return Task.FromResult(new ExecuteCommandResult { Success = true }); });

        Assert.False(result.Success);
        Assert.False(result.Canceled);
        Assert.Equal(0, craneCalls);
        Assert.Equal("apply", adapter.Events[^1]);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task Cm4RestartAcceptsFreshProofForSameSimulatorIdentity()
    {
        var previous = CreatePair(4, "a");
        var refreshedSimulator = previous.Simulator with { Generation = previous.Simulator.Generation + 1 };
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2),
            refreshedSimulator);
        var afterSimulator = new OwnedCm4TransportIdentityPair(
            afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 6));
        var adapter = new FakeAdapter(previous, afterSimulator) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var simulatorRestartCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ =>
            {
                adapter.Events.Add("restart-cm4");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            _ =>
            {
                simulatorRestartCalls++;
                adapter.Events.Add("restart-simulator");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            });

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, simulatorRestartCalls);
        Assert.Equal(["invalidate", "restart-cm4", "wait-cm4", "restart-simulator", "wait-replacement", "apply"], adapter.Events);
        Assert.Equal(afterSimulator, controller.LastAppliedPair);
    }

    [Fact]
    public async Task Cm4RestartDoesNotAcceptAReplacementSimulatorIdentityBeforeItsRestart()
    {
        var previous = CreatePair(4, "a");
        var changedSimulator = previous.Simulator with
        {
            ResourceId = "different-simulator-key",
            ContainerId = new string('f', 64),
            Generation = previous.Simulator.Generation + 1,
        };
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2),
            changedSimulator);
        var finalPair = new OwnedCm4TransportIdentityPair(afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 6));
        var adapter = new FakeAdapter(previous, finalPair) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var simulatorRestartCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ =>
            {
                adapter.Events.Add("restart-cm4");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            _ =>
            {
                simulatorRestartCalls++;
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            });

        Assert.False(result.Success);
        Assert.Equal(0, simulatorRestartCalls);
        Assert.Equal(["invalidate", "restart-cm4", "wait-cm4"], adapter.Events);
        Assert.Null(controller.LastAppliedPair);
        Assert.Equal(Cm4TransportTargetPhase.Invalidated, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task Cm4RestartAcceptsFreshProofForSameCm4AfterSimulatorReplacement()
    {
        var previous = CreatePair(4, "a");
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2),
            previous.Simulator);
        var refreshedCm4 = afterCm4.Cm4 with { Generation = afterCm4.Cm4.Generation + 1 };
        var replacement = new OwnedCm4TransportIdentityPair(
            refreshedCm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 6));
        var adapter = new FakeAdapter(previous, replacement) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ =>
            {
                adapter.Events.Add("restart-cm4");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            _ =>
            {
                adapter.Events.Add("restart-simulator");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            });

        Assert.True(result.Success, result.Message);
        Assert.Equal(["invalidate", "restart-cm4", "wait-cm4", "restart-simulator", "wait-replacement", "apply"], adapter.Events);
        Assert.Equal(replacement, controller.LastAppliedPair);
    }

    [Fact]
    public async Task UnknownCm4InvalidationDoesNotDispatchEitherRestartOrApply()
    {
        var previous = CreatePair(4, "a");
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2),
            previous.Simulator);
        var finalPair = new OwnedCm4TransportIdentityPair(afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 5));
        var adapter = new FakeAdapter(previous, finalPair) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair)
        {
            InvalidationResult = new(false, true, 1, "unknown"),
        };
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var cm4RestartCalls = 0;
        var simulatorRestartCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ => { cm4RestartCalls++; return Task.FromResult(new ExecuteCommandResult { Success = true }); },
            _ => { simulatorRestartCalls++; return Task.FromResult(new ExecuteCommandResult { Success = true }); });

        Assert.False(result.Success);
        Assert.Equal(0, cm4RestartCalls);
        Assert.Equal(0, simulatorRestartCalls);
        Assert.Equal(["invalidate"], adapter.Events);
        Assert.Null(controller.LastAppliedPair);
    }

    [Fact]
    public async Task FailedCm4RestartDoesNotRestartSimulatorOrApplyTarget()
    {
        var previous = CreatePair(4, "a");
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2), previous.Simulator);
        var finalPair = new OwnedCm4TransportIdentityPair(afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 5));
        var adapter = new FakeAdapter(previous, finalPair) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var simulatorRestartCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ =>
            {
                adapter.Events.Add("restart-cm4");
                return Task.FromResult(new ExecuteCommandResult { Success = false, Message = "restart failed" });
            },
            _ =>
            {
                simulatorRestartCalls++;
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            });

        Assert.False(result.Success);
        Assert.Equal(["invalidate", "restart-cm4"], adapter.Events);
        Assert.Equal(0, simulatorRestartCalls);
        Assert.Null(controller.LastAppliedPair);
        Assert.Equal(Cm4TransportTargetPhase.Invalidated, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task CancellationAfterCm4RestartDoesNotDispatchSimulatorRestartOrApply()
    {
        var previous = CreatePair(4, "a");
        var afterCm4 = new OwnedCm4TransportIdentityPair(
            new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('c', 63) + "c", 2), previous.Simulator);
        var finalPair = new OwnedCm4TransportIdentityPair(afterCm4.Cm4,
            new("run-01", "engine-01", "simulator", "sim-key-b", new string('d', 63) + "d", 5));
        var adapter = new FakeAdapter(previous, finalPair) { Cm4Replacement = afterCm4 };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        using var cancellation = new CancellationTokenSource();
        var simulatorRestartCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(cancellation.Token),
            _ =>
            {
                adapter.Events.Add("restart-cm4");
                cancellation.Cancel();
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            _ =>
            {
                simulatorRestartCalls++;
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            });

        Assert.False(result.Success);
        Assert.True(result.Canceled);
        Assert.Equal(["invalidate", "restart-cm4"], adapter.Events);
        Assert.Equal(0, simulatorRestartCalls);
        Assert.Null(controller.LastAppliedPair);
        Assert.Equal(Cm4TransportTargetPhase.Invalidated, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task UnconfirmedCm4ReplacementDoesNotRestartSimulatorOrApply()
    {
        var previous = CreatePair(4, "a");
        var invalidReplacement = new OwnedCm4TransportIdentityPair(previous.Cm4, previous.Simulator);
        var finalPair = CreatePair(5, "d");
        var adapter = new FakeAdapter(previous, finalPair) { Cm4Replacement = invalidReplacement };
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");
        var simulatorRestartCalls = 0;

        var result = await coordinator.ExecuteCm4RestartAsync(
            CreateCommandContext(CancellationToken.None),
            _ =>
            {
                adapter.Events.Add("restart-cm4");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            _ =>
            {
                simulatorRestartCalls++;
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            });

        Assert.False(result.Success);
        Assert.Equal(["invalidate", "restart-cm4", "wait-cm4"], adapter.Events);
        Assert.Equal(0, simulatorRestartCalls);
        Assert.Null(controller.LastAppliedPair);
    }

    [Fact]
    public async Task UnknownRemoteInvalidationDoesNotRestartSimulator()
    {
        var pair = CreatePair(1, "a");
        var adapter = new FakeAdapter(pair, CreatePair(2, "b"));
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair)
        {
            InvalidationResult = new(false, true, 1, "unknown"),
        };
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");

        var result = await ExecuteAsync(coordinator, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(["invalidate"], adapter.Events);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task CancellationAfterInvalidationDoesNotRestartOrRestoreTarget()
    {
        var pair = CreatePair(1, "a");
        using var cancellation = new CancellationTokenSource();
        var adapter = new FakeAdapter(pair, CreatePair(2, "b"));
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair)
        {
            AfterInvalidation = cancellation.Cancel,
        };
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");

        var result = await ExecuteAsync(coordinator, cancellation.Token);

        Assert.False(result.Success);
        Assert.True(result.Canceled);
        Assert.Equal(["invalidate"], adapter.Events);
        Assert.Equal(Cm4TransportTargetPhase.Invalidated, controller.Snapshot().Phase);
        Assert.Null(controller.LastAppliedPair);
    }

    [Fact]
    public async Task CancellationAfterInvalidationAckDoesNotDispatchRestart()
    {
        var pair = CreatePair(1, "a");
        using var cancellation = new CancellationTokenSource();
        var adapter = new FakeAdapter(pair, CreatePair(2, "b"));
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair)
        {
            AfterInvalidation = cancellation.Cancel,
        };
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");

        var result = await ExecuteAsync(coordinator, cancellation.Token);

        Assert.False(result.Success);
        Assert.True(result.Canceled);
        Assert.Equal(0, adapter.RestartDispatchAttempts);
        Assert.Equal(["invalidate"], adapter.Events);
        Assert.Equal(Cm4TransportTargetPhase.Invalidated, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task CancellationAfterFinalApplyAckDoesNotReturnSuccess()
    {
        var previous = CreatePair(1, "a");
        var replacement = CreatePair(2, "b");
        using var cancellation = new CancellationTokenSource();
        var adapter = new FakeAdapter(previous, replacement);
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair)
        {
            AfterApply = cancellation.Cancel,
        };
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");

        var result = await ExecuteAsync(coordinator, cancellation.Token);

        Assert.False(result.Success);
        Assert.True(result.Canceled);
        Assert.Equal(["invalidate", "restart", "wait-replacement", "apply"], adapter.Events);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task OperationDeadlineDuringFinalApplyDoesNotReturnSuccess()
    {
        var previous = CreatePair(1, "a");
        var replacement = CreatePair(2, "b");
        var adapter = new FakeAdapter(previous, replacement);
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair)
        {
            BlockApply = true,
        };
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(
            adapter, controller, "run-01", TimeSpan.FromMilliseconds(100));

        var result = await ExecuteAsync(coordinator, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Canceled);
        Assert.Equal(["invalidate", "restart", "wait-replacement", "apply"], adapter.Events);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
        Assert.Null(controller.LastAppliedPair);
    }

    [Fact]
    public async Task UnknownReplacementApplyDoesNotRestorePriorTarget()
    {
        var previous = CreatePair(1, "a");
        var replacement = CreatePair(2, "b");
        var adapter = new FakeAdapter(previous, replacement);
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair)
        {
            FinalApplyResult = new(false, true, 2, "unknown"),
        };
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");

        var result = await ExecuteAsync(coordinator, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(["invalidate", "restart", "wait-replacement", "apply"], adapter.Events);
        Assert.Null(controller.LastAppliedPair);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
    }

    private static OwnedCm4TransportIdentityPair CreatePair(long generation, string suffix) => new(
        new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('a', 64), 1),
        new("run-01", "engine-01", "simulator", "sim-key-" + suffix, new string('b', 63) + suffix, generation));

    private static Task<OwnedCm4SimulatorReconfigurationResult> ExecuteAsync(
        OwnedCm4SimulatorReconfigurationCoordinator coordinator,
        CancellationToken cancellationToken) => coordinator.ExecuteAsync(
            CreateCommandContext(cancellationToken),
            _ => Task.FromResult(new ExecuteCommandResult { Success = true }));

    private static ExecuteCommandContext CreateCommandContext(CancellationToken cancellationToken) => new()
    {
        Services = new ServiceCollection().BuildServiceProvider(),
        ResourceName = "simulator",
        CancellationToken = cancellationToken,
        Logger = NullLogger.Instance,
        Arguments = new InteractionInputCollection([]),
    };

    private static ResourceCommandAnnotation AddRestartCommand(
        IResource resource,
        string displayName,
        Func<ExecuteCommandContext, Task<ExecuteCommandResult>> execute)
    {
        var command = new ResourceCommandAnnotation(
            KnownResourceCommands.RestartCommand,
            displayName,
            _ => ResourceCommandState.Enabled,
            execute,
            displayName,
            arguments: null,
            confirmationMessage: null,
            iconName: "ArrowCounterclockwise",
            iconVariant: IconVariant.Regular,
            isHighlighted: false);
        resource.Annotations.Add(command);
        return command;
    }

    private sealed class FakeAdapter(
        OwnedCm4TransportIdentityPair current,
        OwnedCm4TransportIdentityPair replacement) : IOwnedCm4SimulatorReconfigurationAdapter
    {
        internal List<string> Events { get; } = [];
        internal int RestartDispatchAttempts { get; private set; }
        internal OwnedCm4TransportIdentityPair? Cm4Replacement { get; init; }
        internal bool CraneOwned { get; init; } = true;

        public bool IsCurrentOwnedCrane() => CraneOwned;

        public OwnedCm4TransportIdentityPair? GetCurrentOwnedPair() =>
            Events.Contains("restart-simulator", StringComparer.Ordinal) ? replacement :
            Events.Contains("restart-cm4", StringComparer.Ordinal) ? Cm4Replacement ?? replacement :
            Events.Contains("restart", StringComparer.Ordinal) ? replacement : current;

        public async Task<ExecuteCommandResult> RestartSimulatorAsync(
            Func<ExecuteCommandContext, Task<ExecuteCommandResult>> restartCommand,
            ExecuteCommandContext commandContext,
            CancellationToken cancellationToken)
        {
            RestartDispatchAttempts++;
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("restart");
            return await restartCommand(commandContext);
        }

        public Task<OwnedCm4TransportIdentityPair?> WaitForReplacementAsync(
            OwnedCm4TransportIdentityPair previous,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("wait-replacement");
            return Task.FromResult<OwnedCm4TransportIdentityPair?>(replacement);
        }

        public Task<OwnedCm4TransportIdentityPair?> WaitForCm4ReplacementAsync(
            OwnedCm4TransportIdentityPair previous,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("wait-cm4");
            return Task.FromResult(Cm4Replacement);
        }
    }

    private sealed class FakeController(
        List<string> events,
        Func<OwnedCm4TransportIdentityPair?> getCurrent) : IOwnedCm4TransportTargetController
    {
        private Cm4TransportTargetPhase phase = Cm4TransportTargetPhase.Applied;
        internal Cm4TransportTargetInvalidationResult InvalidationResult { get; init; } = new(true, false, 1, null);
        internal Cm4TransportTargetApplyResult? FinalApplyResult { get; init; }
        internal Action? AfterInvalidation { get; init; }
        internal Action? AfterApply { get; init; }
        internal bool BlockApply { get; init; }
        internal OwnedCm4TransportIdentityPair? LastAppliedPair { get; private set; }

        public Task<Cm4TransportTargetInvalidationResult> InvalidateCurrentTargetAsync(
            string helperLaunchId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            events.Add("invalidate");
            phase = InvalidationResult.Invalidated
                ? Cm4TransportTargetPhase.Invalidated
                : InvalidationResult.Unknown ? Cm4TransportTargetPhase.Unknown : phase;
            AfterInvalidation?.Invoke();
            return Task.FromResult(InvalidationResult);
        }

        public async Task<Cm4TransportTargetApplyResult> ApplyCurrentTargetAsync(
            string helperLaunchId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            events.Add("apply");
            if (BlockApply)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            if (FinalApplyResult is not null)
            {
                phase = Cm4TransportTargetPhase.Unknown;
                return FinalApplyResult;
            }

            LastAppliedPair = getCurrent();
            phase = Cm4TransportTargetPhase.Applied;
            AfterApply?.Invoke();
            return new Cm4TransportTargetApplyResult(true, false, 2, null);
        }

        public long Invalidate()
        {
            phase = Cm4TransportTargetPhase.Unknown;
            return 3;
        }

        public bool TryReinitializeAfterConfirmedCm4Replacement(
            OwnedCm4TransportIdentity previousCm4,
            OwnedCm4TransportIdentity replacementCm4) => false;

        public bool IsInvalidatedFor(OwnedCm4TransportIdentityPair identities) =>
            phase == Cm4TransportTargetPhase.Invalidated && Equals(getCurrent(), identities);

        public Cm4TransportTargetSnapshot Snapshot() => new(1, phase, null, null, null);
    }
}
