using Duck.Testing.RefereeDriver;
using Duck.Testing.RefereeDriver.Hosting;
using Microsoft.Extensions.Configuration;

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
        var worker = CreateWorker(source, control, readiness, resultPath, TimeSpan.FromSeconds(1));

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("passed", result.Status);
        Assert.Equal("post_game", result.StageReached);
        Assert.Equal("PostGame", result.TerminationKind);
        Assert.True(result.FinalReadiness?.IsComplete);
        Assert.Equal(
            [GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart],
            control.Actions);
        Assert.Equal(1, readiness.InitialCalls);
        Assert.Equal(1, readiness.FinalCalls);
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal("passed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("post_game", json.RootElement.GetProperty("stageReached").GetString());
    }

    [Fact]
    public async Task RunMatchPersistsInitialReadinessEvidenceAndDoesNotStartWhenIncomplete()
    {
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "match-result.json");
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
        var control = new RecordingControlClient();
        var readiness = new RecordingReadinessProbe(incomplete);
        var worker = CreateWorker(new SequenceRefereeCommandSource(), control, readiness, resultPath, TimeSpan.FromSeconds(1));

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Empty(control.Actions);
        Assert.Equal(incomplete, result.InitialReadiness);
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
        var control = new RecordingControlClient();
        var readiness = new RecordingReadinessProbe(CompleteReadiness(), CompleteReadiness());
        var worker = CreateWorker(source, control, readiness, resultPath, TimeSpan.FromMilliseconds(30));

        var result = await worker.RunMatchAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal("maximum_time", result.StageReached);
        Assert.Equal("MaximumTimeReached", result.TerminationKind);
        Assert.Contains("MATCH_MAXIMUM_TIME_REACHED", result.Failure, StringComparison.Ordinal);
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
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
        TimeSpan maximumDuration)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MatchController:MaximumDuration"] = maximumDuration.ToString("c"),
            ["MatchController:ResultPath"] = resultPath,
        }).Build();
        return new(new RefereeDriverFixture(source, control), source, readiness, configuration);
    }

    private static MatchControllerReadiness CompleteReadiness() =>
        new(["simulator", "game-controller", "crane", "duck", "tigers-blue", "autoref-tigers", "ssl-log-recorder"],
            ParticipantResourcesReady: true, GameControllerApiAndRefereeReady: true, VisionReady: true,
            ParticipantEvidence: new(DateTimeOffset.UtcNow,
            [
                new("simulator", "Running", null), new("game-controller", "Running", null),
                new("crane", "Running", null), new("duck", "Running", null),
                new("tigers-blue", "Running", null), new("autoref-tigers", "Running", null),
                new("ssl-log-recorder", "Running", null),
            ]));

    private static RefereeCommandSnapshot Snapshot(
        Referee.Types.Command command, uint counter, Referee.Types.Stage stage) =>
        new(command, counter, Stage: stage, PacketTimestamp: counter);

    private sealed class RecordingReadinessProbe(params MatchControllerReadiness[] results)
        : IMatchControllerReadinessProbe
    {
        private int index;
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
    }

    private sealed class RecordingControlClient : IGameControllerControlClient
    {
        public List<GameControllerContinueAction> Actions { get; } = [];
        public Task SendContinueActionAsync(GameControllerContinueAction action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceRefereeCommandSource(params RefereeCommandSnapshot[] snapshots)
        : IRefereeCommandSource
    {
        private readonly Queue<RefereeCommandSnapshot> remaining = new(snapshots);
        public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(uint? previousCommandCounter, CancellationToken cancellationToken)
        {
            if (remaining.TryDequeue(out var snapshot))
                return snapshot;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
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
