namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Optional document-format capability for acquiring native evidence from an
/// approved inclusive range of stable ordered content units.
/// </summary>
public interface IContentUnitRangeDocumentFormat : IDocumentFormat
{
    /// <summary>Recognizes the source and acquires evidence only from the requested units.</summary>
    ValueTask<NativeEvidenceExtractionResult> TryExtractNativeEvidenceAsync(
        DocumentSource source,
        ContentUnitRange contentUnitRange,
        CancellationToken cancellationToken = default);
}
