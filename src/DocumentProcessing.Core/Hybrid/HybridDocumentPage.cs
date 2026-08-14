using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Hybrid;

/// <summary>
/// One page of the unified hybrid stream.
///
/// Elements are already in deterministic page reading order. Physical page
/// boundaries remain explicit and are not semantic segmentation boundaries.
///
/// ContentViewport preserves page-level geometric provenance inside the
/// canonical page coordinate space.
/// </summary>
public sealed class HybridDocumentPage
{
    private static readonly NormalizedRectangle FullPageViewport =
        new(
            0,
            0,
            1,
            1);

    public HybridDocumentPage(
        int physicalPageNumber,
        IReadOnlyList<HybridDocumentElement>? elements = null)
        : this(
            physicalPageNumber,
            FullPageViewport,
            elements)
    {
    }

    public HybridDocumentPage(
        int physicalPageNumber,
        NormalizedRectangle contentViewport,
        IReadOnlyList<HybridDocumentElement>? elements = null)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (contentViewport.Right -
                contentViewport.Left <= 0 ||
            contentViewport.Bottom -
                contentViewport.Top <= 0)
        {
            throw new ArgumentException(
                "Content viewport must have positive width and height.",
                nameof(contentViewport));
        }

        var resolved =
            elements ??
            Array.Empty<HybridDocumentElement>();

        if (resolved.Any(
                element =>
                    element.PhysicalPageNumber !=
                    physicalPageNumber))
        {
            throw new ArgumentException(
                "All hybrid elements must belong to the page.",
                nameof(elements));
        }

        for (var index = 1;
             index < resolved.Count;
             index++)
        {
            if (resolved[index - 1].ReadingOrder >=
                resolved[index].ReadingOrder)
            {
                throw new ArgumentException(
                    "Hybrid page elements must be in strictly increasing reading order.",
                    nameof(elements));
            }
        }

        PhysicalPageNumber =
            physicalPageNumber;

        ContentViewport =
            contentViewport;

        Elements =
            resolved.ToArray();
    }

    public int PhysicalPageNumber { get; }

    public NormalizedRectangle ContentViewport { get; }

    public IReadOnlyList<HybridDocumentElement> Elements { get; }

    public IReadOnlyList<HybridDocumentElement> AuthoritativeTextElements =>
        Elements
            .Where(
                element =>
                    element.HasAuthoritativeText)
            .ToArray();

    public IReadOnlyList<HybridDocumentElement> VisualElements =>
        Elements
            .Where(
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Visual)
            .ToArray();

    public bool HasUnresolvedEvidence =>
        Elements.Any(
            element =>
                element.Kind is
                    HybridDocumentElementKind.UnresolvedText or
                    HybridDocumentElementKind.Deferred);
}
