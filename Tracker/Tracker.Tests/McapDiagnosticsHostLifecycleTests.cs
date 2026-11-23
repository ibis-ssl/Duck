using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Diagnostics.Mcap;

namespace Tracker.Tests;

/// <summary>
/// ホスト登録・終了経路における MCAP 診断記録の完了条件を確認する。
/// </summary>
public sealed class McapDiagnosticsHostLifecycleTests
{
    /// <summary>
    /// 何を確認しているか: RuntimeHost と DebugHost のホスト形態で、正常停止の末尾ログを完成した MCAP から読めること。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostRegistration_CompletesMcapAfterNormalShutdown(bool useWebApplication)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-mcap-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        using var host = CreateHost(useWebApplication);
        var provider = new McapDiagnosticsLoggerProvider(directory);

        try
        {
            await McapDiagnosticsHostRunner.RunAsync(
                host.Services.GetRequiredService<ILoggerFactory>(), provider, () => host.RunAsync());

            var reader = new TrackerDiagnosticsLogReader(
                Options.Create(new VisionReceiverOptions()),
                new TrackerDiagnosticsOptions { DirectoryPath = directory });
            var file = Assert.Single(reader.ListFiles());
            var snapshot = reader.ReadFile(file.FullPath);
            Assert.Null(snapshot.Error);
            Assert.NotEmpty(snapshot.Entries);
            Assert.Equal("正常停止を識別する末尾ログ", snapshot.Entries[^1].Message);
        }
        finally
        {
            provider.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: ホスト起動が失敗した場合に完成 MCAP を作らず、ホスト失敗を呼び出し元へ返すこと。
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenHostFails_DoesNotPublishCompletedMcap()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-mcap-host-failed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddHostedService<FailStartupService>();
        using var host = builder.Build();
        var provider = new McapDiagnosticsLoggerProvider(directory);

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => McapDiagnosticsHostRunner.RunAsync(
                host.Services.GetRequiredService<ILoggerFactory>(), provider, () => host.RunAsync()));

            Assert.Empty(Directory.EnumerateFiles(directory, "*.mcap"));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.partial"));
        }
        finally
        {
            provider.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 何を確認しているか: 正常停止後の MCAP 書き込み失敗を成功として扱わず、途中出力を完成名にしないこと。
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenRecordingFails_DoesNotPublishCompletedMcap()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-mcap-record-failed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var partialPath = Path.Combine(directory, "run.mcap.partial");
        var completedPath = Path.Combine(directory, "run.mcap");
        var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        var failingStream = new FailingWriteStream(output);
        var provider = new McapDiagnosticsLoggerProvider(partialPath, completedPath, failingStream);
        failingStream.FailWrites = true;
        using var host = CreateHost(useWebApplication: false);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => McapDiagnosticsHostRunner.RunAsync(
                    host.Services.GetRequiredService<ILoggerFactory>(), provider, () => host.RunAsync()));

            Assert.False(File.Exists(partialPath));
            Assert.False(File.Exists(completedPath));
        }
        finally
        {
            provider.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IHost CreateHost(bool useWebApplication)
    {
        if (useWebApplication)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
                ContentRootPath = AppContext.BaseDirectory,
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton<StopAfterStartupService>();
            builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<StopAfterStartupService>());
            return builder.Build();
        }

        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddSingleton<StopAfterStartupService>();
        hostBuilder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<StopAfterStartupService>());
        return hostBuilder.Build();
    }

    private sealed class StopAfterStartupService(
        IHostApplicationLifetime hostLifetime,
        ILogger<StopAfterStartupService> logger) : IHostedLifecycleService
    {
        public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogWarning("ホスト起動マーカー");
            return Task.CompletedTask;
        }

        public Task StartedAsync(CancellationToken cancellationToken)
        {
            _ = StopAfterStartupAsync(cancellationToken);
            return Task.CompletedTask;
        }

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogWarning("正常停止を識別する末尾ログ");
            return Task.CompletedTask;
        }

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task StopAfterStartupAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            hostLifetime.StopApplication();
        }
    }

    private sealed class FailStartupService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("ホスト起動の故障注入");

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingWriteStream(Stream inner) : Stream
    {
        public bool FailWrites { get; set; }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfRequested();
            inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfRequested();
            inner.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            ThrowIfRequested();
            inner.WriteByte(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ThrowIfRequested()
        {
            if (FailWrites)
            {
                throw new IOException("故障注入: 診断ファイルへの書き込みを拒否しました。");
            }
        }
    }
}
