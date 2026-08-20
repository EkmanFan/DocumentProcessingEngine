using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Optional format-neutral capability for adapters that can acquire native
/// extraction and low-level raster observations from the same physical
/// document traversal.
///
/// Implementations MUST keep native extraction authoritative:
///
/// - native extraction failures propagate;
/// - ordinary raster-observation failures are returned as non-authoritative
///   acquisition-failure evidence;
/// - caller cancellation propagates;
/// - <see cref="OutOfMemoryException"/> propagates.
///
/// The ordinary <see cref="IDocumentExtractor"/> and
/// <see cref="IVisualRasterObservationSource"/> contracts remain the fallback.
/// </summary>
public interface IDocumentExtractorWithRasterObservations
    : IDocumentExtractor
{
    bool CanExtractWithRasterObservations(
        DocumentFormatId format,
        IVisualRasterObservationSource rasterObservationSource);

    ValueTask<DocumentExtractionWithRasterObservationsResult>
        ExtractWithRasterObservationsAsync(
            DocumentSource source,
            DocumentFormatId format,
            IVisualRasterObservationSource rasterObservationSource,
            CancellationToken cancellationToken = default);
}
