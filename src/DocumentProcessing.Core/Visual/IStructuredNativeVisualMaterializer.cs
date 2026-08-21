using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Visual;

/// <summary>
/// Format-extensible boundary for copying one selected structured-document
/// visual into a consumer-owned destination.
/// </summary>
public interface IStructuredNativeVisualMaterializer
{
    bool CanMaterialize(
        DocumentFormatId format);

    ValueTask<StructuredNativeVisualMaterialization> MaterializeAsync(
        DocumentSource source,
        DocumentFormatId format,
        StructuredNativeVisual visual,
        Stream destination,
        CancellationToken cancellationToken = default);
}
