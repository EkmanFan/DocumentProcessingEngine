using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Layout;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Deterministic adapters from validated Phase 14-17 evidence into neutral
/// hybrid page elements.
///
/// The factory never performs OCR, layout analysis, spatial matching, or
/// reconciliation. Those decisions remain explicit upstream boundaries.
/// </summary>
public static class HybridDocumentElementFactory
{
    #region Methods

    public static HybridDocumentElement FromNative(
        int physicalPageNumber,
        DocumentTextBlock nativeBlock)
    {
        ArgumentNullException.ThrowIfNull(
            nativeBlock);

        return FromNativeWithReadingOrder(
            physicalPageNumber,
            nativeBlock,
            nativeBlock.ReadingOrder ??
            nativeBlock.SourceSequence);
    }

    /// <summary>
    /// Projects an authoritative native block into a caller-resolved hybrid
    /// stream position while retaining the original native block unchanged as
    /// provenance.
    ///
    /// The caller owns the ordering decision. This factory performs no layout
    /// matching or authority selection.
    /// </summary>
    public static HybridDocumentElement FromNativeWithReadingOrder(
        int physicalPageNumber,
        DocumentTextBlock nativeBlock,
        int readingOrder)
    {
        ArgumentNullException.ThrowIfNull(
            nativeBlock);

        if (readingOrder <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readingOrder));
        }

        return new HybridDocumentElement(
            physicalPageNumber,
            readingOrder,
            HybridDocumentElementKind.Text,
            nativeBlock.Bounds,
            nativeBlock.Text,
            TextSelectionOrigin.Native,
            nativeBlock);
    }

    public static HybridDocumentElement FromReconciliation(
        TextReconciliationResult reconciliation)
    {
        ArgumentNullException.ThrowIfNull(
            reconciliation);

        var input =
            reconciliation.Input;

        var layout =
            input.OcrRegion
                ?.SourceLayoutObservation;

        var nativeBlock =
            input.NativeBlock;

        var readingOrder =
            ResolveReadingOrder(
                layout,
                nativeBlock);

        var bounds =
            layout?.Bounds ??
            nativeBlock?.Bounds ??
            throw new InvalidOperationException(
                "Reconciliation without spatial evidence cannot become a hybrid element.");

        if (!reconciliation.IsResolved)
        {
            return new HybridDocumentElement(
                input.PhysicalPageNumber,
                readingOrder,
                HybridDocumentElementKind.UnresolvedText,
                bounds,
                text: null,
                TextSelectionOrigin.None,
                nativeBlock,
                layout,
                reconciliation);
        }

        var kind =
            layout is null
                ? HybridDocumentElementKind.Text
                : MapTextualKind(
                    layout.Kind);

        return new HybridDocumentElement(
            input.PhysicalPageNumber,
            readingOrder,
            kind,
            bounds,
            reconciliation.SelectedText,
            reconciliation.SelectedOrigin,
            nativeBlock,
            layout,
            reconciliation);
    }

    public static HybridDocumentElement FromPreservedVisual(
        PreservedVisualEvidence preservedVisual)
    {
        ArgumentNullException.ThrowIfNull(
            preservedVisual);

        var layout =
            preservedVisual.SourceLayoutObservation;

        if (layout.Kind !=
            LayoutObservationKind.Figure)
        {
            throw new InvalidOperationException(
                $"Layout kind {layout.Kind} cannot back a visual hybrid element.");
        }

        if (layout.ReadingOrder is null)
        {
            throw new InvalidOperationException(
                "Preserved visual requires explicit layout reading order.");
        }

        return new HybridDocumentElement(
            layout.PhysicalPageNumber,
            layout.ReadingOrder.Value,
            HybridDocumentElementKind.Visual,
            layout.Bounds,
            text: null,
            TextSelectionOrigin.None,
            nativeBlock: null,
            layout,
            reconciliation: null,
            preservedVisual);
    }

    public static HybridDocumentElement FromDeferred(
        LayoutObservation layoutObservation)
    {
        ArgumentNullException.ThrowIfNull(
            layoutObservation);

        if (LayoutTextPolicy.IsTextRecognitionCandidate(
                layoutObservation.Kind))
        {
            throw new InvalidOperationException(
                $"Layout kind {layoutObservation.Kind} is text-authorized and cannot be deferred.");
        }

        if (layoutObservation.ReadingOrder is null)
        {
            throw new InvalidOperationException(
                "Deferred layout evidence requires explicit reading order.");
        }

        return new HybridDocumentElement(
            layoutObservation.PhysicalPageNumber,
            layoutObservation.ReadingOrder.Value,
            HybridDocumentElementKind.Deferred,
            layoutObservation.Bounds,
            text: null,
            TextSelectionOrigin.None,
            nativeBlock: null,
            layoutObservation);
    }

    private static int ResolveReadingOrder(
        LayoutObservation? layout,
        DocumentTextBlock? nativeBlock)
    {
        if (layout is not null)
        {
            return layout.ReadingOrder ??
                   throw new InvalidOperationException(
                       "Layout-backed hybrid element requires explicit reading order.");
        }

        if (nativeBlock is not null)
        {
            return nativeBlock.ReadingOrder ??
                   nativeBlock.SourceSequence;
        }

        throw new InvalidOperationException(
            "Hybrid text element requires layout or native-block ordering evidence.");
    }

    private static HybridDocumentElementKind MapTextualKind(
        LayoutObservationKind kind)
    {
        if (!LayoutTextPolicy.IsTextRecognitionCandidate(
                kind))
        {
            throw new InvalidOperationException(
                $"Layout kind {kind} is not authorized as textual hybrid content.");
        }

        return kind switch
        {
            LayoutObservationKind.Text =>
                HybridDocumentElementKind.Text,

            LayoutObservationKind.Heading =>
                HybridDocumentElementKind.Heading,

            LayoutObservationKind.Caption =>
                HybridDocumentElementKind.Caption,

            // Table is a source-layout role, not a final text-flow type.
            // OCR-recovered table text participates in neutral text flow while
            // the original LayoutObservationKind.Table remains attached as
            // provenance. Cell/row/column structure is intentionally not
            // inferred by this fallback.
            LayoutObservationKind.Table =>
                HybridDocumentElementKind.Text,

            _ =>
                throw new InvalidOperationException(
                    $"Layout kind {kind} is not a supported textual hybrid kind.")
        };
    }

    #endregion
}
