using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Ocr;

namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Explicit native/OCR evidence pair for one page-local reconciliation
/// candidate.
///
/// Pairing is supplied by the caller in Phase 17A. Automatic spatial matching
/// and end-to-end hybrid orchestration remain later concerns.
/// </summary>
public sealed class TextReconciliationInput
{
    public TextReconciliationInput(
        int physicalPageNumber,
        NativeTextStatus nativeStatus,
        DocumentTextBlock? nativeBlock,
        OcrRegionResult? ocrRegion)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be greater than zero.");
        }

        if (!Enum.IsDefined(nativeStatus))
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

        if (nativeStatus != NativeTextStatus.Missing &&
            nativeBlock is null)
        {
            throw new ArgumentException(
                "Healthy or suspicious native status requires a native text block.",
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

        if (nativeBlock is not null &&
            ocrRegion is not null &&
            !HasPositiveIntersection(
                nativeBlock.Bounds,
                ocrRegion.SourceLayoutObservation.Bounds))
        {
            throw new ArgumentException(
                "Paired native and OCR regions must have a positive spatial intersection.",
                nameof(ocrRegion));
        }

        PhysicalPageNumber = physicalPageNumber;
        NativeStatus = nativeStatus;
        NativeBlock = nativeBlock;
        OcrRegion = ocrRegion;
    }

    public int PhysicalPageNumber { get; }

    public NativeTextStatus NativeStatus { get; }

    public DocumentTextBlock? NativeBlock { get; }

    public OcrRegionResult? OcrRegion { get; }

    private static bool HasPositiveIntersection(
        NormalizedRectangle left,
        NormalizedRectangle right)
    {
        var intersectionLeft = Math.Max(left.Left, right.Left);
        var intersectionTop = Math.Max(left.Top, right.Top);
        var intersectionRight = Math.Min(left.Right, right.Right);
        var intersectionBottom = Math.Min(left.Bottom, right.Bottom);

        return intersectionRight > intersectionLeft &&
               intersectionBottom > intersectionTop;
    }
}
