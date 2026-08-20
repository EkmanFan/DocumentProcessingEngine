using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Locations;

/// <summary>
/// Describes one authoritative physical page in a paginated source document.
/// </summary>
/// <remarks>
/// This descriptor preserves page existence and content viewport even when the
/// page contains no processed elements. Element membership and reading order
/// remain authoritative on the root element collection and its locations.
/// </remarks>
public sealed record PagedDocumentPageDescriptor
{
    #region Properties

    /// <summary>
    /// Gets the one-based physical page number.
    /// </summary>
    public int PhysicalPageNumber { get; }

    /// <summary>
    /// Gets the normalized content viewport for the physical page.
    /// </summary>
    public NormalizedRectangle ContentViewport { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one physical-page descriptor.
    /// </summary>
    public PagedDocumentPageDescriptor(
        int physicalPageNumber,
        NormalizedRectangle contentViewport)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (contentViewport.Right -
                contentViewport.Left <=
            0d ||
            contentViewport.Bottom -
                contentViewport.Top <=
            0d)
        {
            throw new ArgumentException(
                "Content viewport must have positive width and height.",
                nameof(contentViewport));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        ContentViewport =
            contentViewport;
    }

    #endregion
}
