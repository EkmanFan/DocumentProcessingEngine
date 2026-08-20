namespace DocumentProcessing.Core.Results;

/// <summary>
/// Non-duplicating quality payload for DocumentIngestionResult.
///
/// Phase 19B intentionally exposed a rich analytical view. The final result
/// keeps only quality evidence that is not already represented authoritatively
/// by its source, element and structural-segment fields.
///
/// V1 currently needs a separate payload only for aggregated OCR confidence.
/// Resolved state, exclusion, origin, divergence, normalization change,
/// preserved-visual presence, mixed origin and unresolved-segment state remain
/// authoritative on the element/segment graph and can be derived without
/// duplicating serialized truth.
/// </summary>
public sealed record DocumentIngestionQualityObservations
{
    public static DocumentIngestionQualityObservations Empty { get; } =
        new(
            Array.Empty<DocumentElementOcrQualityObservation>());

    public DocumentIngestionQualityObservations(
        IReadOnlyList<DocumentElementOcrQualityObservation>
            ocrConfidenceObservations)
    {
        ArgumentNullException.ThrowIfNull(
            ocrConfidenceObservations);

        var observations =
            ocrConfidenceObservations
                .ToArray();

        if (observations.Any(
                observation =>
                    observation is null))
        {
            throw new ArgumentException(
                "OCR quality observations cannot contain null values.",
                nameof(ocrConfidenceObservations));
        }

        if (observations
                .Select(
                    observation =>
                        observation.ElementId)
                .Distinct(
                    StringComparer.Ordinal)
                .Count() !=
            observations.Length)
        {
            throw new ArgumentException(
                "An element can have at most one OCR confidence observation.",
                nameof(ocrConfidenceObservations));
        }

        OcrConfidenceObservations =
            observations;
    }

    public IReadOnlyList<DocumentElementOcrQualityObservation>
        OcrConfidenceObservations { get; }
}
