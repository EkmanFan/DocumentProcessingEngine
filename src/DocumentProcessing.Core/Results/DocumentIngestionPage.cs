using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Results;

/// <summary>
/// Portable page structure in a completed document-ingestion result.
///
/// V1 page coordinates use the existing normalized top-left page coordinate
/// space. OrderedElementIds is the authoritative page-to-element membership
/// sequence; element content/provenance remains authoritative in the root
/// Elements collection.
/// </summary>
public sealed record DocumentIngestionPage
{
    public DocumentIngestionPage(
        int physicalPageNumber,
        NormalizedRectangle contentViewport,
        IReadOnlyList<string> orderedElementIds)
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

        ArgumentNullException.ThrowIfNull(
            orderedElementIds);

        if (orderedElementIds.Any(
                string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Page element IDs cannot be empty.",
                nameof(orderedElementIds));
        }

        var normalizedElementIds =
            orderedElementIds
                .Select(
                    value =>
                        value.Trim())
                .ToArray();

        if (normalizedElementIds
                .Distinct(
                    StringComparer.Ordinal)
                .Count() !=
            normalizedElementIds.Length)
        {
            throw new ArgumentException(
                "A page cannot reference the same element more than once.",
                nameof(orderedElementIds));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        ContentViewport =
            contentViewport;

        OrderedElementIds =
            normalizedElementIds;
    }

    /// <summary>
    /// Deterministic document-local page identifier.
    /// </summary>
    public string PageId =>
        $"p{PhysicalPageNumber:D6}";

    public int PhysicalPageNumber { get; }

    public NormalizedRectangle ContentViewport { get; }

    public IReadOnlyList<string> OrderedElementIds { get; }
}
