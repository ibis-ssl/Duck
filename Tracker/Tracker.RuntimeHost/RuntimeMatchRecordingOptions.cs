namespace Tracker.RuntimeHost;

/// <summary>RuntimeHost の試合録画設定。</summary>
public sealed class RuntimeMatchRecordingOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "RuntimeHost:MatchRecording";

    /// <summary>空の場合は録画しない。指定時は RuntimeHost の稼働中に一つの MCAP を記録する。</summary>
    public string DirectoryPath { get; init; } = string.Empty;
}
