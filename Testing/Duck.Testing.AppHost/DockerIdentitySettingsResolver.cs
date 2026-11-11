using System.ComponentModel;

namespace Duck.Testing.AppHost;

internal sealed record DockerIdentitySettings(string ContextName, string ExpectedEngineId);

/// <summary>Resolves the active Docker context and verifies its Engine identity without changing Docker configuration.</summary>
internal static class DockerIdentitySettingsResolver
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

    internal static string ResolveExecutable(string? configuredExecutable)
    {
        if (!string.IsNullOrWhiteSpace(configuredExecutable))
            return configuredExecutable;

        var desktopExecutable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "DockerDesktop", "resources", "bin", "docker.exe");
        return OperatingSystem.IsWindows() && File.Exists(desktopExecutable)
            ? desktopExecutable
            : "docker";
    }

    internal static async Task<DockerIdentitySettings> ResolveAsync(
        string? configuredContext,
        string? configuredEngineId,
        Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<DockerStdioCommandResult>> capture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);

        var context = configuredContext?.Trim();
        if (string.IsNullOrWhiteSpace(context))
        {
            var contextResult = await CaptureAsync(
                capture,
                ["context", "show"],
                "Could not determine the active Docker context. Ensure the Docker CLI is available and run `docker context show` successfully.",
                cancellationToken).ConfigureAwait(false);
            context = ReadSingleValue(contextResult, "active Docker context", "docker context show");
        }

        var engineResult = await CaptureAsync(
            capture,
            ["--context", context, "info", "--format", "{{.ID}}"],
            $"Could not read Docker Engine identity for context '{context}'. Ensure Docker Desktop is running and `docker --context {context} info` succeeds.",
            cancellationToken).ConfigureAwait(false);
        var actualEngineId = ReadSingleValue(engineResult, "Docker Engine ID", $"docker --context {context} info");

        var expectedEngineId = configuredEngineId?.Trim();
        if (!string.IsNullOrWhiteSpace(expectedEngineId) &&
            !string.Equals(expectedEngineId, actualEngineId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The configured expected Docker Engine ID does not match the Engine selected by context '{context}'. Correct Testing:IdentityControl:DockerContext or Testing:IdentityControl:ExpectedEngineId.");
        }

        return new(context, string.IsNullOrWhiteSpace(expectedEngineId) ? actualEngineId : expectedEngineId);
    }

    private static async Task<DockerStdioCommandResult> CaptureAsync(
        Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<DockerStdioCommandResult>> capture,
        IReadOnlyList<string> arguments,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        DockerStdioCommandResult result;
        try
        {
            result = await capture(arguments, CommandTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or TimeoutException or OperationCanceledException or Win32Exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{failureMessage} Docker CLI exited with code {result.ExitCode}.");

        return result;
    }

    private static string ReadSingleValue(DockerStdioCommandResult result, string description, string command)
    {
        var value = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\n') || value.Contains('\r'))
            throw new InvalidOperationException($"Docker CLI returned no single {description} for `{command}`.");

        return value;
    }
}
