using System.Net.WebSockets;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Duck.Testing.RefereeDriver;

/// <summary>注入された内部接続先から試合状態だけを読む観測専用clientの境界。</summary>
internal sealed class GameControllerStateClient
{
    private static readonly Regex GoDurationPart = new(
        @"(?<value>\d+(?:\.\d+)?)(?<unit>ns|us|µs|ms|s|m|h)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly Uri endpoint;
    private readonly Func<Uri, CancellationToken, Task<WebSocket>> connect;
    private readonly GameControllerStateReadOptions options;
    private readonly TimeProvider clock;

    internal GameControllerStateClient(
        Uri endpoint,
        Func<Uri, CancellationToken, Task<WebSocket>> connect,
        GameControllerStateReadOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(connect);
        this.endpoint = endpoint;
        this.connect = connect;
        this.options = options ?? new();
        clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>接続と受信に同じ有限期限を適用し、初期Outputの後に対応済みの公開状態を観測する。呼出取消は伝播する。</summary>
    internal async Task<GameControllerMatchSnapshot?> ReadMatchStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.MaximumMessageBytes <= 0 || options.Timeout <= TimeSpan.Zero ||
            options.Timeout > TimeSpan.FromMilliseconds(uint.MaxValue - 1))
            return null;
        var started = clock.GetTimestamp();
        using var deadline = new CancellationTokenSource(options.Timeout, clock);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var operationToken = operation.Token;
        WebSocket? socket = null;
        try
        {
            socket = await connect(endpoint, operationToken).ConfigureAwait(false);
            using var message = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                operationToken.ThrowIfCancellationRequested();
                if (ReadWindowExpired(started))
                    return null;
                var received = await socket.ReceiveAsync(buffer.AsMemory(), operationToken).ConfigureAwait(false);
                operationToken.ThrowIfCancellationRequested();
                if (ReadWindowExpired(started) || received.MessageType != WebSocketMessageType.Text)
                    return null;
                if (message.Length + received.Count > options.MaximumMessageBytes)
                    return null;
                message.Write(buffer, 0, received.Count);
                if (!received.EndOfMessage)
                    continue;

                using var output = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                if (output.RootElement.ValueKind != JsonValueKind.Object)
                    return null;
                if (!output.RootElement.TryGetProperty("matchState", out var matchState) ||
                    matchState.ValueKind == JsonValueKind.Null)
                {
                    message.SetLength(0);
                    continue;
                }
                if (matchState.ValueKind != JsonValueKind.Object)
                    return null;

                var normalized = GameControllerStateMapping.Normalize(matchState);
                operationToken.ThrowIfCancellationRequested();
                return ReadWindowExpired(started) ? null : normalized;
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        catch (JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        catch (WebSocketException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        finally
        {
            if (socket is not null)
            {
                await GameControllerWebSocketTransportOperations.CloseOutputGracefullyAsync(socket).ConfigureAwait(false);
                socket.Dispose();
            }
        }
    }

    /// <summary>読取開始からの単調経過で期限到達と時計逆行を拒否し、遅い正常応答も成功にしない。</summary>
    /// <summary>Reads current match state and the controller's explicitly offered continue actions.</summary>
    internal async Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.MaximumMessageBytes <= 0 || options.Timeout <= TimeSpan.Zero ||
            options.Timeout > TimeSpan.FromMilliseconds(uint.MaxValue - 1))
            return null;

        var started = clock.GetTimestamp();
        using var deadline = new CancellationTokenSource(options.Timeout, clock);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        WebSocket? socket = null;
        try
        {
            socket = await connect(endpoint, operation.Token).ConfigureAwait(false);
            GameControllerMatchSnapshot? match = null;
            IReadOnlyList<GameControllerContinueActionStatus>? actions = null;
            var buffer = new byte[4096];
            while (true)
            {
                operation.Token.ThrowIfCancellationRequested();
                if (ReadWindowExpired(started))
                    return null;
                using var message = new MemoryStream();
                while (true)
                {
                    var received = await socket.ReceiveAsync(buffer.AsMemory(), operation.Token).ConfigureAwait(false);
                    operation.Token.ThrowIfCancellationRequested();
                    if (ReadWindowExpired(started) || received.MessageType != WebSocketMessageType.Text ||
                        message.Length + received.Count > options.MaximumMessageBytes)
                        return null;
                    message.Write(buffer, 0, received.Count);
                    if (received.EndOfMessage)
                        break;
                }

                using var output = JsonDocument.Parse(message.GetBuffer().AsMemory(0, checked((int)message.Length)));
                if (output.RootElement.ValueKind != JsonValueKind.Object)
                    return null;
                if (output.RootElement.TryGetProperty("matchState", out var matchState) &&
                    matchState.ValueKind == JsonValueKind.Object)
                    match = GameControllerStateMapping.Normalize(matchState);
                if (output.RootElement.TryGetProperty("gcState", out var gcState) &&
                    gcState.ValueKind == JsonValueKind.Object)
                    actions = ReadContinueActions(gcState);

                if (match is not null && actions is not null)
                {
                    operation.Token.ThrowIfCancellationRequested();
                    if (ReadWindowExpired(started))
                        return null;
                    return new(match.Command.ToString(), match.Phase.ToString(), match.Stage,
                        match.StageTimeLeft, actions, match.NextCommand, match.NextCommandForTeam,
                        match.BlueScore, match.YellowScore, match.PlacementX, match.PlacementY);
                }
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        catch (JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        catch (WebSocketException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        finally
        {
            if (socket is not null)
            {
                await GameControllerWebSocketTransportOperations.CloseOutputGracefullyAsync(socket).ConfigureAwait(false);
                socket.Dispose();
            }
        }
    }

    private static IReadOnlyList<GameControllerContinueActionStatus>? ReadContinueActions(JsonElement gcState)
    {
        if (!gcState.TryGetProperty("continueActions", out var actions) ||
            actions.ValueKind != JsonValueKind.Array)
            return null;

        var result = new List<GameControllerContinueActionStatus>();
        foreach (var action in actions.EnumerateArray())
        {
            if (action.ValueKind != JsonValueKind.Object)
                return null;
            var type = ReadString(action, "type");
            var forTeam = ReadString(action, "forTeam");
            var state = ReadString(action, "state");
            if (type is null || forTeam is null || state is null)
                return null;
            var issueTexts = new List<string>();
            if (!action.TryGetProperty("continuationIssues", out var issues) || issues.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var issue in issues.EnumerateArray())
                issueTexts.Add(issue.ValueKind == JsonValueKind.String ? issue.GetString() ?? string.Empty : issue.GetRawText());
            result.Add(new(type, forTeam, state, issueTexts.Count != 0, issueTexts));
        }
        return result;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private bool ReadWindowExpired(long started)
    {
        var elapsed = clock.GetElapsedTime(started, clock.GetTimestamp());
        return elapsed < TimeSpan.Zero || elapsed >= options.Timeout;
    }
}

/// <summary>APIの数値列挙やUDPの整数値を直接使わない、明示的な指令区分。</summary>
public enum GameControllerCommandKind
{
    Unknown,
    Halt,
    Stop,
    Kickoff,
    NormalStart,
    ForceStart,
    BallPlacement,
    Direct,
    Penalty,
}

/// <summary>公開matchStateから正規化する試合状態。未確認は成功状態にしない。</summary>
public enum GameControllerMatchPhase
{
    Unknown,
    Halt,
    Stop,
    Kickoff,
    Running,
    BallPlacement,
    FreeKick,
    Penalty,
}

/// <summary>API公開状態の正規化結果。UDP件数・受信時刻・所有証明は含めない。</summary>
public sealed record GameControllerMatchSnapshot(
    GameControllerCommandKind Command,
    GameControllerMatchPhase Phase,
    string? Stage = null,
    TimeSpan? StageTimeLeft = null,
    string? NextCommand = null,
    string? NextCommandForTeam = null,
    uint? BlueScore = null,
    uint? YellowScore = null,
    float? PlacementX = null,
    float? PlacementY = null,
    string? CommandForTeam = null,
    string? PhaseForTeam = null);

/// <summary>一回の状態読取に明示注入する有限窓と、完成message全体のbyte上限。</summary>
internal sealed record GameControllerStateReadOptions
{
    // GC outputs include cumulative event history; bound the whole logical message, not each fragment.
    internal int MaximumMessageBytes { get; init; } = 1024 * 1024;
    internal TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);
}
