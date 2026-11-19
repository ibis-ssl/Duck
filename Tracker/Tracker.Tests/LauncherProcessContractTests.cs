using System.Diagnostics;

namespace Tracker.Tests;

/// <summary>起動元の作業木選択、ログ確保、AppHost の終了値とログ回収を検証します。</summary>
public sealed class LauncherProcessContractTests
{
    /// <summary>別の作業木がカレントでも、起動ファイルが配置された作業木を選ぶことを確認します。</summary>
    [Fact]
    public void RootComesFromTheLauncherFile()
    {
        using var directory = new TemporaryDirectory();
        var other = directory.CreateRepo("other");
        var desired = directory.CreateRepo("desired");

        Assert.NotEqual(other, Launcher.FindRepositoryRoot(Path.Combine(desired, "scripts", "start.cs")));
        Assert.Equal(desired, Launcher.FindRepositoryRoot(Path.Combine(desired, "scripts", "start.cs")));
        Assert.Throws<InvalidOperationException>(() => Launcher.FindRepositoryRoot("relative.cs"));
    }

    /// <summary>停止 ID の省略と末尾 LF を拒否し、別のスタックを誤って選ぶのを防ぐことを確認します。</summary>
    [Fact]
    public void StopRequiresAnExplicitIdWithoutTrailingNewline()
    {
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--stop-persistent"]));
        Assert.Throws<InvalidOperationException>(() => Launcher.Options.Parse(
            ["--stop-persistent", "--stack-id", "test-stack\n"]));
    }

    /// <summary>Docker の stack/run filter を通過する所有者混在を検出し、削除前に停止することを確認します。</summary>
    [Fact]
    public async Task StopRejectsMixedOwnersBeforeRemovingAnyContainer()
    {
        using var directory = new TemporaryDirectory();
        var commands = new List<string>();
        var lockPath = Path.Combine(directory.Path, "stack.lock");
        var stackId = $"test-stack-{Guid.NewGuid():N}";

        var resources = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["owned"] = new(StringComparer.Ordinal)
            {
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "duck",
            },
            ["foreign"] = new(StringComparer.Ordinal)
            {
                ["duck.aspire.owner"] = "another-owner",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "debug-host",
            },
        };

        Task<(int ExitCode, string Stdout, string Stderr)> Docker(string[] arguments)
        {
            commands.Add(arguments[0]);
            Assert.ThrowsAny<IOException>(() => File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            if (arguments[0] == "ps")
            {
                var filters = arguments.Select((value, index) => (value, index))
                    .Where(item => item.value == "--filter" && item.index + 1 < arguments.Length)
                    .Select(item => arguments[item.index + 1])
                    .Where(value => value.StartsWith("label=", StringComparison.Ordinal))
                    .Select(value => value["label=".Length..].Split('=', 2))
                    .Select(parts => (Key: parts[0], Value: parts[1]))
                    .ToArray();
                var matches = resources.Where(pair => filters.All(filter =>
                    pair.Value.TryGetValue(filter.Key, out var value) && value == filter.Value))
                    .Select(pair => pair.Key);
                return Task.FromResult((0, string.Join('\n', matches), ""));
            }
            if (arguments[0] == "inspect")
                return Task.FromResult((0, System.Text.Json.JsonSerializer.Serialize(resources[arguments[^1]]), ""));
            return Task.FromResult((0, "", ""));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Launcher.StopPersistentAsync(stackId, lockPath, Docker));

        Assert.Equal(new[] { "ps", "inspect", "inspect" }, commands);
    }

    /// <summary>マニフェストがある場合も、別 Compose project と別 role の同一 stack/run 異所有者を変更前に拒否します。</summary>
    [Fact]
    public async Task StopRejectsForeignOwnerAcrossComposeProjectsBeforeAnyMutation()
    {
        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "stack.lock");
        var stackId = $"test-stack-{Guid.NewGuid():N}";
        var projectHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{stackId}\0{stackId}"))).ToLowerInvariant()[..20];
        var project = $"duck-{projectHash}";
        var manifestPath = Path.Combine(Path.GetTempPath(), $"{project}.compose.yaml");
        var commands = new List<string>();
        var resources = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["compose-owned"] = new(StringComparer.Ordinal)
            {
                ["com.docker.compose.project"] = project,
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "compose-cm4-sim",
            },
            ["duck-owned"] = new(StringComparer.Ordinal)
            {
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "duck",
            },
            ["foreign-project"] = new(StringComparer.Ordinal)
            {
                ["com.docker.compose.project"] = "unrelated-compose-project",
                ["duck.aspire.owner"] = "another-owner",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "compose-other-role",
            },
        };
        await File.WriteAllTextAsync(manifestPath, "persistent manifest");
        try
        {
            Task<(int ExitCode, string Stdout, string Stderr)> Docker(string[] arguments)
            {
                commands.Add(arguments[0] == "network" ? "network ls" : arguments[0]);
                Assert.ThrowsAny<IOException>(() => File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                if (arguments[0] == "ps" || (arguments.Length > 1 && arguments[0] == "network" && arguments[1] == "ls"))
                {
                    var filters = arguments.Select((value, index) => (value, index))
                        .Where(item => item.value == "--filter" && item.index + 1 < arguments.Length)
                        .Select(item => arguments[item.index + 1])
                        .Where(value => value.StartsWith("label=", StringComparison.Ordinal))
                        .Select(value => value["label=".Length..].Split('=', 2))
                        .Select(parts => (Key: parts[0], Value: parts[1]))
                        .ToArray();
                    if (arguments[0] == "network")
                        return Task.FromResult((0, "", ""));
                    var matches = resources.Where(pair => filters.All(filter =>
                        pair.Value.TryGetValue(filter.Key, out var value) && value == filter.Value))
                        .Select(pair => pair.Key);
                    return Task.FromResult((0, string.Join('\n', matches), ""));
                }
                if (arguments[0] == "inspect")
                    return Task.FromResult((0, System.Text.Json.JsonSerializer.Serialize(resources[arguments[^1]]), ""));
                return Task.FromResult((0, "", ""));
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Launcher.StopPersistentAsync(stackId, lockPath, Docker));

            Assert.DoesNotContain("rm", commands);
            Assert.DoesNotContain("compose", commands);
            Assert.Equal(new[] { "ps", "inspect", "network ls", "ps", "inspect", "inspect", "inspect" }, commands);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>停止時の Compose down 失敗では再試行用マニフェストを保持し、成功した再試行後にだけ消すことを確認します。</summary>
    [Fact]
    public async Task StopKeepsManifestAfterDownFailureAndDeletesItAfterRetry()
    {
        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "stack.lock");
        var stackId = $"test-stack-{Guid.NewGuid():N}";
        var projectHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{stackId}\0{stackId}"))).ToLowerInvariant()[..20];
        var project = $"duck-{projectHash}";
        var manifestPath = Path.Combine(Path.GetTempPath(), $"{project}.compose.yaml");
        var resources = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["compose-owned"] = new(StringComparer.Ordinal)
            {
                ["com.docker.compose.project"] = project,
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "compose-cm4-sim",
            },
            ["duck-owned"] = new(StringComparer.Ordinal)
            {
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = stackId,
                ["duck.aspire.run"] = stackId,
                ["duck.aspire.resource"] = "duck",
            },
        };
        var failFirstDown = true;
        await File.WriteAllTextAsync(manifestPath, "persistent compose manifest");
        try
        {
            Task<(int ExitCode, string Stdout, string Stderr)> Docker(string[] arguments)
            {
                if (arguments[0] == "ps" || (arguments.Length > 1 && arguments[0] == "network" && arguments[1] == "ls"))
                {
                    var filters = arguments.Select((value, index) => (value, index))
                        .Where(item => item.value == "--filter" && item.index + 1 < arguments.Length)
                        .Select(item => arguments[item.index + 1])
                        .Where(value => value.StartsWith("label=", StringComparison.Ordinal))
                        .Select(value => value["label=".Length..].Split('=', 2))
                        .Select(parts => (Key: parts[0], Value: parts[1]))
                        .ToArray();
                    if (arguments[0] == "network")
                        return Task.FromResult((0, "", ""));
                    var matches = resources.Where(pair => filters.All(filter =>
                        pair.Value.TryGetValue(filter.Key, out var value) && value == filter.Value))
                        .Select(pair => pair.Key);
                    return Task.FromResult((0, string.Join('\n', matches), ""));
                }
                if (arguments[0] == "inspect")
                    return Task.FromResult((0, System.Text.Json.JsonSerializer.Serialize(resources[arguments[^1]]), ""));
                if (arguments[0] == "rm")
                {
                    resources.Remove(arguments[^1]);
                    return Task.FromResult((0, "", ""));
                }
                if (arguments[0] == "compose" && arguments.Contains("down", StringComparer.Ordinal))
                {
                    if (failFirstDown)
                    {
                        failFirstDown = false;
                        return Task.FromResult((17, "", "simulated Compose down failure"));
                    }
                    resources.Remove("compose-owned");
                    return Task.FromResult((0, "", ""));
                }
                return Task.FromResult((0, "", ""));
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Launcher.StopPersistentAsync(stackId, lockPath, Docker));
            Assert.True(File.Exists(manifestPath));

            await Launcher.StopPersistentAsync(stackId, lockPath, Docker);

            Assert.False(File.Exists(manifestPath));
            Assert.Empty(resources);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>新規一時起動の失敗は専用回収を行い、既存常駐スタックの失敗では全体回収を呼ばないことを確認します。</summary>
    [Fact]
    public async Task FailureCleanupIsLimitedToNewTemporaryLaunches()
    {
        using var directory = new TemporaryDirectory();
        var (exe, args) = Child("exit 23", "exit 23");
        var cleanupCalls = 0;
        Task Cleanup()
        {
            cleanupCalls++;
            return Task.CompletedTask;
        }

        var temporaryCleanup = Launcher.CreateFailureCleanup(persistent: false, Cleanup);
        Assert.NotNull(temporaryCleanup);
        Assert.Equal(23, await Launcher.RunLoggedAsync(
            exe, args, directory.Path, new Dictionary<string, string?>(),
            Path.Combine(directory.Path, "temporary.log"), temporaryCleanup));
        Assert.Equal(1, cleanupCalls);

        var persistentCleanup = Launcher.CreateFailureCleanup(persistent: true, Cleanup);
        Assert.Null(persistentCleanup);
        Assert.Equal(23, await Launcher.RunLoggedAsync(
            exe, args, directory.Path, new Dictionary<string, string?>(),
            Path.Combine(directory.Path, "persistent.log"), persistentCleanup));
        Assert.Equal(1, cleanupCalls);
    }

    /// <summary>ログを新規作成できない場合は、AppHost を開始せず既存記録も保持することを確認します。</summary>
    [Fact]
    public async Task AppHostStartsOnlyAfterLogIsOpened()
    {
        using var directory = new TemporaryDirectory();
        var marker = Path.Combine(directory.Path, "started");
        var (exe, args) = Child($"Set-Content -LiteralPath '{marker}' started", $"echo started > '{marker}'");
        var occupied = Path.Combine(directory.Path, "occupied.log");
        File.WriteAllText(occupied, "existing");

        await Assert.ThrowsAsync<IOException>(() => Launcher.RunLoggedAsync(
            exe, args, directory.Path, new Dictionary<string, string?>(), occupied));

        Assert.False(File.Exists(marker));
        Assert.Equal("existing", File.ReadAllText(occupied));
    }

    /// <summary>AppHost の終了値を保ち、終了前に標準出力と標準エラーをログへ回収することを確認します。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public async Task AppHostReturnsTheChildExitAfterDrainingBothPipes(int exitCode)
    {
        using var directory = new TemporaryDirectory();
        var (exe, args) = Child(
            $"Write-Output final-out; [Console]::Error.WriteLine('final-err'); exit {exitCode}",
            $"echo final-out; echo final-err >&2; exit {exitCode}");
        var logPath = Path.Combine(directory.Path, "output.log");
        var cleanupCalled = false;

        Assert.Equal(exitCode, await Launcher.RunLoggedAsync(
            exe, args, directory.Path, new Dictionary<string, string?>(), logPath, () =>
            {
                cleanupCalled = true;
                return Task.CompletedTask;
            }));
        Assert.Equal(exitCode != 0, cleanupCalled);

        var lines = File.ReadAllLines(logPath);
        Assert.Contains("final-out", lines[..^1]);
        Assert.Contains("final-err", lines[..^1]);
        Assert.Equal($"AppHost exited with code {exitCode}; stdout and stderr drained.", lines[^1]);
    }

    /// <summary>ログ書き込み失敗時に AppHost と子孫を停止し、全プロセスを回収することを確認します。</summary>
    [Fact]
    public async Task LogWriteFailureTerminatesTheChildAndDescendant()
    {
        using var directory = new TemporaryDirectory();
        var writer = new FailingWriter();
        var cleanupCalled = false;
        var (exe, args) = Child(
            """$child = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList @('-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 18') -WindowStyle Hidden -PassThru; Write-Output $child.Id; Write-Output $PID; Start-Sleep -Seconds 18""",
            """sleep 18 & child=$!; echo $child; echo $$; wait""");
        try
        {
            var action = Launcher.RunAppHostAsync(exe, args, directory.Path,
                new Dictionary<string, string?>(), writer, () =>
                {
                    cleanupCalled = true;
                    return Task.CompletedTask;
                });
            await Assert.ThrowsAsync<IOException>(() => action.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(cleanupCalled);
            Assert.Equal(2, writer.ProcessIds.Count);
            foreach (var processId in writer.ProcessIds)
                await AssertProcessExitedAsync(processId);
        }
        finally
        {
            foreach (var processId in writer.ProcessIds)
            {
                try
                {
                    using var child = Process.GetProcessById(processId);
                    if (!child.HasExited) child.Kill(entireProcessTree: true);
                }
                catch (ArgumentException) { }
            }
        }
    }

    /// <summary>対象プロセスが終了するまで短時間確認します。</summary>
    private static async Task AssertProcessExitedAsync(int processId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited) return;
            }
            catch (ArgumentException) { return; }
            await Task.Delay(50);
        }
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(processId));
    }

    private static (string FileName, string[] Arguments) Child(string powershell, string shell) =>
        OperatingSystem.IsWindows()
            ? (Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                ["-NoProfile", "-NonInteractive", "-Command", powershell])
            : ("/bin/sh", ["-c", shell]);

    /// <summary>テスト用の独立した最小リポジトリを作ります。</summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>このテストだけが使用する一時領域です。</summary>
        public string Path { get; } = Directory.CreateTempSubdirectory("duck-launcher-test-").FullName;

        /// <summary>ランチャーが識別できる最小構成の作業木を作ります。</summary>
        public string CreateRepo(string name)
        {
            var root = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(System.IO.Path.Combine(root, "Testing", "Duck.Testing.AppHost"));
            Directory.CreateDirectory(System.IO.Path.Combine(root, "scripts"));
            File.Create(System.IO.Path.Combine(root, "Duck.slnx")).Dispose();
            return root;
        }

        /// <summary>テストで作成した一時領域を削除します。</summary>
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>二つのプロセス ID を記録してからログ書き込み失敗を再現します。</summary>
    private sealed class FailingWriter : StringWriter
    {
        /// <summary>障害前に受け取った AppHost と子孫の識別子です。</summary>
        public List<int> ProcessIds { get; } = [];

        /// <summary>二つ目の識別子を書き込む際に I/O 障害を発生させます。</summary>
        public override void WriteLine(string? value)
        {
            ProcessIds.Add(int.Parse(value!));
            if (ProcessIds.Count == 2) throw new IOException("Simulated logging failure.");
        }
    }
}
