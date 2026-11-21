using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Tracker.Core;
using Tracker.RuntimeHost;

namespace Tracker.Performance;

/// <summary>
/// 1 つの設定ファイルから解決した追跡設定と周期。構成の識別用ハッシュを同じバイト列から求める。
/// </summary>
/// <param name="Tracker">RuntimeHost の設定解決処理が返した設定。</param>
/// <param name="IntervalMilliseconds">仮想周期の間隔（ミリ秒）。</param>
/// <param name="Sha256">指定した設定ファイル全体の SHA-256。</param>
internal sealed record ReplayConfiguration(
    TrackerRuntimeResolvedOptions Tracker,
    int IntervalMilliseconds,
    string Sha256)
{
    /// <summary>
    /// RuntimeHost と同じバインドおよびプロファイル解決を、実行サービスを起動せずに適用する。
    /// </summary>
    internal static ReplayConfiguration Load(string path, string? profile)
    {
        var bytes = File.ReadAllBytes(path);
        using var json = new MemoryStream(bytes, writable: false);
        using var configuration = new ConfigurationManager();
        configuration.AddJsonStream(json);
        if (profile is not null)
        {
            RuntimeHostCommandLine.ApplyOverrides(configuration, ["--profile", profile]);
        }

        var trackerOptions = new RuntimeTrackerOptions();
        configuration.GetSection(RuntimeTrackerOptions.SectionName).Bind(trackerOptions);
        var hostOptions = new RuntimeHostOptions();
        configuration.GetSection(RuntimeHostOptions.SectionName).Bind(hostOptions);
        if (hostOptions.OperationLoopIntervalMilliseconds <= 0)
        {
            throw new InvalidDataException("RuntimeHost の周期には正のミリ秒を指定してください。");
        }

        var resolved = RuntimeTrackerConfigurationResolver.Resolve(trackerOptions);
        if (!resolved.Enabled)
        {
            throw new InvalidDataException("有効な追跡処理を計測するために Tracker:Enabled を有効にしてください。");
        }

        return new ReplayConfiguration(
            resolved,
            hostOptions.OperationLoopIntervalMilliseconds,
            Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
