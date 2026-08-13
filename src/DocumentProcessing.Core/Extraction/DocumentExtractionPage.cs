namespace DocumentProcessing.Core.Extraction;

public sealed class DocumentExtractionPage
{
    public DocumentExtractionPage(int physicalPageNumber, string sourceText)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be greater than zero.");
        }

        PhysicalPageNumber = physicalPageNumber;
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
    }

    public int PhysicalPageNumber { get; }
    public string SourceText { get; }
}
