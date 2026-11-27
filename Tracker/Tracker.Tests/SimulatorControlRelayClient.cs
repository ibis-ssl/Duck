using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Duck.Testing.RefereeDriver;
using Duck.Testing.RefereeDriver.Hosting;

namespace Tracker.Tests;

/// <summary>HTTP 中継経由で Simulator 制御指令を送り、応答観測を取得するテスト用クライアントです。</summary>
/// <param name="endpoint">Simulator 制御中継の基点 URI です。</param>
/// <param name="httpClient">HTTP 送信に使う任意のクライアントです。省略時は内部で作成します。</param>
internal sealed class SimulatorControlRelayClient(Uri endpoint, HttpClient? httpClient = null) : ISimulatorBallControlClient, IDisposable
{
    private static readonly TimeSpan ObservationRequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient client = httpClient ?? new HttpClient();
    private readonly bool ownsClient = httpClient is null;
    private SimulatorSetupDispatchReceipt? initialSetupDispatch;
    private SimulatorResponseObservation? initialSetupObservation;

    /// <summary>青チームの半面に応じた初期配置指令を送り、送信証跡を受け取ります。</summary>
    /// <param name="blueTeamOnPositiveHalf">青チームが正の半面にいる場合は <see langword="true"/> です。</param>
    /// <param name="sendCancellationToken">指令の送信待ちを中止するトークンです。</param>
    /// <param name="observationCancellationToken">インターフェイス契約に合わせて受け取る応答観測用トークンです。観測は中継側の寿命に従います。</param>
    /// <returns>送信要求 ID と送信データの証跡です。</returns>
    /// <exception cref="SimulatorControlRelayDispatchException">中継が指令を拒否したか、応答証跡が不正です。</exception>
    public async Task<SimulatorSetupDispatchReceipt> SendInitialOwnHalfSetupAsync(
        bool blueTeamOnPositiveHalf,
        CancellationToken sendCancellationToken,
        CancellationToken observationCancellationToken)
    {
        // The remote relay owns response observation lifetime through ApplicationStopping;
        // this caller token bounds only its HTTP request and JSON receipt read.
        _ = observationCancellationToken;
        if (initialSetupDispatch is not null)
            throw new InvalidOperationException("The initial simulator setup has already been sent through the relay.");

        using var response = await client.PostAsJsonAsync(
            new Uri(endpoint, "setup/initial-own-half"),
            new SimulatorControlRelayApplication.InitialOwnHalfSetupRequest(blueTeamOnPositiveHalf),
            sendCancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(sendCancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateDispatchException(response, responseBody,
                "Simulator setup relay rejected the initial setup dispatch.");
        }

        SimulatorSetupDispatchReceipt? dispatch;
        try
        {
            dispatch = JsonSerializer.Deserialize<SimulatorSetupDispatchReceipt>(responseBody, WebJsonOptions);
        }
        catch (JsonException exception)
        {
            throw CreateDispatchException(response, responseBody,
                "Simulator setup relay returned an unparsable dispatch receipt.", exception);
        }

        var validationError = ValidateDispatchReceipt(dispatch);
        if (validationError is not null)
        {
            throw CreateDispatchException(response, responseBody, validationError);
        }

        // Validation above must complete before the receipt becomes the key for the later GET.
        initialSetupDispatch = dispatch!;
        return dispatch!;
    }

    /// <summary>初期配置指令に対応する Simulator 応答観測を中継から取得します。</summary>
    /// <param name="cancellationToken">HTTP 応答待ちを中止するトークンです。</param>
    /// <returns>対応する応答観測です。未送信時は <see langword="null"/> を返します。</returns>
    /// <exception cref="HttpRequestException">中継の応答を取得できませんでした。</exception>
    /// <exception cref="InvalidOperationException">応答が要求 ID に対応しないか、観測を含みません。</exception>
    public async Task<SimulatorResponseObservation?> CompleteInitialSetupObservationAsync(
        CancellationToken cancellationToken = default)
    {
        if (initialSetupDispatch is null || initialSetupObservation is not null)
            return initialSetupObservation;

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(ObservationRequestTimeout);
        using var response = await client.GetAsync(
            new Uri(endpoint, $"setup/initial-own-half/observation/{initialSetupDispatch.RequestId}"),
            HttpCompletionOption.ResponseHeadersRead,
            requestTimeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync(requestTimeout.Token).ConfigureAwait(false);
        var observation = JsonSerializer.Deserialize<SimulatorResponseObservation>(responseJson, WebJsonOptions)
            ?? throw new InvalidOperationException("Simulator setup relay returned no observation.");
        if (string.IsNullOrWhiteSpace(observation.RequestId) ||
            !string.Equals(observation.RequestId, initialSetupDispatch.RequestId, StringComparison.Ordinal))
            throw new InvalidOperationException("Simulator setup relay returned an observation for a different request.");
        initialSetupObservation = observation;
        return initialSetupObservation;
    }

    /// <summary>指定位置へのボール配置指令を中継へ送ります。</summary>
    /// <param name="x">フィールド上の X 座標です。</param>
    /// <param name="y">フィールド上の Y 座標です。</param>
    /// <param name="cancellationToken">HTTP 要求を中止するトークンです。</param>
    /// <returns>中継からの応答確認を表す非同期処理です。</returns>
    /// <exception cref="HttpRequestException">中継が指令を受理しませんでした。</exception>
    public async Task SendBallToPositionAsync(float x, float y, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri(endpoint, "setup/place-ball"),
            new SimulatorControlRelayApplication.BallPlacementRequest(x, y),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>このクライアントが所有する HTTP クライアントを破棄します。</summary>
    public void Dispose()
    {
        if (ownsClient)
            client.Dispose();
    }

    private static string? ValidateDispatchReceipt(SimulatorSetupDispatchReceipt? dispatch)
    {
        if (dispatch is null)
            return "Simulator setup relay returned no dispatch receipt.";
        if (string.IsNullOrWhiteSpace(dispatch.RequestId))
            return "Simulator setup relay dispatch receipt has a blank request ID.";
        if (dispatch.SentDatagramBytes <= 0)
            return "Simulator setup relay dispatch receipt has a non-positive datagram byte count.";
        if (!IsSha256(dispatch.SentDatagramSha256))
            return "Simulator setup relay dispatch receipt has a missing or invalid datagram SHA-256.";
        return null;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static SimulatorControlRelayDispatchException CreateDispatchException(
        HttpResponseMessage response,
        string rawResponseBody,
        string details,
        Exception? innerException = null) =>
        new(response.StatusCode, response.ReasonPhrase, rawResponseBody, details, innerException);
}

/// <summary>Simulator 制御中継が初期配置指令を拒否したことを示します。</summary>
internal sealed class SimulatorControlRelayDispatchException : InvalidOperationException
{
    /// <summary>HTTP 応答と失敗理由を含む例外を作成します。</summary>
    /// <param name="statusCode">中継から返された HTTP 状態コードです。</param>
    /// <param name="reasonPhrase">中継から返された状態説明です。</param>
    /// <param name="rawResponseBody">中継の未加工応答本文です。</param>
    /// <param name="details">失敗の説明です。</param>
    /// <param name="innerException">元の JSON 解析例外です。存在しない場合は <see langword="null"/> です。</param>
    internal SimulatorControlRelayDispatchException(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string rawResponseBody,
        string details,
        Exception? innerException = null)
        : base($"{details} HTTP {(int)statusCode} {reasonPhrase ?? "(no reason phrase)"}; raw POST response body: {rawResponseBody}", innerException)
    {
        HttpStatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        RawResponseBody = rawResponseBody;
    }

    /// <summary>中継が返した HTTP 状態コードです。</summary>
    internal HttpStatusCode HttpStatusCode { get; }
    /// <summary>中継が返した状態説明です。</summary>
    internal string? ReasonPhrase { get; }
    /// <summary>中継から返された未加工の応答本文です。</summary>
    internal string RawResponseBody { get; }
}
