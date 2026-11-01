using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Duck.Testing.AppHost;

/// <summary>名前空間共有候補の2資源モデルを生成し、秘匿情報を含まない射影を返す。</summary>
internal static class NamespaceProbeModelFactory
{
    internal const string OwnerResourceName = "namespace-probe-owner";
    internal const string FollowerResourceName = "namespace-probe-follower";
    internal const string AspNetImage = "mcr.microsoft.com/dotnet/aspnet";
    internal const string AspNetImageSha256 = "8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca";
    internal const string PublishMountTarget = "/app";
    internal const int OwnerPort = 39110;
    internal const int FollowerPort = 39111;
    internal const string OwnerRole = "namespace-probe-owner";
    internal const string FollowerRole = "namespace-probe-follower";

    internal enum NetworkMode
    {
        SharedNamespace,
        DefaultBridge,
    }

    private const string EntryAssembly = "Duck.Testing.RefereeDriver.Host.dll";
    private static readonly string[] RequiredPublishFiles =
    [
        EntryAssembly,
        "Duck.Testing.RefereeDriver.Host.deps.json",
        "Duck.Testing.RefereeDriver.Host.runtimeconfig.json",
        "Duck.Testing.RefereeDriver.dll",
        "Duck.Testing.Diagnostics.dll",
        "Google.Protobuf.dll",
        "SslProto.dll",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>
    /// 既存配布先を読み取り専用で割り当てた所有元・依存側をbuilderへ登録し、登録済み注釈からJSON用の安全な射影を作る。
    /// DCPの既定Networksはこの段階では生成されず、builderのBuild/Runも呼び出さない。
    /// </summary>
    internal static Task<NamespaceProbeModelDto> AddResourcesAndProjectAsync(
        IDistributedApplicationBuilder builder,
        string runId,
        string publishDirectory,
        CancellationToken cancellationToken = default)
        => AddResourcesAndProjectAsync(
            builder,
            runId,
            publishDirectory,
            NetworkMode.SharedNamespace,
            cancellationToken);

    /// <summary>指定したネットワーク候補で、既存の二資源モデルを登録して射影します。</summary>
    internal static async Task<NamespaceProbeModelDto> AddResourcesAndProjectAsync(
        IDistributedApplicationBuilder builder,
        string runId,
        string publishDirectory,
        NetworkMode networkMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!Enum.IsDefined(networkMode))
        {
            throw new ArgumentOutOfRangeException(nameof(networkMode));
        }
        var validatedRunId = ValidateRunId(runId);
        ValidateExistingPublishOutput(publishDirectory);

        if (builder.Resources.Any())
        {
            throw new InvalidOperationException("The namespace model requires an empty AppHost resource collection.");
        }

        var ownerContainerName = $"duck-namespace-probe-owner-{validatedRunId}";
        var followerContainerName = $"duck-namespace-probe-follower-{validatedRunId}";

        var owner = builder.AddContainer(OwnerResourceName, AspNetImage)
            .WithImageSHA256(AspNetImageSha256)
            .WithImagePullPolicy(ImagePullPolicy.Never)
            .WithContainerName(ownerContainerName)
            .WithBindMount(publishDirectory, PublishMountTarget, isReadOnly: true)
            .WithArgs(
                "dotnet",
                $"{PublishMountTarget}/{EntryAssembly}",
                "--RefereeDriver:Enabled=false",
                $"--urls=http://0.0.0.0:{OwnerPort}")
            .WithHttpEndpoint(targetPort: OwnerPort, name: "namespace-probe-owner-http", isProxied: false);

        var ownerEndpoint = owner.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Single(endpoint => endpoint.Name == "namespace-probe-owner-http");
        ownerEndpoint.Port = null;
        ownerEndpoint.TargetHost = "127.0.0.1";

        owner.WithContainerRuntimeArgs(
            "--label", $"{ContainerIdentityExecHandoff.RunIdLabel}={validatedRunId}",
            "--label", $"{ContainerIdentityExecHandoff.RoleLabel}={OwnerRole}");

        var followerBuilder = builder.AddContainer(FollowerResourceName, AspNetImage)
            .WithImageSHA256(AspNetImageSha256)
            .WithImagePullPolicy(ImagePullPolicy.Never)
            .WithContainerName(followerContainerName)
            .WithBindMount(publishDirectory, PublishMountTarget, isReadOnly: true)
            .WithArgs(
                "dotnet",
                $"{PublishMountTarget}/{EntryAssembly}",
                "--RefereeDriver:Enabled=false",
                $"--urls=http://0.0.0.0:{FollowerPort}");

        if (networkMode == NetworkMode.SharedNamespace)
        {
            followerBuilder.WithContainerRuntimeArgs(
                "--network", $"container:{ownerContainerName}",
                "--label", $"{ContainerIdentityExecHandoff.RunIdLabel}={validatedRunId}",
                "--label", $"{ContainerIdentityExecHandoff.RoleLabel}={FollowerRole}");
        }
        else
        {
            followerBuilder.WithContainerRuntimeArgs(
                "--label", $"{ContainerIdentityExecHandoff.RunIdLabel}={validatedRunId}",
                "--label", $"{ContainerIdentityExecHandoff.RoleLabel}={FollowerRole}");
        }

        followerBuilder.WaitForStart(owner);

        var resources = new[] { owner.Resource, followerBuilder.Resource };
        var projections = new List<NamespaceProbeResourceDto>(resources.Length);
        foreach (var resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            projections.Add(await ProjectResourceAsync(resource, cancellationToken).ConfigureAwait(false));
        }

        return new NamespaceProbeModelDto(
            SchemaVersion: 1,
            ModelStage: "AppHost resources only; DCP networks are not included",
            Resources: projections);
    }

    internal static string Serialize(NamespaceProbeModelDto model) => JsonSerializer.Serialize(model, JsonOptions);

    internal static string ValidateRunId(string? runId)
    {
        if (string.IsNullOrWhiteSpace(runId) ||
            runId.Length > 64 ||
            runId.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new InvalidOperationException(
                "The namespace model run ID must contain only ASCII letters, digits, hyphens, and underscores, up to 64 characters.");
        }

        return runId;
    }

    private static void ValidateExistingPublishOutput(string publishDirectory)
    {
        if (string.IsNullOrWhiteSpace(publishDirectory) || !Directory.Exists(publishDirectory))
        {
            throw new InvalidOperationException(
                "The existing RefereeDriver publish output is missing; it was not created or downloaded.");
        }

        var missingFiles = RequiredPublishFiles
            .Where(fileName => !File.Exists(Path.Combine(publishDirectory, fileName)))
            .ToArray();
        if (missingFiles.Length > 0)
        {
            throw new InvalidOperationException(
                $"The existing RefereeDriver publish output is incomplete; missing files: {string.Join(", ", missingFiles)}.");
        }
    }

    private static async Task<NamespaceProbeResourceDto> ProjectResourceAsync(
        ContainerResource resource,
        CancellationToken cancellationToken)
    {
        var image = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        var containerName = resource.Annotations.OfType<ContainerNameAnnotation>().Single().Name;
        var pullPolicy = resource.Annotations.OfType<ContainerImagePullPolicyAnnotation>().Single().ImagePullPolicy;

        var arguments = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await annotation.Callback(
                new CommandLineArgsCallbackContext(arguments, resource, cancellationToken)).ConfigureAwait(false);
        }

        var runtimeArguments = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            await annotation.Callback(
                new ContainerRuntimeArgsCallbackContext(runtimeArguments, cancellationToken)).ConfigureAwait(false);
        }

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>()
            .Select(endpoint => new NamespaceProbeEndpointDto(
                endpoint.Name,
                endpoint.Protocol.ToString(),
                endpoint.Port,
                endpoint.TargetPort,
                endpoint.TargetHost,
                endpoint.IsProxied))
            .ToArray();
        var mounts = resource.Annotations.OfType<ContainerMountAnnotation>()
            .Select(mount => new NamespaceProbeMountDto(
                mount.Type.ToString(),
                mount.Target,
                mount.IsReadOnly))
            .ToArray();
        var dependencies = resource.Annotations.OfType<WaitAnnotation>()
            .Select(wait => wait.Resource.Name)
            .ToArray();

        return new NamespaceProbeResourceDto(
            ResourceName: resource.Name,
            ResourceType: resource.GetType().Name,
            ContainerName: containerName,
            Image: image.Image,
            ImageSha256: image.SHA256,
            ImagePullPolicy: pullPolicy.ToString(),
            Arguments: arguments.Select(argument => argument?.ToString() ?? string.Empty).ToArray(),
            RuntimeArguments: runtimeArguments.Select(argument => argument?.ToString() ?? string.Empty).ToArray(),
            Endpoints: endpoints,
            Mounts: mounts,
            WaitForStart: dependencies);
    }
}

internal sealed record NamespaceProbeModelDto(
    int SchemaVersion,
    string ModelStage,
    IReadOnlyList<NamespaceProbeResourceDto> Resources);

internal sealed record NamespaceProbeResourceDto(
    string ResourceName,
    string ResourceType,
    string ContainerName,
    string Image,
    string? ImageSha256,
    string ImagePullPolicy,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> RuntimeArguments,
    IReadOnlyList<NamespaceProbeEndpointDto> Endpoints,
    IReadOnlyList<NamespaceProbeMountDto> Mounts,
    IReadOnlyList<string> WaitForStart);

internal sealed record NamespaceProbeEndpointDto(
    string Name,
    string Protocol,
    int? HostPort,
    int? TargetPort,
    string? TargetHost,
    bool? IsProxied);

internal sealed record NamespaceProbeMountDto(string Type, string Target, bool IsReadOnly);
