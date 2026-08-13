using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Extraction;

public interface IDocumentExtractor
{
    bool CanExtract(DocumentFormatId format);

    ValueTask<DocumentExtractionResult> ExtractAsync(
        DocumentSource source,
        DocumentFormatId format,
        CancellationToken cancellationToken = default);
}
