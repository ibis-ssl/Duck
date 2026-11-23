using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Core;
using Tracker.Diagnostics.Mcap;

namespace Tracker.Tests;

public sealed class TrackerDiagnosticsMcapReaderTests
{
    /// <summary>
    /// 何を確認しているか: foxglove.Log の MCAP 1本を一覧・読み取り、発生元、重要度、時刻、本文を表示用に保つこと。
    /// </summary>
    [Fact]
    public void ReadFile_ListsAndParsesStandardMcapDiagnostics()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-mcap-reader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var trackerOptions = new TrackerOptions
            {
                Diagnostics = new TrackerDiagnosticsOptions { DirectoryPath = directory },
            };
            var resolvedOptions = TrackerConfigurationResolver.Resolve(trackerOptions);
            var provider = new McapDiagnosticsLoggerProvider(resolvedOptions.Diagnostics.DirectoryPath);
            provider.CreateLogger("Duck.Tests.Reader").LogWarning("標準 MCAP 診断ログ");
            provider.Complete();

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions()),
                resolvedOptions.Diagnostics);
            var file = Assert.Single(reader.ListFiles());
            var snapshot = reader.ReadFile(file.FullPath);

            Assert.EndsWith(".mcap", file.FileName, StringComparison.Ordinal);
            Assert.Null(snapshot.Error);
            var entry = Assert.Single(snapshot.Entries);
            Assert.Equal(3, entry.FoxgloveLevel);
            Assert.Equal("Duck.Tests.Reader", entry.SourceName);
            Assert.Equal("標準 MCAP 診断ログ", entry.Message);
            Assert.NotEqual(DateTimeOffset.MinValue, entry.Timestamp);
            Assert.Equal("", entry.SourceFile);
            Assert.Equal(0, entry.SourceLine);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: 未完了の MCAP を自動一覧と明示パスのどちらからも閲覧できないこと。
    /// </summary>
    [Fact]
    public void ListFilesAndReadFile_ExcludeIncompleteMcap()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-mcap-partial-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var partialPath = Path.Combine(directory, "run.mcap.partial");
        File.WriteAllBytes(partialPath, [0x89, 0x4D, 0x43, 0x41, 0x50]);

        try
        {
            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions()),
                new TrackerDiagnosticsOptions { DirectoryPath = directory, FilePath = partialPath });

            Assert.Empty(reader.ListFiles());
            var snapshot = reader.ReadFile(partialPath);
            Assert.Empty(snapshot.Entries);
            Assert.Contains("Incomplete", snapshot.Error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
