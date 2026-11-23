using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Core;
using Tracker.Diagnostics.Mcap;

namespace Tracker.Tests;

/// <summary>
/// 診断ログ MCAP の読み取りと CaptureOn からの独立性を確認する。
/// </summary>
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

    /// <summary>
    /// 何を確認しているか: CaptureOn のファイル列挙が失敗しても、診断ディレクトリの MCAP を直接読み取れること。
    /// </summary>
    [Fact]
    public void ReadFile_ReadsMcapWithoutEnumeratingCaptureFiles()
    {
        var diagnosticsDirectory = Path.Combine(Path.GetTempPath(), $"duck-mcap-independent-{Guid.NewGuid():N}");
        var captureDirectory = Path.Combine(diagnosticsDirectory, "captures");
        Directory.CreateDirectory(captureDirectory);
        string mcapPath;

        try
        {
            using (var provider = new McapDiagnosticsLoggerProvider(diagnosticsDirectory))
            {
                provider.CreateLogger("Duck.Tests.Reader").LogWarning("CaptureOn 列挙に依存しない MCAP");
                provider.Complete();
            }

            mcapPath = Assert.Single(Directory.EnumerateFiles(diagnosticsDirectory, "*.mcap"));
            var visionOptions = new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions { DirectoryPath = captureDirectory },
            };
            IEnumerable<string> EnumerateFiles(string directory, string pattern, SearchOption option)
            {
                if (string.Equals(Path.GetFullPath(directory), Path.GetFullPath(captureDirectory), StringComparison.Ordinal))
                {
                    throw new IOException("CaptureOn の列挙を故障注入しました。");
                }

                return Directory.EnumerateFiles(directory, pattern, option);
            }

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(visionOptions),
                new TrackerDiagnosticsOptions { DirectoryPath = diagnosticsDirectory },
                EnumerateFiles);

            Assert.Throws<IOException>(() => reader.ListFiles());
            var snapshot = reader.ReadFile(mcapPath);
            Assert.Null(snapshot.Error);
            Assert.Equal("CaptureOn 列挙に依存しない MCAP", Assert.Single(snapshot.Entries).Message);
        }
        finally
        {
            Directory.Delete(diagnosticsDirectory, recursive: true);
        }
    }
}
