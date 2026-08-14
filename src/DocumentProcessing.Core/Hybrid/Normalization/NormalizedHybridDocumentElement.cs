using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Core.Hybrid.Normalization;

/// <summary>
/// Deterministic normalized projection of one unified hybrid element.
///
/// The original hybrid element remains the source of truth for provenance.
/// Normalization may change authoritative text and may mark recurring margins
/// as excluded, but it never turns visual/deferred/unresolved evidence into
/// text.
/// </summary>
public sealed class NormalizedHybridDocumentElement
{
    public NormalizedHybridDocumentElement(
        HybridDocumentElement sourceElement,
        string? text,
        DocumentBlockExclusionReason? exclusionReason = null,
        TextDehyphenationResult? normalizationDehyphenation = null)
    {
        SourceElement =
            sourceElement ??
            throw new ArgumentNullException(
                nameof(sourceElement));

        var normalizedText =
            string.IsNullOrWhiteSpace(
                text)
                ? null
                : text.Trim();

        if (SourceElement.HasAuthoritativeText)
        {
            if (normalizedText is null)
            {
                throw new ArgumentException(
                    "Authoritative source element requires normalized text.",
                    nameof(text));
            }
        }
        else
        {
            if (normalizedText is not null)
            {
                throw new ArgumentException(
                    "Non-authoritative hybrid evidence cannot acquire normalized text.",
                    nameof(text));
            }

            if (exclusionReason is not null)
            {
                throw new ArgumentException(
                    "Non-authoritative hybrid evidence cannot receive a text-flow exclusion.",
                    nameof(exclusionReason));
            }

            if (normalizationDehyphenation is not null)
            {
                throw new ArgumentException(
                    "Non-authoritative hybrid evidence cannot receive text dehyphenation.",
                    nameof(normalizationDehyphenation));
            }
        }

        if (normalizationDehyphenation is not null)
        {
            if (!normalizationDehyphenation.Changed)
            {
                throw new ArgumentException(
                    "Normalization dehyphenation evidence is retained only when it changed text.",
                    nameof(normalizationDehyphenation));
            }

            if (SourceElement.TextOrigin !=
                    TextSelectionOrigin.Ocr ||
                SourceElement.Reconciliation
                    ?.Input.OcrRegion is null)
            {
                throw new ArgumentException(
                    "Normalization dehyphenation requires explicit OCR-region provenance.",
                    nameof(normalizationDehyphenation));
            }
        }

        Text =
            normalizedText;

        ExclusionReason =
            exclusionReason;

        NormalizationDehyphenation =
            normalizationDehyphenation;
    }

    public HybridDocumentElement SourceElement { get; }

    public int PhysicalPageNumber =>
        SourceElement.PhysicalPageNumber;

    public int ReadingOrder =>
        SourceElement.ReadingOrder;

    public HybridDocumentElementKind Kind =>
        SourceElement.Kind;

    public NormalizedRectangle Bounds =>
        SourceElement.Bounds;

    public string? SourceText =>
        SourceElement.Text;

    /// <summary>
    /// Normalized authoritative text. Null for Visual, UnresolvedText and
    /// Deferred source elements.
    /// </summary>
    public string? Text { get; }

    public TextSelectionOrigin TextOrigin =>
        SourceElement.TextOrigin;

    public DocumentTextBlock? NativeBlock =>
        SourceElement.NativeBlock;

    public LayoutObservation? LayoutObservation =>
        SourceElement.LayoutObservation;

    public TextReconciliationResult? Reconciliation =>
        SourceElement.Reconciliation;

    public PreservedVisualEvidence? PreservedVisual =>
        SourceElement.PreservedVisual;

    /// <summary>
    /// OCR-boundary dehyphenation introduced specifically by hybrid
    /// normalization when the selected OcrOnly text had not already been
    /// prepared by reconciliation.
    /// </summary>
    public TextDehyphenationResult? NormalizationDehyphenation { get; }

    public DocumentBlockExclusionReason? ExclusionReason { get; }

    public bool HasAuthoritativeText =>
        SourceElement.HasAuthoritativeText &&
        Text is not null;

    public bool IsExcluded =>
        ExclusionReason.HasValue;

    public bool IsTextFlowElement =>
        HasAuthoritativeText &&
        !IsExcluded;

    public bool IsResolved =>
        SourceElement.IsResolved;
}
