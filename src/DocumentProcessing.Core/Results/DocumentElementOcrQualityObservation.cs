using DocumentProcessing.Core.Quality;

namespace DocumentProcessing.Core.Results;

/// <summary>
/// Irreducible OCR-confidence quality evidence retained by the final result.
///
/// Other element quality facts from Phase 19B are already authoritative in the
/// element/segment result graph and are deliberately not copied here.
/// </summary>
public sealed record DocumentElementOcrQualityObservation
{
    public DocumentElementOcrQualityObservation(
        string elementId,
        OcrConfidenceSummary confidence)
    {
        if (string.IsNullOrWhiteSpace(
                elementId))
        {
            throw new ArgumentException(
                "OCR quality observation element ID cannot be empty.",
                nameof(elementId));
        }

        ElementId =
            elementId.Trim();

        Confidence =
            confidence ??
            throw new ArgumentNullException(
                nameof(confidence));
    }

    public string ElementId { get; }

    public OcrConfidenceSummary Confidence { get; }
}
