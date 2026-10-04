namespace Duck.Testing.AppHost;

public static class DockerServiceReadiness
{
    public static string[] CreateCraneReadinessProbeArguments(string containerId) =>
        CraneProbeDiagnostics.CreateCraneReadinessProbeArguments(containerId);

    public static readonly string Cm4SimListenerScript = """
        port_inodes=$(awk '$2 ~ /:3039$/ { print $10 }' /proc/net/udp /proc/net/udp6 2>/dev/null)
        for pid_dir in /proc/[0-9]*; do
          [ "$(cat "$pid_dir/comm" 2>/dev/null)" = cm4_sim ] || continue
          for fd in "$pid_dir"/fd/*; do
            link=$(readlink "$fd" 2>/dev/null) || continue
            for inode in $port_inodes; do
              [ "$link" = "socket:[$inode]" ] && echo cm4_sim-listening-12345 && exit 0
            done
          done
        done
        exit 1
        """;

    public static bool CraneHasCoordinator(string output) =>
        output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Trim().Equals("/session_controller", StringComparison.Ordinal));

    public static bool CraneProbeSucceeded(int exitCode, string output) =>
        CraneProbeDiagnostics.Evaluate(exitCode, output, string.Empty, TimeSpan.Zero).Ready;

    public static bool Cm4SimOwnsListener(string output) =>
        output.Contains("cm4_sim-listening-12345", StringComparison.Ordinal);
}
