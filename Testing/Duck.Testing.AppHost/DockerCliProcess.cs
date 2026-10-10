using System.Diagnostics;

namespace Duck.Testing.AppHost;

/// <summary>Builds host Docker CLI processes without passing arguments through a shell.</summary>
public static class DockerCliProcess
{
    public static ProcessStartInfo CreateStartInfo(string executable, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
