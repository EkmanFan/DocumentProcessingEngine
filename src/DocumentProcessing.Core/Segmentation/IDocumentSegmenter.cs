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
}
