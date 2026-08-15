using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Format-extensible source boundary for deterministic low-level visual
/// raster/geometry observations.
///
/// Implementations measure source evidence only. They must not assign
/// <see cref="VisualEvidenceKind"/>, <see cref="VisualDisposition"/> or an
/// execution route.
/// </summary>
public interface IVisualRasterObservationSource
{
    bool CanObserve(
        DocumentFormatId format);

    ValueTask<IReadOnlyList<PageVisualRasterObservations>> ObserveAsync(
        DocumentSource source,
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        CancellationToken cancellationToken = default);
}
