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
