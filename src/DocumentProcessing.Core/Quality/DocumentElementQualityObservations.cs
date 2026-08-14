using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.Quality;

/// <summary>
/// Neutral, decomposable quality facts for one portable document element.
///
/// This type does not decide whether the element is acceptable for a particular
/// consumer. It records deterministic facts only.
/// </summary>
public sealed record DocumentElementQualityObservations
{
    public DocumentElementQualityObservations(
        string sourceDocumentSha256,
        string elementId,
        string? segmentId,
        HybridDocumentElementKind kind,
        TextSelectionOrigin textOrigin,
        bool hasAuthoritativeText,
        bool isResolved,
        bool isExcluded,
        bool hasReconciliationDivergence,
        bool normalizationChangedText,
        bool hasPreservedVisual,
        bool hasOcrEvidence,
        OcrConfidenceSummary? ocrConfidence)
    {
        SourceDocumentSha256 =
            NormalizeSha256(
                sourceDocumentSha256,
                nameof(sourceDocumentSha256));

        if (string.IsNullOrWhiteSpace(
                elementId))
        {
            throw new ArgumentException(
                "Element quality observation ID cannot be empty.",
                nameof(elementId));
        }

        if (!Enum.IsDefined(
                kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind));
        }

        if (!Enum.IsDefined(
                textOrigin))
        {
            throw new ArgumentOutOfRangeException(
                nameof(textOrigin));
        }

        if (!hasOcrEvidence &&
            ocrConfidence is not null)
        {
            throw new ArgumentException(
                "OCR confidence cannot exist when no OCR evidence is present.",
                nameof(ocrConfidence));
        }

        ElementId =
            elementId.Trim();

        SegmentId =
            string.IsNullOrWhiteSpace(
                segmentId)
                ? null
                : segmentId.Trim();

        Kind = kind;
        TextOrigin = textOrigin;
        HasAuthoritativeText = hasAuthoritativeText;
        IsResolved = isResolved;
        IsExcluded = isExcluded;
        HasReconciliationDivergence =
            hasReconciliationDivergence;
        NormalizationChangedText =
            normalizationChangedText;
        HasPreservedVisual =
            hasPreservedVisual;
        HasOcrEvidence =
            hasOcrEvidence;
        OcrConfidence =
            ocrConfidence;
    }

    public string SourceDocumentSha256 { get; }

    public string ElementId { get; }

    public string? SegmentId { get; }

    public HybridDocumentElementKind Kind { get; }

    public TextSelectionOrigin TextOrigin { get; }

    public bool HasAuthoritativeText { get; }

    public bool IsResolved { get; }

    public bool IsExcluded { get; }

    public bool HasReconciliationDivergence { get; }

    public bool NormalizationChangedText { get; }

    public bool HasPreservedVisual { get; }

    public bool HasOcrEvidence { get; }

    public OcrConfidenceSummary? OcrConfidence { get; }

    public bool HasOcrConfidenceObservations =>
        OcrConfidence is not null;

    private static string NormalizeSha256(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
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
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        return normalized;
    }
}
