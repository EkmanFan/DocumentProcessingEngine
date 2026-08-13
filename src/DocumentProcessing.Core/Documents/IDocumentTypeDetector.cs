namespace DocumentProcessing.Core.Documents;

public interface IDocumentTypeDetector
{
    ValueTask<DocumentTypeDetectionResult> DetectAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default);
}
