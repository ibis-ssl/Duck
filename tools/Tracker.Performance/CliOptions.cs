using System.Globalization;

namespace Tracker.Performance;

/// <summary>
/// 再生の入力と反復条件。入力ファイルのパスは計測結果に書き出さない。
/// </summary>
/// <param name="ArchivePath">SSL ログの gzip ファイル。</param>
/// <param name="SettingsPath">指定した RuntimeHost の JSON 設定ファイル。</param>
/// <param name="SourceRevision">利用者が指定した Git コミットの完全な SHA。</param>
/// <param name="Profile">有効にするプロファイル。未指定時は設定ファイルに従う。</param>
/// <param name="DurationSeconds">最初の入力から選択する区間の長さ（秒）。</param>
/// <param name="Iterations">正式な計測パスの回数。</param>
/// <param name="WarmupSeconds">正式な計測前に行う暖機時間の下限（秒）。</param>
internal sealed record CliOptions(
    string ArchivePath,
    string SettingsPath,
    string SourceRevision,
    string? Profile,
    int DurationSeconds,
    int Iterations,
    int WarmupSeconds)
{
    /// <summary>
    /// 必須の入力パス、コミット、区間長と反復条件の引数を示す。
    /// </summary>
    internal const string Usage =
        "Tracker.Performance --archive <log.gz> --settings <appsettings.json> --source-revision <40桁のGit SHA> " +
        "[--profile <name>] [--duration-seconds <30>] [--iterations <7>] [--warmup-seconds <5以上>]";

    /// <summary>
    /// 引数を一度ずつ読み、ファイルを開く前に必須項目と数値範囲を検証する。
    /// </summary>
    internal static CliOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            var name = args[i];
            if (name is not ("--archive" or "--settings" or "--source-revision" or "--profile"
                or "--duration-seconds" or "--iterations" or "--warmup-seconds"))
            {
                throw new ArgumentException($"不明なオプションです: {name}");
            }

            if (i + 1 == args.Length || string.IsNullOrWhiteSpace(args[i + 1])
                || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"{name} の値を指定してください。");
            }

            if (!values.TryAdd(name, args[i + 1]))
            {
                throw new ArgumentException($"{name} は一度だけ指定してください。");
            }
        }

        var revision = Require("--source-revision");
        if (revision.Length != 40 || !revision.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("--source-revision に40桁の Git SHA を指定してください。");
        }

        return new CliOptions(
            Require("--archive"),
            Require("--settings"),
            revision.ToLowerInvariant(),
            values.GetValueOrDefault("--profile"),
            ReadNumber("--duration-seconds", 30, 1),
            ReadNumber("--iterations", 7, 1),
            ReadNumber("--warmup-seconds", 5, 5));

        string Require(string name) =>
            values.GetValueOrDefault(name) ?? throw new ArgumentException($"{name} は必須です。");

        int ReadNumber(string name, int fallback, int minimum)
        {
            if (!values.TryGetValue(name, out var value))
            {
                return fallback;
            }

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                || number < minimum)
            {
                throw new ArgumentException($"{name} に {minimum} 以上の整数を指定してください。");
            }

            return number;
        }
    }
}
