namespace DocumentProcessing.Core.Hybrid;

/// <summary>
/// One page of the unified hybrid stream.
///
/// Elements are already in deterministic page reading order. Physical page
/// boundaries remain explicit and are not semantic segmentation boundaries.
/// </summary>
public sealed class HybridDocumentPage
{
    public HybridDocumentPage(
        int physicalPageNumber,
        IReadOnlyList<HybridDocumentElement>? elements = null)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
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

        Elements =
            resolved.ToArray();
    }

    public int PhysicalPageNumber { get; }

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
