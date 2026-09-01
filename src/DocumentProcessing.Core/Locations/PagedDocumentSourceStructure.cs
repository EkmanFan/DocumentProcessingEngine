namespace DocumentProcessing.Core.Locations;

/// <summary>
/// Optional processed physical-page structure for a paginated source document.
/// </summary>
/// <remarks>
/// The page collection is exact and contiguous for the processed selection.
/// <see cref="SourcePhysicalPageCount"/> preserves the complete source size so
/// a partial processing result never masquerades as a shorter source document.
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
    public int SourcePhysicalPageCount { get; }

    /// <summary>Gets the number of physical pages retained in this result.</summary>
    public int ProcessedPhysicalPageCount =>
        Pages.Count;

    /// <summary>
    /// Backward-compatible alias for the complete source physical-page count.
    /// </summary>
    public int PhysicalPageCount =>
        SourcePhysicalPageCount;

    #endregion

    #region ctor

    /// <summary>
    /// Creates an authoritative physical-page structure.
    /// </summary>
    public PagedDocumentSourceStructure(
        IReadOnlyList<PagedDocumentPageDescriptor> pages)
        : this(
            ResolveSourcePhysicalPageCount(
                pages),
            pages)
    {
    }

    /// <summary>
    /// Creates an authoritative processed-page structure while retaining the
    /// complete physical-page count of the source document.
    /// </summary>
    public PagedDocumentSourceStructure(
        int sourcePhysicalPageCount,
        IReadOnlyList<PagedDocumentPageDescriptor> pages)
    {
        ArgumentNullException.ThrowIfNull(
            pages);

        if (sourcePhysicalPageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourcePhysicalPageCount),
                sourcePhysicalPageCount,
                "Source physical-page count must be positive.");
        }

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

        var firstPhysicalPageNumber =
            pageArray[0]
                .PhysicalPageNumber;

        for (var index = 0;
             index < pageArray.Length;
             index++)
        {
            var expectedPhysicalPageNumber =
                firstPhysicalPageNumber +
                index;

            if (pageArray[index].PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new ArgumentException(
                    "Paged source structure must contain a contiguous processed physical-page selection.",
                    nameof(pages));
            }
        }

        if (pageArray[^1].PhysicalPageNumber >
            sourcePhysicalPageCount)
        {
            throw new ArgumentException(
                "Processed physical pages cannot exceed the source physical-page count.",
                nameof(pages));
        }

        Pages =
            pageArray;

        SourcePhysicalPageCount =
            sourcePhysicalPageCount;
    }

    private static int ResolveSourcePhysicalPageCount(
        IReadOnlyList<PagedDocumentPageDescriptor>? pages)
    {
        ArgumentNullException.ThrowIfNull(
            pages);

        return pages.Count == 0
            ? 1
            : pages.Max(
                page =>
                    page.PhysicalPageNumber);
    }

    #endregion
}
