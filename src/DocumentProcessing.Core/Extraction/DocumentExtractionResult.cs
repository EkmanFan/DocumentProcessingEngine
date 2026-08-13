using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Extraction;

public sealed class DocumentExtractionResult
{
    public DocumentExtractionResult(DocumentFormatId format)
    {
        Format = format;
    }

    public DocumentFormatId Format { get; }
}
