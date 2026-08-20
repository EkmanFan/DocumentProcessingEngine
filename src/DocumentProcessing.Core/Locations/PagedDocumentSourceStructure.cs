namespace DocumentProcessing.Core.Locations;

/// <summary>
/// Optional complete physical-page structure for a paginated source document.
/// </summary>
/// <remarks>
/// The page collection is exact and contiguous from physical page 1. Keeping
/// this structure optional preserves PDF page-count and empty-page custody
/// without imposing pagination on EPUB, DOCX, or other non-paginated formats.
/// </remarks>
public sealed record PagedDocumentSourceStructure
    : DocumentSourceStructure
{
    #region Properties

    /// <summary>
    /// Gets every physical page in exact source order.
    /// </summary>
    public IReadOnlyList<PagedDocumentPageDescriptor> Pages { get; }

    /// <summary>
    /// Gets the exact number of physical pages in the source.
    /// </summary>
    public int PhysicalPageCount =>
        Pages.Count;

    #endregion

    #region ctor

    /// <summary>
    /// Creates an authoritative physical-page structure.
    /// </summary>
    public PagedDocumentSourceStructure(
        IReadOnlyList<PagedDocumentPageDescriptor> pages)
    {
        ArgumentNullException.ThrowIfNull(
            pages);

        var pageArray =
            pages.ToArray();

        if (pageArray.Length == 0)
        {
            throw new ArgumentException(
                "Paged source structure must contain at least one physical page.",
                nameof(pages));
        }

        if (pageArray.Any(
                page =>
                    page is null))
        {
            throw new ArgumentException(
                "Paged source structure cannot contain null pages.",
                nameof(pages));
        }

        for (var index = 0;
             index < pageArray.Length;
             index++)
        {
            var expectedPhysicalPageNumber =
                index +
                1;

            if (pageArray[index].PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new ArgumentException(
                    "Paged source structure must contain exact physical-page order starting at page 1.",
                    nameof(pages));
            }
        }

        Pages =
            pageArray;
    }

    #endregion
}
