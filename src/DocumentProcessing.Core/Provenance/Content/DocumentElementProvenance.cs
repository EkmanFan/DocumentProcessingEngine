using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Portable custody projection for one normalized hybrid element.
///
/// Backend-specific raw payloads and raw layout labels are deliberately absent.
/// Text/hash consistency is enforced by the model itself rather than relying
/// only on the builder that created it.
/// </summary>
public sealed record DocumentElementProvenance
{
    public DocumentElementProvenance(
        string sourceDocumentSha256,
        string elementId,
        int physicalPageNumber,
        int readingOrder,
        HybridDocumentElementKind kind,
        NormalizedRectangle bounds,
        string? segmentId,
        string? selectedSourceText,
        string? selectedSourceTextSha256,
        string? normalizedText,
        string? normalizedTextSha256,
        TextSelectionOrigin textOrigin,
        int? nativeBlockSourceSequence,
        int? layoutObservationSequence,
        LayoutObservationKind? layoutKind,
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
        PreservedVisualProvenance? preservedVisual)
    {
        SourceDocumentSha256 =
            NormalizeSha256(
                sourceDocumentSha256,
                nameof(sourceDocumentSha256));

        if (string.IsNullOrWhiteSpace(
                elementId))
        {
            throw new ArgumentException(
                "Element ID cannot be empty.",
                nameof(elementId));
        }

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

        if ((selectedSourceText is null) !=
            (selectedSourceTextSha256 is null))
        {
            throw new ArgumentException(
                "Selected source text and its hash must either both exist or both be absent.");
        }

        if ((normalizedText is null) !=
            (normalizedTextSha256 is null))
        {
            throw new ArgumentException(
                "Normalized text and its hash must either both exist or both be absent.");
        }

        if ((selectedSourceText is null) !=
            (normalizedText is null))
        {
            throw new ArgumentException(
                "Selected and normalized authoritative text must either both exist or both be absent.");
        }

        ElementId =
            elementId.Trim();

        PhysicalPageNumber = physicalPageNumber;
        ReadingOrder = readingOrder;
        Kind = kind;
        Bounds = bounds;

        SegmentId =
            string.IsNullOrWhiteSpace(
                segmentId)
                ? null
                : segmentId.Trim();

        SelectedSourceText =
            selectedSourceText;

        SelectedSourceTextSha256 =
            selectedSourceTextSha256 is null
                ? null
                : NormalizeAndVerifyTextSha256(
                    selectedSourceText!,
                    selectedSourceTextSha256,
                    nameof(selectedSourceTextSha256),
                    "Selected source text");

        NormalizedText =
            normalizedText;

        NormalizedTextSha256 =
            normalizedTextSha256 is null
                ? null
                : NormalizeAndVerifyTextSha256(
                    normalizedText!,
                    normalizedTextSha256,
                    nameof(normalizedTextSha256),
                    "Normalized text");

        var actualNormalizationChangedText =
            selectedSourceText is not null &&
            normalizedText is not null &&
            !string.Equals(
                selectedSourceText,
                normalizedText,
                StringComparison.Ordinal);

        if (normalizationChangedText !=
            actualNormalizationChangedText)
        {
            throw new ArgumentException(
                "NormalizationChangedText must match the exact selected-source/final-text comparison.",
                nameof(normalizationChangedText));
        }

        TextOrigin = textOrigin;
        NativeBlockSourceSequence = nativeBlockSourceSequence;
        LayoutObservationSequence = layoutObservationSequence;
        LayoutKind = layoutKind;
        OcrBackendId = NormalizeOptional(ocrBackendId);
        OcrProfileId = NormalizeOptional(ocrProfileId);
        ReconciliationDecision = reconciliationDecision;
        TextsEquivalent = textsEquivalent;
        HasReconciliationDivergence = hasReconciliationDivergence;
        SelectedTextPreparation = selectedTextPreparation;
        NormalizationDehyphenation = normalizationDehyphenation;
        NormalizationChangedText = normalizationChangedText;
        ExclusionReason = exclusionReason;
        IsResolved = isResolved;
        PreservedVisual = preservedVisual;
    }

    public string SourceDocumentSha256 { get; }
    public string ElementId { get; }
    public int PhysicalPageNumber { get; }
    public int ReadingOrder { get; }
    public HybridDocumentElementKind Kind { get; }
    public NormalizedRectangle Bounds { get; }
    public string? SegmentId { get; }
    public string? SelectedSourceText { get; }
    public string? SelectedSourceTextSha256 { get; }
    public string? NormalizedText { get; }
    public string? NormalizedTextSha256 { get; }
    public TextSelectionOrigin TextOrigin { get; }
    public int? NativeBlockSourceSequence { get; }
    public int? LayoutObservationSequence { get; }
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
    public bool IsExcluded => ExclusionReason.HasValue;
    public bool IsResolved { get; }
    public PreservedVisualProvenance? PreservedVisual { get; }

    private static string? NormalizeOptional(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static string NormalizeAndVerifyTextSha256(
        string text,
        string value,
        string parameterName,
        string description)
    {
        var normalized =
            NormalizeSha256(
                value,
                parameterName);

        if (!ProvenanceTextHashing.MatchesUtf8Sha256(
                text,
                normalized))
        {
            throw new ArgumentException(
                $"{description} SHA-256 does not match the exact UTF-8 text.",
                parameterName);
        }

        return normalized;
    }

    private static string NormalizeSha256(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "SHA-256 value cannot be empty.",
                parameterName);
        }

        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (normalized.Length != 64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        return normalized;
    }
}
