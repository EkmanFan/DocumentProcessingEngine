using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Core.Segmentation;

/// <summary>
/// Produces structural document units from normalized content.
/// This boundary does not perform retrieval chunking.
/// </summary>
public interface IDocumentSegmenter
{
    DocumentSegmentationResult Segment(
        DocumentTextNormalizationResult document,
        CancellationToken cancellationToken = default);

    DocumentSegmentationResult Segment(
        DocumentTextNormalizationResult document,
        DocumentSegmentationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            options);

        if (options.HeadingHints.Count > 0)
        {
            throw new NotSupportedException(
                "This document segmenter does not support heading hints.");
        }

        return Segment(
            document,
            cancellationToken);
    }
}
