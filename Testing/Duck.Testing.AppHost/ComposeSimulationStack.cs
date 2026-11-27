using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.WebSockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duck.Testing.AppHost;

/// <summary>Aspire アプリケーションモデルに Compose サービスを表すリソースです。</summary>
/// <param name="name">Aspire 内でこのリソースを識別する名前です。</param>
internal sealed class ComposeServiceResource(string name) :
    Resource(name), IResourceWithWaitSupport, IResourceWithEndpoints, IResourceWithEnvironment;

/// <summary>Compose サービスに割り当てるバインドマウントの定義です。</summary>
/// <param name="Source">ホスト側のソースパスです。</param>
/// <param name="Target">コンテナー内のマウント先です。</param>
/// <param name="ReadOnly">読み取り専用でマウントするかどうかを示します。</param>
internal sealed record ComposeBindMount(string Source, string Target, bool ReadOnly);

/// <summary>Compose サービスからホストへ公開するポートの設定です。</summary>
/// <param name="EndpointResource">Aspire エンドポイントを所有するリソースです。</param>
/// <param name="EndpointName">公開対象のエンドポイント名です。</param>
/// <param name="OwnerServiceName">ポートを所有する Compose サービス名です。</param>
/// <param name="HostAddress">ホスト側のバインドアドレスです。</param>
/// <param name="TargetPort">コンテナー内の宛先ポートです。</param>
/// <param name="Protocol">エンドポイントの通信プロトコルです。</param>
/// <param name="WaitForHttpReady">HTTP readiness を待つかどうかを示します。</param>
/// <param name="WaitForWebSocketReady">WebSocket readiness を待つかどうかを示します。</param>
internal sealed record ComposePortBinding(
    ComposeServiceResource EndpointResource,
    string EndpointName,
    string OwnerServiceName,
    string HostAddress,
    int TargetPort,
    System.Net.Sockets.ProtocolType Protocol,
    bool WaitForHttpReady = false,
    bool WaitForWebSocketReady = false);

/// <summary>Compose サービスが待ち受けるエンドポイントの定義です。</summary>
/// <param name="Name">エンドポイント名です。</param>
/// <param name="TargetPort">コンテナー内の待ち受けポートです。</param>
/// <param name="Protocol">使用する通信プロトコルです。</param>
internal sealed record ComposeServiceEndpoint(string Name, int TargetPort, System.Net.Sockets.ProtocolType Protocol);
/// <summary>Compose サービスの Crane readiness 条件です。</summary>
/// <param name="ServiceName">readiness を確認するサービス名です。</param>
/// <param name="ExpectedTeamName">応答で期待するチーム名です。</param>
internal sealed record ComposeCraneReadiness(string ServiceName, string ExpectedTeamName);

/// <summary>Compose サービスとそのコンテナー設定を表します。</summary>
/// <param name="resource">Aspire に登録するサービスリソースです。</param>
/// <param name="serviceName">Compose マニフェスト内のサービス名です。</param>
/// <param name="image">サービスに使うイメージ名です。</param>
/// <param name="command">サービスへ渡すコマンド引数です。</param>
/// <param name="environment">サービスに渡す環境変数です。</param>
/// <param name="mounts">サービスに割り当てるバインドマウントです。</param>
/// <param name="sharesOwnerNamespace">所有者サービスのネットワーク名前空間を共有するかどうかです。</param>
/// <param name="dependsOn">起動前に必要なサービス名です。</param>
/// <param name="entrypoint">サービスで上書きするエントリポイントです。</param>
/// <param name="usesBridgeNetwork">専用ブリッジネットワークを使うかどうかです。</param>
/// <param name="buildContext">ローカルイメージを構築するコンテキストです。</param>
internal sealed class ComposeServiceDefinition(
    ComposeServiceResource resource,
    string serviceName,
    string? image,
    IReadOnlyList<string> command,
    IReadOnlyDictionary<string, string> environment,
    IReadOnlyList<ComposeBindMount> mounts,
    bool sharesOwnerNamespace,
    IReadOnlyList<string> dependsOn,
    string? entrypoint,
    bool usesBridgeNetwork,
    string? buildContext)
{
    /// <summary>Aspire に登録した Compose サービスのリソースです。</summary>
    public ComposeServiceResource Resource { get; } = resource;
    /// <summary>Compose ファイル内で使用するサービス名です。</summary>
    public string ServiceName { get; } = serviceName;
    /// <summary>サービスに使用するイメージ名です。ローカルビルドの場合は <see langword="null"/> です。</summary>
    public string? Image { get; private set; } = image;
    internal void UsePreparedImage(string reference)
    {
        if (Image is null) throw new InvalidOperationException("Cannot replace a local build context.");
        Image = reference;
        UsePreparedPlatform();
    }
    internal string? Platform { get; private set; }
    internal void UsePreparedPlatform() => Platform = "linux/amd64";
    /// <summary>サービスのローカルビルドコンテキストです。</summary>
    public string? BuildContext { get; } = buildContext;
    /// <summary>サービスへ渡すコマンド引数です。</summary>
    public IReadOnlyList<string> Command { get; } = command;
    /// <summary>サービスへ渡す環境変数です。</summary>
    public IReadOnlyDictionary<string, string> Environment { get; } = environment;
    /// <summary>サービスに割り当てるバインドマウントです。</summary>
    public IReadOnlyList<ComposeBindMount> Mounts { get; } = mounts;
    /// <summary>所有者サービスのネットワーク名前空間を共有するかどうかを示します。</summary>
    public bool SharesOwnerNamespace { get; } = sharesOwnerNamespace;
    /// <summary>サービスの起動前に必要な Compose サービス名です。</summary>
    public IReadOnlyList<string> DependsOn { get; } = dependsOn;
    /// <summary>サービスに指定するエントリポイントです。</summary>
    public string? Entrypoint { get; } = entrypoint;
    /// <summary>サービスが専用のブリッジネットワークを使用するかどうかを示します。</summary>
    public bool UsesBridgeNetwork { get; } = usesBridgeNetwork;
}

/// <summary>Compose プロジェクト、サービス、ポート公開の設定とマニフェスト生成を管理します。</summary>
/// <param name="projectName">Compose プロジェクト名です。</param>
/// <param name="composeFilePath">生成する Compose マニフェストのパスです。</param>
/// <param name="dockerExecutable">Docker CLI の実行ファイルです。</param>
/// <param name="dockerContextName">使用する Docker コンテキスト名です。</param>
/// <param name="useHostNetworking">Compose サービスでホストネットワークを使用するかどうかです。</param>
/// <param name="stackId">Docker 資源の所有スタックを識別する ID です。</param>
/// <param name="runId">Docker 資源の実行を識別する ID です。</param>
/// <param name="retainOnAppHostStop">AppHost 停止後も Compose 資源を保持するかどうかです。</param>
/// <param name="pullMissingImages">不足イメージを Compose 起動前に取得するかどうかです。</param>
internal sealed class ComposeSimulationStack(
    string projectName,
    string composeFilePath,
    string dockerExecutable,
    string? dockerContextName = null,
    bool useHostNetworking = false,
    string? stackId = null,
    string? runId = null,
    bool retainOnAppHostStop = false,
    bool pullMissingImages = false)
{
    /// <summary>ネットワーク名前空間を所有する CM4 シミュレーターの Compose サービス名です。</summary>
    public const string OwnerServiceName = "cm4-sim";
    private readonly List<ComposeServiceDefinition> services = [];
    private readonly List<ComposePortBinding> portBindings = [];
    private ComposeCraneReadiness? craneReadiness;

    /// <summary>Compose プロジェクト名です。</summary>
    public string ProjectName { get; } = projectName;
    /// <summary>Docker リソースを識別するスタック ID です。</summary>
    public string StackId { get; } = stackId ?? projectName;
    /// <summary>Docker リソースを識別する実行 ID です。</summary>
    public string RunId { get; } = runId ?? projectName;
    /// <summary>Compose が使用するシミュレーションネットワーク名です。</summary>
    public string NetworkName { get; } = $"{projectName}_duck-simulation";
    /// <summary>生成する Compose マニフェストのパスです。</summary>
    public string ComposeFilePath { get; } = composeFilePath;
    /// <summary>Compose コマンドに使用する Docker 実行ファイルです。</summary>
    public string DockerExecutable { get; } = dockerExecutable;
    /// <summary>Docker CLI に渡すコンテキスト名です。</summary>
    public string? DockerContextName { get; } = dockerContextName;

    internal string[] CreateComposeArguments(params string[] arguments) =>
        DockerContextName is null
            ? ["compose", "--project-name", ProjectName, "--file", ComposeFilePath, .. arguments]
            : ["--context", DockerContextName, "compose", "--project-name", ProjectName, "--file", ComposeFilePath, .. arguments];
    /// <summary>サービスにホストネットワークを使用するかどうかを示します。</summary>
    public bool UseHostNetworking { get; } = useHostNetworking;
    /// <summary>AppHost 停止後も Compose プロジェクトを保持するかどうかを示します。</summary>
    public bool RetainOnAppHostStop { get; } = retainOnAppHostStop;
    /// <summary>不足しているイメージを Compose 起動時に取得するかどうかを示します。</summary>
    public bool PullMissingImages { get; } = pullMissingImages;
    /// <summary>スタックに登録された Compose サービスです。</summary>
    public IReadOnlyList<ComposeServiceDefinition> Services => services;
    /// <summary>ホストへ公開する Compose エンドポイントです。</summary>
    public IReadOnlyList<ComposePortBinding> PortBindings => portBindings;
    /// <summary>設定されている Crane の readiness 条件です。</summary>
    public ComposeCraneReadiness? CraneReadiness => craneReadiness;

    /// <summary>指定したサービスの Crane readiness 条件を登録します。</summary>
    /// <param name="serviceName">readiness を確認するサービス名です。</param>
    /// <param name="expectedTeamName">応答で期待するチーム名です。</param>
    /// <exception cref="ArgumentException">引数が空白または空です。</exception>
    /// <exception cref="InvalidOperationException">readiness 条件がすでに登録されています。</exception>
    public void RequireCraneReadiness(string serviceName, string expectedTeamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTeamName);
        if (craneReadiness is not null)
        {
            throw new InvalidOperationException("Compose stack Crane readiness can only be configured once.");
        }
        craneReadiness = new ComposeCraneReadiness(serviceName, expectedTeamName);
    }

    /// <summary>Compose サービスと対応する Aspire リソースをスタックへ登録します。</summary>
    /// <param name="builder">リソースを登録する分散アプリケーションビルダーです。</param>
    /// <param name="resourceName">Aspire リソース名です。</param>
    /// <param name="serviceName">Compose サービス名です。</param>
    /// <param name="image">使用するイメージ名です。<paramref name="buildContext"/> を指定する場合は <see langword="null"/> にします。</param>
    /// <param name="command">コンテナーへ渡すコマンド引数です。</param>
    /// <param name="environment">コンテナーの環境変数です。</param>
    /// <param name="mounts">コンテナーに割り当てるバインドマウントです。</param>
    /// <param name="endpoints">サービスが待ち受けるエンドポイントです。</param>
    /// <param name="isOwner">サービスがネットワーク名前空間の所有者かどうかを指定します。</param>
    /// <param name="entrypoint">コンテナーのエントリポイントです。</param>
    /// <param name="dependsOn">サービスの依存先です。</param>
    /// <param name="usesBridgeNetwork">ブリッジネットワークを使用するかどうかを指定します。</param>
    /// <param name="buildContext">ローカルイメージビルドのコンテキストです。</param>
    /// <returns>登録した Aspire Compose サービスリソースです。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> が <see langword="null"/> です。</exception>
    /// <exception cref="ArgumentException">サービス名が無効、またはイメージとビルドコンテキストの指定が不正です。</exception>
    public ComposeServiceResource AddService(
        IDistributedApplicationBuilder builder,
        string resourceName,
        string serviceName,
        string? image,
        IEnumerable<string>? command = null,
        IReadOnlyDictionary<string, string>? environment = null,
        IEnumerable<ComposeBindMount>? mounts = null,
        IEnumerable<ComposeServiceEndpoint>? endpoints = null,
        bool isOwner = false,
        string? entrypoint = null,
        IEnumerable<string>? dependsOn = null,
        bool usesBridgeNetwork = false,
        string? buildContext = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidateServiceName(serviceName);
        if ((image is null) == (buildContext is null))
            throw new ArgumentException("A Compose service requires either a fixed image or a local build context.");
        var resource = new ComposeServiceResource(resourceName);
        var resourceBuilder = builder.AddResource(resource)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Docker Compose service",
                State = new ResourceStateSnapshot(KnownResourceStates.NotStarted, KnownResourceStateStyles.Info),
                Properties = [],
            });
        _ = resourceBuilder;
        foreach (var endpoint in endpoints ?? [])
        {
            resourceBuilder.WithEndpoint(
                port: UseHostNetworking && !usesBridgeNetwork ? endpoint.TargetPort : null,
                targetPort: endpoint.TargetPort,
                name: endpoint.Name,
                isProxied: false,
                protocol: endpoint.Protocol);
            var endpointAnnotation = resource.Annotations.OfType<EndpointAnnotation>()
                .Single(annotation => annotation.Name == endpoint.Name);
            endpointAnnotation.Port = UseHostNetworking && !usesBridgeNetwork ? endpoint.TargetPort : null;
            endpointAnnotation.TargetHost = "127.0.0.1";
        }
        foreach (var (key, value) in environment ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            resourceBuilder.WithEnvironment(key, value);
        }
        services.Add(new ComposeServiceDefinition(
            resource,
            serviceName,
            image,
            command?.ToArray() ?? [],
            environment?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal),
            mounts?.ToArray() ?? [],
            !isOwner && !usesBridgeNetwork,
            dependsOn?.ToArray() ?? (isOwner ? [] : [OwnerServiceName]),
            entrypoint,
            usesBridgeNetwork,
            buildContext));
        return resource;
    }

    /// <summary>Compose サービスのエンドポイントをホスト公開ポートとして登録します。</summary>
    /// <param name="endpointResource">エンドポイントを持つサービスリソースです。</param>
    /// <param name="endpointName">リソースに登録済みのエンドポイント名です。</param>
    /// <param name="targetPort">コンテナー内の宛先ポートです。</param>
    /// <param name="protocol">公開する通信プロトコルです。</param>
    /// <param name="waitForHttpReady">HTTP readiness を待つ場合は <see langword="true"/> です。</param>
    /// <param name="waitForWebSocketReady">WebSocket readiness を待つ場合は <see langword="true"/> です。</param>
    /// <exception cref="ArgumentNullException"><paramref name="endpointResource"/> が <see langword="null"/> です。</exception>
    /// <exception cref="ArgumentException">エンドポイント名が空です。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="targetPort"/> が有効な TCP/UDP ポート範囲外です。</exception>
    /// <exception cref="InvalidOperationException">指定したエンドポイントがリソースまたはスタックに登録されていません。</exception>
    public void AddPublishedEndpoint(
        ComposeServiceResource endpointResource,
        string endpointName,
        int targetPort,
        System.Net.Sockets.ProtocolType protocol,
        bool waitForHttpReady = false,
        bool waitForWebSocketReady = false)
    {
        ArgumentNullException.ThrowIfNull(endpointResource);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        if (targetPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(targetPort));
        }

        var annotation = endpointResource.Annotations.OfType<EndpointAnnotation>()
            .SingleOrDefault(endpoint => endpoint.Name == endpointName)
            ?? throw new InvalidOperationException($"Endpoint '{endpointName}' is not registered on '{endpointResource.Name}'.");
        if (annotation.TargetPort != targetPort || annotation.Protocol != protocol)
        {
            throw new InvalidOperationException($"Endpoint '{endpointName}' does not match Compose target {protocol}/{targetPort}.");
        }

        var service = services.SingleOrDefault(candidate => ReferenceEquals(candidate.Resource, endpointResource))
            ?? throw new InvalidOperationException($"Compose resource '{endpointResource.Name}' is not part of this stack.");
        portBindings.Add(new ComposePortBinding(
            endpointResource,
            endpointName,
            service.SharesOwnerNamespace ? OwnerServiceName : service.ServiceName,
            "127.0.0.1",
            targetPort,
            protocol,
            waitForHttpReady,
            waitForWebSocketReady));
    }

    /// <summary>登録済みサービスとポート設定から Compose YAML を生成します。</summary>
    /// <returns>Compose プロジェクトを表す YAML 文字列です。</returns>
    /// <exception cref="InvalidOperationException">サービスが未登録、または名前空間所有者が一意に存在しません。</exception>
    public string RenderYaml()
    {
        if (services.Count == 0 ||
            services.Count(service => service.ServiceName == OwnerServiceName && !service.SharesOwnerNamespace) != 1)
        {
            throw new InvalidOperationException("The Compose stack requires exactly one cm4-sim namespace owner.");
        }

        var yaml = new StringBuilder("services:\n");
        foreach (var service in services.OrderBy(service => service.SharesOwnerNamespace).ThenBy(service => service.ServiceName, StringComparer.Ordinal))
        {
            yaml.Append("  ").Append(service.ServiceName).AppendLine(":");
            if (service.BuildContext is { } context)
            {
                yaml.AppendLine("    build:");
                yaml.Append("      context: ").Append(Quote(context)).Append('\n');
            }
            else
            {
                yaml.Append("    image: ").Append(Quote(service.Image!)).Append('\n');
            }
            if (service.Platform is not null)
                yaml.Append("    platform: ").Append(Quote(service.Platform)).Append('\n');
            if (service.Entrypoint is not null)
            {
                yaml.Append("    entrypoint: [").Append(Quote(service.Entrypoint)).AppendLine("]");
            }
            if (service.Command.Count > 0)
            {
                yaml.Append("    command: [")
                    .Append(string.Join(", ", service.Command.Select(Quote)))
                    .AppendLine("]");
            }
            if (service.Environment.Count > 0)
            {
                yaml.AppendLine("    environment:");
                foreach (var (key, value) in service.Environment.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    yaml.Append("      - ").Append(Quote($"{key}={value}")).Append('\n');
                }
            }
            yaml.AppendLine("    labels:")
                .Append("      duck.aspire.owner: ").Append(Quote(DockerContainerSpec.OwnerValue)).Append('\n')
                .Append("      duck.aspire.stack: ").Append(Quote(StackId)).Append('\n')
                .Append("      duck.aspire.run: ").Append(Quote(RunId)).Append('\n')
                .Append("      duck.aspire.resource: ").Append(Quote($"compose-{service.Resource.Name}")).Append('\n');
            if (service.SharesOwnerNamespace)
            {
                yaml.Append("    network_mode: ").Append(Quote($"service:{OwnerServiceName}")).Append('\n');
            }
            else if (UseHostNetworking && !service.UsesBridgeNetwork)
            {
                yaml.AppendLine("    network_mode: \"host\"");
            }
            if (service.DependsOn.Count > 0)
            {
                yaml.AppendLine("    depends_on:");
                foreach (var dependency in service.DependsOn)
                {
                    yaml.Append("      - ").Append(Quote(dependency)).Append('\n');
                }
            }
            if (!service.SharesOwnerNamespace && (!UseHostNetworking || service.UsesBridgeNetwork))
            {
                var published = portBindings.Where(binding => binding.OwnerServiceName == service.ServiceName).ToArray();
                if (published.Length > 0)
                {
                    yaml.AppendLine("    ports:");
                    foreach (var binding in published)
                    {
                        var protocol = binding.Protocol == System.Net.Sockets.ProtocolType.Udp ? "/udp" : "/tcp";
                        yaml.Append("      - ").Append(Quote($"{binding.HostAddress}::{binding.TargetPort}{protocol}")).Append('\n');
                    }
                }
                yaml.AppendLine("    networks:")
                    .AppendLine("      - duck-simulation");
            }
            if (service.Mounts.Count > 0)
            {
                yaml.AppendLine("    volumes:");
                foreach (var mount in service.Mounts)
                {
                    yaml.AppendLine("      - type: bind")
                        .Append("        source: ").Append(Quote(mount.Source)).Append('\n')
                        .Append("        target: ").Append(Quote(mount.Target)).Append('\n')
                        .Append("        read_only: ").Append(mount.ReadOnly ? "true" : "false").Append('\n')
                        .AppendLine("        bind:")
                        .AppendLine("          create_host_path: false");
                }
            }
        }

        if (!UseHostNetworking || services.Any(service => service.UsesBridgeNetwork))
        {
            yaml.AppendLine("networks:")
                .AppendLine("  duck-simulation:")
                .AppendLine("    driver: bridge");
        }
        return yaml.ToString();
    }

    private static string Quote(string value) => JsonSerializer.Serialize(value.Replace("$", "$$", StringComparison.Ordinal));

    private static void ValidateServiceName(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        if (serviceName.Any(character => !(char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '-' or '_')))
        {
            throw new ArgumentException("Compose service names must use lowercase ASCII letters, digits, hyphen, and underscore.", nameof(serviceName));
        }
    }
}

/// <summary>Compose スタックを Aspire のサービスとホストライフサイクルへ登録します。</summary>
internal static class ComposeSimulationStackRegistration
{
    /// <summary>Compose スタックを作成し、Aspire のサービスとホストライフサイクルへ登録します。</summary>
    /// <param name="builder">スタックを登録する分散アプリケーションビルダーです。</param>
    /// <param name="dockerExecutable">Docker CLI の実行ファイルです。</param>
    /// <param name="dockerContextName">使用する Docker コンテキスト名です。</param>
    /// <param name="stackId">スタックを識別する ID です。</param>
    /// <param name="runId">今回の実行を識別する ID です。</param>
    /// <param name="useHostNetworking">ホストネットワークを使う場合は <see langword="true"/> です。</param>
    /// <param name="retainOnAppHostStop">AppHost 停止時にプロジェクトを保持する場合は <see langword="true"/> です。</param>
    /// <param name="pullMissingImages">不足イメージを取得する場合は <see langword="true"/> です。</param>
    /// <returns>登録した Compose スタックです。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> が <see langword="null"/> です。</exception>
    /// <exception cref="ArgumentException"><paramref name="dockerExecutable"/> が空白です。</exception>
    public static ComposeSimulationStack Add(
        IDistributedApplicationBuilder builder,
        string dockerExecutable,
        string? dockerContextName,
        string stackId,
        string runId,
        bool useHostNetworking = false,
        bool retainOnAppHostStop = false,
        bool pullMissingImages = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(dockerExecutable);
        var projectHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{stackId}\0{runId}")))
            .ToLowerInvariant()[..20];
        var projectName = $"duck-{projectHash}";
        var composeFilePath = Path.Combine(Path.GetTempPath(), $"{projectName}.compose.yaml");
        var stack = new ComposeSimulationStack(
            projectName, composeFilePath, dockerExecutable, dockerContextName, useHostNetworking,
            stackId, runId, retainOnAppHostStop, pullMissingImages);
        builder.Services.AddSingleton(stack);
        builder.Services.AddSingleton<ComposeSimulationStackOwner>();
        builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<ComposeSimulationStackOwner>());
        return stack;
    }
}

/// <summary>Docker Compose コマンドの終了状態と出力です。</summary>
/// <param name="ExitCode">プロセスの終了コードです。</param>
/// <param name="StandardOutput">標準出力の内容です。</param>
/// <param name="StandardError">標準エラーの内容です。</param>
internal sealed record ComposeCommandResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Docker Compose の実行とログ追従を抽象化します。</summary>
internal interface IComposeCommandRunner
{
    /// <summary>Docker Compose コマンドを実行し、終了コードと標準出力を返します。</summary>
    /// <param name="executable">Docker CLI の実行ファイルです。</param>
    /// <param name="arguments">Compose サブコマンドと引数です。</param>
    /// <param name="cancellationToken">実行を中止するキャンセルトークンです。</param>
    /// <returns>終了コード、標準出力、標準エラーを含む結果です。</returns>
    /// <exception cref="InvalidOperationException">Docker CLI を起動できません。</exception>
    /// <exception cref="OperationCanceledException">実行がキャンセルされました。</exception>
    Task<ComposeCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    /// <summary>Docker Compose のログ追従プロセスを開始します。</summary>
    /// <param name="executable">Docker CLI の実行ファイルです。</param>
    /// <param name="arguments">Compose ログコマンドの引数です。</param>
    /// <param name="writeLine">ログ行を受け取るコールバックです。</param>
    /// <returns>ログプロセスを停止・破棄する非同期 disposable です。</returns>
    /// <exception cref="InvalidOperationException">ログプロセスを起動できません。</exception>
    IAsyncDisposable StartLogs(string executable, IReadOnlyList<string> arguments, Action<string> writeLine);
}

/// <summary>Docker CLI を起動して Compose コマンドとログ追従を行います。</summary>
internal sealed class DockerComposeCommandRunner : IComposeCommandRunner
{
    /// <summary>Docker Compose コマンドを起動し、結果を取得します。</summary>
    /// <param name="executable">Docker CLI の実行ファイルです。</param>
    /// <param name="arguments">Compose サブコマンドと引数です。</param>
    /// <param name="cancellationToken">実行を中止するキャンセルトークンです。</param>
    /// <returns>終了コード、標準出力、標準エラーを含む結果です。</returns>
    /// <exception cref="InvalidOperationException">Docker CLI を起動できません。</exception>
    /// <exception cref="OperationCanceledException">実行がキャンセルされました。</exception>
    public async Task<ComposeCommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = DockerCliProcess.CreateStartInfo(executable, arguments);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("docker compose could not be started.");
        }

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
        return new ComposeCommandResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    /// <summary>Compose ログ追従プロセスを開始します。</summary>
    /// <param name="executable">Docker CLI の実行ファイルです。</param>
    /// <param name="arguments">Compose ログコマンドの引数です。</param>
    /// <param name="writeLine">標準出力と標準エラーから受け取った行の通知先です。</param>
    /// <returns>ログプロセスを停止・破棄する非同期 disposable です。</returns>
    /// <exception cref="InvalidOperationException">ログプロセスを起動できません。</exception>
    public IAsyncDisposable StartLogs(string executable, IReadOnlyList<string> arguments, Action<string> writeLine) =>
        new DockerComposeLogFollower(executable, arguments, writeLine);

    /// <summary>Compose ログプロセスの出力を読み取り、破棄時にプロセスを停止します。</summary>
    private sealed class DockerComposeLogFollower : IAsyncDisposable
    {
        private readonly Process process;
        private readonly Task stdout;
        private readonly Task stderr;

        /// <summary>Compose ログ追従プロセスを開始します。</summary>
        /// <param name="executable">Docker CLI の実行ファイルです。</param>
        /// <param name="arguments">Compose ログコマンドの引数です。</param>
        /// <param name="writeLine">各ログ行の通知先です。</param>
        /// <exception cref="InvalidOperationException">ログプロセスを起動できません。</exception>
        public DockerComposeLogFollower(string executable, IReadOnlyList<string> arguments, Action<string> writeLine)
        {
            var startInfo = DockerCliProcess.CreateStartInfo(executable, arguments);
            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
            {
                throw new InvalidOperationException("docker compose logs could not be started.");
            }
            stdout = PumpAsync(process.StandardOutput, writeLine);
            stderr = PumpAsync(process.StandardError, writeLine);
        }

        /// <summary>ログ追従プロセスを終了し、読み取り処理とリソースを解放します。</summary>
        /// <returns>プロセス終了と出力読み取りの完了を表す非同期操作です。</returns>
        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // It exited between the check and kill.
                }
            }
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            }
            finally
            {
                process.Dispose();
            }
        }

        private static async Task PumpAsync(StreamReader reader, Action<string> writeLine)
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                writeLine(line);
            }
        }
    }
}

/// <summary>Compose プロジェクトの起動、状態通知、監視、停止をホストライフサイクルに結び付けます。</summary>
internal sealed class ComposeSimulationStackOwner : IHostedLifecycleService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan OwnerReplacementWaitTimeout = TimeSpan.FromMinutes(2);
    private readonly ComposeSimulationStack stack;
    private readonly IComposeCommandRunner runner;
    private readonly ResourceNotificationService notifications;
    private readonly ResourceLoggerService resourceLogger;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly TimeSpan pollInterval;
    private readonly TimeSpan ownerReplacementWaitTimeout;
    private readonly TimeSpan craneReadinessTimeout;
    private readonly CancellationTokenSource monitorCancellation = new();
    private IAsyncDisposable? logs;
    private Task? monitorTask;
    private readonly object startupLock = new();
    private Task? startupTask;
    private bool started;
    private string? ownerContainerId;

    /// <summary>Compose スタックのホストライフサイクル管理サービスを作成します。</summary>
    /// <param name="stack">起動と停止を管理する Compose スタックです。</param>
    /// <param name="notifications">Aspire リソース状態を通知するサービスです。</param>
    /// <param name="resourceLogger">Aspire リソースのログを記録するサービスです。</param>
    /// <param name="applicationLifetime">ホストのライフサイクルを通知するサービスです。</param>
    public ComposeSimulationStackOwner(
        ComposeSimulationStack stack,
        ResourceNotificationService notifications,
        ResourceLoggerService resourceLogger,
        IHostApplicationLifetime applicationLifetime)
        : this(stack, new DockerComposeCommandRunner(), notifications, resourceLogger, applicationLifetime)
    {
    }

    /// <summary>Compose 資源の状態監視、ログ記録、ライフサイクルを管理します。</summary>
    /// <param name="stack">起動・停止を管理する Compose スタックです。</param>
    /// <param name="runner">Compose コマンドとログ追従を実行する処理です。</param>
    /// <param name="notifications">Aspire 資源の状態を通知するサービスです。</param>
    /// <param name="resourceLogger">Aspire 資源のログを記録するサービスです。</param>
    /// <param name="applicationLifetime">ホストのライフサイクルを通知するサービスです。</param>
    /// <param name="pollInterval">状態監視の間隔です。</param>
    /// <param name="ownerReplacementWaitTimeout">所有者コンテナー再作成後の復帰待ち時間です。</param>
    /// <param name="craneReadinessTimeout">Crane readiness の確認を待つ時間です。</param>
    internal ComposeSimulationStackOwner(
        ComposeSimulationStack stack,
        IComposeCommandRunner runner,
        ResourceNotificationService notifications,
        ResourceLoggerService resourceLogger,
        IHostApplicationLifetime applicationLifetime,
        TimeSpan? pollInterval = null,
        TimeSpan? ownerReplacementWaitTimeout = null,
        TimeSpan? craneReadinessTimeout = null)
    {
        this.stack = stack;
        this.runner = runner;
        this.notifications = notifications;
        this.resourceLogger = resourceLogger;
        this.applicationLifetime = applicationLifetime;
        this.pollInterval = pollInterval ?? PollInterval;
        this.ownerReplacementWaitTimeout = ownerReplacementWaitTimeout ?? OwnerReplacementWaitTimeout;
        this.craneReadinessTimeout = craneReadinessTimeout ?? TimeSpan.FromMinutes(6);
    }

    /// <summary>Aspire の通常の hosted service より先に Compose スタックを起動します。</summary>
    /// <param name="cancellationToken">起動を中止するキャンセルトークンです。</param>
    /// <returns>スタック起動の完了を表す非同期操作です。</returns>
    /// <exception cref="OperationCanceledException">起動がキャンセルされました。</exception>
    public Task StartingAsync(CancellationToken cancellationToken) => StartExecutionAsync(cancellationToken);

    /// <summary>ホスト起動時にスタックを起動し、先行起動済みの場合はその処理を共有します。</summary>
    /// <param name="cancellationToken">起動を中止するキャンセルトークンです。</param>
    /// <returns>スタック起動の完了を表す非同期操作です。</returns>
    /// <exception cref="OperationCanceledException">起動がキャンセルされました。</exception>
    public Task StartAsync(CancellationToken cancellationToken) => StartExecutionAsync(cancellationToken);

    private Task StartExecutionAsync(CancellationToken cancellationToken)
    {
        lock (startupLock)
        {
            startupTask ??= StartExecutionCoreAsync(cancellationToken);
            return startupTask;
        }
    }

    private async Task StartExecutionCoreAsync(CancellationToken cancellationToken)
    {
        var composeUpStarted = false;
        var existingPersistentManifest = stack.RetainOnAppHostStop && File.Exists(stack.ComposeFilePath)
            ? await File.ReadAllBytesAsync(stack.ComposeFilePath, cancellationToken).ConfigureAwait(false)
            : null;
        Directory.CreateDirectory(Path.GetDirectoryName(stack.ComposeFilePath)!);
        var pendingManifest = stack.ComposeFilePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(pendingManifest, stack.RenderYaml(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(pendingManifest, stack.ComposeFilePath, overwrite: true);
        }
        finally
        {
            TryDeleteComposeFile(pendingManifest);
        }
        try
        {
            var buildArguments = stack.Services.Any(service => service.BuildContext is not null)
                ? new[] { "--build" }
                : Array.Empty<string>();
            composeUpStarted = true;
            await RunRequiredAsync(ComposeArguments([
                    "up", "--pull", stack.PullMissingImages ? "missing" : "never",
                    "--detach", "--wait", "--wait-timeout", "120", .. buildArguments]), cancellationToken)
                .ConfigureAwait(false);
            ownerContainerId = await ReadOwnerContainerIdAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Compose did not report a running cm4-sim owner after startup.");
            logs = runner.StartLogs(stack.DockerExecutable, ComposeArguments("logs", "--follow", "--no-color", "--timestamps"), WriteLog);
            await ProjectPublishedPortsAsync(cancellationToken).ConfigureAwait(false);
            await WaitForPublishedHttpEndpointsAsync(cancellationToken).ConfigureAwait(false);
            await WaitForCraneReadinessAsync(cancellationToken).ConfigureAwait(false);
            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.Running, KnownResourceStateStyles.Success, null)
                    .ConfigureAwait(false);
            }
            started = true;
            monitorTask = MonitorAsync(monitorCancellation.Token);
        }
        catch (Exception exception)
        {
            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error, exception.Message)
                    .ConfigureAwait(false);
            }
            if (logs is not null)
            {
                await logs.DisposeAsync().ConfigureAwait(false);
                logs = null;
            }
            if (existingPersistentManifest is null)
            {
                await TryCleanupAsync().ConfigureAwait(false);
            }
            else if (composeUpStarted)
            {
                resourceLogger.GetLogger(stack.Services[0].Resource)
                    .LogWarning("Compose up was attempted for persistent project {ProjectName}; keeping its manifest because the command may have changed resources.", stack.ProjectName);
            }
            else
            {
                await RestorePersistentManifestAsync(existingPersistentManifest).ConfigureAwait(false);
            }
            throw;
        }
    }

    /// <summary>ホストの起動完了時に呼び出されます。追加の処理は行いません。</summary>
    /// <param name="cancellationToken">ホストのキャンセルトークンです。</param>
    /// <returns>完了済みの非同期操作です。</returns>
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>ホスト停止の初期段階に呼び出されます。追加の処理は行いません。</summary>
    /// <param name="cancellationToken">ホストのキャンセルトークンです。</param>
    /// <returns>完了済みの非同期操作です。</returns>
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>状態監視とログ追従を停止します。</summary>
    /// <param name="cancellationToken">停止待機を中止するキャンセルトークンです。</param>
    /// <returns>監視とログ処理の停止を表す非同期操作です。</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        monitorCancellation.Cancel();
        if (monitorTask is not null)
        {
            try
            {
                await monitorTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The AppHost is stopping; project cleanup happens after all other hosted services stop.
            }
        }
        if (logs is not null)
        {
            await logs.DisposeAsync().ConfigureAwait(false);
            logs = null;
        }
    }

    /// <summary>ホスト停止後に Compose プロジェクトを保持またはクリーンアップします。</summary>
    /// <param name="cancellationToken">ホストのキャンセルトークンです。</param>
    /// <returns>プロジェクト停止処理の完了を表す非同期操作です。</returns>
    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        if (started)
        {
            if (stack.RetainOnAppHostStop)
            {
                resourceLogger.GetLogger(stack.Services[0].Resource)
                    .LogInformation("Retaining persistent Compose project {ProjectName} after AppHost shutdown.", stack.ProjectName);
                return;
            }

            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.Stopping, KnownResourceStateStyles.Info, null)
                    .ConfigureAwait(false);
            }
            await CleanupProjectAsync().ConfigureAwait(false);
            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.Finished, KnownResourceStateStyles.Success, null)
                    .ConfigureAwait(false);
                resourceLogger.Complete(service.Resource);
            }
        }
        TryDeleteComposeFile();
    }

    private async Task ProjectPublishedPortsAsync(CancellationToken cancellationToken)
    {
        foreach (var binding in stack.PortBindings)
        {
            var endpoint = binding.EndpointResource.Annotations.OfType<EndpointAnnotation>()
                .Single(annotation => annotation.Name == binding.EndpointName);
            var service = stack.Services.Single(candidate => candidate.ServiceName == binding.OwnerServiceName);
            if (stack.UseHostNetworking && !service.UsesBridgeNetwork)
            {
                // Host networking has no published-port allocation for `compose port` to query.
                ComposeEndpointProjection.SetAllocatedPort(endpoint, binding.HostAddress, binding.TargetPort);
                continue;
            }
            var output = await RunRequiredAsync(
                ComposeArguments(
                    "port",
                    "--protocol",
                    binding.Protocol == System.Net.Sockets.ProtocolType.Udp ? "udp" : "tcp",
                    binding.OwnerServiceName,
                    binding.TargetPort.ToString(CultureInfo.InvariantCulture)),
                cancellationToken).ConfigureAwait(false);
            var (address, port) = ParsePublishedEndpoint(output);
            ComposeEndpointProjection.SetAllocatedPort(endpoint, address, port);
        }
    }

    private async Task WaitForPublishedHttpEndpointsAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        foreach (var binding in stack.PortBindings.Where(binding => binding.WaitForHttpReady || binding.WaitForWebSocketReady))
        {
            var endpoint = binding.EndpointResource.Annotations.OfType<EndpointAnnotation>()
                .Single(annotation => annotation.Name == binding.EndpointName);
            var allocation = endpoint.AllocatedEndpoint
                ?? throw new InvalidOperationException($"Compose did not allocate endpoint '{binding.EndpointName}'.");
            var uri = binding.WaitForWebSocketReady
                ? DcpBaseGameControllerReadinessProbe.CreateControlApiUri(
                    new Uri($"http://{allocation.Address}:{allocation.Port}"))
                : new Uri($"http://{allocation.Address}:{allocation.Port}/health/ready");
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (binding.WaitForWebSocketReady)
                    {
                        using var socket = new ClientWebSocket();
                        await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
                        if (socket.State == WebSocketState.Open)
                            break;
                    }
                    else
                    {
                        using var response = await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                        if (response.IsSuccessStatusCode)
                            break;
                    }
                }
                catch (Exception exception) when (exception is HttpRequestException or WebSocketException or IOException)
                {
                    // Compose reported running; allow the endpoint service time to finish initialization.
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"Compose endpoint '{binding.EndpointName}' did not become ready.");
            }
        }
    }

    private async Task WaitForCraneReadinessAsync(CancellationToken cancellationToken)
    {
        if (stack.CraneReadiness is not { } readiness)
        {
            return;
        }

        var deadline = DateTimeOffset.UtcNow + craneReadinessTimeout;
        var probe = CraneProbeDiagnostics.CreateCraneReadinessProbeArguments("compose-container-placeholder", readiness.ExpectedTeamName)
            .Skip(2)
            .ToArray();
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await runner.RunAsync(
                stack.DockerExecutable,
                ComposeArguments(["exec", "-T", readiness.ServiceName, .. probe]),
                cancellationToken).ConfigureAwait(false);
            var outcome = CraneProbeDiagnostics.Evaluate(
                result.ExitCode, result.StandardOutput, result.StandardError, TimeSpan.Zero);
            if (outcome.Ready)
            {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The Compose Crane service did not pass the existing ROS coordinator and team readiness probe.");
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
                var currentOwnerId = await ReadOwnerContainerIdAsync(cancellationToken).ConfigureAwait(false);
                if (currentOwnerId is null)
                {
                    currentOwnerId = await WaitForOwnerContainerAsync(cancellationToken).ConfigureAwait(false);
                    if (currentOwnerId is null)
                    {
                        throw new TimeoutException("The cm4-sim owner did not return as a running container after it stopped.");
                    }
                }

                if (!string.Equals(ownerContainerId, currentOwnerId, StringComparison.Ordinal))
                {
                    await RecreateOwnerNamespaceFollowersAsync(currentOwnerId, cancellationToken).ConfigureAwait(false);
                    ownerContainerId = currentOwnerId;
                }

                var running = await ReadRunningServicesAsync(cancellationToken).ConfigureAwait(false);
                var missing = stack.Services.Select(service => service.ServiceName)
                    .Where(serviceName => !running.Contains(serviceName)).ToArray();
                if (missing.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Compose services stopped unexpectedly: {string.Join(", ", missing)}.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal AppHost shutdown.
        }
        catch (Exception exception)
        {
            AppHostTerminationState.MarkFailed();
            Console.Error.WriteLine($"Compose stack monitor stopped AppHost for project '{stack.ProjectName}': {exception}");
            foreach (var service in stack.Services)
            {
                await PublishStateAsync(service.Resource, KnownResourceStates.RuntimeUnhealthy, KnownResourceStateStyles.Error, exception.Message)
                    .ConfigureAwait(false);
            }
            applicationLifetime.StopApplication();
        }
    }

    private async Task<string?> ReadOwnerContainerIdAsync(CancellationToken cancellationToken)
    {
        var output = await RunRequiredAsync(
            ComposeArguments("ps", "--quiet", ComposeSimulationStack.OwnerServiceName),
            cancellationToken).ConfigureAwait(false);
        var ids = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ids.Length switch
        {
            0 => null,
            1 => ids[0],
            _ => throw new InvalidOperationException("Compose reported more than one running cm4-sim owner."),
        };
    }

    private async Task<string?> WaitForOwnerContainerAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(ownerReplacementWaitTimeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var containerId = await ReadOwnerContainerIdAsync(cancellationToken).ConfigureAwait(false);
            if (containerId is not null)
            {
                return containerId;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<HashSet<string>> ReadRunningServicesAsync(CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(
            stack.DockerExecutable,
            ComposeArguments("ps", "--all", "--services", "--status", "running"),
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker compose ps failed: {result.StandardError.Trim()}");
        }

        return result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task RecreateOwnerNamespaceFollowersAsync(
        string replacementOwnerId,
        CancellationToken cancellationToken)
    {
        var followers = stack.Services
            .Where(service => service.SharesOwnerNamespace)
            .Select(service => service.ServiceName)
            .ToArray();
        if (followers.Length > 0)
        {
            await RunRequiredAsync(
                ComposeArguments([
                    "up", "--pull", "never", "--detach", "--wait", "--wait-timeout", "120",
                    "--force-recreate", "--no-deps", .. followers]),
                cancellationToken).ConfigureAwait(false);
        }

        await ProjectPublishedPortsAsync(cancellationToken).ConfigureAwait(false);
        await WaitForPublishedHttpEndpointsAsync(cancellationToken).ConfigureAwait(false);
        await WaitForCraneReadinessAsync(cancellationToken).ConfigureAwait(false);

        var currentOwnerId = await ReadOwnerContainerIdAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(currentOwnerId, replacementOwnerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The cm4-sim owner changed again before its namespace followers were restored.");
        }

        var running = await ReadRunningServicesAsync(cancellationToken).ConfigureAwait(false);
        var missing = stack.Services.Select(service => service.ServiceName)
            .Where(serviceName => !running.Contains(serviceName)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Compose namespace followers did not recover after cm4-sim replacement: {string.Join(", ", missing)}.");
        }

        foreach (var service in stack.Services)
        {
            await PublishStateAsync(service.Resource, KnownResourceStates.Running, KnownResourceStateStyles.Success, null)
                .ConfigureAwait(false);
        }

        resourceLogger.GetLogger(stack.Services.Single(service =>
                service.ServiceName == ComposeSimulationStack.OwnerServiceName).Resource)
            .LogInformation(
                "Detected replacement cm4-sim container {ContainerId}; recreated network namespace followers: {Followers}.",
                replacementOwnerId,
                string.Join(", ", followers));
    }

    private async Task<string> RunRequiredAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(stack.DockerExecutable, arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker {string.Join(' ', arguments)} failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }
        return result.StandardOutput.Trim();
    }

    private string[] ComposeArguments(params string[] arguments) =>
        stack.CreateComposeArguments(arguments);

    private async Task PublishStateAsync(
        ComposeServiceResource resource,
        string state,
        string style,
        string? detail)
    {
        await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = new ResourceStateSnapshot(state, style),
            StartTimeStamp = state == KnownResourceStates.Running ? DateTime.UtcNow : snapshot.StartTimeStamp,
            StopTimeStamp = state == KnownResourceStates.Finished || state == KnownResourceStates.FailedToStart
                ? DateTime.UtcNow
                : snapshot.StopTimeStamp,
            Properties = string.IsNullOrWhiteSpace(detail)
                ? snapshot.Properties
                : snapshot.Properties.Add(new ResourcePropertySnapshot("detail", detail)),
        }).ConfigureAwait(false);
    }

    private void WriteLog(string line)
    {
        var separator = line.IndexOf('|');
        var serviceName = separator > 0 ? line[..separator].Trim().Trim('[', ']') : "docker-compose";
        var service = stack.Services.FirstOrDefault(candidate => candidate.ServiceName == serviceName);
        resourceLogger.GetLogger(service?.Resource ?? stack.Services[0].Resource)
            .LogInformation("{ComposeLogLine}", line);
    }

    private static (string Address, int Port) ParsePublishedEndpoint(string value)
    {
        var endpointText = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
        if (endpointText is null || !IPEndPoint.TryParse(endpointText, out var endpoint))
        {
            throw new InvalidOperationException($"docker compose port returned an invalid endpoint: '{value}'.");
        }
        return (endpoint.Address.ToString(), endpoint.Port);
    }

    private async Task TryCleanupAsync()
    {
        try
        {
            await CleanupProjectAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            resourceLogger.GetLogger(stack.Services[0].Resource)
                .LogError(exception, "Compose project cleanup failed after startup error for {ProjectName}.", stack.ProjectName);
            return;
        }
        TryDeleteComposeFile();
    }

    private async Task RestorePersistentManifestAsync(byte[] manifest)
    {
        var restorePath = stack.ComposeFilePath + ".restore.tmp";
        try
        {
            await File.WriteAllBytesAsync(restorePath, manifest, CancellationToken.None).ConfigureAwait(false);
            File.Move(restorePath, stack.ComposeFilePath, overwrite: true);
            resourceLogger.GetLogger(stack.Services[0].Resource)
                .LogWarning("Startup failed while reusing persistent Compose project {ProjectName}; its existing resources were left untouched.", stack.ProjectName);
        }
        catch (Exception exception)
        {
            resourceLogger.GetLogger(stack.Services[0].Resource)
                .LogError(exception, "Could not restore the existing persistent Compose manifest {ComposeFilePath}.", stack.ComposeFilePath);
        }
        finally
        {
            TryDeleteComposeFile(restorePath);
        }
    }

    private async Task CleanupProjectAsync()
    {
        using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        // Local images are generated by this Compose project. Upstream services specify their pinned image names.
        var imageArguments = stack.Services.Any(service => service.BuildContext is not null)
            ? new[] { "--rmi", "local" }
            : Array.Empty<string>();
        await RunRequiredAsync(
            ComposeArguments(["down", "--remove-orphans", "--timeout", "15", .. imageArguments]),
            cleanupBudget.Token).ConfigureAwait(false);
    }

    private void TryDeleteComposeFile() => TryDeleteComposeFile(stack.ComposeFilePath);

    private void TryDeleteComposeFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException exception)
        {
            resourceLogger.GetLogger(stack.Services[0].Resource)
                .LogWarning(exception, "Could not remove the temporary Compose file {ComposeFilePath}.", path);
        }
    }
}
