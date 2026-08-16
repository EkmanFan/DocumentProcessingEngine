using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Visual;

/// <summary>
/// Format-extensible boundary for materializing one exact source visual
/// occurrence into a standalone caller-owned asset stream.
///
/// The contract is neutral: it does not classify semantic visual kind, choose
/// a visual execution action, invoke layout/OCR, or select authoritative
/// document output.
/// </summary>
public interface ISourceVisualAssetMaterializer
{
    bool CanMaterialize(
        DocumentFormatId format);

    ValueTask<SourceVisualAssetMaterialization> MaterializeAsync(
        DocumentSource source,
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        int physicalPageNumber,
        int sourceVisualIndex,
        Stream destination,
        CancellationToken cancellationToken = default);
}
