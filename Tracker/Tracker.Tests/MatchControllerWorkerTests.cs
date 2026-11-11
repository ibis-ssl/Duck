using Duck.Testing.RefereeDriver;
using Duck.Testing.RefereeDriver.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tracker.Tests;

public sealed class MatchControllerWorkerTests
{
    [Fact]
    public void ParticipantReadinessRequiresFreshCurrentRunningEvidenceForEveryExpectedResource()
    {
        var expected = new[] { "simulator", "game-controller", "crane", "duck", "tigers-blue", "autoref-tigers", "ssl-log-recorder" };
        var now = DateTimeOffset.UtcNow;
        var current = CompleteReadiness().ParticipantEvidence!;

        Assert.True(MatchControllerReadinessProbe.IsParticipantEvidenceReady(current, expected, now));
        Assert.False(MatchControllerReadinessProbe.IsParticipantEvidenceReady(
            current with { ObservedAtUtc = now - TimeSpan.FromSeconds(3) }, expected, now));
        Assert.False(MatchControllerReadinessProbe.IsParticipantEvidenceReady(
            current with { Resources = current.Resources.Where(resource => resource.ResourceId != "tigers-blue").ToArray() }, expected, now));
        Assert.False(MatchControllerReadinessProbe.IsParticipantEvidenceReady(
            current with
            {
                Resources = current.Resources.Select(resource => resource.ResourceId == "autoref-tigers"
                    ? resource with { HealthStatus = "Unhealthy" }
                    : resource).ToArray(),
            }, expected, now));
        Assert.False(MatchControllerReadinessProbe.IsParticipantEvidenceReady(
            current with
            {
                Resources = current.Resources.Select(resource => resource.ResourceId == "tigers-blue"
                    ? resource with { ContainerId = null }
                    : resource).ToArray(),
            }, expected, now));
        Assert.False(MatchControllerReadinessProbe.IsParticipantEvidenceReady(
            current with
            {
                Resources = current.Resources.Select(resource => resource.ResourceId == "autoref-tigers"
                    ? resource with { ServiceEvidenceObservedAtUtc = now - TimeSpan.FromMinutes(3) }
                    : resource).ToArray(),
            }, expected, now));
        Assert.False(MatchControllerReadinessProbe.IsParticipantEvidenceReady(
            current with
            {
                Resources = current.Resources.Select(resource => resource.ResourceId == "ssl-log-recorder"
                    ? resource with { ServiceEvidenceObservedAtUtc = null }
                    : resource).ToArray(),
            }, expected, now));
        Assert.False(MatchControllerReadinessProbe.IsParticipantEvidenceReady(
            current with
            {
                Resources = current.Resources.Select(resource => resource.ResourceId == "ssl-log-recorder"
                    ? resource with { ServiceEvidenceObservedAtUtc = now - TimeSpan.FromSeconds(16) }
                    : resource).ToArray(),
            }, expected, now));
    }

    [Fact]
    public void UnknownHealthWithoutPositiveServiceEvidenceDoesNotPassForTargetContainers()
    {
        var expected = new[] { "simulator", "game-controller", "crane", "duck", "tigers-blue", "autoref-tigers", "ssl-log-recorder" };
        var now = DateTimeOffset.UtcNow;
        var current = CompleteReadiness().ParticipantEvidence!;

        foreach (var healthStatus in new string?[] { null, "Unknown" })
        {
            var noPositiveEvidence = current with
            {
                Resources = current.Resources.Select(resource => resource.ResourceId == "tigers-blue"
                    ? resource with
                    {
                        HealthStatus = healthStatus,
                        ServiceEvidenceReady = false,
                        ServiceEvidenceObservedAtUtc = null,
                    }
                    : resource).ToArray(),
            };

            Assert.False(MatchControllerReadinessProbe.IsParticipantEvidenceReady(noPositiveEvidence, expected, now));
        }
    }

    [Fact]
    public void UnsupportedTargetSignalFailsClosedAndPreservesItsReason()
    {
        var expected = new[] { "simulator", "game-controller", "crane", "duck", "tigers-blue", "autoref-tigers", "ssl-log-recorder" };
        var now = DateTimeOffset.UtcNow;
        var current = CompleteReadiness().ParticipantEvidence! with
        {
            Resources = CompleteReadiness().ParticipantEvidence!.Resources.Select(resource =>
                resource.ResourceId == "tigers-blue"
                    ? resource with
                    {
                        ServiceEvidenceReady = false,
                        ServiceEvidenceReason = "Target-side receive/decode diagnostics unavailable.",
                    }
                    : resource).ToArray(),
        };

        Assert.False(MatchControllerReadinessProbe.IsParticipantEvidenceReady(current, expected, now));
        var json = System.Text.Json.JsonSerializer.Serialize(current,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var restored = System.Text.Json.JsonSerializer.Deserialize<MatchParticipantStatusEvidence>(json,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Equal("Target-side receive/decode diagnostics unavailable.",
            Assert.Single(restored!.Resources, resource => resource.ResourceId == "tigers-blue").ServiceEvidenceReason);
    }

    [Fact]
    public void VisionReadinessUsesTheRuntimeHealthResponseContract()
    {
        using var response = System.Text.Json.JsonDocument.Parse("{\"IsReady\":true}");
        using var wrongCase = System.Text.Json.JsonDocument.Parse("{\"isReady\":true}");
        using var notReady = System.Text.Json.JsonDocument.Parse("{\"IsReady\":false}");

        Assert.True(MatchControllerReadinessProbe.IsVisionResponseReady(response.RootElement));
        Assert.False(MatchControllerReadinessProbe.IsVisionResponseReady(wrongCase.RootElement));
        Assert.False(MatchControllerReadinessProbe.IsVisionResponseReady(notReady.RootElement));
    }

    [Fact]
    public async Task RunMatchPersistsCompletePostGameResultAfterInitialStop()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
        var source = new SequenceRefereeCommandSource(
            Snapshot(Referee.Types.Command.Stop, 1, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.PrepareKickoffBlue, 2, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.NormalStart, 3, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.Stop, 4, Referee.Types.Stage.PostGame));
        var control = new RecordingControlClient();
        var readiness = new RecordingReadinessProbe(CompleteReadiness(), CompleteReadiness());
        var simulator = new RecordingSimulatorBallControlClient();
        var logger = new RecordingLogger<MatchControllerWorker>();
        var worker = CreateWorker(source, control, readiness, resultPath, TimeSpan.FromSeconds(1), simulator, logger);

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("passed", result.Status);
        Assert.Equal("post_game", result.StageReached);
        Assert.Equal("PostGame", result.TerminationKind);
        Assert.True(result.FinalReadiness?.IsComplete);
        Assert.Equal(Referee.Types.Command.Stop, result.InitialStopSnapshot?.Command);
        Assert.Equal("STOP", result.InitialStopCommand);
        Assert.Equal(
            [GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart],
            control.Actions);
        Assert.Equal(1, readiness.InitialCalls);
        Assert.Equal(1, readiness.FinalCalls);
        Assert.Equal([true], control.AutoContinueChanges);
        Assert.Equal(1, simulator.InitialSetupCalls);
        Assert.Equal(1, simulator.ObservationCompletionCalls);
        Assert.Equal("response_received", result.InitialSetupSimulatorResponse?.Status);
        Assert.Contains(logger.Messages, message => message.Contains("normal_start_observed", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("match_terminated", StringComparison.Ordinal) &&
            message.Contains("\"status\":\"passed\"", StringComparison.Ordinal) &&
            message.Contains("\"terminationKind\":\"PostGame\"", StringComparison.Ordinal));
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal("passed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("post_game", json.RootElement.GetProperty("stageReached").GetString());
        Assert.Equal("response_received", json.RootElement.GetProperty("initialSetupSimulatorResponse").GetProperty("status").GetString());
        var setupResponse = json.RootElement.GetProperty("initialSetupSimulatorResponse");
        Assert.Equal("127.0.0.1", setupResponse.GetProperty("destinationIp").GetString());
        Assert.Equal(10300, setupResponse.GetProperty("destinationPort").GetInt32());
        Assert.Equal(result.InitialSetupSimulatorResponse!.SendStartedAtUtc,
            setupResponse.GetProperty("sendStartedAtUtc").GetDateTimeOffset());
        Assert.Equal(result.InitialSetupSimulatorResponse.SendCompletedAtUtc,
            setupResponse.GetProperty("sendCompletedAtUtc").GetDateTimeOffset());
        Assert.Equal(result.InitialSetupSimulatorResponse.ResponseReceivedAtUtc,
            setupResponse.GetProperty("responseReceivedAtUtc").GetDateTimeOffset());
        Assert.Equal((int)Referee.Types.Command.Stop,
            json.RootElement.GetProperty("initialStopSnapshot").GetProperty("command").GetInt32());
        Assert.Equal("STOP", json.RootElement.GetProperty("initialStopCommand").GetString());
    }

    [Fact]
    public async Task RunMatchFailsBeforeInitialSetupWhenRefereeOmitsBlueHalf()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
        var stopWithoutDirection = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 1, YellowTeamName: "ibis",
            Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 1);
        var source = new SequenceRefereeCommandSource(stopWithoutDirection);
        var control = new RecordingControlClient { AutoContinueEnabled = false };
        var readiness = new RecordingReadinessProbe(CompleteReadiness());
        var simulator = new RecordingSimulatorBallControlClient();
        var worker = CreateWorker(source, control, readiness, resultPath, TimeSpan.FromSeconds(1), simulator);

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Contains("did not declare which half belongs to the blue team", result.Failure, StringComparison.Ordinal);
        Assert.Equal(0, simulator.InitialSetupCalls);
        Assert.Empty(control.Actions);
    }

    [Fact]
    public async Task RunMatchFailsClosedWhenAutoContinueWasNotDisabledBeforeStart()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
        var source = new SequenceRefereeCommandSource(
            Snapshot(Referee.Types.Command.Stop, 1, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.PrepareKickoffBlue, 2, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.NormalStart, 3, Referee.Types.Stage.NormalFirstHalf));
        var control = new RecordingControlClient { AutoContinueEnabled = true };
        var readiness = new RecordingReadinessProbe(CompleteReadiness());
        var worker = CreateWorker(source, control, readiness, resultPath, TimeSpan.FromSeconds(1));

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal("start", result.StageReached);
        Assert.Empty(control.Actions);
        Assert.Contains("autoContinue=false", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunMatchPersistsInitialReadinessEvidenceAndDoesNotStartWhenIncomplete()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
        var incomplete = CompleteReadiness() with
        {
            ParticipantResourcesReady = false,
            FailureReason = "AUTH_TOKEN=must-not-be-logged",
            ParticipantEvidence = CompleteReadiness().ParticipantEvidence! with
            {
                Resources = CompleteReadiness().ParticipantEvidence!.Resources
                    .Where(resource => resource.ResourceId != "tigers-blue").ToArray(),
            },
        };
        var control = new RecordingControlClient();
        var readiness = new RecordingReadinessProbe(incomplete);
        var logger = new RecordingLogger<MatchControllerWorker>();
        var worker = CreateWorker(new SequenceRefereeCommandSource(), control, readiness, resultPath, TimeSpan.FromSeconds(1), logger: logger);

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Empty(control.Actions);
        Assert.Equal(incomplete, result.InitialReadiness);
        Assert.Contains(logger.Messages, message => message.Contains("match_terminated", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("AUTH_TOKEN", StringComparison.Ordinal));
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.False(json.RootElement.GetProperty("initialReadiness").GetProperty("participantResourcesReady").GetBoolean());
        Assert.Contains(json.RootElement.GetProperty("initialReadiness").GetProperty("participantEvidence")
            .GetProperty("resources").EnumerateArray(), resource => resource.GetProperty("resourceId").GetString() == "simulator");
        Assert.DoesNotContain(json.RootElement.GetProperty("initialReadiness").GetProperty("participantEvidence")
            .GetProperty("resources").EnumerateArray(), resource => resource.GetProperty("resourceId").GetString() == "tigers-blue");
    }

    [Fact]
    public async Task RunMatchPersistsMaximumTimeAsFailure()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
        var source = new SequenceRefereeCommandSource(
            Snapshot(Referee.Types.Command.Stop, 1, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.PrepareKickoffYellow, 2, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.NormalStart, 3, Referee.Types.Stage.NormalFirstHalf));
        var control = new RecordingControlClient
        {
            KickoffTeam = "YELLOW",
            StateWhenAutoContinueEnabled = new(
                "Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(11),
                [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
                "FORCE_START", "UNKNOWN", 0, 0),
        };
        var readiness = new RecordingReadinessProbe(CompleteReadiness(), CompleteReadiness());
        var logger = new RecordingLogger<MatchControllerWorker>();
        var worker = CreateWorker(source, control, readiness, resultPath, TimeSpan.FromMilliseconds(30), logger: logger);

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal("maximum_time", result.StageReached);
        Assert.Equal("MaximumTimeReached", result.TerminationKind);
        Assert.Contains("MATCH_MAXIMUM_TIME_REACHED", result.Failure, StringComparison.Ordinal);
        Assert.Contains(result.MaximumTimeContinuationState!.ContinueActions,
            action => action.Type == "FORCE_START" && action.State == "READY_MANUAL");
        Assert.Contains(logger.Messages, message => message.Contains("match_terminated", StringComparison.Ordinal) &&
            message.Contains("\"status\":\"failed\"", StringComparison.Ordinal) &&
            message.Contains("MaximumTimeReached", StringComparison.Ordinal));
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("FORCE_START", json.RootElement.GetProperty("maximumTimeContinuationState")
            .GetProperty("continueActions")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task RunMatchPersistsMaximumTimeFailureAfterFirstInMatchStop()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
        var source = new SequenceRefereeCommandSource(
            Snapshot(Referee.Types.Command.Stop, 1, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.PrepareKickoffYellow, 2, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.NormalStart, 3, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.Stop, 4, Referee.Types.Stage.NormalFirstHalf));
        var control = new RecordingControlClient { KickoffTeam = "YELLOW" };
        var readiness = new RecordingReadinessProbe(CompleteReadiness(), CompleteReadiness());
        var worker = CreateWorker(source, control, readiness, resultPath, TimeSpan.FromMilliseconds(75));

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal("maximum_time", result.StageReached);
        Assert.Equal("MaximumTimeReached", result.TerminationKind);
        Assert.Contains("MATCH_MAXIMUM_TIME_REACHED", result.Failure, StringComparison.Ordinal);
        Assert.Equal(1, readiness.FinalCalls);
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("maximum_time", json.RootElement.GetProperty("stageReached").GetString());
        Assert.Equal("MaximumTimeReached", json.RootElement.GetProperty("terminationKind").GetString());
        Assert.Contains("MATCH_MAXIMUM_TIME_REACHED",
            json.RootElement.GetProperty("failure").GetString(), StringComparison.Ordinal);
        Assert.True(json.RootElement.TryGetProperty("maximumTimeContinuationState", out _));
    }

    [Fact]
    public async Task RunMatchUsesOneConfiguredReadinessBudgetForInitialNormalStart()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
        var source = new SequenceRefereeCommandSource(
            Snapshot(Referee.Types.Command.Stop, 1, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.PrepareKickoffYellow, 2, Referee.Types.Stage.NormalFirstHalf));
        var control = new RecordingControlClient { KickoffTeam = "YELLOW", NormalStartState = "WAITING" };
        var readiness = new RecordingReadinessProbe(CompleteReadiness())
        {
            ReadinessTimeout = TimeSpan.FromMilliseconds(100),
        };
        var simulator = new RecordingSimulatorBallControlClient();
        var worker = CreateWorker(source, control, readiness, resultPath, TimeSpan.FromMinutes(2), simulator);

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal("start", result.StageReached);
        Assert.Contains("configured readiness timeout", result.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, simulator.ObservationCompletionCalls);
        Assert.Equal("response_received", result.InitialSetupSimulatorResponse?.Status);
        Assert.Equal([GameControllerContinueAction.NextCommand], control.Actions);
        Assert.Equal("Kickoff", result.StartTimeoutContinuationState?.Command);
        Assert.Equal("NORMAL_FIRST_HALF_PRE", result.StartTimeoutContinuationState?.Stage);
        Assert.Contains(result.StartTimeoutContinuationState!.ContinueActions,
            action => action.Type == "NORMAL_START" && action.State == "WAITING");
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal("Kickoff", json.RootElement.GetProperty("startTimeoutContinuationState")
            .GetProperty("command").GetString());
    }

    [Fact]
    public async Task RunMatchPersistsPostGameFailureWhenFinalInputsAreIncomplete()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
        var source = new SequenceRefereeCommandSource(
            Snapshot(Referee.Types.Command.Stop, 1, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.PrepareKickoffBlue, 2, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.NormalStart, 3, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.Stop, 4, Referee.Types.Stage.PostGame));
        var readiness = new RecordingReadinessProbe(
            CompleteReadiness(),
            CompleteReadiness() with { VisionReady = false, FailureReason = "vision-not-ready" });
        var worker = CreateWorker(source, new RecordingControlClient(), readiness, resultPath, TimeSpan.FromSeconds(1));

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal("post_game", result.StageReached);
        Assert.Contains("vision-not-ready", result.Failure, StringComparison.Ordinal);
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.GetProperty("finalReadiness").GetProperty("visionReady").GetBoolean());
    }

    [Fact]
    public async Task RunMatchPersistsPartialResultWhenFinalParticipantEvidenceIsMissing()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
        var source = new SequenceRefereeCommandSource(
            Snapshot(Referee.Types.Command.Stop, 1, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.PrepareKickoffBlue, 2, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.NormalStart, 3, Referee.Types.Stage.NormalFirstHalf),
            Snapshot(Referee.Types.Command.Stop, 4, Referee.Types.Stage.PostGame));
        var incomplete = CompleteReadiness() with
        {
            ParticipantResourcesReady = false,
            FailureReason = "participantResourcesReady=False",
            ParticipantEvidence = CompleteReadiness().ParticipantEvidence! with
            {
                Resources = CompleteReadiness().ParticipantEvidence!.Resources
                    .Where(resource => resource.ResourceId != "tigers-blue").ToArray(),
            },
        };
        var readiness = new RecordingReadinessProbe(CompleteReadiness(), incomplete);
        var worker = CreateWorker(source, new RecordingControlClient(), readiness, resultPath, TimeSpan.FromSeconds(1));

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal("post_game", result.StageReached);
        Assert.Contains("participantResourcesReady=False", result.Failure, StringComparison.Ordinal);
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        var resources = json.RootElement.GetProperty("finalReadiness").GetProperty("participantEvidence").GetProperty("resources");
        Assert.DoesNotContain(resources.EnumerateArray(), resource => resource.GetProperty("resourceId").GetString() == "tigers-blue");
    }

    private static MatchControllerWorker CreateWorker(
        IRefereeCommandSource source,
        RecordingControlClient control,
        RecordingReadinessProbe readiness,
        string resultPath,
        TimeSpan maximumDuration,
        RecordingSimulatorBallControlClient? simulator = null,
        ILogger<MatchControllerWorker>? logger = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MatchController:MaximumDuration"] = maximumDuration.ToString("c"),
            ["MatchController:ResultPath"] = resultPath,
        }).Build();
        var driver = new RefereeDriverFixture(source, control, source as IRefereeLifecycleObservationSource);
        return new(driver, readiness, simulator ?? new RecordingSimulatorBallControlClient(), configuration,
            logger: logger);
    }

    private static MatchControllerReadiness CompleteReadiness()
    {
        var now = DateTimeOffset.UtcNow;
        var fullContainerId = new string('a', 64);
        MatchParticipantResourceEvidence Evidence(string resourceId) => new(
            resourceId,
            "Running",
            "Healthy",
            $"current-{resourceId}",
            fullContainerId,
            "test-run",
            resourceId is "tigers-blue" or "autoref-tigers" or "ssl-log-recorder",
            resourceId is "tigers-blue" or "autoref-tigers" or "ssl-log-recorder" ? now : null,
            resourceId == "ssl-log-recorder" ? 1 : null,
            resourceId == "ssl-log-recorder" ? now : null);

        return
        new(["simulator", "game-controller", "crane", "duck", "tigers-blue", "autoref-tigers", "ssl-log-recorder"],
            ParticipantResourcesReady: true, GameControllerApiAndRefereeReady: true, VisionReady: true,
            ParticipantEvidence: new(DateTimeOffset.UtcNow,
            new[] { "simulator", "game-controller", "crane", "duck", "tigers-blue", "autoref-tigers", "ssl-log-recorder" }
                .Select(Evidence).ToArray()));
    }

    private static RefereeCommandSnapshot Snapshot(
        Referee.Types.Command command, uint counter, Referee.Types.Stage stage)
    {
        if ((command is Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow) &&
            stage == Referee.Types.Stage.NormalFirstHalf)
            stage = Referee.Types.Stage.NormalFirstHalfPre;
        var nextCommand = command switch
        {
            Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow =>
                Referee.Types.Command.NormalStart,
            _ => (Referee.Types.Command?)null,
        };
        return new(command, counter, YellowTeamName: "ibis", Stage: stage, PacketTimestamp: counter,
            NextCommand: nextCommand, BlueTeamOnPositiveHalf: false);
    }

    private sealed class RecordingReadinessProbe(params MatchControllerReadiness[] results)
        : IMatchControllerReadinessProbe
    {
        private int index;
        public TimeSpan ReadinessTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int InitialCalls { get; private set; }
        public int FinalCalls { get; private set; }

        public Task<MatchControllerReadiness> WaitForInitialReadinessAsync(CancellationToken cancellationToken)
        {
            InitialCalls++;
            return Task.FromResult(results[index++]);
        }

        public Task<MatchControllerReadiness> ReadFinalReadinessAsync(
            RefereeCommandSnapshot finalSnapshot, CancellationToken cancellationToken)
        {
            FinalCalls++;
            return Task.FromResult(results[index++]);
        }

        public Task<MatchParticipantStatusEvidence?> ReadCurrentParticipantEvidenceAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(results.FirstOrDefault()?.ParticipantEvidence);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class RecordingControlClient : IGameControllerControlClient
    {
        public List<GameControllerContinueAction> Actions { get; } = [];
        public List<bool> AutoContinueChanges { get; } = [];
        public bool AutoContinueEnabled { get; set; }
        public string KickoffTeam { get; set; } = "BLUE";
        public string NormalStartState { get; set; } = "READY_AUTO";
        public GameControllerContinuationState? StateWhenAutoContinueEnabled { get; set; }

        public Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AutoContinueEnabled && StateWhenAutoContinueEnabled is { } matchState)
                return Task.FromResult<GameControllerContinuationState?>(matchState);
            return Task.FromResult<GameControllerContinuationState?>(new(
                "Kickoff", "Kickoff", "NORMAL_FIRST_HALF_PRE", TimeSpan.FromSeconds(8),
                [new("NORMAL_START", "UNKNOWN", NormalStartState, HasContinuationIssues: false)],
                "KICKOFF", KickoffTeam, 0, 0));
        }

        public Task<bool?> ReadAutoContinueAsync(CancellationToken cancellationToken) =>
            Task.FromResult<bool?>(AutoContinueEnabled);

        public Task<bool> SetAutoContinueAsync(bool enabled, CancellationToken cancellationToken)
        {
            AutoContinueEnabled = enabled;
            AutoContinueChanges.Add(enabled);
            return Task.FromResult(true);
        }

        public Task SendContinueActionAsync(GameControllerContinueAction action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSimulatorBallControlClient : ISimulatorBallControlClient
    {
        public int SendCalls { get; private set; }
        public int InitialSetupCalls { get; private set; }
        public int ObservationCompletionCalls { get; private set; }

        public Task<SimulatorResponseObservation?> CompleteInitialSetupObservationAsync()
        {
            ObservationCompletionCalls++;
            return Task.FromResult<SimulatorResponseObservation?>(InitialSetupCalls == 0 ? null :
                new("response_received", "127.0.0.1:10300", 0, [], "Test response",
                    DestinationIp: "127.0.0.1", DestinationPort: 10300,
                    SendStartedAtUtc: new DateTimeOffset(2026, 11, 8, 1, 0, 0, TimeSpan.Zero),
                    SendCompletedAtUtc: new DateTimeOffset(2026, 11, 8, 1, 0, 1, TimeSpan.Zero),
                    ResponseReceivedAtUtc: new DateTimeOffset(2026, 11, 8, 1, 0, 2, TimeSpan.Zero)));
        }

        public Task SendBallToPositionAsync(float x, float y, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCalls++;
            return Task.CompletedTask;
        }

        public Task SendInitialOwnHalfSetupAsync(bool blueTeamOnPositiveHalf, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitialSetupCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceRefereeCommandSource(params RefereeCommandSnapshot[] snapshots)
        : IRefereeCommandSource, IRefereeLifecycleObservationSource
    {
        private readonly Queue<RefereeCommandSnapshot> remaining = new(snapshots);
        private long sequence;
        public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(uint? previousCommandCounter, CancellationToken cancellationToken)
        {
            if (remaining.TryDequeue(out var snapshot))
            {
                latestSnapshot = snapshot;
                return snapshot;
            }
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }

        public async Task<RefereeLifecycleObservation> WaitForNextAsync(
            long previousRequestSequence,
            CancellationToken cancellationToken)
        {
            RefereeCommandSnapshot snapshot;
            if (latestSnapshot is { } current && current != lastLifecycleSnapshot)
                snapshot = current;
            else
                snapshot = await WaitForChangeAsync(null, cancellationToken);
            lastLifecycleSnapshot = snapshot;
            sequence = Math.Max(sequence, previousRequestSequence) + 1;
            var (command, phase) = snapshot.Command switch
            {
                Referee.Types.Command.Halt => ("Halt", "Halt"),
                Referee.Types.Command.Stop => ("Stop", "Stop"),
                Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow => ("Kickoff", "Kickoff"),
                Referee.Types.Command.NormalStart => ("NormalStart", "Running"),
                _ => ("Unknown", "Unknown"),
            };
            var stage = snapshot.Stage switch
            {
                Referee.Types.Stage.NormalFirstHalfPre => "NORMAL_FIRST_HALF_PRE",
                Referee.Types.Stage.NormalFirstHalf => "NORMAL_FIRST_HALF",
                Referee.Types.Stage.NormalHalfTime => "NORMAL_HALF_TIME",
                Referee.Types.Stage.NormalSecondHalfPre => "NORMAL_SECOND_HALF_PRE",
                Referee.Types.Stage.NormalSecondHalf => "NORMAL_SECOND_HALF",
                Referee.Types.Stage.PostGame => "POST_GAME",
                _ => "UNSUPPORTED",
            };
            var expectedTeam = snapshot.Command switch
            {
                Referee.Types.Command.PrepareKickoffBlue => "BLUE",
                Referee.Types.Command.PrepareKickoffYellow => "YELLOW",
                _ => "UNKNOWN",
            };
            var api = new GameControllerMatchSnapshot(
                snapshot.Command is Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow
                    ? GameControllerCommandKind.Kickoff
                    : command == "NormalStart" ? GameControllerCommandKind.NormalStart
                    : command == "Halt" ? GameControllerCommandKind.Halt
                    : command == "Stop" ? GameControllerCommandKind.Stop
                    : GameControllerCommandKind.Unknown,
                snapshot.Command is Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow
                    ? GameControllerMatchPhase.Kickoff
                    : phase == "Running" ? GameControllerMatchPhase.Running
                    : phase == "Halt" ? GameControllerMatchPhase.Halt
                    : phase == "Stop" ? GameControllerMatchPhase.Stop
                    : GameControllerMatchPhase.Unknown,
                stage,
                null,
                expectedTeam == "UNKNOWN" ? null : "KICKOFF",
                expectedTeam == "UNKNOWN" ? null : expectedTeam,
                snapshot.BlueScore,
                snapshot.YellowScore,
                CommandForTeam: expectedTeam,
                PhaseForTeam: expectedTeam);
            return new(sequence, snapshot, command, phase, stage, null, api);
        }

        private RefereeCommandSnapshot? latestSnapshot;
        private RefereeCommandSnapshot? lastLifecycleSnapshot;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "issue49-match-controller-tests-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
