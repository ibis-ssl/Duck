using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
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
            var provider = new McapDiagnosticsLoggerProvider(directory);
            provider.CreateLogger("Duck.Tests.Reader").LogWarning("標準 MCAP 診断ログ");
            provider.Complete();

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions()),
                new TrackerDiagnosticsOptions { DirectoryPath = directory });
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
}
