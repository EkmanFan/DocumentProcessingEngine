using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Processing;

namespace DocumentProcessing.Core.Ocr;

/// <summary>
/// OCR evidence for one layout-authorized page region.
/// </summary>
public sealed class OcrRegionResult
{
    public OcrRegionResult(
        string backendId,
        string profileId,
        LayoutObservation sourceLayoutObservation,
        IReadOnlyList<OcrTextObservation>? textObservations = null)
    {
        if (string.IsNullOrWhiteSpace(backendId))
        {
            throw new ArgumentException(
                "OCR backend ID cannot be empty.",
                nameof(backendId));
        }

        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new ArgumentException(
                "OCR profile ID cannot be empty.",
                nameof(profileId));
        }

        ArgumentNullException.ThrowIfNull(sourceLayoutObservation);

        var resolvedObservations =
            textObservations ??
            Array.Empty<OcrTextObservation>();

        if (resolvedObservations.Any(
                observation =>
                    observation.PhysicalPageNumber !=
                    sourceLayoutObservation.PhysicalPageNumber))
        {
            throw new ArgumentException(
                "All OCR observations must belong to the source layout page.",
                nameof(textObservations));
        }

        if (resolvedObservations.Any(
                observation =>
                    observation.SourceLayoutObservationSequence !=
                    sourceLayoutObservation.ObservationSequence))
        {
            throw new ArgumentException(
                "All OCR observations must reference the source layout region.",
                nameof(textObservations));
        }

        BackendId = backendId.Trim();
        ProfileId = profileId.Trim();
        SourceLayoutObservation = sourceLayoutObservation;
        TextObservations = resolvedObservations.ToArray();
    }

    /// <summary>
    /// Stable DPEngine capability represented by this result.
    /// </summary>
    public ProcessingCapability Capability =>
        ProcessingCapability.TextRecognition;

    /// <summary>
    /// Concrete software/backend that produced this evidence.
    /// Provenance only: Engine decisions must not branch on this value.
    /// </summary>
    public string BackendId { get; }

    /// <summary>
    /// Application-supplied, versioned processing profile identifying the
    /// configured OCR service/model combination used for this result.
    /// Provenance/reproducibility only: Engine decisions must not branch on
    /// this value.
    /// </summary>
    public string ProfileId { get; }

    public LayoutObservation SourceLayoutObservation { get; }

    public IReadOnlyList<OcrTextObservation> TextObservations { get; }
}
