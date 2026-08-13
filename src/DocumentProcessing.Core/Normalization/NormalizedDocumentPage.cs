using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Normalization;

/// <summary>
/// Normalized textual projection of one physical source page.
/// </summary>
public sealed class NormalizedDocumentPage
{
    public NormalizedDocumentPage(
        DocumentExtractionPage sourcePage,
        IReadOnlyList<NormalizedDocumentTextBlock>? blocks = null)
    {
        SourcePage =
            sourcePage ??
            throw new ArgumentNullException(nameof(sourcePage));

        Blocks =
            blocks ??
            Array.Empty<NormalizedDocumentTextBlock>();
    }

    public DocumentExtractionPage SourcePage { get; }

    public int PhysicalPageNumber =>
        SourcePage.PhysicalPageNumber;

    public IReadOnlyList<NormalizedDocumentTextBlock> Blocks { get; }
}
