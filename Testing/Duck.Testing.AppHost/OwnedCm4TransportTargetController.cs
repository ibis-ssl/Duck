using System.Diagnostics;
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
    Invalidating,
    Invalidated,
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

internal sealed record Cm4TransportTargetInvalidationResult(
    bool Invalidated,
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

internal interface IOwnedCm4TransportTargetController
{
    Task<Cm4TransportTargetInvalidationResult> InvalidateCurrentTargetAsync(
        string helperLaunchId,
        CancellationToken cancellationToken);

    Task<Cm4TransportTargetApplyResult> ApplyCurrentTargetAsync(
        string helperLaunchId,
        CancellationToken cancellationToken);

    long Invalidate();

    /// <summary>停止と回収が確定した旧CM4から、現在所有中の交換先へ初期適用を許可します。</summary>
    bool TryReinitializeAfterConfirmedCm4Replacement(
        OwnedCm4TransportIdentity previousCm4,
        OwnedCm4TransportIdentity replacementCm4);

    bool IsInvalidatedFor(OwnedCm4TransportIdentityPair identities);

    Cm4TransportTargetSnapshot Snapshot();
}

internal sealed class OwnedCm4TransportTargetController(
    IDockerStdioCommandRunner runner,
    string dockerContext,
    string expectedEngineId,
    string runId,
    Func<OwnedCm4TransportIdentityPair?> getCurrent,
    Func<OwnedCm4TransportIdentity?>? getCurrentSessionIdentity = null) : IOwnedCm4TransportTargetController
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
    private OwnedCm4TransportIdentityPair? pendingIdentities;
    private string? activeHelperLaunchId;
    private string? pendingHelperLaunchId;
    private string? activeTargetAddress;

    public Task<Cm4TransportTargetInvalidationResult> InvalidateCurrentTargetAsync(
        string helperLaunchId,
        CancellationToken cancellationToken)
    {
        var (invalidationEpoch, requested, recordedHelperLaunchId) = BeginRemoteInvalidation();
        if (requested is null || !string.Equals(recordedHelperLaunchId, helperLaunchId, StringComparison.Ordinal) ||
            !IsValidIdentityPair(requested) || !IsValidHelperLaunchId(helperLaunchId))
        {
            MarkUnknown(invalidationEpoch);
            return Task.FromResult(new Cm4TransportTargetInvalidationResult(
                false, true, invalidationEpoch, "The current target identity is unavailable for remote invalidation."));
        }

        OwnedCm4TransportIdentityPair? current;
        try
        {
            current = getCurrent();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            MarkUnknown(invalidationEpoch);
            return Task.FromResult(new Cm4TransportTargetInvalidationResult(
                false, true, invalidationEpoch, "Current owner identity could not be read."));
        }

        if (!Equals(current, requested))
        {
            MarkUnknown(invalidationEpoch);
            return Task.FromResult(new Cm4TransportTargetInvalidationResult(
                false, true, invalidationEpoch, "The owner generation changed before remote invalidation."));
        }

        return InvalidateCoreAsync(requested, helperLaunchId, invalidationEpoch, cancellationToken);
    }

    public Task<Cm4TransportTargetApplyResult> ApplyCurrentTargetAsync(
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

        if (!MarkApplying(operationEpoch, current, helperLaunchId))
        {
            return Task.FromResult(new Cm4TransportTargetApplyResult(
                false, false, operationEpoch, "A newer configuration epoch superseded this request."));
        }

        return ApplyCoreAsync(current, helperLaunchId, operationEpoch, cancellationToken);
    }

    /// <summary>旧 CM4 の停止と実行物回収が確認された後だけ、現在所有中の交換先へ初期適用を許可します。</summary>
    public bool TryReinitializeAfterConfirmedCm4Replacement(
        OwnedCm4TransportIdentity previousCm4,
        OwnedCm4TransportIdentity replacementCm4)
    {
        ArgumentNullException.ThrowIfNull(previousCm4);
        ArgumentNullException.ThrowIfNull(replacementCm4);
        if (!string.Equals(previousCm4.RunId, runId, StringComparison.Ordinal) ||
            !string.Equals(replacementCm4.RunId, runId, StringComparison.Ordinal) ||
            !string.Equals(previousCm4.EngineId, expectedEngineId, StringComparison.Ordinal) ||
            !string.Equals(replacementCm4.EngineId, expectedEngineId, StringComparison.Ordinal) ||
            !string.Equals(previousCm4.ResourceName, Cm4ResourceName, StringComparison.Ordinal) ||
            !string.Equals(replacementCm4.ResourceName, Cm4ResourceName, StringComparison.Ordinal) ||
            string.Equals(previousCm4.ContainerId, replacementCm4.ContainerId, StringComparison.Ordinal) ||
            replacementCm4.Generation <= previousCm4.Generation ||
            !OwnedContainerInspection.IsFullId(previousCm4.ContainerId) ||
            !OwnedContainerInspection.IsFullId(replacementCm4.ContainerId))
        {
            return false;
        }

        lock (stateLock)
        {
            var wasApplied = phase == Cm4TransportTargetPhase.Applied &&
                activeIdentities?.Cm4 is { } appliedCm4 &&
                SamePhysicalIdentity(appliedCm4, previousCm4);
            var wasInvalidated = phase == Cm4TransportTargetPhase.Invalidated &&
                pendingIdentities?.Cm4 is { } invalidatedCm4 &&
                SamePhysicalIdentity(invalidatedCm4, previousCm4);
            if (!wasApplied && !wasInvalidated)
            {
                return false;
            }

            OwnedCm4TransportIdentityPair? current;
            try
            {
                current = getCurrent();
            }
            catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
            {
                return false;
            }

            if (current is null || !IsValidIdentityPair(current) ||
                !Equals(current.Cm4, replacementCm4))
            {
                return false;
            }

            if (wasInvalidated)
            {
                var priorSimulator = pendingIdentities?.Simulator;
                if (priorSimulator is null ||
                    !Equals(current.Simulator, priorSimulator with { Generation = current.Simulator.Generation }))
                {
                    return false;
                }
            }

            epoch = checked(epoch + 1);
            activeIdentities = null;
            activeHelperLaunchId = null;
            activeTargetAddress = null;
            if (wasApplied)
            {
                phase = Cm4TransportTargetPhase.Initial;
                pendingIdentities = null;
                pendingHelperLaunchId = null;
            }
            else
            {
                // Confirmed replacement updates the proof binding without reviving the invalidated route.
                pendingIdentities = current;
            }
            return true;
        }
    }

    public bool IsInvalidatedFor(OwnedCm4TransportIdentityPair identities)
    {
        lock (stateLock)
        {
            return phase == Cm4TransportTargetPhase.Invalidated &&
                pendingIdentities is not null &&
                SamePhysicalIdentity(pendingIdentities.Cm4, identities.Cm4) &&
                SamePhysicalIdentity(pendingIdentities.Simulator, identities.Simulator);
        }
    }
    public Cm4TransportTargetSnapshot Snapshot()
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

    public long Invalidate()
    {
        lock (stateLock)
        {
            epoch = checked(epoch + 1);
            phase = Cm4TransportTargetPhase.Unknown;
            activeIdentities = null;
            pendingIdentities = null;
            activeHelperLaunchId = null;
            pendingHelperLaunchId = null;
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
        var operationTimer = Stopwatch.StartNew();
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

            var engineId = await CaptureMetadataAsync(
                ["--context", dockerContext, "info", "--format", "{{.ID}}"],
                RemainingOperationTime(operationTimer), operationToken).ConfigureAwait(false);
            if (!string.Equals(engineId.Trim(), expectedEngineId, StringComparison.Ordinal) ||
                !IsCurrent(operationEpoch, requested))
            {
                return Rejected(operationEpoch, "The expected engine or current owner generation did not match.");
            }

            var cm4Inspection = await InspectAsync(requested.Cm4, Cm4Role, operationEpoch, requested,
                RemainingOperationTime(operationTimer), operationToken).ConfigureAwait(false);
            if (cm4Inspection is null) return Rejected(operationEpoch, "The owned CM4 inspection did not match.");
            var simulatorInspection = await InspectAsync(
                requested.Simulator, SimulatorRole, operationEpoch, requested,
                RemainingOperationTime(operationTimer), operationToken).ConfigureAwait(false);
            if (simulatorInspection is null ||
                !string.Equals(cm4Inspection.Name, simulatorInspection.Name, StringComparison.Ordinal) ||
                !string.Equals(cm4Inspection.Id, simulatorInspection.Id, StringComparison.Ordinal))
            {
                return Rejected(operationEpoch, "The owned containers do not share one inspected network.");
            }

            var controlCm4 = GetControlIdentity(requested.Cm4);
            if (controlCm4 is null)
            {
                return Rejected(operationEpoch, "The current CM4 helper session does not match the owned container.");
            }

            var request = new Cm4TransportTargetMessage(
                ProtocolVersion,
                "applyTarget",
                runId,
                expectedEngineId,
                requested.Cm4.ResourceId,
                requested.Cm4.ContainerId,
                controlCm4.Generation,
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
            if (!IsControlIdentityCurrent(requested.Cm4, controlCm4) || !IsCurrent(operationEpoch, requested))
            {
                return Rejected(operationEpoch, "The current CM4 helper session or owner proof changed before application.");
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
                RemainingOperationTime(operationTimer),
                operationToken).ConfigureAwait(false);
            if (response.ExitCode != 0 ||
                !TryReadSingleAcknowledgement(response.StandardOutput, request, "applied", out var acknowledgement) ||
                acknowledgement != request with { Type = "applied" } ||
                !IsControlIdentityCurrent(requested.Cm4, controlCm4) ||
                !IsCurrent(operationEpoch, requested))
            {
                return RejectedAfterDispatch(operationEpoch, "The control acknowledgement or current generation did not match.");
            }

            var currentEngineId = await CaptureMetadataAsync(
                ["--context", dockerContext, "info", "--format", "{{.ID}}"],
                RemainingOperationTime(operationTimer), operationToken).ConfigureAwait(false);
            var latestCm4 = await InspectAsync(requested.Cm4, Cm4Role, operationEpoch, requested,
                RemainingOperationTime(operationTimer), operationToken).ConfigureAwait(false);
            var latestSimulator = await InspectAsync(
                requested.Simulator, SimulatorRole, operationEpoch, requested,
                RemainingOperationTime(operationTimer), operationToken).ConfigureAwait(false);
            if (!string.Equals(currentEngineId.Trim(), expectedEngineId, StringComparison.Ordinal) ||
                latestCm4 is null || latestSimulator is null ||
                !string.Equals(latestCm4.Name, cm4Inspection.Name, StringComparison.Ordinal) ||
                !string.Equals(latestCm4.Id, cm4Inspection.Id, StringComparison.Ordinal) ||
                !string.Equals(latestSimulator.Name, simulatorInspection.Name, StringComparison.Ordinal) ||
                !string.Equals(latestSimulator.Id, simulatorInspection.Id, StringComparison.Ordinal) ||
                !latestSimulator.Address.Equals(simulatorInspection.Address) ||
                !IsControlIdentityCurrent(requested.Cm4, controlCm4) ||
                !IsCurrent(operationEpoch, requested))
            {
                return RejectedAfterDispatch(operationEpoch, "The inspected owner generation changed after acknowledgement.");
            }

            lock (stateLock)
            {
                operationToken.ThrowIfCancellationRequested();
                if (epoch != operationEpoch || phase != Cm4TransportTargetPhase.Applying)
                {
                    return RejectedAfterDispatch(operationEpoch, "A newer configuration epoch superseded this acknowledgement.");
                }

                phase = Cm4TransportTargetPhase.Applied;
                activeIdentities = requested;
                pendingIdentities = null;
                activeHelperLaunchId = helperLaunchId;
                pendingHelperLaunchId = null;
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

    private async Task<Cm4TransportTargetInvalidationResult> InvalidateCoreAsync(
        OwnedCm4TransportIdentityPair requested,
        string helperLaunchId,
        long operationEpoch,
        CancellationToken cancellationToken)
    {
        var acquired = false;
        var operationTimer = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OperationLimit);
        var operationToken = deadline.Token;
        try
        {
            await applyGate.WaitAsync(operationToken).ConfigureAwait(false);
            acquired = true;
            if (!IsCurrent(operationEpoch, requested, Cm4TransportTargetPhase.Invalidating))
            {
                return InvalidationUnknown(operationEpoch, "The owner generation changed before remote invalidation.");
            }

            var engineId = await CaptureMetadataAsync(
                ["--context", dockerContext, "info", "--format", "{{.ID}}"],
                RemainingOperationTime(operationTimer), operationToken).ConfigureAwait(false);
            if (!string.Equals(engineId.Trim(), expectedEngineId, StringComparison.Ordinal) ||
                !IsCurrent(operationEpoch, requested, Cm4TransportTargetPhase.Invalidating))
            {
                return InvalidationUnknown(operationEpoch, "The expected engine or current owner generation did not match.");
            }

            var cm4Inspection = await InspectAsync(requested.Cm4, Cm4Role, operationEpoch, requested,
                RemainingOperationTime(operationTimer), operationToken, Cm4TransportTargetPhase.Invalidating)
                .ConfigureAwait(false);
            if (cm4Inspection is null)
            {
                return InvalidationUnknown(operationEpoch, "The owned CM4 inspection did not match.");
            }

            var simulatorInspection = await InspectAsync(
                requested.Simulator, SimulatorRole, operationEpoch, requested,
                RemainingOperationTime(operationTimer), operationToken, Cm4TransportTargetPhase.Invalidating)
                .ConfigureAwait(false);
            if (simulatorInspection is null ||
                !string.Equals(cm4Inspection.Name, simulatorInspection.Name, StringComparison.Ordinal) ||
                !string.Equals(cm4Inspection.Id, simulatorInspection.Id, StringComparison.Ordinal))
            {
                return InvalidationUnknown(operationEpoch, "The owned containers do not share one inspected network.");
            }

            var controlCm4 = GetControlIdentity(requested.Cm4);
            if (controlCm4 is null)
            {
                return InvalidationUnknown(operationEpoch, "The current CM4 helper session does not match the owned container.");
            }

            var request = new Cm4TransportTargetMessage(
                ProtocolVersion,
                "invalidateTarget",
                runId,
                expectedEngineId,
                requested.Cm4.ResourceId,
                requested.Cm4.ContainerId,
                controlCm4.Generation,
                helperLaunchId,
                requested.Simulator.ResourceId,
                requested.Simulator.ContainerId,
                requested.Simulator.Generation,
                operationEpoch,
                "0.0.0.0");
            var requestLine = JsonSerializer.Serialize(request, JsonOptions);
            if (Encoding.UTF8.GetByteCount(requestLine) + 1 > MaximumMessageBytes)
            {
                return InvalidationUnknown(operationEpoch, "The remote invalidation request exceeded its size limit.");
            }
            if (!IsControlIdentityCurrent(requested.Cm4, controlCm4) ||
                !IsCurrent(operationEpoch, requested, Cm4TransportTargetPhase.Invalidating))
            {
                return InvalidationUnknown(operationEpoch, "The current CM4 helper session or owner proof changed before invalidation.");
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
                RemainingOperationTime(operationTimer),
                operationToken).ConfigureAwait(false);
            if (response.ExitCode != 0 ||
                !TryReadSingleAcknowledgement(response.StandardOutput, request, "invalidated", out var acknowledgement) ||
                acknowledgement != request with { Type = "invalidated" } ||
                !IsControlIdentityCurrent(requested.Cm4, controlCm4) ||
                !IsCurrent(operationEpoch, requested, Cm4TransportTargetPhase.Invalidating))
            {
                return InvalidationUnknown(operationEpoch,
                    "The remote invalidation acknowledgement or current generation did not match.");
            }

            var currentEngineId = await CaptureMetadataAsync(
                ["--context", dockerContext, "info", "--format", "{{.ID}}"],
                RemainingOperationTime(operationTimer), operationToken).ConfigureAwait(false);
            var latestCm4 = await InspectAsync(requested.Cm4, Cm4Role, operationEpoch, requested,
                RemainingOperationTime(operationTimer), operationToken, Cm4TransportTargetPhase.Invalidating)
                .ConfigureAwait(false);
            var latestSimulator = await InspectAsync(
                requested.Simulator, SimulatorRole, operationEpoch, requested,
                RemainingOperationTime(operationTimer), operationToken, Cm4TransportTargetPhase.Invalidating)
                .ConfigureAwait(false);
            if (!string.Equals(currentEngineId.Trim(), expectedEngineId, StringComparison.Ordinal) ||
                latestCm4 is null || latestSimulator is null ||
                !string.Equals(latestCm4.Name, cm4Inspection.Name, StringComparison.Ordinal) ||
                !string.Equals(latestCm4.Id, cm4Inspection.Id, StringComparison.Ordinal) ||
                !string.Equals(latestSimulator.Name, simulatorInspection.Name, StringComparison.Ordinal) ||
                !string.Equals(latestSimulator.Id, simulatorInspection.Id, StringComparison.Ordinal) ||
                !latestSimulator.Address.Equals(simulatorInspection.Address) ||
                !IsControlIdentityCurrent(requested.Cm4, controlCm4) ||
                !IsCurrent(operationEpoch, requested, Cm4TransportTargetPhase.Invalidating))
            {
                return InvalidationUnknown(operationEpoch,
                    "The inspected owner generation changed after invalidation acknowledgement.");
            }

            lock (stateLock)
            {
                if (operationToken.IsCancellationRequested || epoch != operationEpoch ||
                    phase != Cm4TransportTargetPhase.Invalidating)
                {
                    return InvalidationUnknown(operationEpoch,
                        "A newer operation or cancellation superseded the invalidation acknowledgement.");
                }

                phase = Cm4TransportTargetPhase.Invalidated;
                activeIdentities = null;
                activeHelperLaunchId = null;
                pendingIdentities = requested;
                pendingHelperLaunchId = helperLaunchId;
                activeTargetAddress = null;
            }

            return new Cm4TransportTargetInvalidationResult(true, false, operationEpoch, null);
        }
        catch (OperationCanceledException)
        {
            MarkUnknown(operationEpoch);
            return new Cm4TransportTargetInvalidationResult(
                false, true, operationEpoch, "The remote invalidation was cancelled or its deadline expired.");
        }
        catch (TimeoutException)
        {
            MarkUnknown(operationEpoch);
            return new Cm4TransportTargetInvalidationResult(
                false, true, operationEpoch, "The remote invalidation deadline expired.");
        }
        catch (Exception exception) when (
            exception is IOException or
            InvalidOperationException or
            JsonException or
            ArgumentException)
        {
            MarkUnknown(operationEpoch);
            return new Cm4TransportTargetInvalidationResult(
                false, true, operationEpoch, "The remote invalidation result is unknown.");
        }
        finally
        {
            if (acquired) applyGate.Release();
        }
    }

    private Cm4TransportTargetInvalidationResult InvalidationUnknown(long operationEpoch, string error)
    {
        MarkUnknown(operationEpoch);
        return new Cm4TransportTargetInvalidationResult(false, true, operationEpoch, error);
    }

    private async Task<Cm4TransportNetworkEndpoint?> InspectAsync(
        OwnedCm4TransportIdentity identity,
        string role,
        long operationEpoch,
        OwnedCm4TransportIdentityPair requested,
        TimeSpan remaining,
        CancellationToken cancellationToken,
        Cm4TransportTargetPhase expectedPhase = Cm4TransportTargetPhase.Applying)
    {
        if (!OwnedContainerInspection.IsFullId(identity.ContainerId) ||
            !IsCurrent(operationEpoch, requested, expectedPhase))
        {
            return null;
        }

        var json = await CaptureMetadataAsync(
            ["--context", dockerContext, "inspect", "--format", SafeInspectFormat, identity.ContainerId],
            remaining, cancellationToken).ConfigureAwait(false);
        if (!IsCurrent(operationEpoch, requested, expectedPhase) ||
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
            pendingIdentities = null;
            activeHelperLaunchId = null;
            pendingHelperLaunchId = null;
            activeTargetAddress = null;
            return epoch;
        }
    }

    private bool MarkApplying(
        long expectedEpoch,
        OwnedCm4TransportIdentityPair requested,
        string helperLaunchId)
    {
        lock (stateLock)
        {
            if (epoch != expectedEpoch || phase != Cm4TransportTargetPhase.Unknown) return false;
            phase = Cm4TransportTargetPhase.Applying;
            pendingIdentities = requested;
            pendingHelperLaunchId = helperLaunchId;
            return true;
        }
    }

    private (long Epoch, OwnedCm4TransportIdentityPair? Identities, string? HelperLaunchId)
        BeginRemoteInvalidation()
    {
        lock (stateLock)
        {
            var identities = phase == Cm4TransportTargetPhase.Applied
                ? activeIdentities
                : phase == Cm4TransportTargetPhase.Applying ? pendingIdentities : null;
            var helperLaunchId = phase == Cm4TransportTargetPhase.Applied
                ? activeHelperLaunchId
                : phase == Cm4TransportTargetPhase.Applying ? pendingHelperLaunchId : null;
            epoch = checked(epoch + 1);
            phase = Cm4TransportTargetPhase.Invalidating;
            activeIdentities = null;
            pendingIdentities = identities;
            activeHelperLaunchId = null;
            pendingHelperLaunchId = helperLaunchId;
            activeTargetAddress = null;
            return (epoch, identities, helperLaunchId);
        }
    }

    private bool IsCurrent(
        long expectedEpoch,
        OwnedCm4TransportIdentityPair requested,
        Cm4TransportTargetPhase expectedPhase = Cm4TransportTargetPhase.Applying)
    {
        var current = getCurrent();
        lock (stateLock)
        {
            return epoch == expectedEpoch &&
                phase == expectedPhase &&
                Equals(current, requested);
        }
    }

    private bool IsControlIdentityCurrent(
        OwnedCm4TransportIdentity current,
        OwnedCm4TransportIdentity controlIdentity)
    {
        var latest = GetControlIdentity(current);
        return latest is not null && SamePhysicalIdentity(latest, controlIdentity) &&
            latest.Generation == controlIdentity.Generation;
    }

    private OwnedCm4TransportIdentity? GetControlIdentity(OwnedCm4TransportIdentity current)
    {
        if (getCurrentSessionIdentity is null)
        {
            return current;
        }

        OwnedCm4TransportIdentity? launched;
        try
        {
            launched = getCurrentSessionIdentity();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }

        return launched is not null && SamePhysicalIdentity(launched, current) &&
            launched.Generation > 0 && launched.Generation <= current.Generation
            ? current with { Generation = launched.Generation }
            : null;
    }

    private static bool SamePhysicalIdentity(OwnedCm4TransportIdentity left, OwnedCm4TransportIdentity right) =>
        left.RunId == right.RunId && left.EngineId == right.EngineId &&
        left.ResourceName == right.ResourceName && left.ResourceId == right.ResourceId &&
        left.ContainerId == right.ContainerId;

    private void MarkUnknown(long expectedEpoch)
    {
        lock (stateLock)
        {
            if (epoch != expectedEpoch) return;
            phase = Cm4TransportTargetPhase.Unknown;
            activeIdentities = null;
            pendingIdentities = null;
            activeHelperLaunchId = null;
            pendingHelperLaunchId = null;
            activeTargetAddress = null;
        }
    }

    private Cm4TransportTargetApplyResult Rejected(long operationEpoch, string error)
    {
        MarkUnknown(operationEpoch);
        return new Cm4TransportTargetApplyResult(false, false, operationEpoch, error);
    }

    private Cm4TransportTargetApplyResult RejectedAfterDispatch(long operationEpoch, string error)
    {
        MarkUnknown(operationEpoch);
        return new Cm4TransportTargetApplyResult(false, true, operationEpoch, error);
    }

    private async Task<string> CaptureMetadataAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var result = await runner.CaptureBoundedAsync(
            arguments, MaximumMessageBytes, timeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new IOException("Docker metadata command failed.");
        }

        return result.StandardOutput;
    }

    private static TimeSpan RemainingOperationTime(Stopwatch timer)
    {
        var remaining = OperationLimit - timer.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException("The target application deadline expired.");
        }

        return remaining;
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
        string expectedType,
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
            acknowledgement.Type == expectedType &&
            acknowledgement == request with { Type = expectedType };
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
