using System.Diagnostics;
using System.Text.Json;

namespace Tracker.Tests;

/// <summary>開発用ランチャーの引数、作業木、所有資源の停止と出力の回収を検証します。</summary>
public sealed class LauncherProcessContractTests
{
    /// <summary>起動で補った既定識別子と、停止時に明示した識別子を区別します。</summary>
    [Theory]
    [InlineData("duck-local-development")]
    [InlineData("test-stack-130")]
    public void PersistentStopAcceptsEitherExplicitId(string id)
    {
        var start = Launcher.Options.Parse(id == Launcher.Options.DefaultStackId
            ? ["--persistent"] : ["--persistent", "--stack-id", id]);
        var stop = Launcher.Options.Parse(["--stop-persistent", "--stack-id", id]);
        Assert.True(start.Persistent);
        Assert.True(stop.StopPersistent);
        Assert.Equal(id, start.StackId);
        Assert.Equal(start.StackId, stop.StackId);
    }

    /// <summary>誤った停止先の選択を防ぐため、識別子の省略と末尾改行を拒否します。</summary>
    [Fact]
    public void StopRequiresAnExplicitIdWithoutTrailingNewline()
    {
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--stop-persistent"]));
        Assert.Throws<InvalidOperationException>(() => Launcher.Options.Parse(
            ["--stop-persistent", "--stack-id", "test-stack\n"]));
    }

    /// <summary>別の作業木がカレントディレクトリでも、指定した起動ファイルの配置先を選びます。</summary>
    [Fact]
    public void RootComesFromTheLauncherFile()
    {
        using var directory = new TemporaryDirectory();
        var other = directory.CreateRepo("other");
        var desired = directory.CreateRepo("desired");
        Assert.NotEqual(other, Launcher.FindRepositoryRoot(Path.Combine(desired, "scripts", "start.cs")));
        Assert.Equal(desired, Launcher.FindRepositoryRoot(Path.Combine(desired, "scripts", "start.cs")));
    }

    /// <summary>ロックを別の実行が保持する場合は資源の列挙も行いません。</summary>
    [Fact]
    public async Task StopDoesNotCallDockerWhileTheLockIsHeld()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "stack.lock");
        using var lease = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Launcher.StopPersistentAsync("test-stack", path, _ =>
        {
            called = true;
            return Task.FromResult((0, "", ""));
        }));
        Assert.False(called);
    }

    /// <summary>後から見つかった資源の所有が一致しない場合も、一件も削除しません。</summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("stack")]
    [InlineData("run")]
    public async Task StopVerifiesAllLabelsBeforeDeleting(string mismatched)
    {
        using var directory = new TemporaryDirectory();
        var commands = new List<string>();
        var path = Path.Combine(directory.Path, "stack.lock");

        Task<(int, string, string)> Docker(string[] args)
        {
            commands.Add(args[0]);
            Assert.ThrowsAny<IOException>(() => File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
            if (args[0] == "ps") return Task.FromResult((0, "owned\nforeign\n", ""));
            var labels = new Dictionary<string, string>
            {
                ["duck.aspire.owner"] = "duck-apphost",
                ["duck.aspire.stack"] = "test-stack",
                ["duck.aspire.run"] = "test-stack"
            };
            if (args[^1] == "foreign") labels["duck.aspire." + mismatched] = "different";
            return Task.FromResult((0, JsonSerializer.Serialize(labels), ""));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => Launcher.StopPersistentAsync("test-stack", path, Docker));
        Assert.Equal(new[] { "ps", "inspect", "inspect" }, commands);
    }

    /// <summary>指定したログを確保できないときは子を開始しません。</summary>
    [Fact]
    public async Task AppHostStartsOnlyAfterLogIsOpened()
    {
        using var directory = new TemporaryDirectory();
        var marker = Path.Combine(directory.Path, "started");
        var (exe, args) = Child($"echo started > '{marker}'", $"echo started > '{marker}'");
        var occupied = Path.Combine(directory.Path, "occupied.log");
        File.WriteAllText(occupied, "existing");
        await Assert.ThrowsAsync<IOException>(() => Launcher.RunLoggedAsync(exe, args, directory.Path,
            new Dictionary<string, string?>(), occupied));
        Assert.False(File.Exists(marker));
        Assert.Equal("existing", File.ReadAllText(occupied));
    }

    /// <summary>終了値を報告するときに標準出力と標準エラーがログに揃っていることを確認します。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public async Task AppHostReturnsTheChildExitAfterDrainingBothPipes(int exitCode)
    {
        using var directory = new TemporaryDirectory();
        var (exe, args) = Child($"echo final-out; [Console]::Error.WriteLine('final-err'); exit {exitCode}",
            $"echo final-out; echo final-err >&2; exit {exitCode}");
        var log = Path.Combine(directory.Path, "output.log");
        Assert.Equal(exitCode, await Launcher.RunLoggedAsync(exe, args, directory.Path, new Dictionary<string, string?>(), log));
        var lines = File.ReadAllLines(log);
        Assert.Contains("final-out", lines[..^1]);
        Assert.Contains("final-err", lines[..^1]);
        Assert.Equal($"AppHost exited with code {exitCode}; stdout and stderr drained.", lines[^1]);
    }

    /// <summary>ログ障害時に子と子孫を残さず回収し、パイプ処理を終了することを確認します。</summary>
    [Fact]
    public async Task LogWriteFailureTerminatesTheChildAndDescendant()
    {
        using var directory = new TemporaryDirectory();
        var writer = new FailingWriter();
        var (exe, args) = Child(
            """$child = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList @('-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 18') -WindowStyle Hidden -PassThru; Write-Output $child.Id; Write-Output $PID; Start-Sleep -Seconds 18""",
            """sleep 18 & child=$!; echo $child; echo $$; wait""");
        try
        {
            var action = Launcher.RunAppHostAsync(exe, args, directory.Path, new Dictionary<string, string?>(), writer);
            await Assert.ThrowsAsync<IOException>(() => action.WaitAsync(TimeSpan.FromSeconds(10)));
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

    /// <summary>子の識別子を記録し、出力先の書き込み障害を再現します。</summary>
    private sealed class FailingWriter : StringWriter
    {
        /// <summary>障害前に記録した子孫と子の識別子です。</summary>
        public List<int> ProcessIds { get; } = [];

        /// <summary>子と子孫を記録した後、次の書き込みで障害を通知します。</summary>
        public override void WriteLine(string? value)
        {
            ProcessIds.Add(int.Parse(value!));
            if (ProcessIds.Count == 2) throw new IOException("Simulated logging failure.");
        }
    }

    /// <summary>試験用の一時ディレクトリと作業木を用意します。</summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>この試験だけが使用する一時領域です。</summary>
        public string Path { get; } = Directory.CreateTempSubdirectory("duck-launcher-test-").FullName;

        /// <summary>ランチャーが作業木として識別できる最小構成を配置します。</summary>
        public string CreateRepo(string name)
        {
            var root = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(System.IO.Path.Combine(root, "Testing", "Duck.Testing.AppHost"));
            Directory.CreateDirectory(System.IO.Path.Combine(root, "scripts"));
            File.Create(System.IO.Path.Combine(root, "Duck.slnx")).Dispose();
            return root;
        }

        /// <summary>試験で作成した一時領域を削除します。</summary>
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
