using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

internal sealed class RecordingRefereeCommandSource(
    IRefereeCommandSource inner,
    ConcurrentQueue<RefereeCommandSnapshot> snapshots,
    Action<string>? writeLine = null) : IRefereeCommandSource, IDisposable
{
    public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
        uint? previousCommandCounter,
        CancellationToken cancellationToken)
    {
        var snapshot = await inner.WaitForChangeAsync(previousCommandCounter, cancellationToken);
        snapshots.Enqueue(snapshot);
        writeLine?.Invoke($"referee command={snapshot.Command} counter={snapshot.CommandCounter}");
        return snapshot;
    }

    public void Dispose()
    {
        (inner as IDisposable)?.Dispose();
    }
}

internal sealed record RefereeHealthReadEvidence(
    int? HttpStatusCode,
    bool IsReady,
    string? Command,
    uint? CommandCounter,
    string? YellowTeamName,
    DateTimeOffset? ReadStartedAtUtc,
    DateTimeOffset? ReadCompletedAtUtc,
    double? ReadElapsedMilliseconds,
    string? Reason = null,
    string? UdpSourceKey = null,
    string? UdpRemoteEndpoint = null,
    string? ApiCommand = null,
    string? ApiPhase = null);

internal sealed record MatchFinalInputEvidence(
    bool GameControllerApiFresh,
    bool RefereePacketFresh,
    bool SslVisionInputFresh,
    bool ParticipantResourcesFresh)
{
    public bool IsComplete => GameControllerApiFresh && RefereePacketFresh &&
        SslVisionInputFresh && ParticipantResourcesFresh;
}

internal sealed class RefereeCommandCounterGapException(
    uint expectedCounter,
    uint previousCounter,
    RefereeHealthReadEvidence? currentHealth)
    : InvalidOperationException(
        $"REFEREE_COUNTER_GAP: expected command counter {expectedCounter} after {previousCounter}; " +
        $"observed {currentHealth?.CommandCounter?.ToString() ?? "unavailable"}; refusing to infer an unobserved referee state. " +
        $"Current health (sanitized): {System.Text.Json.JsonSerializer.Serialize((object?)currentHealth ?? "unavailable")}")
{
    public RefereeHealthReadEvidence? CurrentHealth { get; } = currentHealth;
}

internal sealed class AspireAcceptanceResultWriter(
    ConcurrentQueue<RefereeCommandSnapshot> refereeSnapshots)
{
    private Exception? failure;
    private RefereeHealthReadEvidence? refereeHealthAtFailure;
    private RefereeMatchTerminationResult? matchTermination;
    private MatchFinalInputEvidence? finalInputEvidence;

    public void RecordFailure(Exception exception)
    {
        failure = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public void RecordRefereeHealthAtFailure(RefereeHealthReadEvidence? evidence)
    {
        refereeHealthAtFailure = evidence;
    }

    public void RecordMatchTermination(
        RefereeMatchTerminationResult result,
        MatchFinalInputEvidence? finalInputEvidence)
    {
        matchTermination = result ?? throw new ArgumentNullException(nameof(result));
        this.finalInputEvidence = finalInputEvidence;
        if (!result.IsSuccess)
        {
            failure ??= new TimeoutException(
                $"MATCH_MAXIMUM_TIME_REACHED: execution stopped without POST_GAME; " +
                $"last referee command was {result.LastSnapshot.Command} at counter {result.LastSnapshot.CommandCounter}.");
        }
        else if (finalInputEvidence?.IsComplete != true)
        {
            failure ??= new InvalidOperationException(
                "MATCH_FINAL_EVIDENCE_MISSING: POST_GAME was observed without fresh Game Controller API, referee, SSL-Vision, and participant-resource evidence.");
        }
    }

    public static InvalidOperationException CreateMotionFailure(RefereeStateLostException exception) =>
        new(exception.StateLoss.ToFailureMessage(), exception);

    public async Task WriteAsync(
        string path,
        string stageReached,
        int refereeRawDatagramCount,
        int? visionPacketsConsumed,
        int? trackerPacketsConsumed,
        object? comparisonMeasurement,
        object? receiverSockets,
        object? movement)
    {
        if (matchTermination is null && (stageReached is "post_game" or "maximum_time"))
        {
            failure ??= new InvalidOperationException(
                "MATCH_TERMINATION_MISSING: final match results require an observed POST_GAME stage or an explicit maximum-time outcome.");
        }

        var evidence = new
        {
            status = failure is null ? "passed" : "failed",
            failure = failure?.ToString(),
            refereeHealthAtCounterGap = FormatHealthRead(FindRefereeHealthRead(failure)),
            refereeHealthAtFailure = FormatHealthRead(refereeHealthAtFailure),
            stageReached,
            refereeCommands = refereeSnapshots.ToArray().Select(snapshot => new
            {
                command = snapshot.Command.ToString(),
                snapshot.CommandCounter,
                stage = snapshot.Stage?.ToString(),
                snapshot.PacketTimestamp,
            }),
            refereeRawDatagramCount,
            refereeStateLoss = FindRefereeStateLoss(failure),
            matchTermination = matchTermination is null
                ? null
                : new
                {
                    kind = matchTermination.Kind.ToString(),
                    command = matchTermination.LastSnapshot.Command.ToString(),
                    stage = matchTermination.LastSnapshot.Stage?.ToString(),
                    matchTermination.LastSnapshot.CommandCounter,
                    matchTermination.LastSnapshot.PacketTimestamp,
                    finalInputEvidence,
                },
            visionPacketsConsumed,
            trackerPacketsConsumed,
            comparisonMeasurement,
            packetCountSemantics = "Consumed counts record datagrams read by the harness; null means that reader stage was not reached and does not imply network ingress was zero.",
            receiverSockets,
            movement,
            runner = Environment.GetEnvironmentVariable("RUNNER_OS"),
            sha = Environment.GetEnvironmentVariable("GITHUB_SHA"),
        };

        await File.WriteAllTextAsync(
            path,
            System.Text.Json.JsonSerializer.Serialize(
                evidence,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static RefereeHealthReadEvidence? FindRefereeHealthRead(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is RefereeCommandCounterGapException gap)
                return gap.CurrentHealth;
        }

        return null;
    }

    private static object FormatHealthRead(RefereeHealthReadEvidence? health)
    {
        if (health is null)
            return "unavailable";

        return new
        {
            httpStatusCode = health.HttpStatusCode,
            isReady = health.IsReady,
            command = health.Command,
            commandCounter = health.CommandCounter,
            yellowTeamName = health.YellowTeamName,
            reason = health.Reason,
            udpSourceKey = health.UdpSourceKey,
            udpRemoteEndpoint = health.UdpRemoteEndpoint,
            apiCommand = health.ApiCommand,
            apiPhase = health.ApiPhase,
            readStartedAtUtc = health.ReadStartedAtUtc,
            readCompletedAtUtc = health.ReadCompletedAtUtc,
            readElapsedMilliseconds = health.ReadElapsedMilliseconds,
        };
    }

    private static RefereeStateLossEvent? FindRefereeStateLoss(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is RefereeStateLostException stateLost)
            {
                return stateLost.StateLoss;
            }
        }

        return null;
    }
}

internal sealed class LoopbackUdpRefereePacketReceiver : IRefereePacketReceiver, IDisposable
{
    private readonly UdpClient client = new(AddressFamily.InterNetwork);
    private readonly IPEndPoint endpoint;

    public IPEndPoint Endpoint => endpoint;

    public bool IsDisposed { get; private set; }

    public LoopbackUdpRefereePacketReceiver()
    {
        client.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        endpoint = (IPEndPoint)client.Client.LocalEndPoint!;
    }

    public async Task PublishAsync(
        Referee.Types.Command command,
        uint commandCounter,
        Referee.Types.Stage stage = Referee.Types.Stage.NormalFirstHalf)
    {
        var packet = new Referee
        {
            PacketTimestamp = commandCounter,
            Stage = stage,
            Command = command,
            CommandCounter = commandCounter,
            CommandTimestamp = commandCounter,
            Yellow = new Referee.Types.TeamInfo { Name = "crane" },
        };

        await client.SendAsync(packet.ToByteArray(), endpoint);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
        (await client.ReceiveAsync(cancellationToken)).Buffer;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            client.Dispose();
            IsDisposed = true;
        }
    }
}
