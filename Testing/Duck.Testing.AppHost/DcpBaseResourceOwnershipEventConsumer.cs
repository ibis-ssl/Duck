using System.Diagnostics;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;

namespace Duck.Testing.AppHost;

internal enum DcpBaseResourceOwnershipDisposition
{
    MissingSettings,
    CandidatesUnavailable,
    EngineRejected,
    ContainerRejected,
    NetworkObservationRejected,
    CandidateChanged,
    DeadlineExceeded,
    InspectionFailed,
    OwnershipVerifiedServiceReadinessUnknown,
}

internal sealed record DcpBaseResourceOwnershipResult(
    DcpBaseResourceOwnershipDisposition Disposition,
    string Reason,
    string? ResourceName,
    string? ObservedNetworkName,
    int InspectCount);

/// <summary>DCP の現在候補を Docker Engine と照合し、同じ世代が続いている場合だけ所有確認を記録します。</summary>
internal sealed class DcpBaseResourceOwnershipEventConsumer(
    DcpBaseResourceReadinessRegistry registry,
    IDockerStdioCommandRunner runner,
    string? dockerContextName,
    string? expectedEngineId)
{
    private static readonly TimeSpan VerificationBudget = TimeSpan.FromSeconds(3);
    private const int MaximumEngineOutputCharacters = 1024;
    private const int MaximumInspectOutputCharacters = 64 * 1024;

    internal async Task<DcpBaseResourceOwnershipResult> VerifyCurrentAsync(
        Func<string, ResourceEvent?> getCurrentByResourceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(getCurrentByResourceId);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(dockerContextName) || string.IsNullOrWhiteSpace(expectedEngineId))
        {
            return Result(DcpBaseResourceOwnershipDisposition.MissingSettings, "Docker context or expected engine ID is not configured.");
        }

        var candidates = registry.GetCandidates()
            .Where(candidate => !candidate.ResourceKeyAmbiguous &&
                !string.IsNullOrWhiteSpace(candidate.ResourceId) &&
                !string.IsNullOrWhiteSpace(candidate.StackId) &&
                candidate.State == "Running" &&
                OwnedContainerInspection.IsFullId(candidate.ContainerId))
            .ToArray();
        if (candidates.Length == 0)
        {
            return Result(DcpBaseResourceOwnershipDisposition.CandidatesUnavailable, "No current unambiguous running full-ID standard DCP candidate is available for inspection.");
        }
        if (candidates.All(candidate => candidate.IdentityVerified && candidate.NetworkVerified && candidate.OwnershipVerified))
        {
            var changedCandidate = candidates.FirstOrDefault(candidate =>
                !IsCurrentCandidate(candidate, getCurrentByResourceId));
            if (changedCandidate is not null)
            {
                foreach (var candidate in candidates)
                {
                    registry.InvalidateOwnershipProof(
                        candidate,
                        "The current DCP notification no longer matches cached ownership evidence.");
                }

                return Result(
                    DcpBaseResourceOwnershipDisposition.CandidateChanged,
                    "The current resource key, state, or full container ID no longer matches cached ownership evidence.",
                    changedCandidate.ResourceName);
            }
            if (!registry.AreOwnershipProofsCurrent(candidates))
            {
                return Result(
                    DcpBaseResourceOwnershipDisposition.CandidateChanged,
                    "The ownership candidate generation changed while cached evidence was being revalidated.");
            }

            var networkName = candidates.Select(candidate => candidate.ObservedNetworkName)
                .Distinct(StringComparer.Ordinal).Count() == 1
                ? candidates[0].ObservedNetworkName
                : null;
            return Result(
                DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown,
                "The current standard DCP container identities and common default-network membership remain verified; service readiness is unverified.",
                observedNetworkName: networkName);
        }

        var timer = Stopwatch.StartNew();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(VerificationBudget);
        var inspectCount = 0;
        try
        {
            var engineResult = await runner.CaptureBoundedAsync(
                ["--context", dockerContextName, "info", "--format", "{{.ID}}"],
                MaximumEngineOutputCharacters,
                RemainingBudget(timer),
                budget.Token).ConfigureAwait(false);
            if (engineResult.ExitCode != 0 ||
                !string.Equals(engineResult.StandardOutput.Trim(), expectedEngineId, StringComparison.Ordinal))
            {
                return Result(DcpBaseResourceOwnershipDisposition.EngineRejected, "Docker Engine did not match the configured engine ID.");
            }

            var proofs = new List<DcpBaseResourceOwnershipProof>(candidates.Length);
            string? sharedNetworkName = null;
            string? sharedNetworkId = null;
            var networkShapeIsShared = true;
            foreach (var candidate in candidates)
            {
                budget.Token.ThrowIfCancellationRequested();
                var inspectResult = await runner.CaptureBoundedAsync(
                    ["--context", dockerContextName, "inspect", "--format", "{{json .}}", candidate.ContainerId!],
                    MaximumInspectOutputCharacters,
                    RemainingBudget(timer),
                    budget.Token).ConfigureAwait(false);
                inspectCount++;
                if (inspectResult.ExitCode != 0 ||
                    !OwnedContainerInspection.MatchesStandardBaseResource(
                        inspectResult.StandardOutput,
                        candidate.ContainerId!,
                        candidate.RunId,
                        candidate.Role,
                        candidate.StackId,
                        candidate.ResourceName))
                {
                    return Result(
                        DcpBaseResourceOwnershipDisposition.ContainerRejected,
                        "Container ID, running state, or standard ownership labels did not match.",
                        candidate.ResourceName,
                        inspectCount: inspectCount);
                }

                var hasValidNetworkShape = OwnedContainerInspection.TryReadStandardDcpNetworkEndpoint(
                    inspectResult.StandardOutput,
                    out var endpoint) && endpoint?.NetworkId is not null;
                if (!hasValidNetworkShape)
                {
                    networkShapeIsShared = false;
                    proofs.Add(new DcpBaseResourceOwnershipProof(
                        candidate.Resource,
                        candidate.ResourceId,
                        candidate.ContainerId!,
                        candidate.Generation,
                        engineResult.StandardOutput.Trim(),
                        null));
                    continue;
                }

                var validEndpoint = endpoint!;
                if (sharedNetworkName is not null &&
                    (!string.Equals(sharedNetworkName, validEndpoint.NetworkName, StringComparison.Ordinal) ||
                     !string.Equals(sharedNetworkId, validEndpoint.NetworkId, StringComparison.OrdinalIgnoreCase)))
                {
                    networkShapeIsShared = false;
                }
                sharedNetworkName ??= validEndpoint.NetworkName;
                sharedNetworkId ??= validEndpoint.NetworkId;
                proofs.Add(new DcpBaseResourceOwnershipProof(
                    candidate.Resource,
                    candidate.ResourceId,
                    candidate.ContainerId!,
                    candidate.Generation,
                    engineResult.StandardOutput.Trim(),
                    validEndpoint.NetworkName));
            }

            foreach (var candidate in candidates)
            {
                if (!IsCurrentCandidate(candidate, getCurrentByResourceId))
                {
                    return Result(
                        DcpBaseResourceOwnershipDisposition.CandidateChanged,
                        "A resource key or full container ID changed while ownership inspection was in progress.",
                        candidate.ResourceName,
                        inspectCount: inspectCount);
                }
            }

            budget.Token.ThrowIfCancellationRequested();
            if (!registry.TryMarkIdentityVerified(
                    proofs,
                    sharedStandardDcpNetworkVerified: networkShapeIsShared && !string.IsNullOrWhiteSpace(sharedNetworkName)))
            {
                return Result(
                    DcpBaseResourceOwnershipDisposition.CandidateChanged,
                    "The candidate generation changed before identity evidence could be recorded.",
                    inspectCount: inspectCount);
            }

            return Result(
                networkShapeIsShared
                    ? DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown
                    : DcpBaseResourceOwnershipDisposition.NetworkObservationRejected,
                networkShapeIsShared
                    ? "The current standard DCP container identities and their common default-network membership were verified; service readiness remains unverified."
                    : "Docker Engine and ownership labels were verified; network attachments are missing, invalid, or disagree.",
                observedNetworkName: networkShapeIsShared ? sharedNetworkName : null,
                inspectCount: inspectCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Result(
                DcpBaseResourceOwnershipDisposition.DeadlineExceeded,
                "The shared ownership inspection deadline expired before all metadata was checked.",
                inspectCount: inspectCount);
        }
        catch (TimeoutException)
        {
            return Result(
                DcpBaseResourceOwnershipDisposition.DeadlineExceeded,
                "Docker metadata inspection exceeded the shared deadline.",
                inspectCount: inspectCount);
        }
        catch (Exception exception) when (
            exception is IOException or
            JsonException or
            InvalidOperationException)
        {
            return Result(
                DcpBaseResourceOwnershipDisposition.InspectionFailed,
                "Docker metadata inspection failed before identity evidence could be recorded.",
                inspectCount: inspectCount);
        }
    }

    private static DcpBaseResourceOwnershipResult Result(
        DcpBaseResourceOwnershipDisposition disposition,
        string reason,
        string? resourceName = null,
        string? observedNetworkName = null,
        int inspectCount = 0) =>
        new(disposition, reason, resourceName, observedNetworkName, inspectCount);

    private static bool IsCurrentCandidate(
        DcpBaseResourceIdentityCandidate candidate,
        Func<string, ResourceEvent?> getCurrentByResourceId)
    {
        var current = getCurrentByResourceId(candidate.ResourceId);
        return current is not null &&
            ReferenceEquals(current.Resource, candidate.Resource) &&
            current.ResourceId == candidate.ResourceId &&
            current.Snapshot.State?.Text == "Running" &&
            RegisteredContainerEventProjection.TryProject(
                candidate.Resource,
                candidate.ResourceId,
                candidate.Role,
                current,
                current,
                out var latest) &&
            latest is not null &&
            latest.ContainerId == candidate.ContainerId;
    }

    private static TimeSpan RemainingBudget(Stopwatch timer)
    {
        var remaining = VerificationBudget - timer.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException("DCP ownership inspection exceeded its shared deadline.");
        }

        return remaining;
    }
}
