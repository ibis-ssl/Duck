using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Duck.Testing.AppHost;

public sealed record CraneProbeOutcome(
    int ExitCode,
    long ElapsedMilliseconds,
    bool SetupMarkerFound,
    bool CoordinatorFound,
    bool Ready,
    string Classification,
    string StandardOutputExcerpt,
    bool StandardOutputTruncated,
    string StandardErrorExcerpt,
    bool StandardErrorTruncated);

public sealed record CraneProbeProgress(int Attempt, CraneProbeOutcome Outcome);

public sealed class CraneProbeDiagnosticState
{
    private readonly Stopwatch emissionClock = Stopwatch.StartNew();
    private string? lastClassification;
    private TimeSpan lastEmission;

    public int AttemptCount { get; private set; }

    public CraneProbeOutcome? Latest { get; private set; }

    public CraneProbeProgress? Record(CraneProbeOutcome outcome) => Record(outcome, emissionClock.Elapsed);

    internal CraneProbeProgress? Record(CraneProbeOutcome outcome, TimeSpan monotonicNow)
    {
        AttemptCount++;
        Latest = outcome;
        var classificationChanged = !string.Equals(lastClassification, outcome.Classification, StringComparison.Ordinal);
        var heartbeatElapsed = monotonicNow - lastEmission >= TimeSpan.FromSeconds(2);
        if (!classificationChanged && !heartbeatElapsed)
        {
            return null;
        }

        lastClassification = outcome.Classification;
        lastEmission = monotonicNow;
        return new CraneProbeProgress(AttemptCount, outcome);
    }

    public string CreateFinalSummary(bool shutdownRequested = false) => JsonSerializer.Serialize(new
    {
        event_name = "duck_crane_probe_final",
        recorded_at_utc = DateTimeOffset.UtcNow,
        shutdown_requested = shutdownRequested,
        completed_attempts = AttemptCount,
        attempts = AttemptCount,
        latest_attempt = AttemptCount,
        latest = Latest,
    });
}

public static partial class CraneProbeDiagnostics
{
    public const string SetupMarker = "DUCK_CRANE_ROS_SETUP_OK";
    public const int ExcerptLimit = 512;

    private static readonly Regex SecretField = new(
        "(?<key>['\\\"]?[A-Za-z0-9_.-]*(?:token|password|passwd|secret|credential|private[_-]?key|client[_-]?key|api[_-]?key|access[_-]?key|authorization)[A-Za-z0-9_.-]*['\\\"]?)(?<separator>\\s*[:=]\\s*)(?<value>\\\"(?:\\\\.|[^\\\"\\\\])*\\\"|'(?:''|\\\\.|[^'])*'|[^\\s,;}\\]]+)",
        RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Bearer = new(@"\bBearer\s+[A-Za-z0-9._~+/-]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PrivateKeyBlock = new(
        "-----BEGIN [^-]*PRIVATE KEY-----.*?-----END [^-]*PRIVATE KEY-----",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex DashboardToken = new(@"([?&]t=)[A-Za-z0-9._~+/-]+={0,2}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string[] CreateCraneReadinessProbeArguments(string containerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        return
        [
            "exec", containerId,
            "timeout", "--signal=TERM", "--kill-after=2s", "10s",
            "bash", "-lc", $"source /root/ibis_ws/install/setup.bash && printf '{SetupMarker}\\n' && exec ros2 node list",
        ];
    }

    public static CraneProbeOutcome Evaluate(int exitCode, string standardOutput, string standardError, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        var graphOutput = OutputAfterExactMarker(standardOutput, out var markerFound);
        var coordinatorFound = markerFound && DockerServiceReadiness.CraneHasCoordinator(graphOutput);
        var ready = exitCode == 0 && coordinatorFound;
        var classification = exitCode == 124
            ? markerFound ? "ros_graph_timeout" : "setup_timeout"
            : exitCode == 137
                ? markerFound ? "ros_graph_killed" : "setup_killed"
            : exitCode != 0
                ? markerFound ? "ros_graph_nonzero" : "setup_nonzero"
                : !markerFound ? "setup_marker_missing"
                : coordinatorFound ? "coordinator_match"
                : "coordinator_missing";

        var sanitizedOutput = Sanitize(standardOutput);
        var sanitizedError = Sanitize(standardError);
        return new CraneProbeOutcome(
            exitCode,
            Math.Max(0, (long)elapsed.TotalMilliseconds),
            markerFound,
            coordinatorFound,
            ready,
            classification,
            Excerpt(sanitizedOutput, out var outputTruncated),
            outputTruncated,
            Excerpt(sanitizedError, out var errorTruncated),
            errorTruncated);
    }

    public static string Sanitize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = PrivateKeyBlock.Replace(text, "[REDACTED PRIVATE KEY BLOCK]");
        text = Bearer.Replace(text, "Bearer [REDACTED]");
        text = DashboardToken.Replace(text, "$1[REDACTED]");
        return SecretField.Replace(text, match =>
        {
            var value = match.Groups["value"].Value;
            var replacement = value.StartsWith('"') ? "\"[REDACTED]\""
                : value.StartsWith('\'') ? "'[REDACTED]'"
                : "[REDACTED]";
            return match.Groups["key"].Value + match.Groups["separator"].Value + replacement;
        });
    }

    public static string SerializeProgress(CraneProbeProgress progress) => JsonSerializer.Serialize(new
    {
        event_name = "duck_crane_probe_progress",
        recorded_at_utc = DateTimeOffset.UtcNow,
        attempt = progress.Attempt,
        progress.Outcome,
    });

    public static string SerializeAttemptStarted(int attempt) => JsonSerializer.Serialize(new
    {
        event_name = "duck_crane_probe_started",
        recorded_at_utc = DateTimeOffset.UtcNow,
        attempt,
    });

    internal static void WriteRecord(string? path, string jsonLine, Action<string> secondaryWriter)
    {
        // The producer sanitizes full probe output before serializing bounded excerpts.
        // Open/append/close for each record so cancellation cannot strand a buffered record.
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                File.AppendAllText(path, jsonLine + "\n", new System.Text.UTF8Encoding(false));
            }
            catch (Exception)
            {
                TryWriteSecondary(secondaryWriter, "{\"event_name\":\"duck_crane_diagnostics_write_failed\"}");
            }
        }

        TryWriteSecondary(secondaryWriter, jsonLine);
    }

    private static void TryWriteSecondary(Action<string> writer, string jsonLine)
    {
        try
        {
            writer(jsonLine);
        }
        catch (Exception)
        {
            // Diagnostic transport must not change readiness, retries, or owned cleanup.
        }
    }

    private static string OutputAfterExactMarker(string output, out bool markerFound)
    {
        var lines = output.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (!string.Equals(lines[index].TrimEnd('\r'), SetupMarker, StringComparison.Ordinal))
            {
                continue;
            }

            markerFound = true;
            return string.Join('\n', lines.Skip(index + 1));
        }

        markerFound = false;
        return string.Empty;
    }

    private static string Excerpt(string text, out bool truncated)
    {
        truncated = text.Length > ExcerptLimit;
        return truncated ? text[..ExcerptLimit] : text;
    }
}
