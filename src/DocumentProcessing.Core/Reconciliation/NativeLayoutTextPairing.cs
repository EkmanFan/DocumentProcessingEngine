using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Pairing result for one OCR-authorized layout target.
///
/// ComparableNativeEvidence is exposed only when ownership is deterministic.
/// Ambiguous word ownership fails closed and therefore carries no usable
/// comparable evidence.
/// </summary>
public sealed class NativeLayoutTextPairing
{
    public NativeLayoutTextPairing(
        LayoutObservation targetLayoutObservation,
        NativeLayoutTextPairingStatus status,
        ComparableNativeTextEvidence? comparableNativeEvidence = null,
        IReadOnlyList<DocumentWord>? ambiguousWords = null)
    {
        ArgumentNullException.ThrowIfNull(
            targetLayoutObservation);

        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        var resolvedAmbiguousWords =
            ambiguousWords?.ToArray() ??
            [];

        if (resolvedAmbiguousWords.Any(
                word =>
                    word is null))
        {
            throw new ArgumentException(
                "Ambiguous word evidence cannot contain null values.",
                nameof(ambiguousWords));
        }

        if (resolvedAmbiguousWords
            .GroupBy(
                word =>
                    word,
                ReferenceEqualityComparer.Instance)
            .Any(
                group =>
                    group.Count() > 1))
        {
            throw new ArgumentException(
                "Ambiguous word evidence cannot contain duplicate word references.",
                nameof(ambiguousWords));
        }

        switch (status)
        {
            case NativeLayoutTextPairingStatus.NoNativeEvidence:
                if (comparableNativeEvidence is not null ||
                    resolvedAmbiguousWords.Length > 0)
                {
                    throw new ArgumentException(
                        "NoNativeEvidence cannot carry comparable or ambiguous evidence.");
                }

                break;

            case NativeLayoutTextPairingStatus.Comparable:
                if (comparableNativeEvidence is null)
                {
                    throw new ArgumentException(
                        "Comparable status requires comparable native evidence.",
                        nameof(comparableNativeEvidence));
                }

                if (!ReferenceEquals(
                        comparableNativeEvidence.SourceLayoutObservation,
                        targetLayoutObservation))
                {
                    throw new ArgumentException(
                        "Comparable native evidence must originate from the target layout observation.",
                        nameof(comparableNativeEvidence));
                }

                if (resolvedAmbiguousWords.Length > 0)
                {
                    throw new ArgumentException(
                        "Comparable status cannot carry ambiguous word evidence.",
                        nameof(ambiguousWords));
                }

                break;

            case NativeLayoutTextPairingStatus.AmbiguousWordOwnership:
                if (comparableNativeEvidence is not null)
                {
                    throw new ArgumentException(
                        "AmbiguousWordOwnership must fail closed and cannot expose usable " +
                        "comparable native evidence.",
                        nameof(comparableNativeEvidence));
                }

                if (resolvedAmbiguousWords.Length == 0)
                {
                    throw new ArgumentException(
                        "AmbiguousWordOwnership requires at least one ambiguous native word.",
                        nameof(ambiguousWords));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status));
        }

        TargetLayoutObservation =
            targetLayoutObservation;

        Status =
            status;

        ComparableNativeEvidence =
            comparableNativeEvidence;

        AmbiguousWords =
            resolvedAmbiguousWords;
    }

    public LayoutObservation TargetLayoutObservation { get; }

    public NativeLayoutTextPairingStatus Status { get; }

    /// <summary>
    /// Usable target-centric native evidence only when Status == Comparable.
    /// </summary>
    public ComparableNativeTextEvidence? ComparableNativeEvidence { get; }

    /// <summary>
    /// Native words claimed by more than one OCR-authorized text target.
    /// Populated only for AmbiguousWordOwnership.
    /// </summary>
    public IReadOnlyList<DocumentWord> AmbiguousWords { get; }

    public bool IsComparable =>
        Status ==
        NativeLayoutTextPairingStatus.Comparable;
}
