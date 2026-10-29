using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duck.Testing.AppHost;

internal sealed record OwnedCm4TransportIdentity(
    string RunId,
    string EngineId,
    string ResourceName,
    string ResourceId,
    string ContainerId,
    long Generation);

internal sealed record OwnedCm4TransportIdentityPair(
    OwnedCm4TransportIdentity Cm4,
    OwnedCm4TransportIdentity Simulator);

internal enum Cm4TransportTargetPhase
{
    Initial,
    Applying,
    Applied,
    Unknown,
}

internal sealed record Cm4TransportTargetSnapshot(
    long Epoch,
    Cm4TransportTargetPhase Phase,
    string? Cm4ContainerId,
    string? SimulatorContainerId,
    string? TargetAddress);

internal sealed record Cm4TransportTargetApplyResult(
    bool Applied,
    bool Unknown,
    long Epoch,
    string? Error);

internal sealed record Cm4TransportTargetMessage(
    int Version,
    string Type,
    string RunId,
    string EngineId,
    string Cm4ResourceId,
    string Cm4ContainerId,
    long Cm4Generation,
    string HelperLaunchId,
    string SimulatorResourceId,
    string SimulatorContainerId,
    long SimulatorGeneration,
    long ConfigEpoch,
    string TargetAddress);

internal sealed record Cm4TransportNetworkEndpoint(string Name, string Id, IPAddress Address);

internal sealed class OwnedCm4TransportTargetController(
    IDockerStdioCommandRunner runner,
    string dockerContext,
    string expectedEngineId,
    string runId,
    Func<OwnedCm4TransportIdentityPair?> getCurrent)
{
    private const string Cm4ResourceName = "cm4-sim";
    private const string SimulatorResourceName = "simulator";
    private const string Cm4Role = "cm4-sim";
    private const string SimulatorRole = "simulator";
    private const string HelperPath = "/opt/duck/cm4-transport-control";
    private const string SafeInspectFormat =
        "{{.Id}}\n{{.State.Status}}\n{{.HostConfig.NetworkMode}}\n" +
        "{{index .Config.Labels \"com.ibis-ssl.duck.run-id\"}}\n" +
        "{{index .Config.Labels \"com.ibis-ssl.duck.role\"}}\n" +
        "{{range $name, $network := .NetworkSettings.Networks}}{{printf \"%s\\t%s\\t%s\\n\" $name $network.NetworkID $network.IPAddress}}{{end}}";
    private const int ProtocolVersion = 1;
    private const int MaximumMessageBytes = 4096;
    private static readonly TimeSpan OperationLimit = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly object stateLock = new();
    private readonly SemaphoreSlim applyGate = new(1, 1);
    private long epoch;
    private Cm4TransportTargetPhase phase = Cm4TransportTargetPhase.Initial;
    private OwnedCm4TransportIdentityPair? activeIdentities;
    private string? activeTargetAddress;

    internal Task<Cm4TransportTargetApplyResult> ApplyCurrentTargetAsync(
        string helperLaunchId,
        CancellationToken cancellationToken)
    {
        var operationEpoch = BeginApply();
        OwnedCm4TransportIdentityPair? current;
        try
        {
            current = getCurrent();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            MarkUnknown(operationEpoch);
            return Task.FromResult(new Cm4TransportTargetApplyResult(
                false, true, operationEpoch, "Current owner identity could not be read."));
        }

        if (current is null || !IsValidIdentityPair(current) || !IsValidHelperLaunchId(helperLaunchId))
        {
            MarkUnknown(operationEpoch);
            return Task.FromResult(new Cm4TransportTargetApplyResult(false, false, operationEpoch, "Current owner identity is invalid."));
        }

        if (!MarkApplying(operationEpoch))
        {
            return Task.FromResult(new Cm4TransportTargetApplyResult(
                false, false, operationEpoch, "A newer configuration epoch superseded this request."));
        }

        return ApplyCoreAsync(current, helperLaunchId, operationEpoch, cancellationToken);
    }

    internal Cm4TransportTargetSnapshot Snapshot()
    {
        lock (stateLock)
        {
            return new(
                epoch,
                phase,
                activeIdentities?.Cm4.ContainerId,
                activeIdentities?.Simulator.ContainerId,
                activeTargetAddress);
        }
    }

    internal long Invalidate()
    {
        lock (stateLock)
        {
            epoch = checked(epoch + 1);
            phase = Cm4TransportTargetPhase.Unknown;
            activeIdentities = null;
            activeTargetAddress = null;
            return epoch;
        }
    }

    private async Task<Cm4TransportTargetApplyResult> ApplyCoreAsync(
        OwnedCm4TransportIdentityPair requested,
        string helperLaunchId,
        long operationEpoch,
        CancellationToken cancellationToken)
    {
        var acquired = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OperationLimit);
        var operationToken = deadline.Token;
        try
        {
            await applyGate.WaitAsync(operationToken).ConfigureAwait(false);
            acquired = true;
            if (!IsCurrent(operationEpoch, requested))
            {
                return Rejected(operationEpoch, "The owner generation changed before application.");
            }

            var engineId = await runner.CaptureAsync(
                ["--context", dockerContext, "info", "--format", "{{.ID}}"],
                operationToken).ConfigureAwait(false);
            if (!string.Equals(engineId.Trim(), expectedEngineId, StringComparison.Ordinal) ||
                !IsCurrent(operationEpoch, requested))
            {
                return Rejected(operationEpoch, "The expected engine or current owner generation did not match.");
            }

            var cm4Inspection = await InspectAsync(requested.Cm4, Cm4Role, operationEpoch, requested, operationToken).ConfigureAwait(false);
            if (cm4Inspection is null) return Rejected(operationEpoch, "The owned CM4 inspection did not match.");
            var simulatorInspection = await InspectAsync(
                requested.Simulator, SimulatorRole, operationEpoch, requested, operationToken).ConfigureAwait(false);
            if (simulatorInspection is null ||
                !string.Equals(cm4Inspection.Name, simulatorInspection.Name, StringComparison.Ordinal) ||
                !string.Equals(cm4Inspection.Id, simulatorInspection.Id, StringComparison.Ordinal))
            {
                return Rejected(operationEpoch, "The owned containers do not share one inspected network.");
            }

            var request = new Cm4TransportTargetMessage(
                ProtocolVersion,
                "applyTarget",
                runId,
                expectedEngineId,
                requested.Cm4.ResourceId,
                requested.Cm4.ContainerId,
                requested.Cm4.Generation,
                helperLaunchId,
                requested.Simulator.ResourceId,
                requested.Simulator.ContainerId,
                requested.Simulator.Generation,
                operationEpoch,
                simulatorInspection.Address.ToString());
            var requestLine = JsonSerializer.Serialize(request, JsonOptions);
            if (Encoding.UTF8.GetByteCount(requestLine) + 1 > MaximumMessageBytes)
            {
                return Rejected(operationEpoch, "The control request exceeded its size limit.");
            }

            var execArguments = new[]
            {
                "--context", dockerContext, "exec", "-i", requested.Cm4.ContainerId,
                HelperPath, "--protocol", "jsonl-v1",
            };
            var response = await runner.ExecuteWithStdioAsync(
                execArguments,
                requestLine + "\n",
                MaximumMessageBytes,
                OperationLimit,
                operationToken).ConfigureAwait(false);
            if (response.ExitCode != 0 ||
                !TryReadSingleAcknowledgement(response.StandardOutput, request, out var acknowledgement) ||
                acknowledgement != request with { Type = "applied" } ||
                !IsCurrent(operationEpoch, requested))
            {
                return Rejected(operationEpoch, "The control acknowledgement or current generation did not match.");
            }

            var currentEngineId = await runner.CaptureAsync(
                ["--context", dockerContext, "info", "--format", "{{.ID}}"],
                operationToken).ConfigureAwait(false);
            var latestCm4 = await InspectAsync(requested.Cm4, Cm4Role, operationEpoch, requested, operationToken).ConfigureAwait(false);
            var latestSimulator = await InspectAsync(
                requested.Simulator, SimulatorRole, operationEpoch, requested, operationToken).ConfigureAwait(false);
            if (!string.Equals(currentEngineId.Trim(), expectedEngineId, StringComparison.Ordinal) ||
                latestCm4 is null || latestSimulator is null ||
                !string.Equals(latestCm4.Name, cm4Inspection.Name, StringComparison.Ordinal) ||
                !string.Equals(latestCm4.Id, cm4Inspection.Id, StringComparison.Ordinal) ||
                !string.Equals(latestSimulator.Name, simulatorInspection.Name, StringComparison.Ordinal) ||
                !string.Equals(latestSimulator.Id, simulatorInspection.Id, StringComparison.Ordinal) ||
                !latestSimulator.Address.Equals(simulatorInspection.Address) ||
                !IsCurrent(operationEpoch, requested))
            {
                return Rejected(operationEpoch, "The inspected owner generation changed after acknowledgement.");
            }

            lock (stateLock)
            {
                if (epoch != operationEpoch || phase != Cm4TransportTargetPhase.Applying)
                {
                    return Rejected(operationEpoch, "A newer configuration epoch superseded this acknowledgement.");
                }

                phase = Cm4TransportTargetPhase.Applied;
                activeIdentities = requested;
                activeTargetAddress = simulatorInspection.Address.ToString();
            }

            return new Cm4TransportTargetApplyResult(true, false, operationEpoch, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkUnknown(operationEpoch);
            throw;
        }
        catch (OperationCanceledException)
        {
            MarkUnknown(operationEpoch);
            return new Cm4TransportTargetApplyResult(false, true, operationEpoch, "The target application deadline expired.");
        }
        catch (TimeoutException)
        {
            MarkUnknown(operationEpoch);
            return new Cm4TransportTargetApplyResult(false, true, operationEpoch, "The target application deadline expired.");
        }
        catch (Exception exception) when (
            exception is IOException or
            InvalidOperationException or
            JsonException or
            ArgumentException)
        {
            MarkUnknown(operationEpoch);
            return new Cm4TransportTargetApplyResult(false, true, operationEpoch, "The target application result is unknown.");
        }
        finally
        {
            if (acquired) applyGate.Release();
        }
    }

    private async Task<Cm4TransportNetworkEndpoint?> InspectAsync(
        OwnedCm4TransportIdentity identity,
        string role,
        long operationEpoch,
        OwnedCm4TransportIdentityPair requested,
        CancellationToken cancellationToken)
    {
        if (!OwnedContainerInspection.IsFullId(identity.ContainerId) ||
            !IsCurrent(operationEpoch, requested))
        {
            return null;
        }

        var json = await runner.CaptureAsync(
            ["--context", dockerContext, "inspect", "--format", SafeInspectFormat, identity.ContainerId],
            cancellationToken).ConfigureAwait(false);
        if (!IsCurrent(operationEpoch, requested) ||
            !TryReadOwnedSummary(json, identity, role, out var endpoint))
        {
            return null;
        }

        return endpoint;
    }

    private long BeginApply()
    {
        lock (stateLock)
        {
            epoch = checked(epoch + 1);
            phase = Cm4TransportTargetPhase.Unknown;
            activeIdentities = null;
            activeTargetAddress = null;
            return epoch;
        }
    }

    private bool MarkApplying(long expectedEpoch)
    {
        lock (stateLock)
        {
            if (epoch != expectedEpoch || phase != Cm4TransportTargetPhase.Unknown) return false;
            phase = Cm4TransportTargetPhase.Applying;
            return true;
        }
    }

    private bool IsCurrent(long expectedEpoch, OwnedCm4TransportIdentityPair requested)
    {
        var current = getCurrent();
        lock (stateLock)
        {
            return epoch == expectedEpoch &&
                phase == Cm4TransportTargetPhase.Applying &&
                Equals(current, requested);
        }
    }

    private void MarkUnknown(long expectedEpoch)
    {
        lock (stateLock)
        {
            if (epoch != expectedEpoch) return;
            phase = Cm4TransportTargetPhase.Unknown;
            activeIdentities = null;
            activeTargetAddress = null;
        }
    }

    private Cm4TransportTargetApplyResult Rejected(long operationEpoch, string error)
    {
        MarkUnknown(operationEpoch);
        return new Cm4TransportTargetApplyResult(false, false, operationEpoch, error);
    }

    private bool IsValidIdentityPair(OwnedCm4TransportIdentityPair pair) =>
        IsValidIdentity(pair.Cm4, Cm4ResourceName, Cm4Role) &&
        IsValidIdentity(pair.Simulator, SimulatorResourceName, SimulatorRole) &&
        pair.Cm4.ContainerId != pair.Simulator.ContainerId &&
        pair.Cm4.ResourceId != pair.Simulator.ResourceId;

    private bool IsValidIdentity(OwnedCm4TransportIdentity identity, string resourceName, string role) =>
        string.Equals(identity.RunId, runId, StringComparison.Ordinal) &&
        string.Equals(identity.EngineId, expectedEngineId, StringComparison.Ordinal) &&
        string.Equals(identity.ResourceName, resourceName, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(identity.ResourceId) &&
        string.Equals(role, resourceName, StringComparison.Ordinal) &&
        OwnedContainerInspection.IsFullId(identity.ContainerId) &&
        identity.Generation > 0;

    private static bool IsValidHelperLaunchId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')) return false;
        }

        return true;
    }

    private static bool TryReadSingleAcknowledgement(
        string output,
        Cm4TransportTargetMessage request,
        out Cm4TransportTargetMessage? acknowledgement)
    {
        acknowledgement = null;
        if (Encoding.UTF8.GetByteCount(output) > MaximumMessageBytes) return false;
        var line = output.EndsWith("\n", StringComparison.Ordinal) ? output[..^1] : output;
        if (line.EndsWith("\r", StringComparison.Ordinal)) line = line[..^1];
        if (line.Length == 0 || line.Contains('\n') || line.Contains('\r')) return false;

        using var document = JsonDocument.Parse(line);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            document.RootElement.EnumerateObject().Count() != 13)
        {
            return false;
        }

        acknowledgement = JsonSerializer.Deserialize<Cm4TransportTargetMessage>(line, JsonOptions);
        return acknowledgement is not null &&
            acknowledgement.Version == ProtocolVersion &&
            acknowledgement.Type == "applied" &&
            acknowledgement == request with { Type = "applied" };
    }

    private bool TryReadOwnedSummary(
        string output,
        OwnedCm4TransportIdentity identity,
        string expectedRole,
        out Cm4TransportNetworkEndpoint endpoint)
    {
        endpoint = default!;
        try
        {
            if (output.Length > MaximumMessageBytes) return false;
            var lines = output.Split('\n');
            if (lines.Length > 1 && lines[^1].Length == 0) lines = lines[..^1];
            if (lines.Length != 6 ||
                !string.Equals(lines[0], identity.ContainerId, StringComparison.Ordinal) ||
                !string.Equals(lines[1], "running", StringComparison.Ordinal) ||
                !string.Equals(lines[3], runId, StringComparison.Ordinal) ||
                !string.Equals(lines[4], expectedRole, StringComparison.Ordinal))
            {
                return false;
            }

            var fields = lines[5].Split('\t');
            if (fields.Length != 3 ||
                string.IsNullOrWhiteSpace(lines[2]) ||
                lines[2] is "host" or "none" ||
                lines[2].StartsWith("container:", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(fields[0]) ||
                !OwnedContainerInspection.IsFullId(fields[1]) ||
                !IPAddress.TryParse(fields[2], out var address) ||
                address.AddressFamily != AddressFamily.InterNetwork ||
                IPAddress.IsLoopback(address) ||
                address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.Broadcast) ||
                address.GetAddressBytes()[0] == 0 ||
                address.GetAddressBytes()[0] >= 224 ||
                address.GetAddressBytes()[0] == 169 && address.GetAddressBytes()[1] == 254)
            {
                return false;
            }

            if (!(lines[2] == "default" ||
                  string.Equals(lines[2], fields[0], StringComparison.Ordinal) ||
                  string.Equals(lines[2], fields[1], StringComparison.Ordinal)))
            {
                return false;
            }

            endpoint = new Cm4TransportNetworkEndpoint(fields[0], fields[1], address);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
