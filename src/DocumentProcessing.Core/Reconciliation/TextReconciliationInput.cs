using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Ocr;

namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Explicit native/OCR evidence for one page-local reconciliation candidate.
///
/// Legacy block-level callers may still supply one DocumentTextBlock directly.
/// Target-centric hybrid execution supplies ComparableNativeTextEvidence, which
/// may retain projected evidence from one or more native source blocks.
///
/// Pairing is performed upstream by NativeLayoutTextPairer. This input performs
/// no automatic spatial matching, fuzzy selection, or authority decision.
/// </summary>
public sealed class TextReconciliationInput
{
    public TextReconciliationInput(
        int physicalPageNumber,
        NativeTextStatus nativeStatus,
        DocumentTextBlock? nativeBlock,
        OcrRegionResult? ocrRegion)
        : this(
            physicalPageNumber,
            nativeStatus,
            nativeBlock,
            comparableNativeEvidence: null,
            ocrRegion)
    {
    }

    /// <summary>
    /// Creates a target-centric reconciliation input from deterministic native
    /// evidence already paired to the OCR-authorized layout target.
    ///
    /// NativeBlock remains populated with the first source block solely for
    /// backward-compatible provenance consumers. ComparableNativeEvidence is
    /// the complete native comparison extent.
    /// </summary>
    public TextReconciliationInput(
        int physicalPageNumber,
        NativeTextStatus nativeStatus,
        ComparableNativeTextEvidence comparableNativeEvidence,
        OcrRegionResult ocrRegion)
        : this(
            physicalPageNumber,
            nativeStatus,
            GetPrimarySourceBlock(
                comparableNativeEvidence),
            comparableNativeEvidence,
            ocrRegion)
    {
    }

    private TextReconciliationInput(
        int physicalPageNumber,
        NativeTextStatus nativeStatus,
        DocumentTextBlock? nativeBlock,
        ComparableNativeTextEvidence? comparableNativeEvidence,
        OcrRegionResult? ocrRegion)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be greater than zero.");
        }

        if (!Enum.IsDefined(
                nativeStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nativeStatus));
        }

        if (nativeStatus == NativeTextStatus.Missing &&
            nativeBlock is not null)
        {
            throw new ArgumentException(
                "Missing native status cannot carry a native text block.",
                nameof(nativeBlock));
        }

        if (nativeStatus == NativeTextStatus.Missing &&
            comparableNativeEvidence is not null)
        {
            throw new ArgumentException(
                "Missing native status cannot carry comparable native evidence.",
                nameof(comparableNativeEvidence));
        }

        if (nativeStatus != NativeTextStatus.Missing &&
            nativeBlock is null)
        {
            throw new ArgumentException(
                "Healthy, suspicious, or unverified native status requires native evidence.",
                nameof(nativeBlock));
        }

        if (ocrRegion is not null &&
            ocrRegion.SourceLayoutObservation.PhysicalPageNumber !=
            physicalPageNumber)
        {
            throw new ArgumentException(
                "OCR evidence must belong to the reconciliation page.",
                nameof(ocrRegion));
        }

        if (comparableNativeEvidence is not null)
        {
            if (comparableNativeEvidence
                    .SourceLayoutObservation
                    .PhysicalPageNumber !=
                physicalPageNumber)
            {
                throw new ArgumentException(
                    "Comparable native evidence must belong to the reconciliation page.",
                    nameof(comparableNativeEvidence));
            }

            if (!ReferenceEquals(
                    comparableNativeEvidence.SourceBlocks[0],
                    nativeBlock))
            {
                throw new ArgumentException(
                    "The compatibility native block must be the first source block " +
                    "of the comparable native evidence.",
                    nameof(nativeBlock));
            }

            if (ocrRegion is null)
            {
                throw new ArgumentException(
                    "Comparable native evidence requires OCR evidence.",
                    nameof(ocrRegion));
            }

            if (!ReferenceEquals(
                    comparableNativeEvidence.SourceLayoutObservation,
                    ocrRegion.SourceLayoutObservation))
            {
                throw new ArgumentException(
                    "Comparable native evidence and OCR evidence must originate " +
                    "from the same layout observation.",
                    nameof(ocrRegion));
            }
        }
        else if (nativeBlock is not null &&
                 ocrRegion is not null &&
                 !HasPositiveIntersection(
                     nativeBlock.Bounds,
                     ocrRegion.SourceLayoutObservation.Bounds))
        {
            throw new ArgumentException(
                "Paired native and OCR regions must have a positive spatial intersection.",
                nameof(ocrRegion));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        NativeStatus =
            nativeStatus;

        NativeBlock =
            nativeBlock;

        ComparableNativeEvidence =
            comparableNativeEvidence;

        OcrRegion =
            ocrRegion;
    }

    public int PhysicalPageNumber { get; }

    public NativeTextStatus NativeStatus { get; }

    /// <summary>
    /// Legacy/compatibility native-block provenance.
    ///
    /// For target-centric reconciliation this is the first source block in
    /// ComparableNativeEvidence. Consumers needing complete provenance must use
    /// ComparableNativeEvidence.SourceBlocks.
    /// </summary>
    public DocumentTextBlock? NativeBlock { get; }

    public ComparableNativeTextEvidence? ComparableNativeEvidence { get; }

    public OcrRegionResult? OcrRegion { get; }

    public bool HasNativeEvidence =>
        NativeBlock is not null;

    private static DocumentTextBlock GetPrimarySourceBlock(
        ComparableNativeTextEvidence comparableNativeEvidence)
    {
        ArgumentNullException.ThrowIfNull(
            comparableNativeEvidence);

        return comparableNativeEvidence
            .SourceBlocks[0];
    }

    private static bool HasPositiveIntersection(
        NormalizedRectangle left,
        NormalizedRectangle right)
    {
        var intersectionLeft =
            Math.Max(
                left.Left,
                right.Left);

        var intersectionTop =
            Math.Max(
                left.Top,
                right.Top);

        var intersectionRight =
            Math.Min(
                left.Right,
                right.Right);

        var intersectionBottom =
            Math.Min(
                left.Bottom,
                right.Bottom);

        return intersectionRight >
                   intersectionLeft &&
               intersectionBottom >
                   intersectionTop;
    }
}
