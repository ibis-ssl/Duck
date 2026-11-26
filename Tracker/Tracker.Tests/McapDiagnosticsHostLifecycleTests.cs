using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Diagnostics.Mcap;
using Tracker.RuntimeHost;
using TrackerConnectionLib;

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
                host.Services.GetRequiredService<ILoggerFactory>(), provider, new McapHostExecutionFailure(), () => host.RunAsync());

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
                host.Services.GetRequiredService<ILoggerFactory>(), provider, new McapHostExecutionFailure(), () => host.RunAsync()));

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
    /// ログを無効にしても RuntimeHost の実受信サービスの例外が失敗になり、停止例外があれば発端と区別して残る。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeReceiverFault_WithLoggingDisabled_PreservesServiceAndOptionalStopFailure(bool failOnStop)
    {
        var builder = Host.CreateApplicationBuilder();
        DisableLogging(builder.Services);
        builder.Configuration["VisionReceiver:Enabled"] = "true";
        builder.Configuration["VisionReceiver:MulticastAddress"] = "invalid-address";
        builder.Configuration["Tracker:Receive:Enabled"] = "false";
        builder.Configuration["Testing:IdentityControl:Enabled"] = "false";
        builder.Services.AddRuntimeHost(builder.Configuration);
        builder.Services.AddSingleton<IOptions<RuntimeVisionReceiverOptions>>(services => new RuntimeReceiverOptionsAfterStartup(
            services.GetRequiredService<IHostApplicationLifetime>(),
            services.GetRequiredService<IOptionsMonitor<RuntimeVisionReceiverOptions>>()));
        if (failOnStop)
        {
            builder.Services.AddHostedService<FailStopService>();
        }

        using var host = builder.Build();
        var (failure, hostReturned) = await RunWithoutPublishingAsync(host);
        var serviceFailure = failure;
        if (failOnStop)
        {
            Assert.False(hostReturned);
            var causes = Assert.IsType<AggregateException>(failure).InnerExceptions;
            Assert.Equal(2, causes.Count);
            serviceFailure = causes[0];
            Assert.Equal(FailStopService.Message, Assert.IsType<IOException>(causes[1]).Message);
        }
        else
        {
            Assert.True(hostReturned);
        }

        Assert.Contains("Invalid VisionReceiver multicast address 'invalid-address'",
            Assert.IsType<InvalidOperationException>(serviceFailure).Message, StringComparison.Ordinal);
    }

    /// <summary>ログを無効にしても DebugHost の実診断サンプル常駐処理の障害で記録が確定しないことを確認する。</summary>
    [Fact]
    public async Task DebugHostSampleFault_WithLoggingDisabled_DoesNotPublishCompletedMcap()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        DisableLogging(builder.Services);
        builder.Services.AddSingleton<McapHostExecutionFailure>();
        var visionOptions = new VisionReceiverOptions
        {
            PacketCapture = new VisionPacketCaptureOptions
            {
                DirectoryPath = "invalid\0path",
                DiagnosticsSampleIntervalMilliseconds = 20,
            },
        };
        var trackerOptions = new TrackerOptions();
        builder.Services.AddSingleton(Options.Create(visionOptions));
        builder.Services.AddSingleton(Options.Create(trackerOptions));
        builder.Services.AddSingleton(TrackerConfigurationResolver.Resolve(trackerOptions));
        var captureControl = new VisionPacketCaptureRuntimeControl(initialEnabled: false);
        builder.Services.AddSingleton(captureControl);
        builder.Services.AddSingleton<VisionPacketCaptureSession>();
        builder.Services.AddSingleton<VisionPacketStore>();
        builder.Services.AddSingleton(new TrackedSnapshotStore(trackerOptions.ActiveProfileName));
        builder.Services.AddSingleton(new MultiTrackerManager<TrackerPacketAdapter>("test-uuid", "test-tracker"));
        builder.Services.AddSingleton<ExternalTrackerSnapshotStore>();
        builder.Services.AddSingleton<VisionLiveComparisonSnapshotComposer>();
        builder.Services.AddSingleton<VisionLiveDisplaySnapshotProvider>();
        builder.Services.AddSingleton<DiagnosticsSampleLogWriter>();
        builder.Services.AddSingleton<DiagnosticsSampleCaptureLoop>();
        builder.Services.AddHostedService<DiagnosticsSampleHostedService>();
        using var host = builder.Build();
        using var enableCapture = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted
            .Register(() => captureControl.SetEnabled(true));

        var (failure, hostReturned) = await RunWithoutPublishingAsync(host);

        Assert.True(hostReturned);
        Assert.Equal("path", Assert.IsType<ArgumentException>(failure).ParamName);
        Assert.Contains(nameof(DiagnosticsSampleHostedService), failure.StackTrace, StringComparison.Ordinal);
    }

    private static void DisableLogging(IServiceCollection services) =>
        services.PostConfigure<LoggerFilterOptions>(options =>
        {
            options.Rules.Clear();
            options.MinLevel = LogLevel.None;
        });

    private static async Task<(Exception Failure, bool HostReturned)> RunWithoutPublishingAsync(IHost host)
    {
        var path = Directory.CreateTempSubdirectory("duck-mcap-service-failure-").FullName;
        var factory = host.Services.GetRequiredService<ILoggerFactory>();
        var executionFailure = host.Services.GetRequiredService<McapHostExecutionFailure>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var provider = new McapDiagnosticsLoggerProvider(path);
        var hostReturned = false;
        try
        {
            var failure = await Record.ExceptionAsync(() => McapDiagnosticsHostRunner.RunAsync(
                factory, provider, executionFailure, async () =>
                {
                    await host.RunAsync(deadline.Token);
                    hostReturned = true;
                }));

            Assert.False(deadline.IsCancellationRequested);
            Assert.Empty(Directory.EnumerateFiles(path));
            return (Assert.IsAssignableFrom<Exception>(failure), hostReturned);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
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
                    host.Services.GetRequiredService<ILoggerFactory>(), provider, new McapHostExecutionFailure(), () => host.RunAsync()));

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

    /// <summary>実ホストの停止時に二次障害を起こす試験用サービス。</summary>
    private sealed class FailStopService : IHostedService
    {
        public const string Message = "停止時の二次障害";

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => throw new IOException(Message);
    }

    /// <summary>実受信サービスが構成を読む時点をホスト起動後にそろえる。</summary>
    private sealed class RuntimeReceiverOptionsAfterStartup(
        IHostApplicationLifetime lifetime,
        IOptionsMonitor<RuntimeVisionReceiverOptions> options) : IOptions<RuntimeVisionReceiverOptions>
    {
        public RuntimeVisionReceiverOptions Value => lifetime.ApplicationStarted.WaitHandle.WaitOne(TimeSpan.FromSeconds(5))
            ? options.CurrentValue
            : throw new TimeoutException("試験ホストの起動を待ち切れませんでした。");
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
