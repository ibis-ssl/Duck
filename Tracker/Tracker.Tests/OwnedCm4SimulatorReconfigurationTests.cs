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

    private sealed class FakeAdapter(
        OwnedCm4TransportIdentityPair current,
        OwnedCm4TransportIdentityPair replacement) : IOwnedCm4SimulatorReconfigurationAdapter
    {
        internal List<string> Events { get; } = [];
        internal int RestartDispatchAttempts { get; private set; }
        internal OwnedCm4TransportIdentityPair? Cm4Replacement { get; init; }

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
