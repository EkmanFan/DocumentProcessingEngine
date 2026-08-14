namespace DocumentProcessing.Core.Hybrid.Normalization;

/// <summary>
/// Normalized projection of one already-assembled physical page.
///
/// Physical page remains provenance. It is not a semantic segmentation
/// boundary.
/// </summary>
public sealed class NormalizedHybridDocumentPage
{
    public NormalizedHybridDocumentPage(
        HybridDocumentPage sourcePage,
        IReadOnlyList<NormalizedHybridDocumentElement>? elements = null)
    {
        SourcePage =
            sourcePage ??
            throw new ArgumentNullException(
                nameof(sourcePage));

        var resolved =
            elements ??
            Array.Empty<NormalizedHybridDocumentElement>();

        if (resolved.Count !=
            SourcePage.Elements.Count)
        {
            throw new ArgumentException(
                "Normalized hybrid page must preserve every source element exactly once.",
                nameof(elements));
        }

        for (var index = 0;
             index < resolved.Count;
             index++)
        {
            if (!ReferenceEquals(
                    resolved[index].SourceElement,
                    SourcePage.Elements[index]))
            {
                throw new ArgumentException(
                    "Normalized hybrid page must preserve source-element identity and order.",
                    nameof(elements));
            }
        }

        Elements =
            resolved.ToArray();
    }

    public HybridDocumentPage SourcePage { get; }

    public int PhysicalPageNumber =>
        SourcePage.PhysicalPageNumber;

    public IReadOnlyList<NormalizedHybridDocumentElement> Elements { get; }

    public IReadOnlyList<NormalizedHybridDocumentElement> TextFlowElements =>
        Elements
            .Where(
                element =>
                    element.IsTextFlowElement)
            .ToArray();

    public IReadOnlyList<NormalizedHybridDocumentElement> VisualElements =>
        Elements
            .Where(
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Visual)
            .ToArray();

    public bool HasUnresolvedEvidence =>
        SourcePage.HasUnresolvedEvidence;
}
