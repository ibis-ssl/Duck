using Duck.Testing.AppHost;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

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
            application.Services.GetRequiredService<ResourceCommandService>(),
            simulator,
            simulator,
            "engine-01");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.RestartSimulatorAsync(cancellation.Token));
        Assert.Equal(0, restartCalls);
    }

    [Fact]
    public async Task ReconfigurationInvalidatesBeforeRestartAndAppliesOnlyTheReplacement()
    {
        var previous = CreatePair(1, "a");
        var replacement = CreatePair(2, "b");
        var adapter = new FakeAdapter(previous, replacement);
        var controller = new FakeController(adapter.Events, adapter.GetCurrentOwnedPair);
        var coordinator = new OwnedCm4SimulatorReconfigurationCoordinator(adapter, controller, "run-01");

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["invalidate", "restart", "wait-replacement", "apply"], adapter.Events);
        Assert.Equal(Cm4TransportTargetPhase.Applied, controller.Snapshot().Phase);
        Assert.Equal(replacement, controller.LastAppliedPair);
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

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

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

        var result = await coordinator.ExecuteAsync(cancellation.Token);

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

        var result = await coordinator.ExecuteAsync(cancellation.Token);

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

        var result = await coordinator.ExecuteAsync(cancellation.Token);

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

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

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

        var result = await coordinator.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(["invalidate", "restart", "wait-replacement", "apply"], adapter.Events);
        Assert.Null(controller.LastAppliedPair);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
    }

    private static OwnedCm4TransportIdentityPair CreatePair(long generation, string suffix) => new(
        new("run-01", "engine-01", "cm4-sim", "cm4-key", new string('a', 64), 1),
        new("run-01", "engine-01", "simulator", "sim-key-" + suffix, new string('b', 63) + suffix, generation));

    private sealed class FakeAdapter(
        OwnedCm4TransportIdentityPair current,
        OwnedCm4TransportIdentityPair replacement) : IOwnedCm4SimulatorReconfigurationAdapter
    {
        internal List<string> Events { get; } = [];
        internal int RestartDispatchAttempts { get; private set; }

        public OwnedCm4TransportIdentityPair? GetCurrentOwnedPair() =>
            Events.Contains("restart", StringComparer.Ordinal) ? replacement : current;

        public Task<bool> RestartSimulatorAsync(CancellationToken cancellationToken)
        {
            RestartDispatchAttempts++;
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("restart");
            return Task.FromResult(true);
        }

        public Task<OwnedCm4TransportIdentityPair?> WaitForReplacementAsync(
            OwnedCm4TransportIdentityPair previous,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("wait-replacement");
            return Task.FromResult<OwnedCm4TransportIdentityPair?>(replacement);
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

        public Cm4TransportTargetSnapshot Snapshot() => new(1, phase, null, null, null);
    }
}
