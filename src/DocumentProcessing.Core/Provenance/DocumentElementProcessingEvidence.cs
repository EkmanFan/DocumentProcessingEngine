using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Portable processing evidence retained for one document element.
/// </summary>
/// <remarks>
/// The documentary element itself lives in the portable result model. This
/// companion record captures how authoritative text was obtained, prepared,
/// reconciled, normalized, or excluded without introducing physical-page or
/// PDF-specific state.
///
/// C2.2 intentionally focuses on text custody. Current raster-specific visual
/// preservation evidence remains in the proven V1 model until it is generalized
/// separately; no existing evidence is removed by this increment.
/// </remarks>
public sealed record DocumentElementProcessingEvidence
{
    #region ctor

    /// <summary>
    /// Creates portable text-processing evidence for one document element.
    /// </summary>
    public DocumentElementProcessingEvidence(
        string elementId,
        DocumentTextSourceKind textSource,
        string? selectedSourceText,
        string? selectedSourceTextSha256,
        int? nativeCandidateSequence,
        int? layoutCandidateSequence,
        string? ocrBackendId,
        string? ocrProfileId,
        TextReconciliationDecision? reconciliationDecision,
        bool? textsEquivalent,
        bool hasReconciliationDivergence,
        TextDehyphenationProvenance? selectedTextPreparation,
        TextDehyphenationProvenance? normalizationDehyphenation,
        bool normalizationChangedText,
        DocumentBlockExclusionReason? exclusionReason,
        bool isResolved,
        LayoutObservationKind? layoutKind = null)
    {
        if (string.IsNullOrWhiteSpace(
                elementId))
        {
            throw new ArgumentException(
                "Element ID cannot be empty.",
                nameof(elementId));
        }

        if ((selectedSourceText is null) !=
            (selectedSourceTextSha256 is null))
        {
            throw new ArgumentException(
                "Selected source text and its hash must either both exist or both be absent.");
        }

        if (selectedSourceText is null &&
            textSource !=
            DocumentTextSourceKind.None)
        {
            throw new ArgumentException(
                "A non-empty text source requires selected source text.",
                nameof(textSource));
        }

        if (selectedSourceText is not null &&
            textSource ==
            DocumentTextSourceKind.None)
        {
            throw new ArgumentException(
                "Selected source text requires a non-empty text source.",
                nameof(textSource));
        }

        if (nativeCandidateSequence is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nativeCandidateSequence));
        }

        if (layoutCandidateSequence is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(layoutCandidateSequence));
        }

        if (layoutCandidateSequence.HasValue !=
            layoutKind.HasValue)
        {
            throw new ArgumentException(
                "Layout candidate sequence and layout kind must either both exist or both be absent.");
        }

        if ((ocrBackendId is null) !=
            (ocrProfileId is null))
        {
            throw new ArgumentException(
                "OCR backend ID and OCR profile ID must either both exist or both be absent.");
        }

        if (ocrBackendId is not null &&
            !layoutCandidateSequence.HasValue)
        {
            throw new ArgumentException(
                "OCR processing evidence must retain its source layout observation.");
        }

        ElementId =
            elementId.Trim();

        TextSource =
            textSource;

        SelectedSourceText =
            selectedSourceText;

        SelectedSourceTextSha256 =
            selectedSourceTextSha256 is null
                ? null
                : NormalizeAndVerifyTextSha256(
                    selectedSourceText!,
                    selectedSourceTextSha256,
                    nameof(selectedSourceTextSha256));

        NativeCandidateSequence =
            nativeCandidateSequence;

        LayoutCandidateSequence =
            layoutCandidateSequence;

        LayoutKind =
            layoutKind;

        OcrBackendId =
            NormalizeOptional(
                ocrBackendId);

        OcrProfileId =
            NormalizeOptional(
                ocrProfileId);

        ReconciliationDecision =
            reconciliationDecision;

        TextsEquivalent =
            textsEquivalent;

        HasReconciliationDivergence =
            hasReconciliationDivergence;

        SelectedTextPreparation =
            selectedTextPreparation;

        NormalizationDehyphenation =
            normalizationDehyphenation;

        NormalizationChangedText =
            normalizationChangedText;

        ExclusionReason =
            exclusionReason;

        IsResolved =
            isResolved;
    }

    #endregion

    #region Properties

    public string ElementId { get; }

    public DocumentTextSourceKind TextSource { get; }

    public string? SelectedSourceText { get; }

    public string? SelectedSourceTextSha256 { get; }

    public int? NativeCandidateSequence { get; }

    public int? LayoutCandidateSequence { get; }

    /// <summary>
    /// Gets the neutral role of the retained source layout observation.
    /// </summary>
    public LayoutObservationKind? LayoutKind { get; }

    public string? OcrBackendId { get; }

    public string? OcrProfileId { get; }

    public TextReconciliationDecision? ReconciliationDecision { get; }

    public bool? TextsEquivalent { get; }

    public bool HasReconciliationDivergence { get; }

    public TextDehyphenationProvenance? SelectedTextPreparation { get; }

    public TextDehyphenationProvenance? NormalizationDehyphenation { get; }

    public bool NormalizationChangedText { get; }

    public DocumentBlockExclusionReason? ExclusionReason { get; }

    public bool IsExcluded =>
        ExclusionReason.HasValue;

    public bool IsResolved { get; }

    #endregion

    #region Methods Validation

    private static string? NormalizeOptional(
        string? value) =>
        string.IsNullOrWhiteSpace(
            value)
            ? null
            : value.Trim();

    private static string NormalizeAndVerifyTextSha256(
        string text,
        string value,
        string parameterName)
    {
        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (normalized.Length != 64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        if (!ProvenanceTextHashing.MatchesUtf8Sha256(
                text,
                normalized))
        {
            throw new ArgumentException(
                "Selected source text SHA-256 does not match the exact UTF-8 text.",
                parameterName);
        }

        return normalized;
    }

    #endregion
}
