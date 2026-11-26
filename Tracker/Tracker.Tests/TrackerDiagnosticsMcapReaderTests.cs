using System.Reflection;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components;
using Fizzy.McapSharp;
using Microsoft.Extensions.Options;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Core;
using Tracker.Diagnostics.Mcap;
using DiagnosticsPage = Tracker.DebugHost.Components.Pages.Diagnostics;

namespace Tracker.Tests;

/// <summary>
/// 診断ログ MCAP の読み取りと CaptureOn からの独立性を確認する。
/// </summary>
public sealed class TrackerDiagnosticsMcapReaderTests
{
    /// <summary>
    /// 何を確認しているか: 実際のホスト登録から正常停止し、停止ログを完成 MCAP 1 本から読み戻せること。
    /// </summary>
    [LinuxTheory]
    [InlineData("Tracker.RuntimeHost", "Tracker.RuntimeHost.dll")]
    [InlineData("Tracker.DebugHost", "Tracker.DebugHost.dll")]
    public async Task RegisteredHost_NormalStop_PreservesShutdownLogInOneMcap(string hostDirectory, string assemblyName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var buildConfiguration = ResolveBuildConfiguration();
        var hostOutputDirectory = Path.Combine(repositoryRoot, "Tracker", hostDirectory, "bin", buildConfiguration, "net10.0");
        var hostAssembly = Path.Combine(hostOutputDirectory, assemblyName);
        Assert.True(File.Exists(hostAssembly), $"Host assembly was not built: {hostAssembly}");
        var diagnosticsDirectory = Path.Combine(Path.GetTempPath(), $"duck-host-registration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(diagnosticsDirectory);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = hostOutputDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
            EnableRaisingEvents = true,
        };
        process.StartInfo.ArgumentList.Add(hostAssembly);
        process.StartInfo.Environment["Tracker__Diagnostics__DirectoryPath"] = diagnosticsDirectory;
        process.StartInfo.Environment["Tracker__Enabled"] = "false";
        process.StartInfo.Environment["Tracker__PublishUdp"] = "false";
        process.StartInfo.Environment["Tracker__Receive__Enabled"] = "false";
        process.StartInfo.Environment["VisionReceiver__Port"] = GetAvailableUdpPort().ToString(System.Globalization.CultureInfo.InvariantCulture);
        process.StartInfo.Environment["VisionReceiver__PacketCapture__Enabled"] = "false";
        process.StartInfo.Environment["Logging__LogLevel__Default"] = "Information";
        process.StartInfo.Environment["Logging__LogLevel__Microsoft"] = "Information";
        process.StartInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

        var output = new System.Collections.Concurrent.ConcurrentQueue<string>();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null) output.Enqueue(eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null) output.Enqueue(eventArgs.Data);
        };

        try
        {
            Assert.True(process.Start(), $"Could not start {assemblyName}.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var startupDeadline = Stopwatch.StartNew();
            while ((!Directory.EnumerateFiles(diagnosticsDirectory, "*.mcap.partial").Any() ||
                    !output.Any(line => line.Contains("Application started", StringComparison.Ordinal) ||
                                        line.Contains("Now listening on", StringComparison.Ordinal))) &&
                   !process.HasExited && startupDeadline.Elapsed < TimeSpan.FromSeconds(20))
            {
                await Task.Delay(50);
            }

            Assert.False(process.HasExited, $"{assemblyName} exited before startup completed: {string.Join(Environment.NewLine, output)}");
            Assert.Single(Directory.EnumerateFiles(diagnosticsDirectory, "*.mcap.partial"));
            Assert.Contains(output, line => line.Contains("Application started", StringComparison.Ordinal) ||
                                            line.Contains("Now listening on", StringComparison.Ordinal));

            var signalResult = SendSignal((int)process.Id, 2);
            Assert.True(signalResult == 0, $"Could not send SIGINT to {assemblyName} (errno {Marshal.GetLastPInvokeError()}): {string.Join(Environment.NewLine, output)}");
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(0, process.ExitCode);

            var completedPath = Assert.Single(Directory.EnumerateFiles(diagnosticsDirectory, "*.mcap"));
            Assert.Empty(Directory.EnumerateFiles(diagnosticsDirectory, "*.mcap.partial"));
            Assert.Empty(Directory.EnumerateFiles(diagnosticsDirectory, "*.log"));

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions()),
                new TrackerDiagnosticsOptions { DirectoryPath = diagnosticsDirectory });
            var listing = reader.ListFiles();
            var listedFile = Assert.Single(listing, file => file.FullPath == completedPath);
            var snapshot = reader.ReadFile(listedFile.FullPath);
            Assert.Null(snapshot.Error);
            Assert.Contains(snapshot.Entries, entry =>
                entry.Message?.Contains("Application is shutting down", StringComparison.Ordinal) == true);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            Directory.Delete(diagnosticsDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: 直下の MCAP と、先行するアクセス不能フォルダーの後にある健全な兄弟フォルダーの MCAP を残し、障害を警告すること。
    /// </summary>
    [Fact]
    public void ListFiles_InaccessibleSubfolder_WarnsAndKeepsRootAndLaterSiblingMcaps()
    {
        var root = Path.Combine(Path.GetTempPath(), $"duck-mcap-sibling-recovery-{Guid.NewGuid():N}");
        var diagnosticsDirectory = Path.Combine(root, "diagnostics");
        var blockedDirectory = Path.Combine(diagnosticsDirectory, "a-inaccessible");
        var healthyDirectory = Path.Combine(diagnosticsDirectory, "z-healthy");
        Directory.CreateDirectory(blockedDirectory);
        Directory.CreateDirectory(healthyDirectory);
        WriteMcap(diagnosticsDirectory, "直下の診断ログ");
        WriteMcap(blockedDirectory, "一覧に出してはいけないログ");
        WriteMcap(healthyDirectory, "後続の兄弟ログ");

        try
        {
            var inspectedDirectories = new List<string>();
            FileAttributes GetAttributes(string path)
            {
                var fullPath = Path.GetFullPath(path);
                if (fullPath == Path.GetFullPath(blockedDirectory))
                {
                    inspectedDirectories.Add("inaccessible");
                    throw new UnauthorizedAccessException("故障注入: サブフォルダーを開けません。");
                }

                if (fullPath == Path.GetFullPath(healthyDirectory))
                {
                    inspectedDirectories.Add("healthy");
                }

                return File.GetAttributes(path);
            }

            IEnumerable<string> EnumerateDirectories(string path)
            {
                if (Path.GetFullPath(path) == Path.GetFullPath(diagnosticsDirectory))
                {
                    // 返却順を固定し、障害フォルダーを健全な兄弟より先に確認させる。
                    return [blockedDirectory, healthyDirectory];
                }

                return [];
            }

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions()),
                new TrackerDiagnosticsOptions { DirectoryPath = diagnosticsDirectory },
                Directory.EnumerateFiles,
                GetAttributes,
                EnumerateDirectories);

            var listing = reader.ListFilesWithMcapFallback();

            Assert.Contains("故障注入: サブフォルダーを開けません。", listing.Warning, StringComparison.Ordinal);
            Assert.Equal(["inaccessible", "healthy"], inspectedDirectories);
            Assert.Equal(2, listing.Files.Count);
            Assert.Contains(listing.Files, file => Path.GetDirectoryName(file.FullPath) == diagnosticsDirectory);
            Assert.Contains(listing.Files, file => Path.GetDirectoryName(file.FullPath) == healthyDirectory);
            Assert.DoesNotContain(listing.Files, file => Path.GetDirectoryName(file.FullPath) == blockedDirectory);

            var rootSnapshot = reader.ReadFile(Assert.Single(listing.Files, file => Path.GetDirectoryName(file.FullPath) == diagnosticsDirectory).FullPath);
            var siblingSnapshot = reader.ReadFile(Assert.Single(listing.Files, file => Path.GetDirectoryName(file.FullPath) == healthyDirectory).FullPath);
            Assert.Null(rootSnapshot.Error);
            Assert.Null(siblingSnapshot.Error);
            Assert.Equal("直下の診断ログ", Assert.Single(rootSnapshot.Entries).Message);
            Assert.Equal("後続の兄弟ログ", Assert.Single(siblingSnapshot.Entries).Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteMcap(string directory, string message)
    {
        using var provider = new McapDiagnosticsLoggerProvider(directory);
        provider.CreateLogger("Duck.Tests.InaccessibleSubfolder").LogWarning(message);
        provider.Complete();
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int SendSignal(int processId, int signalNumber);

    private static int GetAvailableUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

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

            var listing = reader.ListFilesWithMcapFallback();
            Assert.Contains("CaptureOn の列挙を故障注入しました。", listing.Warning, StringComparison.Ordinal);
            Assert.EndsWith(".mcap", Assert.Single(listing.Files).FileName, StringComparison.Ordinal);
            var snapshot = reader.ReadFile(mcapPath);
            Assert.Null(snapshot.Error);
            Assert.Equal("CaptureOn 列挙に依存しない MCAP", Assert.Single(snapshot.Entries).Message);
        }
        finally
        {
            Directory.Delete(diagnosticsDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: MCAP の列挙障害が CaptureOn の既存ログ一覧と直接読取を妨げないこと。
    /// </summary>
    [Fact]
    public void ListFilesAndReadLegacyLog_KeepCaptureOnAvailableWhenMcapEnumerationFails()
    {
        var root = Path.Combine(Path.GetTempPath(), $"duck-mcap-listing-isolation-{Guid.NewGuid():N}");
        var diagnosticsDirectory = Path.Combine(root, "diagnostics");
        var captureDirectory = Path.Combine(root, "captures");
        Directory.CreateDirectory(diagnosticsDirectory);
        Directory.CreateDirectory(captureDirectory);
        var legacyPath = Path.Combine(captureDirectory, "tracker-diagnostics-existing.log");
        File.WriteAllText(legacyPath,
            "2026-05-10T09:38:31.3513054+00:00 Tracker diagnostics profile=sim rawFrame=1 trackedFrame=1");

        try
        {
            IEnumerable<string> EnumerateFiles(string directory, string pattern, SearchOption option)
            {
                if (string.Equals(Path.GetFullPath(directory), Path.GetFullPath(diagnosticsDirectory), StringComparison.Ordinal) &&
                    pattern == "*.mcap")
                {
                    throw new IOException("MCAP の列挙を故障注入しました。");
                }

                return Directory.EnumerateFiles(directory, pattern, option);
            }

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions
                {
                    PacketCapture = new VisionPacketCaptureOptions { DirectoryPath = captureDirectory },
                }),
                new TrackerDiagnosticsOptions { DirectoryPath = diagnosticsDirectory },
                EnumerateFiles);

            var listing = reader.ListFilesWithMcapFallback();
            Assert.Contains("MCAP の列挙を故障注入しました。", listing.Warning, StringComparison.Ordinal);
            var listedLog = Assert.Single(listing.Files);
            Assert.Equal(legacyPath, listedLog.FullPath);

            var snapshot = reader.ReadFile(legacyPath);
            Assert.Null(snapshot.Error);
            Assert.Single(snapshot.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: まだ作られていない任意の保存先は一覧障害として警告しないこと。
    /// </summary>
    [Fact]
    public void ListFiles_MissingOptionalDirectories_DoNotWarn()
    {
        var root = Path.Combine(Path.GetTempPath(), $"duck-mcap-missing-directories-{Guid.NewGuid():N}");
        var diagnosticsDirectory = Path.Combine(root, "diagnostics");
        var captureDirectory = Path.Combine(root, "captures");
        var reader = new TrackerDiagnosticsLogReader(
            Options.Create(new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions { DirectoryPath = captureDirectory },
            }),
            new TrackerDiagnosticsOptions { DirectoryPath = diagnosticsDirectory });

        var listing = reader.ListFilesWithMcapFallback();

        Assert.Empty(listing.Files);
        Assert.Null(listing.Warning);
    }

    /// <summary>
    /// 何を確認しているか: MCAP 保存先へのアクセス拒否を警告し、CaptureOn の既存ログ一覧と読取を維持すること。
    /// </summary>
    [Fact]
    public void ListFiles_AccessDeniedMcapDirectory_WarnsAndKeepsCaptureLogReadable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"duck-mcap-denied-directory-{Guid.NewGuid():N}");
        var diagnosticsDirectory = Path.Combine(root, "diagnostics");
        var captureDirectory = Path.Combine(root, "captures");
        Directory.CreateDirectory(diagnosticsDirectory);
        Directory.CreateDirectory(captureDirectory);
        var legacyPath = Path.Combine(captureDirectory, "tracker-diagnostics-existing.log");
        File.WriteAllText(legacyPath,
            "2026-05-10T09:38:31.3513054+00:00 Tracker diagnostics profile=sim rawFrame=1 trackedFrame=1");

        try
        {
            FileAttributes GetAttributes(string path)
            {
                if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(diagnosticsDirectory), StringComparison.Ordinal))
                {
                    throw new UnauthorizedAccessException("診断保存先へのアクセスを拒否しました。");
                }

                return File.GetAttributes(path);
            }

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions
                {
                    PacketCapture = new VisionPacketCaptureOptions { DirectoryPath = captureDirectory },
                }),
                new TrackerDiagnosticsOptions { DirectoryPath = diagnosticsDirectory },
                Directory.EnumerateFiles,
                GetAttributes);

            var listing = reader.ListFilesWithMcapFallback();
            Assert.Contains("診断保存先へのアクセスを拒否しました。", listing.Warning, StringComparison.Ordinal);
            Assert.Equal(legacyPath, Assert.Single(listing.Files).FullPath);

            var snapshot = reader.ReadFile(legacyPath);
            Assert.Null(snapshot.Error);
            Assert.Single(snapshot.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: 消失した明示 FilePath の確認で警告や例外を起こさず、既存 MCAP の一覧と読取を維持すること。
    /// </summary>
    [Fact]
    public void ListFiles_MissingConfiguredFile_DoesNotAffectExistingMcapListingOrRead()
    {
        var diagnosticsDirectory = Path.Combine(Path.GetTempPath(), $"duck-mcap-missing-configured-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(diagnosticsDirectory);
        var missingPath = Path.Combine(diagnosticsDirectory, "removed-diagnostics.log");

        try
        {
            using (var provider = new McapDiagnosticsLoggerProvider(diagnosticsDirectory))
            {
                provider.CreateLogger("Duck.Tests.MissingConfiguredFile").LogWarning("既存 MCAP は読み取れる");
                provider.Complete();
            }

            var missingFileWasChecked = false;
            FileAttributes GetAttributes(string path)
            {
                if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(missingPath), StringComparison.Ordinal))
                {
                    missingFileWasChecked = true;
                    throw new FileNotFoundException("明示ログは確認前に消失しました。", path);
                }

                return File.GetAttributes(path);
            }

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions()),
                new TrackerDiagnosticsOptions { DirectoryPath = diagnosticsDirectory, FilePath = missingPath },
                Directory.EnumerateFiles,
                GetAttributes);

            var listing = reader.ListFilesWithMcapFallback();
            Assert.True(missingFileWasChecked);
            Assert.Null(listing.Warning);
            var mcapFile = Assert.Single(listing.Files);
            Assert.EndsWith(".mcap", mcapFile.FileName, StringComparison.Ordinal);

            var snapshot = reader.ReadFile(mcapFile.FullPath);
            Assert.Null(snapshot.Error);
            Assert.Equal("既存 MCAP は読み取れる", Assert.Single(snapshot.Entries).Message);
        }
        finally
        {
            Directory.Delete(diagnosticsDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: 初期表示と再読込で CaptureOn 一覧障害を警告しながら MCAP を選択・読取できること。
    /// </summary>
    [Fact]
    public async Task DiagnosticsPage_InitialLoadAndReload_KeepMcapAvailableWhenCaptureListingFails()
    {
        var diagnosticsDirectory = Path.Combine(Path.GetTempPath(), $"duck-mcap-page-recovery-{Guid.NewGuid():N}");
        var captureDirectory = Path.Combine(diagnosticsDirectory, "captures");
        Directory.CreateDirectory(captureDirectory);

        try
        {
            using (var provider = new McapDiagnosticsLoggerProvider(diagnosticsDirectory))
            {
                provider.CreateLogger("Duck.Tests.Page").LogWarning("診断画面へ表示する MCAP");
                provider.Complete();
            }

            IEnumerable<string> EnumerateFiles(string directory, string pattern, SearchOption option)
            {
                if (string.Equals(Path.GetFullPath(directory), Path.GetFullPath(captureDirectory), StringComparison.Ordinal))
                {
                    throw new IOException("CaptureOn 一覧の故障注入");
                }

                return Directory.EnumerateFiles(directory, pattern, option);
            }

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions
                {
                    PacketCapture = new VisionPacketCaptureOptions { DirectoryPath = captureDirectory },
                }),
                new TrackerDiagnosticsOptions { DirectoryPath = diagnosticsDirectory },
                EnumerateFiles);
            var page = new DiagnosticsPage();
            SetInjectedProperty(page, "LogReader", reader);
            SetInjectedProperty(page, "ComparisonReader", new TrackerDiagnosticsComparisonViewStateReader());

            typeof(DiagnosticsPage)
                .GetMethod("OnInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page, null);
            AssertMcapFallbackState(page);

            var reload = typeof(DiagnosticsPage).GetMethod("ReloadAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)reload.Invoke(page, null)!;
            AssertMcapFallbackState(page);

            var markupPath = Path.Combine(FindRepositoryRoot(), "Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor");
            var markup = File.ReadAllText(markupPath);
            Assert.Contains("@fileListWarning", markup, StringComparison.Ordinal);
            Assert.Contains("role=\"alert\"", markup, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(diagnosticsDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: MCAP の表示件数、読み飛ばし、上限による省略を選択中のログに合わせて表示すること。
    /// </summary>
    [Fact]
    public async Task DiagnosticsPage_McapEntrySummary_FollowsSelectedLogAndReportsLimit()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-mcap-page-counts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var limitedPath = Path.Combine(directory, "limited.mcap");
            using (var writer = new McapWriter(File.Create(limitedPath), new McapWriterOptions
            {
                UseChunks = false,
                EmitSummaryRecords = true,
            }))
            {
                var schema = writer.RegisterSchema("foxglove.Log", "jsonschema", "{}"u8.ToArray());
                var channel = writer.RegisterChannel("/diagnostics", "json", schema, null);
                for (uint index = 0; index < 10_002; index++)
                {
                    var header = new McapMessageHeader(channel, index, index, index);
                    writer.WriteToKnownChannel(in header,
                        """{"timestamp":{"sec":1,"nsec":0},"level":3,"message":"valid","name":"test","file":"","line":0}"""u8.ToArray());
                }

                var malformed = new McapMessageHeader(channel, 10_002, 10_002, 10_002);
                writer.WriteToKnownChannel(in malformed, "{}"u8.ToArray());
                writer.Complete();
            }

            using (var provider = new McapDiagnosticsLoggerProvider(directory))
            {
                provider.CreateLogger("Duck.Tests.Page").LogWarning("normal");
                provider.Complete();
            }

            var normalPath = Directory.GetFiles(directory, "*.mcap").Single(path => path != limitedPath);
            var legacyPath = Path.Combine(directory, "tracker-diagnostics-existing.log");
            File.WriteAllText(legacyPath,
                "2026-05-10T09:38:31.3513054+00:00 Tracker diagnostics profile=sim rawFrame=1 trackedFrame=1");
            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions
                {
                    PacketCapture = new VisionPacketCaptureOptions { DirectoryPath = directory },
                }),
                new TrackerDiagnosticsOptions { DirectoryPath = directory });

            var page = new DiagnosticsPage();
            SetInjectedProperty(page, "LogReader", reader);
            SetInjectedProperty(page, "ComparisonReader", new TrackerDiagnosticsComparisonViewStateReader());
            SetInjectedProperty(page, "RenderSnapshotReader", new TrackerRenderSnapshotLogReader(reader));
            typeof(DiagnosticsPage).GetMethod("OnInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page, null);
            var change = typeof(DiagnosticsPage).GetMethod("OnLogFileChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var summary = typeof(DiagnosticsPage).GetProperty("McapEntrySummary", BindingFlags.Instance | BindingFlags.NonPublic)!;

            await (Task)change.Invoke(page, [new ChangeEventArgs { Value = limitedPath }])!;
            Assert.Equal("Displayed: 10000; Skipped: 1; Omitted by display limit: 2. Omitted entries remain in the original MCAP file.", summary.GetValue(page));
            Assert.Equal(10_002, reader.ReadFile(limitedPath, maxEntries: 10_002).Entries.Count);

            await (Task)change.Invoke(page, [new ChangeEventArgs { Value = normalPath }])!;
            Assert.Equal("Displayed: 1; Skipped: 0; Omitted by display limit: 0.", summary.GetValue(page));

            await (Task)change.Invoke(page, [new ChangeEventArgs { Value = legacyPath }])!;
            Assert.Null(summary.GetValue(page));
            Assert.Single(GetPrivateField<IReadOnlyList<TrackerDiagnosticsLogEntry>>(page, "entries"));

            await (Task)change.Invoke(page, [new ChangeEventArgs { Value = limitedPath }])!;
            Assert.Contains("Skipped: 1; Omitted by display limit: 2.", (string)summary.GetValue(page)!);

            var markup = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
                "Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor"));
            Assert.Contains("@if (McapEntrySummary is { } summary)", markup, StringComparison.Ordinal);
            Assert.Contains("role=\"status\">@summary", markup, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertMcapFallbackState(DiagnosticsPage page)
    {
        var warning = GetPrivateField<string>(page, "fileListWarning");
        var files = GetPrivateField<IReadOnlyList<TrackerDiagnosticsLogFile>>(page, "logFiles");
        var selectedPath = GetPrivateField<string?>(page, "selectedLogPath");
        var snapshot = GetPrivateField<TrackerDiagnosticsLogSnapshot?>(page, "snapshot");

        Assert.Contains("CaptureOn 一覧の故障注入", warning, StringComparison.Ordinal);
        Assert.Equal(".mcap", Path.GetExtension(Assert.Single(files).FullPath));
        Assert.Equal(files[0].FullPath, selectedPath);
        Assert.Null(snapshot?.Error);
        Assert.Equal("診断画面へ表示する MCAP", Assert.Single(snapshot!.Entries).Message);
    }

    private static void SetInjectedProperty<TValue>(DiagnosticsPage page, string name, TValue value)
    {
        var property = typeof(DiagnosticsPage).GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var availableProperties = string.Join(
            ", ",
            typeof(DiagnosticsPage)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(candidate => candidate.Name));
        Assert.True(property is not null, $"Injected property {name} was not found. Available: {availableProperties}");
        property!.SetValue(page, value);
    }

    private static TValue GetPrivateField<TValue>(DiagnosticsPage page, string name)
    {
        var field = typeof(DiagnosticsPage).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<TValue>(field!.GetValue(page));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tracker/Tracker.DebugHost/Program.cs")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Duck repository root was not found.");
    }

    private static string ResolveBuildConfiguration()
    {
        var outputDirectory = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        while (outputDirectory.Parent is not null &&
               !string.Equals(outputDirectory.Parent.Name, "bin", StringComparison.OrdinalIgnoreCase))
        {
            outputDirectory = outputDirectory.Parent;
        }

        if (!string.Equals(outputDirectory.Parent?.Name, "bin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"ビルド構成を出力パスから解決できません: {AppContext.BaseDirectory}");
        }

        return outputDirectory.Name;
    }
}

/// <summary>Linux 実ホスト試験を Linux 以外では xUnit 検出時にスキップします。</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class LinuxTheoryAttribute : TheoryAttribute
{
    /// <summary>Linux 以外では実ホストの SIGINT 確認を実行せず、スキップとして表示します。</summary>
    public LinuxTheoryAttribute()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip = "実ホストの SIGINT 終了確認は Linux 向けです。";
        }
    }
}
