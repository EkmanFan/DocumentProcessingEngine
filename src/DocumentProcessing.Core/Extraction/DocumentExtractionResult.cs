using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Extraction;

public sealed class DocumentExtractionResult
{
    public DocumentExtractionResult(
        DocumentFormatId format,
        IReadOnlyList<DocumentExtractionPage>? pages = null,
        int? sourcePhysicalPageCount = null)
    {
        var resolvedPages =
            pages ??
            Array.Empty<DocumentExtractionPage>();

        var minimumSourcePhysicalPageCount =
            resolvedPages.Count == 0
                ? 0
                : resolvedPages.Max(
                    page =>
                        page.PhysicalPageNumber);

        var resolvedSourcePhysicalPageCount =
            sourcePhysicalPageCount ??
            minimumSourcePhysicalPageCount;

        if (resolvedSourcePhysicalPageCount <
            minimumSourcePhysicalPageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourcePhysicalPageCount),
                sourcePhysicalPageCount,
                "Source physical-page count cannot be smaller than a retained page number.");
        }

        Format = format;
        Pages = resolvedPages;
        SourcePhysicalPageCount =
            resolvedSourcePhysicalPageCount;
    }

    public DocumentFormatId Format { get; }
    public IReadOnlyList<DocumentExtractionPage> Pages { get; }

    /// <summary>
    /// Gets the total physical-page count of the source document, including
    /// pages outside the processed selection.
    /// </summary>
    public int SourcePhysicalPageCount { get; }
}
