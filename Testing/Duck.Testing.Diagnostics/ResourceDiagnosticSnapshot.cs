namespace Duck.Testing.Diagnostics;

public enum DiagnosticEvidenceKind
{
    Unknown,
    ServiceDecode,
    AuxiliaryObservation,
}

public enum DiagnosticDecodeStatus
{
    Unknown,
    Succeeded,
    Failed,
}

/// <summary>
/// An in-memory, normalized observation of one input in one resource incarnation.
/// This is not an external service's payload or a wire format.
/// </summary>
/// <param name="Identity">The identity associated with the observation, checked against the expected owner identity.</param>
/// <param name="InputId">The caller's input key, kept separate from other counters in the same resource.</param>
/// <param name="ObservationSequence">A positive, increasing sequence assigned by the local collector before acquisition.</param>
/// <param name="EvidenceKind">Whether the counter belongs to the service's own successful decoding.</param>
/// <param name="DecodeStatus">The latest decode outcome represented by this observation.</param>
/// <param name="SuccessfullyDecodedCount">A nonnegative cumulative count within this identity and input.</param>
/// <param name="DecodeAgeUpperBound">
/// A justified upper bound on time since the latest successful decode when Observe is called,
/// including acquisition delay. Null means the age cannot be established. A poll timestamp,
/// an unverified remote timestamp, or a packet capture timestamp cannot supply this value.
/// </param>
public sealed record ResourceDiagnosticSnapshot(
    ResourceDiagnosticIdentity Identity,
    string InputId,
    long ObservationSequence,
    DiagnosticEvidenceKind EvidenceKind,
    DiagnosticDecodeStatus DecodeStatus,
    long SuccessfullyDecodedCount,
    TimeSpan? DecodeAgeUpperBound);
