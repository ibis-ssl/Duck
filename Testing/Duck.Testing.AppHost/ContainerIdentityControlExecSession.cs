using System.Text.Json;
using Duck.Testing.Diagnostics;
using Tracker.RuntimeHost;

namespace Duck.Testing.AppHost;

internal sealed record ContainerIdentityControlExecResult(bool Accepted, string? BootId, bool RequiresNewGeneration);

internal static class ContainerIdentityControlExecSession
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>短命なコンテナ内プロセスへprepare/challenge/commitを順に送ります。</summary>
    internal static async Task<ContainerIdentityControlExecResult> RunAsync(
        TextReader remoteOutput,
        TextWriter remoteInput,
        ResourceDiagnosticIdentity identity,
        string roleName,
        string? expectedBootId,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(remoteOutput);
        ArgumentNullException.ThrowIfNull(remoteInput);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        var prepare = new RuntimeIdentityControlMessage
        {
            Type = "prepare",
            RunId = identity.RunId,
            ResourceId = identity.ResourceId,
            RoleName = roleName,
            ContainerId = identity.ContainerId,
            Generation = identity.Generation,
        };
        await WriteMessageAsync(remoteInput, prepare, cancellationToken, timeout);

        var challenge = await ReadMessageAsync(remoteOutput, cancellationToken, timeout);
        if (challenge?.Type != "challenge" || string.IsNullOrWhiteSpace(challenge.BootId))
            return new(false, null, false);
        if (expectedBootId is not null && !string.Equals(expectedBootId, challenge.BootId, StringComparison.Ordinal))
            return new(false, challenge.BootId, true);

        var commit = new RuntimeIdentityControlMessage
        {
            Type = "commit",
            ExpectedBootId = challenge.BootId,
            RunId = identity.RunId,
            ResourceId = identity.ResourceId,
            RoleName = roleName,
            ContainerId = identity.ContainerId,
            Generation = identity.Generation,
        };
        await WriteMessageAsync(remoteInput, commit, cancellationToken, timeout);

        var result = await ReadMessageAsync(remoteOutput, cancellationToken, timeout);
        return new(result?.Type == "result" && result.Accepted, challenge.BootId, false);
    }

    private static Task WriteMessageAsync(
        TextWriter writer,
        RuntimeIdentityControlMessage message,
        CancellationToken cancellationToken,
        TimeSpan timeout) => RuntimeIdentityControlRelay.RunWithDeadlineAsync(
            async token =>
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(message, JsonOptions)).WaitAsync(token);
                await writer.FlushAsync(token);
            },
            cancellationToken,
            timeout);

    private static async Task<RuntimeIdentityControlMessage?> ReadMessageAsync(
        TextReader reader,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        var line = await RuntimeIdentityControlRelay.RunWithDeadlineAsync(
            token => RuntimeIdentityControlFraming.ReadLineAsync(reader, token, timeout),
            cancellationToken,
            timeout);
        return string.IsNullOrWhiteSpace(line)
            ? null
            : JsonSerializer.Deserialize<RuntimeIdentityControlMessage>(line, JsonOptions);
    }
}
