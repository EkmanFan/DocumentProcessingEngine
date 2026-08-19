using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Locations;

/// <summary>
/// Identifies a location in a document that has an authoritative physical-page
/// model.
/// </summary>
/// <remarks>
/// The physical page number is one-based. <see cref="Bounds"/> is optional so
/// the same neutral location can describe either an entire physical page or a
/// bounded region within that page.
///
/// This type does not imply that every document format is paginated. Formats
/// such as EPUB must use a different <see cref="DocumentSourceLocation"/>
/// implementation.
/// </remarks>
public sealed record PagedDocumentSourceLocation
    : DocumentSourceLocation
{
    #region ctor

    /// <summary>
    /// Creates a physical-page source location.
    /// </summary>
    /// <param name="physicalPageNumber">
    /// One-based physical page number in the source document.
    /// </param>
    /// <param name="bounds">
    /// Optional normalized region within the physical page.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="physicalPageNumber"/> is not positive.
    /// </exception>
    public PagedDocumentSourceLocation(
        int physicalPageNumber,
        NormalizedRectangle? bounds = null)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be greater than zero.");
        }

        PhysicalPageNumber =
            physicalPageNumber;

        Bounds =
            bounds;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the one-based physical page number.
    /// </summary>
    public int PhysicalPageNumber { get; }

    /// <summary>
    /// Gets the optional normalized region within the physical page.
    /// </summary>
    public NormalizedRectangle? Bounds { get; }

    #endregion
}
