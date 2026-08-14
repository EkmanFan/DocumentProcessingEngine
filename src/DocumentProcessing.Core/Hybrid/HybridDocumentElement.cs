using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Core.Hybrid;

/// <summary>
/// One page-local element in the unified native/OCR/visual stream.
///
/// This model is deliberately pre-segmentation. It retains enough provenance to
/// audit where selected text or preserved visual evidence came from, while
/// refusing to turn unresolved/deferred evidence into document text.
/// </summary>
public sealed class HybridDocumentElement
{
    public HybridDocumentElement(
        int physicalPageNumber,
        int readingOrder,
        HybridDocumentElementKind kind,
        NormalizedRectangle bounds,
        string? text,
        TextSelectionOrigin textOrigin,
        DocumentTextBlock? nativeBlock = null,
        LayoutObservation? layoutObservation = null,
        TextReconciliationResult? reconciliation = null,
        PreservedVisualEvidence? preservedVisual = null)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (readingOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readingOrder));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind));
        }

        if (!Enum.IsDefined(textOrigin))
        {
            throw new ArgumentOutOfRangeException(
                nameof(textOrigin));
        }

        var normalizedText =
            string.IsNullOrWhiteSpace(text)
                ? null
                : text.Trim();

        if (layoutObservation is not null &&
            layoutObservation.PhysicalPageNumber !=
            physicalPageNumber)
        {
            throw new ArgumentException(
                "Layout observation must belong to the element page.",
                nameof(layoutObservation));
        }

        if (reconciliation is not null &&
            reconciliation.Input.PhysicalPageNumber !=
            physicalPageNumber)
        {
            throw new ArgumentException(
                "Reconciliation result must belong to the element page.",
                nameof(reconciliation));
        }

        if (preservedVisual is not null &&
            preservedVisual.SourceLayoutObservation.PhysicalPageNumber !=
            physicalPageNumber)
        {
            throw new ArgumentException(
                "Preserved visual must belong to the element page.",
                nameof(preservedVisual));
        }

        if (reconciliation is not null &&
            reconciliation.Input.OcrRegion is not null &&
            layoutObservation is not null &&
            !ReferenceEquals(
                reconciliation.Input.OcrRegion.SourceLayoutObservation,
                layoutObservation))
        {
            throw new ArgumentException(
                "Reconciliation and element layout observations must be the same evidence object.",
                nameof(layoutObservation));
        }

        if (preservedVisual is not null &&
            layoutObservation is not null &&
            !ReferenceEquals(
                preservedVisual.SourceLayoutObservation,
                layoutObservation))
        {
            throw new ArgumentException(
                "Preserved visual and element layout observations must be the same evidence object.",
                nameof(layoutObservation));
        }

        switch (kind)
        {
            case HybridDocumentElementKind.Text:
            case HybridDocumentElementKind.Heading:
            case HybridDocumentElementKind.Caption:
                ValidateAuthoritativeText(
                    normalizedText,
                    textOrigin,
                    nativeBlock,
                    reconciliation,
                    preservedVisual);
                break;

            case HybridDocumentElementKind.Visual:
                ValidateVisual(
                    normalizedText,
                    textOrigin,
                    nativeBlock,
                    reconciliation,
                    layoutObservation,
                    preservedVisual);
                break;

            case HybridDocumentElementKind.UnresolvedText:
                ValidateUnresolvedText(
                    normalizedText,
                    textOrigin,
                    reconciliation,
                    preservedVisual);
                break;

            case HybridDocumentElementKind.Deferred:
                ValidateDeferred(
                    normalizedText,
                    textOrigin,
                    nativeBlock,
                    reconciliation,
                    layoutObservation,
                    preservedVisual);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        ReadingOrder =
            readingOrder;

        Kind =
            kind;

        Bounds =
            bounds;

        Text =
            normalizedText;

        TextOrigin =
            textOrigin;

        NativeBlock =
            nativeBlock;

        LayoutObservation =
            layoutObservation;

        Reconciliation =
            reconciliation;

        PreservedVisual =
            preservedVisual;
    }

    public int PhysicalPageNumber { get; }

    public int ReadingOrder { get; }

    public HybridDocumentElementKind Kind { get; }

    public NormalizedRectangle Bounds { get; }

    /// <summary>
    /// Authoritative selected text only. Null for visual, unresolved, and
    /// deferred elements.
    /// </summary>
    public string? Text { get; }

    public TextSelectionOrigin TextOrigin { get; }

    public DocumentTextBlock? NativeBlock { get; }

    public LayoutObservation? LayoutObservation { get; }

    public TextReconciliationResult? Reconciliation { get; }

    public PreservedVisualEvidence? PreservedVisual { get; }

    public bool HasAuthoritativeText =>
        Text is not null &&
        TextOrigin is not TextSelectionOrigin.None;

    public bool IsResolved =>
        Kind is not HybridDocumentElementKind.UnresolvedText and
        not HybridDocumentElementKind.Deferred;

    private static void ValidateAuthoritativeText(
        string? text,
        TextSelectionOrigin textOrigin,
        DocumentTextBlock? nativeBlock,
        TextReconciliationResult? reconciliation,
        PreservedVisualEvidence? preservedVisual)
    {
        if (text is null)
        {
            throw new ArgumentException(
                "Textual hybrid element requires authoritative text.",
                nameof(text));
        }

        if (textOrigin == TextSelectionOrigin.None)
        {
            throw new ArgumentException(
                "Textual hybrid element requires a selected text origin.",
                nameof(textOrigin));
        }

        if (preservedVisual is not null)
        {
            throw new ArgumentException(
                "Textual hybrid element cannot carry preserved visual evidence.",
                nameof(preservedVisual));
        }

        if (textOrigin == TextSelectionOrigin.NativePdf &&
            nativeBlock is null)
        {
            throw new ArgumentException(
                "NativePdf text origin requires native block provenance.",
                nameof(nativeBlock));
        }

        if (reconciliation is not null)
        {
            if (!reconciliation.IsResolved)
            {
                throw new ArgumentException(
                    "Authoritative text cannot originate from unresolved reconciliation.",
                    nameof(reconciliation));
            }

            if (reconciliation.SelectedOrigin != textOrigin ||
                !string.Equals(
                    reconciliation.SelectedText,
                    text,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Element text/origin must exactly match the reconciliation selection.",
                    nameof(reconciliation));
            }
        }
    }

    private static void ValidateVisual(
        string? text,
        TextSelectionOrigin textOrigin,
        DocumentTextBlock? nativeBlock,
        TextReconciliationResult? reconciliation,
        LayoutObservation? layoutObservation,
        PreservedVisualEvidence? preservedVisual)
    {
        if (text is not null ||
            textOrigin != TextSelectionOrigin.None)
        {
            throw new ArgumentException(
                "Visual hybrid element cannot carry authoritative text.");
        }

        if (nativeBlock is not null ||
            reconciliation is not null)
        {
            throw new ArgumentException(
                "Visual hybrid element cannot carry native/reconciliation text evidence.");
        }

        if (preservedVisual is null ||
            layoutObservation is null)
        {
            throw new ArgumentException(
                "Visual hybrid element requires preserved visual and layout evidence.");
        }

        if (layoutObservation.Kind != LayoutObservationKind.Figure)
        {
            throw new ArgumentException(
                "Visual hybrid element requires Figure layout evidence.",
                nameof(layoutObservation));
        }
    }

    private static void ValidateUnresolvedText(
        string? text,
        TextSelectionOrigin textOrigin,
        TextReconciliationResult? reconciliation,
        PreservedVisualEvidence? preservedVisual)
    {
        if (text is not null ||
            textOrigin != TextSelectionOrigin.None)
        {
            throw new ArgumentException(
                "Unresolved text element cannot carry authoritative text.");
        }

        if (preservedVisual is not null)
        {
            throw new ArgumentException(
                "Unresolved text element cannot carry preserved visual evidence.",
                nameof(preservedVisual));
        }

        if (reconciliation is null ||
            reconciliation.IsResolved)
        {
            throw new ArgumentException(
                "Unresolved text element requires unresolved reconciliation evidence.",
                nameof(reconciliation));
        }
    }

    private static void ValidateDeferred(
        string? text,
        TextSelectionOrigin textOrigin,
        DocumentTextBlock? nativeBlock,
        TextReconciliationResult? reconciliation,
        LayoutObservation? layoutObservation,
        PreservedVisualEvidence? preservedVisual)
    {
        if (text is not null ||
            textOrigin != TextSelectionOrigin.None)
        {
            throw new ArgumentException(
                "Deferred element cannot carry authoritative text.");
        }

        if (nativeBlock is not null ||
            reconciliation is not null ||
            preservedVisual is not null)
        {
            throw new ArgumentException(
                "Deferred element must retain only neutral layout evidence.");
        }

        if (layoutObservation is null)
        {
            throw new ArgumentException(
                "Deferred element requires layout evidence.",
                nameof(layoutObservation));
        }
    }
}
