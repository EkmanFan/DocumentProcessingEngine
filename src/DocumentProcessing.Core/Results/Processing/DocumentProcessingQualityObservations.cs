namespace DocumentProcessing.Core.Results;

/// <summary>
/// Non-duplicating quality observations retained by the portable processing
/// result.
/// </summary>
/// <remarks>
/// Quality facts already represented authoritatively by the document element,
/// processing-evidence, segment, or visual-asset graph are deliberately not
/// copied here.
///
/// The first portable result needs a separate payload only for aggregated OCR
/// confidence.
/// </remarks>
public sealed record DocumentProcessingQualityObservations
{
    #region Variables and Constants

    /// <summary>
    /// Gets an empty immutable quality-observation collection.
    /// </summary>
    public static DocumentProcessingQualityObservations Empty { get; } =
        new(
            []);

    #endregion

    #region Properties

    /// <summary>
    /// Gets the aggregated OCR-confidence observations.
    /// </summary>
    public IReadOnlyList<DocumentElementOcrQualityObservation>
        OcrConfidenceObservations { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates portable processing-quality observations.
    /// </summary>
    /// <param name="ocrConfidenceObservations">
    /// At most one aggregated OCR-confidence observation per document element.
    /// </param>
    public DocumentProcessingQualityObservations(
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

    #endregion
}
