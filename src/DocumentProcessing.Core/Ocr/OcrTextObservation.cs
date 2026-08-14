using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Ocr;

/// <summary>
/// One OCR-recognized text fragment with page-relative spatial provenance.
///
/// This is evidence produced by OCR. It is not automatically authoritative
/// document text; native/OCR reconciliation remains a separate concern.
/// </summary>
public sealed record OcrTextObservation
{
    public OcrTextObservation(
        int physicalPageNumber,
        int sourceLayoutObservationSequence,
        int observationSequence,
        string text,
        double confidence,
        NormalizedRectangle bounds)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be greater than zero.");
        }

        if (sourceLayoutObservationSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceLayoutObservationSequence));
        }

        if (observationSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observationSequence));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "OCR text cannot be empty.",
                nameof(text));
        }

        if (!double.IsFinite(confidence) ||
            confidence < 0d ||
            confidence > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence),
                confidence,
                "OCR confidence must be finite and between zero and one.");
        }

        PhysicalPageNumber = physicalPageNumber;
        SourceLayoutObservationSequence = sourceLayoutObservationSequence;
        ObservationSequence = observationSequence;
        Text = text.Trim();
        Confidence = confidence;
        Bounds = bounds;
    }

    public int PhysicalPageNumber { get; }

    /// <summary>
    /// ObservationSequence of the layout region that authorized this OCR work.
    /// </summary>
    public int SourceLayoutObservationSequence { get; }

    /// <summary>
    /// Backend sequence of this OCR fragment inside the targeted region.
    /// </summary>
    public int ObservationSequence { get; }

    public string Text { get; }

    public double Confidence { get; }

    /// <summary>
    /// OCR fragment bounds normalized to the full source page, not merely to
    /// the cropped region sent to the recognizer.
    /// </summary>
    public NormalizedRectangle Bounds { get; }
}
