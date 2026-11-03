using System.Collections.Concurrent;
using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class MatchTerminationAcceptanceTests
{
    [Fact]
    public async Task PostGameIsSuccessfulAndPersistsCompleteFinalInputEvidence()
    {
        var source = new SequenceRefereeCommandSource(
            new RefereeCommandSnapshot(
                Referee.Types.Command.Stop, 10, Stage: Referee.Types.Stage.PostGame, PacketTimestamp: 101));
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 10, Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 100);
        var termination = await RefereeDriverFixture.WaitForMatchTerminationAsync(
            source, initial, TimeSpan.FromSeconds(1), CancellationToken.None);
        var snapshots = new ConcurrentQueue<RefereeCommandSnapshot>([initial, termination.LastSnapshot]);
        var writer = new AspireAcceptanceResultWriter(snapshots);
        writer.RecordMatchTermination(
            termination,
            new MatchFinalInputEvidence(true, true, true, true));
        using var artifacts = new TemporaryDirectory();
        var path = Path.Combine(artifacts.Path, "acceptance-result.json");

        await writer.WriteAsync(path, "post_game", 2, 1, 1, null, null, null);

        using var result = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal("passed", result.RootElement.GetProperty("status").GetString());
        var match = result.RootElement.GetProperty("matchTermination");
        Assert.Equal("PostGame", match.GetProperty("kind").GetString());
        Assert.Equal("Stop", match.GetProperty("command").GetString());
        Assert.Equal("PostGame", match.GetProperty("stage").GetString());
        Assert.Equal(10u, match.GetProperty("CommandCounter").GetUInt32());
        Assert.Equal(101ul, match.GetProperty("PacketTimestamp").GetUInt64());
        Assert.True(match.GetProperty("finalInputEvidence").GetProperty("IsComplete").GetBoolean());
    }

    [Fact]
    public async Task MaximumTimeIsFailureAndPersistsLastPartialEvidence()
    {
        var initial = new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 20);
        var source = new BlockingRefereeCommandSource();
        var termination = await RefereeDriverFixture.WaitForMatchTerminationAsync(
            source, initial, TimeSpan.FromMilliseconds(30), CancellationToken.None);
        var snapshots = new ConcurrentQueue<RefereeCommandSnapshot>([initial]);
        var writer = new AspireAcceptanceResultWriter(snapshots);
        writer.RecordMatchTermination(termination, null);
        using var artifacts = new TemporaryDirectory();
        var path = Path.Combine(artifacts.Path, "acceptance-result.json");

        await writer.WriteAsync(path, "maximum_time", 1, null, null, null, null, null);

        using var result = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal("failed", result.RootElement.GetProperty("status").GetString());
        Assert.Contains("MATCH_MAXIMUM_TIME_REACHED", result.RootElement.GetProperty("failure").GetString());
        var match = result.RootElement.GetProperty("matchTermination");
        Assert.Equal("MaximumTimeReached", match.GetProperty("kind").GetString());
        Assert.Equal("NormalStart", match.GetProperty("command").GetString());
        Assert.Equal(20u, match.GetProperty("CommandCounter").GetUInt32());
        Assert.Single(result.RootElement.GetProperty("refereeCommands").EnumerateArray());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, match.GetProperty("finalInputEvidence").ValueKind);
    }

    [Fact]
    public async Task PostGameWithoutAllFreshFinalInputsIsFailure()
    {
        var source = new SequenceRefereeCommandSource(
            new RefereeCommandSnapshot(
                Referee.Types.Command.NormalStart, 31, Stage: Referee.Types.Stage.PostGame, PacketTimestamp: 301));
        var initial = new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 30);
        var termination = await RefereeDriverFixture.WaitForMatchTerminationAsync(
            source, initial, TimeSpan.FromSeconds(1), CancellationToken.None);
        var writer = new AspireAcceptanceResultWriter(
            new ConcurrentQueue<RefereeCommandSnapshot>([initial, termination.LastSnapshot]));
        writer.RecordMatchTermination(
            termination,
            new MatchFinalInputEvidence(true, true, false, true));
        using var artifacts = new TemporaryDirectory();
        var path = Path.Combine(artifacts.Path, "acceptance-result.json");

        await writer.WriteAsync(path, "post_game", 2, 0, 1, null, null, null);

        using var result = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal("failed", result.RootElement.GetProperty("status").GetString());
        Assert.Contains("MATCH_FINAL_EVIDENCE_MISSING", result.RootElement.GetProperty("failure").GetString());
        Assert.False(result.RootElement.GetProperty("matchTermination")
            .GetProperty("finalInputEvidence").GetProperty("IsComplete").GetBoolean());
        Assert.Equal(2, result.RootElement.GetProperty("refereeCommands").GetArrayLength());
    }

    private sealed class SequenceRefereeCommandSource(params RefereeCommandSnapshot[] snapshots)
        : IRefereeCommandSource
    {
        private readonly Queue<RefereeCommandSnapshot> remaining = new(snapshots);

        public ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remaining.TryDequeue(out var snapshot))
            {
                return ValueTask.FromResult(snapshot);
            }

            throw new InvalidOperationException("No referee command remains in the test sequence.");
        }
    }

    private sealed class BlockingRefereeCommandSource : IRefereeCommandSource
    {
        public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The timeout should cancel the blocked source.");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("issue49-termination-");

        public string Path => directory.FullName;

        public void Dispose()
        {
            if (directory.Exists)
            {
                directory.Delete(recursive: true);
            }
        }
    }
}
