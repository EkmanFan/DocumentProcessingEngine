using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Minimal deterministic page-level evidence supplied to a page-processing
/// policy.
///
/// Phase 21.0 intentionally does not decide how this assessment is produced.
/// The later ingestion implementation must derive it from deterministic
/// preflight/native evidence rather than from an LLM or backend recommendation.
/// </summary>
public sealed record PageProcessingAssessment
{
    public PageProcessingAssessment(
        int physicalPageNumber,
        NativeTextStatus nativeTextStatus)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be positive.");
        }

        if (!Enum.IsDefined(
                typeof(NativeTextStatus),
                nativeTextStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nativeTextStatus),
                nativeTextStatus,
                "Native text status must be a defined value.");
        }

        PhysicalPageNumber =
            physicalPageNumber;

        NativeTextStatus =
            nativeTextStatus;
    }

    public int PhysicalPageNumber { get; }

    public NativeTextStatus NativeTextStatus { get; }
}
