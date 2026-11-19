using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tracker.Tests;

/// <summary>Compose プロジェクトの起動、ポートの割り当て、リソースの再作成と後片付けを検証します。</summary>
public sealed class ComposeSimulationStackTests
{
    /// <summary>開発用 UDP transfer が所有プロジェクト内でビルドされ、Tracker endpoint を loopback に公開し、停止時に自身の Compose project と生成物だけを片付けることを確認します。</summary>
    /// <returns>非同期試験の完了を表します。</returns>
    [Fact]
    public async Task DevelopmentTransferBuildsInTheOwnedProjectAndPublishesTrackerOnLoopback()
    {
        var builder = CreateBuilder();
        var composeFile = Path.Combine(Path.GetTempPath(), $"duck-transfer-{Guid.NewGuid():N}.yaml");
        var stack = new ComposeSimulationStack("duck-transfer-test", composeFile, "docker", pullMissingImages: true);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        const int trackerPort = 39111;
        const string endpointName = "tracker-transport";
        const System.Net.Sockets.ProtocolType udp = System.Net.Sockets.ProtocolType.Udp;
        var transfer = stack.AddService(builder, "development-udp-transfer", "development-udp-transfer",
            image: null,
            endpoints: [new ComposeServiceEndpoint(endpointName, trackerPort, udp)],
            buildContext: Path.GetTempPath());
        stack.AddPublishedEndpoint(transfer, endpointName, trackerPort, udp);

        var manifest = stack.RenderYaml().ReplaceLineEndings("\n");
        Assert.Contains("image: \"cm4:locked\"", manifest, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1::39111/udp", manifest, StringComparison.Ordinal);
        var transferSection = manifest[manifest.IndexOf("  development-udp-transfer:", StringComparison.Ordinal)..];
        Assert.Contains("build:\n      context:", transferSection, StringComparison.Ordinal);
        Assert.Contains("network_mode: \"service:cm4-sim\"", transferSection, StringComparison.Ordinal);
        Assert.DoesNotContain("image:", transferSection, StringComparison.Ordinal);
        Assert.Equal("cm4-sim", Assert.Single(stack.PortBindings).OwnerServiceName);

        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner();
        var owner = new ComposeSimulationStackOwner(
            stack, runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>());
        await owner.StartAsync(CancellationToken.None);
        try
        {
            var endpoint = Assert.Single(transfer.Annotations.OfType<EndpointAnnotation>());
            Assert.Equal(49173, endpoint.AllocatedEndpoint?.Port);
            var up = Assert.Single(runner.Calls, call => call.Contains("up", StringComparer.Ordinal));
            Assert.Contains("--build", up);
            Assert.Equal("missing", up[Array.IndexOf(up, "--pull") + 1]);
        }
        finally
        {
            await owner.StopAsync(CancellationToken.None);
            await owner.StoppedAsync(CancellationToken.None);
        }

        var down = Assert.Single(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.Equal(new[] { "--rmi", "local" }, down.TakeLast(2));
        Assert.False(File.Exists(composeFile));
    }

    /// <summary>host networking 構成でも bridge ingress のみが port 公開され、他サービスの名前解決経路が保たれることを確認します。</summary>
    /// <returns>非同期試験の完了を表します。</returns>
    [Fact]
    public async Task HostNetworkingCanPublishAndResolveOnlyTheBridgeIngressService()
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-mixed-network-stack", Path.Combine(Path.GetTempPath(), $"duck-mixed-{Guid.NewGuid():N}.yaml"), "docker",
            useHostNetworking: true);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "udp-relay", "udp-relay", "crane:locked", entrypoint: "perl");
        var ingress = stack.AddService(
            builder,
            "udp-ingress",
            "udp-ingress",
            "crane:locked",
            entrypoint: "perl",
            endpoints: [new ComposeServiceEndpoint("tracker-ingress", 39112, System.Net.Sockets.ProtocolType.Udp)],
            usesBridgeNetwork: true);
        stack.AddPublishedEndpoint(ingress, "tracker-ingress", 39112, System.Net.Sockets.ProtocolType.Udp);

        var manifest = stack.RenderYaml();

        Assert.Contains("udp-relay:", manifest);
        Assert.Contains("network_mode: \"service:cm4-sim\"", manifest);
        Assert.Contains("udp-ingress:", manifest);
        Assert.Contains("127.0.0.1::39112/udp", manifest);
        var ingressSection = manifest[manifest.IndexOf("  udp-ingress:", StringComparison.Ordinal)..];
        Assert.Contains("networks:", ingressSection);
        Assert.Contains("duck-simulation", ingressSection);
        Assert.DoesNotContain("network_mode: \"host\"", ingressSection);
        Assert.Contains("driver: bridge", manifest);
        Assert.Equal("udp-ingress", Assert.Single(stack.PortBindings).OwnerServiceName);

        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner();
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>());
        await owner.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal(49173, ingress.Annotations.OfType<EndpointAnnotation>().Single().AllocatedEndpoint?.Port);
            Assert.Contains(runner.Calls, call => call.SequenceEqual(["compose", "--project-name", "duck-mixed-network-stack", "--file", stack.ComposeFilePath, "port", "--protocol", "udp", "udp-ingress", "39112"]));
        }
        finally
        {
            await owner.StopAsync(CancellationToken.None);
            await owner.StoppedAsync(CancellationToken.None);
        }
    }

    /// <summary>Compose owner が割当 endpoint を反映し、停止時には所有する project だけを停止することを確認します。</summary>
    /// <param name="useHostNetworking">ホストネットワークを使う構成かどうかを指定します。</param>
    /// <returns>非同期試験の完了を表します。</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComposeOwnerProjectsEndpointAndStopsOnlyItsProject(bool useHostNetworking)
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-test-stack", Path.Combine(Path.GetTempPath(), $"duck-test-{Guid.NewGuid():N}.yaml"), "docker",
            useHostNetworking: useHostNetworking);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "simulator", "simulator", "simulator:locked");
        _ = stack.AddService(builder, "crane", "crane", "crane:locked");
        stack.RequireCraneReadiness("crane", "Ibis");
        var gameController = stack.AddService(
            builder,
            "game-controller",
            "game-controller",
            "ssl-game-controller:locked",
            endpoints: [new ComposeServiceEndpoint("control", 8082, System.Net.Sockets.ProtocolType.Tcp)]);
        stack.AddPublishedEndpoint(gameController, "control", 8082, System.Net.Sockets.ProtocolType.Tcp);

        using var app = builder.Build();
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        var resourceLogger = app.Services.GetRequiredService<ResourceLoggerService>();
        var runner = new FakeComposeCommandRunner(rejectPortQueries: useHostNetworking);
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var owner = new ComposeSimulationStackOwner(stack, runner, notifications, resourceLogger, lifetime);
        var endpoint = Assert.Single(gameController.Annotations.OfType<EndpointAnnotation>());
        var endpointReference = new EndpointReference(gameController, endpoint);

        await owner.StartAsync(CancellationToken.None);

        Assert.Equal(useHostNetworking ? "8082" : "49173", await endpointReference.Property(EndpointProperty.Port).GetValueAsync());
        Assert.Equal("127.0.0.1", endpoint.AllocatedEndpoint?.Address);
        Assert.Equal(!useHostNetworking, runner.Calls.Any(call => call.Contains("port", StringComparer.Ordinal)));
        var manifest = await File.ReadAllTextAsync(stack.ComposeFilePath);
        Assert.Contains("network_mode: \"service:cm4-sim\"", manifest);
        Assert.Contains("simulator:locked", manifest);
        Assert.Contains("crane:locked", manifest);
        Assert.Contains("ssl-game-controller:locked", manifest);
        Assert.Equal(3, manifest.Split("network_mode: \"service:cm4-sim\"", StringSplitOptions.None).Length - 1);
        if (useHostNetworking)
        {
            Assert.Contains("network_mode: \"host\"", manifest);
            Assert.DoesNotContain("ports:", manifest);
            Assert.DoesNotContain("networks:", manifest);
        }
        Assert.DoesNotContain("networks:\n      - duck-simulation", manifest[(manifest.IndexOf("  simulator:", StringComparison.Ordinal))..]);
        Assert.Equal("cm4:locked", stack.Services.Single(service => service.ServiceName == "cm4-sim").Image);
        Assert.Equal("ssl-game-controller:locked", stack.Services.Single(service => service.ServiceName == "game-controller").Image);
        Assert.Contains(runner.Calls, call => call.Contains("exec", StringComparer.Ordinal) && call.Contains("crane", StringComparer.Ordinal));

        await owner.StopAsync(CancellationToken.None);
        await owner.StoppedAsync(CancellationToken.None);

        var up = Assert.Single(runner.Calls, call => call.Contains("up", StringComparer.Ordinal));
        Assert.Equal(new[] { "compose", "--project-name", "duck-test-stack", "--file", stack.ComposeFilePath, "up", "--pull", "never", "--detach", "--wait", "--wait-timeout", "120" }, up);
        var down = Assert.Single(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.Contains("--project-name", down);
        Assert.Contains("duck-test-stack", down);
        Assert.DoesNotContain("-v", down);
        Assert.False(File.Exists(stack.ComposeFilePath));
    }

    /// <summary>永続 Compose owner の終了時に project と manifest が保持されることを確認します。</summary>
    /// <returns>非同期試験の完了を表します。</returns>
    [Fact]
    public async Task PersistentComposeOwnerRetainsProjectAndManifestOnAppHostShutdown()
    {
        var builder = CreateBuilder();
        var composeFile = Path.Combine(Path.GetTempPath(), $"duck-persistent-{Guid.NewGuid():N}.yaml");
        var stack = new ComposeSimulationStack(
            "duck-persistent-test", composeFile, "docker", stackId: "persistent-stack", runId: "persistent-run",
            retainOnAppHostStop: true);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "crane", "crane", "crane:locked");

        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner();
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>());

        await owner.StartAsync(CancellationToken.None);
        await owner.StopAsync(CancellationToken.None);
        await owner.StoppedAsync(CancellationToken.None);

        var manifest = await File.ReadAllTextAsync(composeFile);
        Assert.Contains("duck.aspire.stack: \"persistent-stack\"", manifest);
        Assert.Contains("duck.aspire.run: \"persistent-run\"", manifest);
        Assert.DoesNotContain(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.True(File.Exists(composeFile));
        File.Delete(composeFile);
    }

    /// <summary>owner 交代後に namespace follower が再作成され、既存 image の pull を行わないことを確認します。</summary>
    /// <param name="useHostNetworking">ホストネットワークを使う構成かどうかを指定します。</param>
    /// <returns>非同期試験の完了を表します。</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComposeOwnerReplacementRecreatesNamespaceFollowersWithoutPullingImages(bool useHostNetworking)
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-recreate-stack", Path.Combine(Path.GetTempPath(), $"duck-recreate-{Guid.NewGuid():N}.yaml"), "docker",
            useHostNetworking: useHostNetworking);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "simulator", "simulator", "simulator:locked");
        var gameController = stack.AddService(builder, "game-controller", "game-controller", "ssl-game-controller:locked",
            endpoints: [new ComposeServiceEndpoint("control", 8082, System.Net.Sockets.ProtocolType.Tcp)]);
        stack.AddPublishedEndpoint(gameController, "control", 8082, System.Net.Sockets.ProtocolType.Tcp);
        _ = stack.AddService(builder, "crane", "crane", "crane:locked");

        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner(rejectPortQueries: useHostNetworking);
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>(),
            pollInterval: TimeSpan.FromMilliseconds(10),
            ownerReplacementWaitTimeout: TimeSpan.FromSeconds(2));

        await owner.StartAsync(CancellationToken.None);
        try
        {
            runner.OwnerContainerId = null;
            await runner.OwnerUnavailableObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
            runner.OwnerContainerId = new string('b', 64);
            await runner.FollowersRecreated.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await runner.RecoveryStateObserved.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var endpoint = Assert.Single(gameController.Annotations.OfType<EndpointAnnotation>());
            Assert.Equal(useHostNetworking ? 8082 : 49173, endpoint.AllocatedEndpoint?.Port);
            Assert.Equal(!useHostNetworking, runner.Calls.Any(call => call.Contains("port", StringComparer.Ordinal)));
        }
        finally
        {
            await owner.StopAsync(CancellationToken.None);
            await owner.StoppedAsync(CancellationToken.None);
        }

        var recreate = Assert.Single(runner.Calls, call => call.Contains("--force-recreate", StringComparer.Ordinal));
        Assert.Contains("--pull", recreate);
        Assert.Contains("never", recreate);
        var noDependenciesIndex = Array.IndexOf(recreate, "--no-deps");
        Assert.True(noDependenciesIndex >= 0);
        Assert.Equal(
            ["simulator", "game-controller", "crane"],
            recreate[(noDependenciesIndex + 1)..]);
    }

    /// <summary>Compose 起動失敗時に、所有 project に限定した後片付けが実行されることを確認します。</summary>
    /// <param name="buildsDevelopmentTransfer">開発用 UDP 転送イメージをビルドする構成かどうかを指定します。</param>
    /// <returns>非同期試験の完了を表します。</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComposeStartupFailureRunsProjectScopedCleanup(bool buildsDevelopmentTransfer)
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-failed-stack", Path.Combine(Path.GetTempPath(), $"duck-failed-{Guid.NewGuid():N}.yaml"), "docker");
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        if (buildsDevelopmentTransfer)
        {
            _ = stack.AddService(builder, "development-udp-transfer", "development-udp-transfer",
                image: null, buildContext: Path.GetTempPath());
        }
        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner(failUp: true);
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            app.Services.GetRequiredService<IHostApplicationLifetime>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.StartingAsync(CancellationToken.None));

        Assert.Contains("exit 17", error.Message, StringComparison.Ordinal);
        var up = Assert.Single(runner.Calls, call => call.Contains("up", StringComparer.Ordinal));
        Assert.Contains("--pull", up);
        Assert.Contains("never", up);
        Assert.Equal(buildsDevelopmentTransfer, up.Contains("--build"));
        var down = Assert.Single(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.Contains("duck-failed-stack", down);
        Assert.DoesNotContain("-v", down);
        Assert.Equal(buildsDevelopmentTransfer, down.Contains("--rmi"));
        if (buildsDevelopmentTransfer)
            Assert.Equal(new[] { "--rmi", "local" }, down.TakeLast(2));
        Assert.False(File.Exists(stack.ComposeFilePath));
    }

    /// <summary>稼働中の Compose 監視失敗が異常終了値になり、管理対象 project の片付けを行うことを確認します。</summary>
    [Fact]
    public async Task ComposeMonitorFailureMarksAppHostFailedAndCleansItsProject()
    {
        AppHostTerminationState.Reset();
        var builder = CreateBuilder();
        var composeFile = Path.Combine(Path.GetTempPath(), $"duck-monitor-failed-{Guid.NewGuid():N}.yaml");
        var stack = new ComposeSimulationStack("duck-monitor-failed-stack", composeFile, "docker");
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner(failMonitor: true);
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var owner = new ComposeSimulationStackOwner(
            stack,
            runner,
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            lifetime,
            pollInterval: TimeSpan.FromMilliseconds(10));
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStopping.Register(() => stopping.TrySetResult());

        try
        {
            await owner.StartAsync(CancellationToken.None);
            await stopping.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(1, AppHostTerminationState.ExitCode);
            Assert.Contains(runner.Calls, call => call.Contains("--services", StringComparer.Ordinal));
        }
        finally
        {
            await owner.StopAsync(CancellationToken.None);
            await owner.StoppedAsync(CancellationToken.None);
        }

        Assert.Contains(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.False(File.Exists(composeFile));
    }

    /// <summary>Vision 用 UDP ポートを確保できない異常経路が AppHost の非ゼロ終了を記録することを確認します。</summary>
    [Fact]
    public async Task WindowsTransferBindFailureMarksAppHostFailed()
    {
        AppHostTerminationState.Reset();
        using var occupiedSocket = new System.Net.Sockets.UdpClient(
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var occupiedPort = ((System.Net.IPEndPoint)occupiedSocket.Client.LocalEndPoint!).Port;
        var transfer = new WindowsDevelopmentUdpTransfer(
            System.Net.IPAddress.Loopback,
            new EndpointAnnotation(System.Net.Sockets.ProtocolType.Udp, "tracker-transport"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsDevelopmentUdpTransfer>.Instance,
            occupiedPort);

        await Assert.ThrowsAsync<System.Net.Sockets.SocketException>(
            () => transfer.ExecuteTransferAsync(CancellationToken.None));

        Assert.Equal(1, AppHostTerminationState.ExitCode);
    }

    /// <summary>manifest 書き込みがキャンセルされた場合に、既存の永続 manifest が破損せず保持されることを確認します。</summary>
    /// <returns>非同期試験の完了を表します。</returns>
    [Fact]
    public async Task CanceledManifestWritePreservesTheExistingPersistentManifest()
    {
        var builder = CreateBuilder();
        var composeFile = Path.Combine(Path.GetTempPath(), $"duck-canceled-{Guid.NewGuid():N}.yaml");
        var stack = new ComposeSimulationStack("duck-canceled-stack", composeFile, "docker", retainOnAppHostStop: true);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        const string existingManifest = "existing persistent configuration";
        await File.WriteAllTextAsync(composeFile, existingManifest);
        try
        {
            using var app = builder.Build();
            var runner = new FakeComposeCommandRunner();
            var owner = new ComposeSimulationStackOwner(
                stack, runner,
                app.Services.GetRequiredService<ResourceNotificationService>(),
                app.Services.GetRequiredService<ResourceLoggerService>(),
                app.Services.GetRequiredService<IHostApplicationLifetime>());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => owner.StartingAsync(new CancellationToken(canceled: true)));

            Assert.Equal(existingManifest, await File.ReadAllTextAsync(composeFile));
            Assert.False(File.Exists(composeFile + ".tmp"));
            Assert.Empty(runner.Calls);
        }
        finally
        {
            File.Delete(composeFile);
        }
    }

    /// <summary>新規スタックの down 失敗時に再試行用マニフェストを残し、後続の停止で回収できることを確認します。</summary>
    [Fact]
    public async Task FailedComposeDownKeepsManifestUntilRetrySucceeds()
    {
        var stackId = $"duck-retry-{Guid.NewGuid():N}";
        var composeFile = Path.Combine(Path.GetTempPath(), $"{stackId}.yaml");
        var firstBuilder = CreateBuilder();
        var firstStack = new ComposeSimulationStack(stackId, composeFile, "docker");
        _ = firstStack.AddService(firstBuilder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        using (var firstApp = firstBuilder.Build())
        {
            var failedRunner = new FakeComposeCommandRunner(failUp: true, failDown: true);
            var failedOwner = new ComposeSimulationStackOwner(
                firstStack, failedRunner,
                firstApp.Services.GetRequiredService<ResourceNotificationService>(),
                firstApp.Services.GetRequiredService<ResourceLoggerService>(),
                firstApp.Services.GetRequiredService<IHostApplicationLifetime>());

            await Assert.ThrowsAsync<InvalidOperationException>(() => failedOwner.StartAsync(CancellationToken.None));

            Assert.Contains(failedRunner.Calls, call => call.Contains("down", StringComparer.Ordinal));
            Assert.True(File.Exists(composeFile));
        }

        var retryBuilder = CreateBuilder();
        var retryStack = new ComposeSimulationStack(stackId, composeFile, "docker");
        _ = retryStack.AddService(retryBuilder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        using (var retryApp = retryBuilder.Build())
        {
            var retryRunner = new FakeComposeCommandRunner();
            var retryOwner = new ComposeSimulationStackOwner(
                retryStack, retryRunner,
                retryApp.Services.GetRequiredService<ResourceNotificationService>(),
                retryApp.Services.GetRequiredService<ResourceLoggerService>(),
                retryApp.Services.GetRequiredService<IHostApplicationLifetime>());
            await retryOwner.StartAsync(CancellationToken.None);
            await retryOwner.StopAsync(CancellationToken.None);
            await retryOwner.StoppedAsync(CancellationToken.None);

            Assert.Contains(retryRunner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        }

        Assert.False(File.Exists(composeFile));
    }

    /// <summary>既存常駐プロジェクトの再起動に失敗した場合、down を実行せず既存資源のマニフェストを保つことを確認します。</summary>
    [Fact]
    public async Task EarlyPersistentRelaunchFailurePreservesExistingProject()
    {
        var builder = CreateBuilder();
        var composeFile = Path.Combine(Path.GetTempPath(), $"duck-existing-{Guid.NewGuid():N}.yaml");
        var stack = new ComposeSimulationStack("duck-existing-stack", composeFile, "docker", retainOnAppHostStop: true);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        const string existingManifest = "existing persistent project manifest";
        await File.WriteAllTextAsync(composeFile, existingManifest);
        try
        {
            using var app = builder.Build();
            var runner = new FakeComposeCommandRunner();
            var owner = new ComposeSimulationStackOwner(
                stack, runner,
                app.Services.GetRequiredService<ResourceNotificationService>(),
                app.Services.GetRequiredService<ResourceLoggerService>(),
                app.Services.GetRequiredService<IHostApplicationLifetime>());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => owner.StartAsync(new CancellationToken(canceled: true)));

            Assert.DoesNotContain(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
            Assert.Empty(runner.Calls);
            Assert.Equal(existingManifest, await File.ReadAllTextAsync(composeFile));
        }
        finally
        {
            File.Delete(composeFile);
        }
    }

    /// <summary>Compose up が既存コンテナーとマニフェストを更新した後の readiness 失敗では、新しい回復情報を戻さず保つことを確認します。</summary>
    [Fact]
    public async Task ReadinessFailureAfterComposeUpdateKeepsManifestUsedForChangedProject()
    {
        var builder = CreateBuilder();
        var composeFile = Path.Combine(Path.GetTempPath(), $"duck-updated-{Guid.NewGuid():N}.yaml");
        var stack = new ComposeSimulationStack("duck-updated-stack", composeFile, "docker", retainOnAppHostStop: true);
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);
        _ = stack.AddService(builder, "crane", "crane", "crane:locked");
        stack.RequireCraneReadiness("crane", "Tigers");
        const string existingManifest = "previous persistent project manifest";
        await File.WriteAllTextAsync(composeFile, existingManifest);
        try
        {
            using var app = builder.Build();
            var runner = new FakeComposeCommandRunner(mutateOnUp: true, failCraneReadiness: true);
            var owner = new ComposeSimulationStackOwner(
                stack, runner,
                app.Services.GetRequiredService<ResourceNotificationService>(),
                app.Services.GetRequiredService<ResourceLoggerService>(),
                app.Services.GetRequiredService<IHostApplicationLifetime>(),
                craneReadinessTimeout: TimeSpan.FromMilliseconds(10));

            await Assert.ThrowsAsync<TimeoutException>(() => owner.StartAsync(CancellationToken.None));

            Assert.NotNull(runner.ManifestUsedByUp);
            Assert.NotEqual(existingManifest, runner.ManifestUsedByUp);
            Assert.Equal(runner.ManifestUsedByUp, await File.ReadAllTextAsync(composeFile));
            Assert.Equal(new('b', 64), runner.OwnerContainerId);
            Assert.DoesNotContain(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        }
        finally
        {
            File.Delete(composeFile);
        }
    }

    /// <summary>DCP が Compose リソースを待つ前に Compose owner が起動する順序を保証します。</summary>
    /// <returns>非同期試験の完了を表します。</returns>
    [Fact]
    public async Task ComposeOwnerStartsBeforeDcpHostedServiceWaitsForComposeResources()
    {
        var builder = CreateBuilder();
        var stack = new ComposeSimulationStack(
            "duck-lifecycle-stack", Path.Combine(Path.GetTempPath(), $"duck-lifecycle-{Guid.NewGuid():N}.yaml"), "docker");
        _ = stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName, "cm4:locked", isOwner: true);

        using var app = builder.Build();
        var runner = new FakeComposeCommandRunner();
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(app.Services.GetRequiredService<ResourceNotificationService>());
                services.AddSingleton(app.Services.GetRequiredService<ResourceLoggerService>());
                services.AddSingleton<IHostedService>(new DcpStartupProbe(
                    () => runner.Calls.Any(call => call.Contains("up", StringComparer.Ordinal))));
                services.AddSingleton<IHostedService>(serviceProvider => new ComposeSimulationStackOwner(
                    stack,
                    runner,
                    serviceProvider.GetRequiredService<ResourceNotificationService>(),
                    serviceProvider.GetRequiredService<ResourceLoggerService>(),
                    serviceProvider.GetRequiredService<IHostApplicationLifetime>()));
            })
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Contains(runner.Calls, call => call.Contains("up", StringComparer.Ordinal));
        Assert.Contains(runner.Calls, call => call.Contains("down", StringComparer.Ordinal));
        Assert.False(File.Exists(stack.ComposeFilePath));
    }

    private static IDistributedApplicationBuilder CreateBuilder() => DistributedApplication.CreateBuilder(
        new DistributedApplicationOptions
        {
            Args = [],
            AssemblyName = typeof(ComposeSimulationStackTests).Assembly.GetName().Name,
            ProjectDirectory = Projects.Duck_Testing_AppHost.ProjectPath,
            DisableDashboard = true,
        });

    /// <summary>Compose owner の起動、終了、状態照会を記録し、指定した失敗条件を再現する runner です。</summary>
    private sealed class FakeComposeCommandRunner(
        bool failUp = false,
        bool rejectPortQueries = false,
        bool failMonitor = false,
        bool failDown = false,
        bool mutateOnUp = false,
        bool failCraneReadiness = false) : IComposeCommandRunner
    {
        /// <summary>Compose CLI に渡された引数列を記録します。</summary>
        public List<string[]> Calls { get; } = [];
        /// <summary>Compose up が実際に使用したマニフェストの内容です。</summary>
        public string? ManifestUsedByUp { get; private set; }
        /// <summary>所有コンテナを模擬するための ID を保持します。</summary>
        public string? OwnerContainerId { get; set; } = new('a', 64);
        /// <summary>follower 再作成の完了を試験側へ通知します。</summary>
        public TaskCompletionSource<bool> FollowersRecreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>復旧状態の観測を試験側へ通知します。</summary>
        public TaskCompletionSource<bool> RecoveryStateObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>owner が利用不能になったことを試験側へ通知します。</summary>
        public TaskCompletionSource<bool> OwnerUnavailableObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Compose コマンドを記録し、設定された終了結果を模擬します。</summary>
        /// <param name="executable">テスト用の実行処理へ渡す実行ファイル名です。</param>
        /// <param name="arguments">実行ファイルへ渡すコマンドライン引数です。</param>
        /// <param name="cancellationToken">処理を中止するためのトークンです。</param>
        /// <returns>実行した試験操作の結果を返します。</returns>
        public Task<ComposeCommandResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            Calls.Add(arguments.ToArray());
            if (failUp && arguments.Contains("up", StringComparer.Ordinal))
            {
                return Task.FromResult(new ComposeCommandResult(17, "", "fixture startup failure"));
            }
            if (mutateOnUp && arguments.Contains("up", StringComparer.Ordinal))
            {
                var manifestIndex = arguments.ToList().IndexOf("--file");
                ManifestUsedByUp = File.ReadAllText(arguments[manifestIndex + 1]);
                OwnerContainerId = new('b', 64);
            }
            if (arguments.Contains("--quiet", StringComparer.Ordinal))
            {
                if (OwnerContainerId is null)
                {
                    OwnerUnavailableObserved.TrySetResult(true);
                    return Task.FromResult(new ComposeCommandResult(0, "", ""));
                }
                return Task.FromResult(new ComposeCommandResult(0, OwnerContainerId, ""));
            }
            if (arguments.Contains("--services", StringComparer.Ordinal))
            {
                if (failMonitor)
                    return Task.FromResult(new ComposeCommandResult(17, "", "simulated monitor failure"));
                if (FollowersRecreated.Task.IsCompleted)
                    RecoveryStateObserved.TrySetResult(true);
                return Task.FromResult(new ComposeCommandResult(0, "cm4-sim\nsimulator\ngame-controller\ncrane", ""));
            }
            if (arguments.Contains("--force-recreate", StringComparer.Ordinal))
            {
                FollowersRecreated.TrySetResult(true);
            }
            if (arguments.Contains("port", StringComparer.Ordinal))
            {
                Assert.False(rejectPortQueries, "Host networking must not query published ports.");
                return Task.FromResult(new ComposeCommandResult(0, "127.0.0.1:49173", ""));
            }
            if (arguments.Contains("exec", StringComparer.Ordinal))
            {
                if (failCraneReadiness)
                    return Task.FromResult(new ComposeCommandResult(17, "", "simulated Crane readiness failure"));
                return Task.FromResult(new ComposeCommandResult(0, "DUCK_CRANE_ROS_SETUP_OK\n/session_controller\n", ""));
            }
            if (failDown && arguments.Contains("down", StringComparer.Ordinal))
            {
                return Task.FromResult(new ComposeCommandResult(17, "", "simulated cleanup failure"));
            }
            return Task.FromResult(new ComposeCommandResult(0, "", ""));
        }

        /// <summary>ログ追跡処理を模擬する disposable follower を返します。</summary>
        /// <param name="executable">テスト用の実行処理へ渡す実行ファイル名です。</param>
        /// <param name="arguments">実行ファイルへ渡すコマンドライン引数です。</param>
        /// <param name="writeLine">取得したログを行単位で受け取るコールバックです。</param>
        /// <returns>ログ追跡を停止するためのハンドルです。</returns>
        public IAsyncDisposable StartLogs(string executable, IReadOnlyList<string> arguments, Action<string> writeLine) =>
            new FakeLogFollower();
    }

    /// <summary>DCP の起動時点で Compose が先に開始済みであることを検証する hosted service です。</summary>
    private sealed class DcpStartupProbe(Func<bool> composeStarted) : IHostedService
    {
        /// <summary>Compose が DCP の resource 待機より先に開始したことを検証します。</summary>
        /// <param name="cancellationToken">処理を中止するためのトークンです。</param>
        /// <returns>非同期試験の完了を表します。</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Assert.True(composeStarted(), "The Compose stack must be started before DCP waits on its resources.");
            return Task.CompletedTask;
        }

        /// <summary>試験用 hosted service の停止を完了します。</summary>
        /// <param name="cancellationToken">処理を中止するためのトークンです。</param>
        /// <returns>非同期試験の完了を表します。</returns>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>ログ追跡の終了を模擬する非同期破棄可能な follower です。</summary>
    private sealed class FakeLogFollower : IAsyncDisposable
    {
        /// <summary>試験用ログ follower を非同期に破棄します。</summary>
        /// <returns>非同期試験の完了を表します。</returns>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
