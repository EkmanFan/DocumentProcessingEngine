using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Neutral reconciliation result that retains source evidence and makes
/// selection, divergence, and unresolved states explicit.
/// </summary>
public sealed class TextReconciliationResult
{
    public TextReconciliationResult(
        TextReconciliationInput input,
        TextReconciliationDecision decision,
        TextSelectionOrigin selectedOrigin,
        string? selectedText,
        string? ocrText,
        bool? textsEquivalent,
        ComparableNativeTextExtent? comparableNativeExtent = null,
        TextDehyphenationResult? nativeTextPreparation = null,
        TextDehyphenationResult? ocrTextPreparation = null,
        ComparableNativeTextEvidence? comparableNativeEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(
            input);

        if (!Enum.IsDefined(
                decision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(decision));
        }

        if (!Enum.IsDefined(
                selectedOrigin))
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectedOrigin));
        }

        var normalizedSelectedText =
            string.IsNullOrWhiteSpace(
                selectedText)
                ? null
                : selectedText.Trim();

        var normalizedOcrText =
            string.IsNullOrWhiteSpace(
                ocrText)
                ? null
                : ocrText.Trim();

        if ((selectedOrigin == TextSelectionOrigin.None) !=
            (normalizedSelectedText is null))
        {
            throw new ArgumentException(
                "Selected origin None must correspond to no selected text, and vice versa.");
        }

        if (selectedOrigin == TextSelectionOrigin.Native &&
            !input.HasNativeEvidence)
        {
            throw new ArgumentException(
                "Native selection requires native evidence.",
                nameof(selectedOrigin));
        }

        if (selectedOrigin == TextSelectionOrigin.Ocr &&
            normalizedOcrText is null)
        {
            throw new ArgumentException(
                "OCR selection requires usable OCR text.",
                nameof(selectedOrigin));
        }

        if (textsEquivalent is not null &&
            (!input.HasNativeEvidence ||
             normalizedOcrText is null))
        {
            throw new ArgumentException(
                "Text equivalence can only be reported when both native and OCR text exist.",
                nameof(textsEquivalent));
        }

        if (comparableNativeExtent is not null)
        {
            if (input.NativeBlock is null ||
                input.OcrRegion is null)
            {
                throw new ArgumentException(
                    "Comparable native extent requires both native and OCR evidence.",
                    nameof(comparableNativeExtent));
            }

            if (!ReferenceEquals(
                    comparableNativeExtent.SourceBlock,
                    input.NativeBlock))
            {
                throw new ArgumentException(
                    "Comparable native extent must originate from the input native block.",
                    nameof(comparableNativeExtent));
            }

            if (!ReferenceEquals(
                    comparableNativeExtent.SourceLayoutObservation,
                    input.OcrRegion.SourceLayoutObservation))
            {
                throw new ArgumentException(
                    "Comparable native extent must originate from the input OCR layout observation.",
                    nameof(comparableNativeExtent));
            }
        }

        if (comparableNativeEvidence is not null)
        {
            if (input.ComparableNativeEvidence is null ||
                input.OcrRegion is null)
            {
                throw new ArgumentException(
                    "Comparable native evidence requires target-centric input native/OCR evidence.",
                    nameof(comparableNativeEvidence));
            }

            if (!ReferenceEquals(
                    comparableNativeEvidence,
                    input.ComparableNativeEvidence))
            {
                throw new ArgumentException(
                    "Comparable native evidence must be the exact input evidence object.",
                    nameof(comparableNativeEvidence));
            }

            if (!ReferenceEquals(
                    comparableNativeEvidence.SourceLayoutObservation,
                    input.OcrRegion.SourceLayoutObservation))
            {
                throw new ArgumentException(
                    "Comparable native evidence must originate from the input OCR layout observation.",
                    nameof(comparableNativeEvidence));
            }

            if (comparableNativeExtent is not null &&
                !comparableNativeEvidence.Extents.Any(
                    extent =>
                        ReferenceEquals(
                            extent,
                            comparableNativeExtent)))
            {
                throw new ArgumentException(
                    "Legacy comparable extent must belong to the aggregate native evidence.",
                    nameof(comparableNativeExtent));
            }
        }

        if (nativeTextPreparation is not null &&
            comparableNativeExtent is null &&
            comparableNativeEvidence is null)
        {
            throw new ArgumentException(
                "Prepared native reconciliation text requires comparable native evidence.",
                nameof(nativeTextPreparation));
        }

        if (ocrTextPreparation is not null &&
            input.OcrRegion is null)
        {
            throw new ArgumentException(
                "Prepared OCR reconciliation text requires OCR evidence.",
                nameof(ocrTextPreparation));
        }

        Input =
            input;

        Decision =
            decision;

        SelectedOrigin =
            selectedOrigin;

        SelectedText =
            normalizedSelectedText;

        OcrText =
            normalizedOcrText;

        TextsEquivalent =
            textsEquivalent;

        ComparableNativeExtent =
            comparableNativeExtent;

        ComparableNativeEvidence =
            comparableNativeEvidence;

        NativeTextPreparation =
            nativeTextPreparation;

        OcrTextPreparation =
            ocrTextPreparation;
    }

    public TextReconciliationInput Input { get; }

    public TextReconciliationDecision Decision { get; }

    public TextSelectionOrigin SelectedOrigin { get; }

    /// <summary>
    /// Authoritative V1 selection when reconciliation can resolve the candidate;
    /// null means the ambiguity is intentionally unresolved.
    /// </summary>
    public string? SelectedText { get; }

    /// <summary>
    /// OCR fragments composed for comparison and auditing. The original
    /// OcrRegionResult remains available through Input.
    /// </summary>
    public string? OcrText { get; }

    /// <summary>
    /// Conservative comparison result when both text sources are usable.
    /// Null means no two-source comparison was possible.
    /// </summary>
    public bool? TextsEquivalent { get; }

    /// <summary>
    /// Legacy single-source-block comparable extent. Target-centric aggregate
    /// reconciliation exposes this when the aggregate contains exactly one
    /// extent.
    /// </summary>
    public ComparableNativeTextExtent? ComparableNativeExtent { get; }

    /// <summary>
    /// Complete target-centric native evidence used for reconciliation.
    /// </summary>
    public ComparableNativeTextEvidence? ComparableNativeEvidence { get; }

    public IReadOnlyList<DocumentTextBlock> NativeSourceBlocks
    {
        get
        {
            if (ComparableNativeEvidence is not null)
            {
                return ComparableNativeEvidence.SourceBlocks;
            }

            return Input.NativeBlock is null
                ? []
                : [Input.NativeBlock];
        }
    }

    public TextDehyphenationResult? NativeTextPreparation { get; }

    public TextDehyphenationResult? OcrTextPreparation { get; }

    public bool IsResolved =>
        SelectedText is not null;

    public bool HasDivergence =>
        Decision is
            TextReconciliationDecision.HealthyNativePreferred or
            TextReconciliationDecision.Conflict;
}
