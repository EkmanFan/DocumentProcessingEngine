namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Optional document-format capability for acquiring native evidence from an
/// approved inclusive range of original physical pages.
/// </summary>
public interface IPhysicalPageRangeDocumentFormat : IDocumentFormat
{
    /// <summary>Recognizes the source and acquires evidence only from the requested pages.</summary>
    ValueTask<NativeEvidenceExtractionResult> TryExtractNativeEvidenceAsync(
        DocumentSource source,
        PhysicalPageRange physicalPageRange,
        CancellationToken cancellationToken = default);
}
