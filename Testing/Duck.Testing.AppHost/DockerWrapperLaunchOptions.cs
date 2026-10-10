namespace Duck.Testing.AppHost;

public sealed record DockerWrapperLaunchOptions(
    string ResourceName,
    string Image,
    string[] ContainerArguments,
    Dictionary<string, string> Environment,
    string StackId,
    string RunId,
    int HealthPort,
    string DockerExecutable = "docker",
    string ExpectedProcessName = "",
    string ReadinessProfile = "process",
    int StartupTimeoutSeconds = 180,
    int ShutdownTimeoutSeconds = 14,
    int LogFollowerReapTimeoutMilliseconds = 2000,
    string? CraneDiagnosticsPath = null);

public sealed record DockerWrapperLaunchOptionsAnnotation(DockerWrapperLaunchOptions Options)
    : Aspire.Hosting.ApplicationModel.IResourceAnnotation;
