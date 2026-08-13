using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Core.Segmentation;

public sealed class DocumentSegmentationResult
{
    public DocumentSegmentationResult(
        DocumentTextNormalizationResult sourceNormalization,
        string segmentationProfileId,
        IReadOnlyList<DocumentSegment>? segments = null)
    {
        SourceNormalization =
            sourceNormalization ??
            throw new ArgumentNullException(
                nameof(sourceNormalization));

        if (string.IsNullOrWhiteSpace(
                segmentationProfileId))
        {
            throw new ArgumentException(
                "Segmentation profile identifier cannot be empty.",
                nameof(segmentationProfileId));
        }

        SegmentationProfileId =
            segmentationProfileId.Trim();

        Segments =
            segments ??
            Array.Empty<DocumentSegment>();
    }

    public DocumentTextNormalizationResult SourceNormalization { get; }

    public DocumentFormatId Format =>
        SourceNormalization.Format;

    public string SegmentationProfileId { get; }

    public IReadOnlyList<DocumentSegment> Segments { get; }
}
