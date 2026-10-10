using System.Text.Json;

namespace Duck.Testing.AppHost;

/// <summary>
/// Describes the one fixed Docker container owned by an Aspire wrapper resource.
/// </summary>
public sealed class DockerContainerSpec
{
    public const string OwnerLabel = "duck.aspire.owner";
    public const string OwnerValue = "duck-apphost";
    public const string StackLabel = "duck.aspire.stack";
    public const string ResourceLabel = "duck.aspire.resource";
    public const string RunLabel = "duck.aspire.run";
    private static readonly HashSet<string> ReservedDockerOptions = new(StringComparer.Ordinal)
    {
        "--rm", "--name", "-v", "--volume", "--network", "--label", "-l", "--env", "-e",
        "--cidfile", "--detach", "-d", "--restart",
    };

    private readonly string[] containerArguments;
    private readonly KeyValuePair<string, string>[] environment;

    public DockerContainerSpec(
        string resourceName,
        string image,
        IEnumerable<string> containerArguments,
        IReadOnlyDictionary<string, string> environment,
        string stackId,
        string? runId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(image);
        ArgumentNullException.ThrowIfNull(containerArguments);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(stackId);

        ResourceName = ValidateName(resourceName, nameof(resourceName));
        StackId = ValidateName(stackId, nameof(stackId));
        RunId = ValidateName(runId ?? Guid.NewGuid().ToString("N"), nameof(runId));
        Image = image;
        this.containerArguments = containerArguments.ToArray();
        this.environment = environment.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();

        if (this.containerArguments.Any(argument =>
                string.IsNullOrWhiteSpace(argument) || ReservedDockerOptions.Contains(argument)))
        {
            throw new ArgumentException(
                "Container arguments contain a reserved Docker lifecycle option.",
                nameof(containerArguments));
        }

        if (this.environment.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || item.Key.Contains('=') || item.Value is null))
        {
            throw new ArgumentException("Environment entries must have valid keys and non-null values.", nameof(environment));
        }
    }

    public string ResourceName { get; }

    public string StackId { get; }

    public string Image { get; }

    public string RunId { get; }

    public IReadOnlyList<string> ContainerArguments => Array.AsReadOnly(containerArguments);

    public IReadOnlyDictionary<string, string> Environment =>
        environment.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    public string ContainerName => $"duck-{StackId}-{ResourceName}";

    /// <summary>
    /// Builds direct process arguments for <c>docker run</c>; callers must pass this array without a shell.
    /// </summary>
    public string[] CreateRunArguments()
    {
        var arguments = new List<string>
        {
            "run", "--detach", "--name", ContainerName,
            "--network", "host",
            "--label", $"{OwnerLabel}={OwnerValue}",
            "--label", $"{StackLabel}={StackId}",
            "--label", $"{ResourceLabel}={ResourceName}",
            "--label", $"{RunLabel}={RunId}",
        };

        foreach (var (key, value) in environment)
        {
            arguments.Add("--env");
            arguments.Add($"{key}={value}");
        }

        arguments.Add(Image);
        arguments.AddRange(containerArguments);
        return arguments.ToArray();
    }

    /// <summary>Returns a bounded Docker CLI query scoped to this exact wrapper owner.</summary>
    public string[] CreateOwnerDiscoveryArguments() =>
    [
        "ps", "--all", "--quiet",
        "--filter", $"label={OwnerLabel}={OwnerValue}",
        "--filter", $"label={StackLabel}={StackId}",
        "--filter", $"label={ResourceLabel}={ResourceName}",
        "--filter", $"label={RunLabel}={RunId}",
    ];

    /// <summary>Inspects a single ID obtained from this wrapper's cidfile or exact-label query.</summary>
    public static string[] CreateInspectArguments(string containerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        return ["inspect", "--format", "{{json .}}", containerId];
    }

    /// <summary>Inspects before any destructive lifecycle operation is permitted.</summary>
    public bool IsExactOwner(JsonElement inspection, string containerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        try
        {
            var inspectedId = inspection.GetProperty("Id").GetString();
            var labels = inspection.GetProperty("Config").GetProperty("Labels");
            var hostConfig = inspection.GetProperty("HostConfig");
            var name = inspection.GetProperty("Name").GetString();
            var image = inspection.GetProperty("Config").GetProperty("Image").GetString();
            return inspectedId == containerId && name == $"/{ContainerName}" && image == Image &&
                hostConfig.GetProperty("NetworkMode").GetString() == "host" &&
                labels.GetProperty(OwnerLabel).GetString() == OwnerValue &&
                labels.GetProperty(StackLabel).GetString() == StackId &&
                labels.GetProperty(ResourceLabel).GetString() == ResourceName &&
                labels.GetProperty(RunLabel).GetString() == RunId;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Deletes by exact immutable ID only after caller has verified owner inspection.</summary>
    public static string[] CreateRemoveArguments(string containerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        return ["rm", "--force", containerId];
    }

    private static string ValidateName(string value, string parameterName)
    {
        if (value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new ArgumentException("Names may contain only ASCII letters, digits, hyphen, and underscore.", parameterName);
        }

        return value;
    }
}
