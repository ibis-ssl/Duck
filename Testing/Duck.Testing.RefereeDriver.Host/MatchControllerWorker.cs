using System.Text.Json;
using Duck.Testing.RefereeDriver;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Duck.Testing.RefereeDriver.Hosting;

internal sealed record MatchControllerReadiness(
    IReadOnlyList<string> ParticipantResources,
    bool GameControllerApiAndRefereeReady,
    bool VisionReady,
    string? FailureReason = null)
{
    public bool IsComplete => ParticipantResources.Count > 0 &&
        GameControllerApiAndRefereeReady && VisionReady;
}

internal interface IMatchControllerReadinessProbe
{
    Task<MatchControllerReadiness> WaitForInitialReadinessAsync(CancellationToken cancellationToken);
    Task<MatchControllerReadiness> ReadFinalReadinessAsync(CancellationToken cancellationToken);
}

internal sealed class MatchControllerReadinessProbe(
    RefereeApiMatcher matcher,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    TimeProvider clock) : IMatchControllerReadinessProbe
{
    private readonly Uri visionHealthUri = ReadRequiredUri(configuration["MatchController:VisionHealthUri"]);
    private readonly string[] participantResources = ReadParticipantResources(configuration["MatchController:ParticipantResources"]);
    private readonly TimeSpan readinessTimeout = ReadTimeout(configuration["MatchController:ReadinessTimeout"]);

    public async Task<MatchControllerReadiness> WaitForInitialReadinessAsync(CancellationToken cancellationToken)
    {
        var match = await matcher.WaitForCurrentAsync(readinessTimeout, clock, cancellationToken)
            .ConfigureAwait(false);
        var visionReady = await WaitForVisionAsync(cancellationToken).ConfigureAwait(false);
        return new(participantResources, match.IsReady, visionReady,
            match.IsReady && visionReady ? null : $"apiReferee={match.Reason}, visionReady={visionReady}");
    }

    public async Task<MatchControllerReadiness> ReadFinalReadinessAsync(CancellationToken cancellationToken)
    {
        var match = await matcher.ReadAsync(cancellationToken).ConfigureAwait(false);
        var visionReady = await IsVisionReadyAsync(cancellationToken).ConfigureAwait(false);
        return new(participantResources, match.IsReady, visionReady,
            match.IsReady && visionReady ? null : $"apiReferee={match.Reason}, visionReady={visionReady}");
    }

    private async Task<bool> WaitForVisionAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(readinessTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        while (true)
        {
            linked.Token.ThrowIfCancellationRequested();
            if (await IsVisionReadyAsync(linked.Token).ConfigureAwait(false))
                return true;
            await Task.Delay(TimeSpan.FromMilliseconds(250), clock, linked.Token).ConfigureAwait(false);
        }
    }

    private async Task<bool> IsVisionReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClientFactory.CreateClient().GetAsync(visionHealthUri, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return document.RootElement.TryGetProperty("isReady", out var ready) &&
                ready.ValueKind == JsonValueKind.True;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static Uri ReadRequiredUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp
            ? uri
            : throw new InvalidOperationException("MatchController:VisionHealthUri must be an absolute HTTP URI.");

    private static string[] ReadParticipantResources(string? value)
    {
        var resources = value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        return resources.Length == 0
            ? throw new InvalidOperationException("MatchController:ParticipantResources must list the resources awaited by the AppHost.")
            : resources;
    }

    private static TimeSpan ReadTimeout(string? value) =>
        TimeSpan.TryParse(value, out var timeout) && timeout > TimeSpan.Zero
            ? timeout
            : TimeSpan.FromMinutes(2);
}

internal sealed record MatchControllerResult(
    string Status,
    string StageReached,
    string? TerminationKind,
    uint? FinalCommandCounter,
    ulong? FinalPacketTimestamp,
    MatchControllerReadiness? FinalReadiness,
    string? Failure);

internal sealed class MatchControllerWorker(
    RefereeDriverFixture driver,
    IRefereeCommandSource commandSource,
    IMatchControllerReadinessProbe readinessProbe,
    IConfiguration configuration) : BackgroundService
{
    private readonly TimeSpan maximumMatchDuration = ReadDuration(configuration["MatchController:MaximumDuration"]);
    private readonly string resultPath = ReadResultPath(configuration["MatchController:ResultPath"]);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var result = await RunMatchAsync(stoppingToken).ConfigureAwait(false);
        if (result.Status != "passed")
            throw new InvalidOperationException(result.Failure ?? "Match controller reported a failed result.");
    }

    internal async Task<MatchControllerResult> RunMatchAsync(CancellationToken cancellationToken)
    {
        var stage = "readiness";
        RefereeCommandSnapshot? finalSnapshot = null;
        string? terminationKind = null;
        MatchControllerReadiness? finalReadiness = null;
        string? failure = null;
        try
        {
            var initialReadiness = await readinessProbe.WaitForInitialReadinessAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!initialReadiness.IsComplete)
                throw new InvalidOperationException(initialReadiness.FailureReason ?? "Initial match readiness is incomplete.");

            stage = "start";
            finalSnapshot = await driver.DriveToActiveAsync(cancellationToken).ConfigureAwait(false);
            stage = "match";
            var termination = await RefereeDriverFixture.WaitForMatchTerminationAsync(
                commandSource, finalSnapshot, maximumMatchDuration, cancellationToken).ConfigureAwait(false);
            finalSnapshot = termination.LastSnapshot;
            terminationKind = termination.Kind.ToString();

            stage = termination.Kind == RefereeMatchTerminationKind.PostGame ? "post_game" : "maximum_time";
            finalReadiness = await readinessProbe.ReadFinalReadinessAsync(cancellationToken).ConfigureAwait(false);
            if (!termination.IsSuccess)
                failure = "MATCH_MAXIMUM_TIME_REACHED: execution stopped without POST_GAME.";
            else if (!finalReadiness.IsComplete)
                failure = finalReadiness.FailureReason ?? "MATCH_FINAL_EVIDENCE_MISSING: final inputs were not ready.";

            return await PersistAsync(new(
                failure is null ? "passed" : "failed", stage, terminationKind,
                finalSnapshot.CommandCounter, finalSnapshot.PacketTimestamp, finalReadiness, failure)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            return await PersistAsync(new(
                "failed", stage, terminationKind,
                finalSnapshot?.CommandCounter, finalSnapshot?.PacketTimestamp, finalReadiness, failure)).ConfigureAwait(false);
        }
    }

    private async Task<MatchControllerResult> PersistAsync(MatchControllerResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }), CancellationToken.None).ConfigureAwait(false);
        return result;
    }

    private static TimeSpan ReadDuration(string? value) =>
        TimeSpan.TryParse(value, out var duration) && duration > TimeSpan.Zero
            ? duration
            : TimeSpan.FromMinutes(2);

    private static string ReadResultPath(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException("MatchController:ResultPath must point to the mounted results directory.")
            : Path.GetFullPath(value);
}
