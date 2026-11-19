using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Tracker.Core;
using Tracker.RuntimeHost;

namespace Tracker.Performance;

/// <summary>
/// 固定入力の計測を実行し、照合に必要な条件と結果を JSON で出力する。
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>
    /// 引数、入力、設定を検証し、暖機・正式計測・品質照合を順番に実行する。
    /// </summary>
    /// <param name="args">コマンドラインの引数。</param>
    /// <param name="output">計測結果の出力先。</param>
    /// <param name="error">診断メッセージの出力先。</param>
    /// <param name="warmupClock">暖機の経過時間を測る時計。省略時は実際の単調時計を使う。</param>
    internal static int Run(string[] args, TextWriter output, TextWriter error, TimeProvider? warmupClock = null)
    {
        if (args is ["--help"])
        {
            output.WriteLine(CliOptions.Usage);
            return 0;
        }

        try
        {
            var options = CliOptions.Parse(args);
            var configuration = ReplayConfiguration.Load(options.SettingsPath, options.Profile);
            using var archive = File.OpenRead(options.ArchivePath);
            var window = VisionLogReader.Read(archive, options.DurationSeconds);
            return Measure(options, configuration, window, output, error, warmupClock ?? TimeProvider.System);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or IOException
            or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or OverflowException)
        {
            error.WriteLine(exception.Message);
            error.WriteLine(CliOptions.Usage);
            return 1;
        }
    }

    /// <summary>
    /// 暖機後の正式計測と、出力ハッシュを求める品質再生を分けて行う。
    /// </summary>
    private static int Measure(
        CliOptions options,
        ReplayConfiguration configuration,
        VisionWindow window,
        TextWriter output,
        TextWriter error,
        TimeProvider warmupClock)
    {
        var warmupStart = warmupClock.GetTimestamp();
        TimeSpan warmupElapsed;
        var warmupPasses = 0;
        do
        {
            ReplayRunner.Run(window, configuration, hashOutput: false);
            warmupPasses++;
            warmupElapsed = warmupClock.GetElapsedTime(warmupStart);
        }
        while (warmupPasses < 3 || warmupElapsed.TotalSeconds < options.WarmupSeconds);

        var measurements = new ReplayPass[options.Iterations];
        for (var pass = 0; pass < measurements.Length; pass++)
        {
            measurements[pass] = ReplayRunner.Run(window, configuration, hashOutput: false);
        }

        var quality1 = ReplayRunner.Run(window, configuration, hashOutput: true);
        var quality2 = ReplayRunner.Run(window, configuration, hashOutput: true);
        var deterministicWireOutput = quality1.WireSha256 == quality2.WireSha256;
        var consistentCounts = quality1.Counts == quality2.Counts
            && measurements.All(pass => pass.Counts == quality1.Counts);
        var hasTrackedOutput = quality1.Counts.CommittedFrames > 0 && quality1.Counts.OutputPackets > 0;
        var comparisonEligible = hasTrackedOutput && deterministicWireOutput && consistentCounts
            && quality1.Counts.PublishFailures == 0;
        var report = new
        {
            SchemaVersion = 1,
            ComparisonEligible = comparisonEligible,
            Source = new
            {
                options.SourceRevision,
                Tool = BuildIdentity(typeof(Program).Assembly),
                Core = BuildIdentity(typeof(TrackerEngine).Assembly),
                RuntimeHost = BuildIdentity(typeof(RuntimeHostOperationLoop).Assembly),
            },
            Environment = new
            {
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture,
                System.Environment.ProcessorCount,
                Stopwatch.Frequency,
                System.Runtime.GCSettings.IsServerGC,
            },
            Input = new
            {
                window.ArchiveSha256,
                window.PayloadSha256,
                window.ReachedWindowEnd,
                options.DurationSeconds,
                Count = window.Inputs.Count,
                FirstReceiveNs = window.Inputs[0].ReceiverTimestampNs,
                LastReceiveNs = window.Inputs[^1].ReceiverTimestampNs,
                CameraCount = window.CameraDetectionCounts.Count,
                window.CameraDetectionCounts,
                window.GeometryCount,
                window.MaximumRobotsPerInput,
                window.MaximumBallsPerInput,
            },
            Settings = new
            {
                FileSha256 = configuration.Sha256,
                configuration.IntervalMilliseconds,
                configuration.Tracker.EngineSettings,
                configuration.Tracker.PublisherOptions.SourceName,
                configuration.Tracker.PublisherOptions.Uuid,
            },
            Warmup = new { RequestedSeconds = options.WarmupSeconds, Passes = warmupPasses, warmupElapsed.TotalSeconds },
            Measurements = measurements,
            Quality = new
            {
                Runs = new[]
                {
                    new { quality1.WireSha256, quality1.Counts },
                    new { quality2.WireSha256, quality2.Counts },
                },
                DeterministicWireOutput = deterministicWireOutput,
                ConsistentCounts = consistentCounts,
            },
        };
        output.WriteLine(JsonSerializer.Serialize(report, ReportJsonOptions));

        if (!comparisonEligible)
        {
            error.WriteLine(hasTrackedOutput
                ? "再生間の出力または件数が一致しないか、配信の直列化に失敗しました。"
                : "確定または配信が 0 件のため、計測結果を比較に使用できません。");
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// ビルドの構成、SDK が埋め込んだバージョン、モジュール識別子を出力する。
    /// </summary>
    private static object BuildIdentity(Assembly assembly) => new
    {
        Configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
        InformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        assembly.ManifestModule.ModuleVersionId,
    };
}
