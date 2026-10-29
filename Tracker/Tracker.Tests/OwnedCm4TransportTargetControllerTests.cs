using System.Text.Json;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class OwnedCm4TransportTargetControllerTests
{
    private static readonly JsonSerializerOptions ProtocolJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private const string RunId = "run-07h-test";
    private const string EngineId = "engine-07h-test";
    private const string Cm4Id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SimulatorId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string SimulatorReplacementId = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string NetworkId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public async Task AppliesSimulatorAddressThroughOwnedCm4StdioConsumerAndPublishesOnlyMatchingAcknowledgement()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId);
        var controller = new OwnedCm4TransportTargetController(
            runner,
            "desktop-linux",
            EngineId,
            RunId,
            () => identities);

        var result = await controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);

        Assert.True(result.Applied, result.Error);
        Assert.False(result.Unknown);
        Assert.Equal(Cm4TransportTargetPhase.Applied, controller.Snapshot().Phase);
        Assert.Equal("172.30.0.3", controller.Snapshot().TargetAddress);
        Assert.Equal(1, runner.StdioCalls);
        Assert.Equal(
            new[]
            {
                "--context", "desktop-linux", "exec", "-i", Cm4Id,
                "/opt/duck/cm4-transport-control", "--protocol", "jsonl-v1",
            },
            runner.StdioArguments);
        var request = JsonSerializer.Deserialize<Cm4TransportTargetMessage>(runner.StdioInput!, ProtocolJsonOptions);
        Assert.NotNull(request);
        Assert.Equal(RunId, request.RunId);
        Assert.Equal(EngineId, request.EngineId);
        Assert.Equal(Cm4Id, request.Cm4ContainerId);
        Assert.Equal(1, request.Cm4Generation);
        Assert.Equal("helper-launch-01", request.HelperLaunchId);
        Assert.Equal(SimulatorId, request.SimulatorContainerId);
        Assert.Equal(4, request.SimulatorGeneration);
        Assert.Equal(1, request.ConfigEpoch);
        Assert.Equal("172.30.0.3", request.TargetAddress);
        var sharedWireFixture = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "cm4-transport-apply-target.json")).TrimEnd('\r', '\n');
        Assert.Equal(sharedWireFixture, runner.StdioInput!.TrimEnd('\r', '\n'));
        Assert.All(runner.InspectFormats, format =>
        {
            Assert.Contains("NetworkSettings.Networks", format, StringComparison.Ordinal);
            Assert.DoesNotContain(".Config.Env", format, StringComparison.Ordinal);
            Assert.DoesNotContain(".Config.Cmd", format, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RejectsForeignOwnerBeforeStartingTheControlCommand()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId) { ForeignCm4Owner = true };
        var controller = CreateController(runner, () => identities);

        var result = await controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.False(result.Unknown);
        Assert.Equal(0, runner.StdioCalls);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
        Assert.Null(controller.Snapshot().TargetAddress);
    }

    [Fact]
    public async Task RejectsWrongEngineBeforeInspectingOrStartingTheControlCommand()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, "foreign-engine");
        var controller = CreateController(runner, () => identities);

        var result = await controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(0, runner.StdioCalls);
        Assert.Empty(runner.InspectFormats);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task RejectsAcknowledgementForDifferentConfigurationEpoch()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId);
        runner.StdioResultFactory = input =>
        {
            var request = JsonSerializer.Deserialize<Cm4TransportTargetMessage>(input, ProtocolJsonOptions)!;
            return new DockerStdioCommandResult(
                0,
                JsonSerializer.Serialize(request with { Type = "applied", ConfigEpoch = 99 }, ProtocolJsonOptions),
                string.Empty);
        };
        var controller = CreateController(runner, () => identities);

        var result = await controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.True(result.Unknown);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
        Assert.Null(controller.Snapshot().TargetAddress);
    }

    [Fact]
    public async Task RejectsAdditionalStdoutAfterAcknowledgement()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId);
        runner.StdioResultFactory = input =>
        {
            var accepted = FakeDockerRunner.CreateAcknowledgement(input);
            return accepted with { StandardOutput = accepted.StandardOutput + "\n{}" };
        };
        var controller = CreateController(runner, () => identities);

        var result = await controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.True(result.Unknown);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task RejectsDifferentNetworkAttachmentsBeforeStartingTheControlCommand()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId) { DifferentNetworkIds = true };
        var controller = CreateController(runner, () => identities);

        var result = await controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(0, runner.StdioCalls);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
    }

    [Fact]
    public async Task RejectsMulticastSimulatorTargetAddressBeforeStartingTheControlCommand()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId) { InvalidSimulatorAddress = true };
        var controller = CreateController(runner, () => identities);

        var result = await controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(0, runner.StdioCalls);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
        Assert.Null(controller.Snapshot().TargetAddress);
    }

    [Fact]
    public async Task RejectsAcknowledgementWhenSimulatorGenerationChangesDuringApply()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId);
        var controller = CreateController(runner, () => identities);
        runner.AfterStdio = () => identities = new(
            identities.Cm4,
            identities.Simulator with { ContainerId = SimulatorReplacementId, Generation = 5 });

        var result = await controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.True(result.Unknown);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
        Assert.Null(controller.Snapshot().TargetAddress);
    }

    [Fact]
    public async Task CancellationAfterExecBeginsLeavesTargetUnknownAndDoesNotRestorePreviousAddress()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId);
        var controller = CreateController(runner, () => identities);
        runner.StdioHandler = async (_, cancellationToken) =>
        {
            runner.ExecStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable after cancellation.");
        };
        using var cancellation = new CancellationTokenSource();

        var applyTask = controller.ApplyCurrentTargetAsync("helper-launch-01", cancellation.Token);
        await runner.ExecStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => applyTask);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
        Assert.Null(controller.Snapshot().TargetAddress);
    }

    [Fact]
    public async Task LateAcknowledgementFromSupersededEpochCannotPublishOldTarget()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId);
        var controller = CreateController(runner, () => identities);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.StdioHandler = async (request, cancellationToken) =>
        {
            if (runner.StdioCalls == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }

            return FakeDockerRunner.CreateAcknowledgement(request);
        };

        var first = controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = controller.ApplyCurrentTargetAsync("helper-launch-02", CancellationToken.None);
        await WaitForEpochAsync(controller, 2);
        releaseFirst.TrySetResult();

        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(2));
        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(firstResult.Applied);
        Assert.True(firstResult.Unknown);
        Assert.True(secondResult.Applied, secondResult.Error);
        Assert.Equal(2, controller.Snapshot().Epoch);
        Assert.Equal(Cm4TransportTargetPhase.Applied, controller.Snapshot().Phase);
        Assert.Equal(SimulatorId, controller.Snapshot().SimulatorContainerId);
    }

    [Fact]
    public async Task CancellationAtFinalInspectDoesNotPublishAppliedTarget()
    {
        var identities = CreatePair();
        using var cancellation = new CancellationTokenSource();
        var runner = new FakeDockerRunner(identities, EngineId)
        {
            AfterInspect = inspectCount =>
            {
                if (inspectCount == 4) cancellation.Cancel();
            },
        };
        var controller = CreateController(runner, () => identities);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.ApplyCurrentTargetAsync("helper-launch-01", cancellation.Token));

        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
        Assert.Null(controller.Snapshot().TargetAddress);
    }

    [Fact]
    public async Task NonzeroRemoteExitLeavesAppliedStateUnknown()
    {
        var identities = CreatePair();
        var runner = new FakeDockerRunner(identities, EngineId)
        {
            StdioResultFactory = input =>
            {
                var accepted = FakeDockerRunner.CreateAcknowledgement(input);
                return accepted with { ExitCode = 42 };
            },
        };
        var controller = CreateController(runner, () => identities);

        var result = await controller.ApplyCurrentTargetAsync("helper-launch-01", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.True(result.Unknown);
        Assert.Equal(Cm4TransportTargetPhase.Unknown, controller.Snapshot().Phase);
        Assert.Null(controller.Snapshot().TargetAddress);
    }

    private static OwnedCm4TransportIdentityPair CreatePair() => new(
        new(RunId, EngineId, "cm4-sim", "opaque-cm4-resource", Cm4Id, 1),
        new(RunId, EngineId, "simulator", "opaque-simulator-resource", SimulatorId, 4));

    private static OwnedCm4TransportTargetController CreateController(
        FakeDockerRunner runner,
        Func<OwnedCm4TransportIdentityPair?> getCurrent) =>
        new(runner, "desktop-linux", EngineId, RunId, getCurrent);

    private static async Task WaitForEpochAsync(
        OwnedCm4TransportTargetController controller,
        long expectedEpoch)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (controller.Snapshot().Epoch < expectedEpoch)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), deadline.Token);
        }
    }

    private sealed class FakeDockerRunner(
        OwnedCm4TransportIdentityPair identities,
        string engineId) : IDockerStdioCommandRunner
    {
        internal int StdioCalls { get; private set; }
        internal IReadOnlyList<string>? StdioArguments { get; private set; }
        internal string? StdioInput { get; private set; }
        internal List<string> InspectFormats { get; } = [];
        internal bool ForeignCm4Owner { get; init; }
        internal bool DifferentNetworkIds { get; init; }
        internal bool InvalidSimulatorAddress { get; init; }
        internal Action? AfterStdio { get; set; }
        internal Action<int>? AfterInspect { get; set; }
        internal int InspectCalls { get; private set; }
        internal TaskCompletionSource ExecStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<string, CancellationToken, Task<DockerStdioCommandResult>>? StdioHandler { get; set; }
        internal Func<string, DockerStdioCommandResult>? StdioResultFactory { get; set; }

        public Task<string> CaptureAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            if (arguments.Contains("info", StringComparer.Ordinal)) return Task.FromResult(engineId);
            var id = arguments.Last();
            var identity = id == identities.Cm4.ContainerId ? identities.Cm4 : identities.Simulator;
            if (ForeignCm4Owner && ReferenceEquals(identity, identities.Cm4))
            {
                identity = identity with { RunId = "foreign-run" };
            }
            var formatIndex = Array.IndexOf(arguments.ToArray(), "--format");
            if (formatIndex >= 0 && formatIndex + 1 < arguments.Count)
            {
                InspectFormats.Add(arguments[formatIndex + 1]);
            }

            var summary = InspectSummary(identity);
            if (arguments.Contains("inspect", StringComparer.Ordinal))
            {
                InspectCalls++;
                AfterInspect?.Invoke(InspectCalls);
            }

            return Task.FromResult(summary);
        }

        public async Task<DockerStdioCommandResult> CaptureBoundedAsync(
            IReadOnlyList<string> arguments,
            int maximumOutputCharacters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var output = await CaptureAsync(arguments, cancellationToken);
            return new DockerStdioCommandResult(0, output, string.Empty);
        }

        public Task<ContainerIdentityControlExecResult> RelayAsync(
            IReadOnlyList<string> arguments,
            Duck.Testing.Diagnostics.ResourceDiagnosticIdentity identity,
            string roleName,
            string? expectedBootId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ContainerIdentityControlExecResult(false, null, false));

        public Task<DockerStdioCommandResult> ExecuteWithStdioAsync(
            IReadOnlyList<string> arguments,
            string standardInput,
            int maximumOutputCharacters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            StdioCalls++;
            StdioArguments = arguments.ToArray();
            StdioInput = standardInput;
            ExecStarted.TrySetResult();
            if (StdioHandler is not null) return CompleteStdioAsync(standardInput, cancellationToken);
            AfterStdio?.Invoke();
            return Task.FromResult(StdioResultFactory?.Invoke(standardInput) ?? CreateAcknowledgement(standardInput));
        }

        private async Task<DockerStdioCommandResult> CompleteStdioAsync(
            string standardInput,
            CancellationToken cancellationToken)
        {
            var result = await StdioHandler!(standardInput, cancellationToken);
            AfterStdio?.Invoke();
            return result;
        }

        internal static DockerStdioCommandResult CreateAcknowledgement(string standardInput)
        {
            var request = JsonSerializer.Deserialize<Cm4TransportTargetMessage>(standardInput, ProtocolJsonOptions)!;
            var acknowledgement = request with { Type = "applied" };
            return new DockerStdioCommandResult(
                0, JsonSerializer.Serialize(acknowledgement, ProtocolJsonOptions), string.Empty);
        }

        private string InspectSummary(OwnedCm4TransportIdentity identity)
        {
            var address = identity.ResourceName == "cm4-sim"
                ? "172.30.0.2"
                : InvalidSimulatorAddress ? "239.1.2.3" : "172.30.0.3";
            var networkId = identity.ResourceName == "simulator" && DifferentNetworkIds
                ? "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
                : NetworkId;
            return string.Join(
                "\n",
                identity.ContainerId,
                "running",
                "bridge",
                identity.RunId,
                identity.ResourceName,
                $"bridge\t{networkId}\t{address}",
                string.Empty);
        }
    }
}
