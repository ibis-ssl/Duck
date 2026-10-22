namespace Duck.Testing.Diagnostics;

public enum ResourceReadinessReason
{
    NoObservation,
    AwaitingProgress,
    Ready,
    NonServiceEvidence,
    DecodeUnsuccessful,
    InvalidCount,
    CountRegressed,
    UnknownAge,
    InvalidAge,
    Expired,
    InvalidClock,
}

/// <summary>A diagnostic eligibility result, not proof of ownership or whole-service health.</summary>
public sealed record ResourceReadinessResult(bool IsReady, ResourceReadinessReason Reason);

/// <summary>
/// Evaluates normalized decode progress for one input. It has no transport or resource
/// owner integration. Re-reading an unchanged counter never renews the progress lease.
/// </summary>
public sealed class ResourceReadinessState
{
    private readonly object gate = new();
    private readonly string inputId;
    private readonly TimeSpan freshnessWindow;
    private readonly TimeProvider timeProvider;
    private ResourceDiagnosticIdentity expectedIdentity;
    private long lastSequence;
    private long? lastCount;
    private long? lastObservationTimestamp;
    private bool hasProgress;
    private long progressTimestamp;
    private TimeSpan progressAge;
    private ResourceReadinessReason reason = ResourceReadinessReason.NoObservation;

    public ResourceReadinessState(
        ResourceDiagnosticIdentity expectedIdentity,
        string inputId,
        TimeSpan freshnessWindow,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputId);
        if (freshnessWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(freshnessWindow));
        }

        this.expectedIdentity = expectedIdentity;
        this.inputId = inputId;
        this.freshnessWindow = freshnessWindow;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Starts a newer incarnation of the same run/resource. The owner, not the diagnostic
    /// payload, authorizes this change. A process restart may keep the container ID.
    /// </summary>
    public void AdvanceGeneration(ResourceDiagnosticIdentity nextIdentity)
    {
        ArgumentNullException.ThrowIfNull(nextIdentity);
        lock (gate)
        {
            if (nextIdentity.RunId != expectedIdentity.RunId ||
                nextIdentity.ResourceId != expectedIdentity.ResourceId ||
                nextIdentity.Generation <= expectedIdentity.Generation)
            {
                throw new ArgumentException("A newer generation of the same run and resource is required.", nameof(nextIdentity));
            }

            expectedIdentity = nextIdentity;
            lastSequence = 0;
            lastCount = null;
            lastObservationTimestamp = null;
            Invalidate(ResourceReadinessReason.NoObservation);
        }
    }

    /// <summary>
    /// Returns false for a foreign input/identity or a nonpositive, duplicate, or older sequence.
    /// Rejected observations cannot overwrite a newer state. Matching newer observations
    /// return true even when their evidence invalidates diagnostic readiness.
    /// </summary>
    public bool Observe(ResourceDiagnosticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (gate)
        {
            if (snapshot.Identity != expectedIdentity ||
                !string.Equals(snapshot.InputId, inputId, StringComparison.Ordinal) ||
                snapshot.ObservationSequence <= lastSequence)
            {
                return false;
            }

            lastSequence = snapshot.ObservationSequence;
            var now = timeProvider.GetTimestamp();
            if (lastObservationTimestamp is { } previousTimestamp && now < previousTimestamp)
            {
                Invalidate(ResourceReadinessReason.InvalidClock);
                return true;
            }
            lastObservationTimestamp = now;

            if (snapshot.EvidenceKind != DiagnosticEvidenceKind.ServiceDecode)
            {
                Invalidate(ResourceReadinessReason.NonServiceEvidence);
                return true;
            }
            if (snapshot.DecodeStatus != DiagnosticDecodeStatus.Succeeded)
            {
                Invalidate(ResourceReadinessReason.DecodeUnsuccessful);
                return true;
            }
            if (snapshot.SuccessfullyDecodedCount < 0)
            {
                Invalidate(ResourceReadinessReason.InvalidCount);
                return true;
            }
            if (lastCount is { } previousCount && snapshot.SuccessfullyDecodedCount < previousCount)
            {
                Invalidate(ResourceReadinessReason.CountRegressed);
                return true;
            }

            var increased = lastCount is { } count && snapshot.SuccessfullyDecodedCount > count;
            lastCount = snapshot.SuccessfullyDecodedCount;
            if (snapshot.DecodeAgeUpperBound is not { } age)
            {
                Invalidate(ResourceReadinessReason.UnknownAge);
                return true;
            }
            if (age < TimeSpan.Zero)
            {
                Invalidate(ResourceReadinessReason.InvalidAge);
                return true;
            }
            if (age > freshnessWindow)
            {
                Invalidate(ResourceReadinessReason.Expired);
                return true;
            }

            if (increased)
            {
                hasProgress = true;
                progressTimestamp = now;
                progressAge = age;
            }

            reason = hasProgress ? ResourceReadinessReason.Ready : ResourceReadinessReason.AwaitingProgress;
            return true;
        }
    }

    public ResourceReadinessResult GetReadiness()
    {
        lock (gate)
        {
            if (!hasProgress)
            {
                return new(false, reason);
            }

            var now = timeProvider.GetTimestamp();
            if (lastObservationTimestamp is { } previousTimestamp && now < previousTimestamp)
            {
                Invalidate(ResourceReadinessReason.InvalidClock);
                return new(false, reason);
            }

            var elapsed = timeProvider.GetElapsedTime(progressTimestamp, now);
            if (elapsed < TimeSpan.Zero)
            {
                Invalidate(ResourceReadinessReason.InvalidClock);
                return new(false, reason);
            }

            // Subtract before comparing, so an untrusted large age cannot overflow a sum.
            if (elapsed > freshnessWindow || progressAge > freshnessWindow - elapsed)
            {
                return new(false, ResourceReadinessReason.Expired);
            }

            return new(true, ResourceReadinessReason.Ready);
        }
    }

    private void Invalidate(ResourceReadinessReason failure)
    {
        hasProgress = false;
        reason = failure;
    }
}
