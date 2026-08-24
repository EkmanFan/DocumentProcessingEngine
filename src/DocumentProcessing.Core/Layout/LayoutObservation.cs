using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Layout;

/// <summary>
/// Neutral spatial evidence produced by document layout analysis.
///
/// Layout observations intentionally do not carry recognized text. A layout
/// backend may internally run OCR, but OCR content is separate evidence and
/// must not silently become document text through this model.
/// </summary>
public sealed record LayoutObservation
{
    public LayoutObservation(
        int physicalPageNumber,
        int observationSequence,
        int? readingOrder,
        LayoutObservationKind kind,
        NormalizedRectangle bounds,
        string? rawLabel = null)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be greater than zero.");
        }

        if (observationSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observationSequence));
        }

        if (readingOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readingOrder));
        }

        PhysicalPageNumber = physicalPageNumber;
        ObservationSequence = observationSequence;
        ReadingOrder = readingOrder;
        Kind = kind;
        Bounds = bounds;
        RawLabel = string.IsNullOrWhiteSpace(rawLabel)
            ? null
            : rawLabel.Trim();
    }

    public int PhysicalPageNumber { get; }

    /// <summary>
    /// Sequence in which the backend emitted the observation.
    /// It is not assumed to be a native document source sequence.
    /// </summary>
    public int ObservationSequence { get; }

    /// <summary>
    /// Derived reading order when the backend provides or guarantees one.
    /// </summary>
    public int? ReadingOrder { get; }

    public LayoutObservationKind Kind { get; }
    public NormalizedRectangle Bounds { get; }

    /// <summary>
    /// Optional backend label retained as opaque diagnostic/provenance
    /// evidence. Engine policy must never branch on this value; functional
    /// decisions use the neutral Kind instead.
    /// </summary>
    public string? RawLabel { get; }
}
