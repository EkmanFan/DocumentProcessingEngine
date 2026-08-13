using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Extraction;

public sealed class DocumentExtractionResult
{
    public DocumentExtractionResult(
        DocumentFormatId format,
        IReadOnlyList<DocumentExtractionPage>? pages = null)
    {
        Format = format;
        Pages = pages ?? Array.Empty<DocumentExtractionPage>();
    }

    public DocumentFormatId Format { get; }
    public IReadOnlyList<DocumentExtractionPage> Pages { get; }
}
