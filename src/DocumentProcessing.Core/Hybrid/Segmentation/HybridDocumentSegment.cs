using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.Hybrid.Segmentation;

/// <summary>
/// Stable structural unit over the unified normalized hybrid stream.
///
/// SourceElements retain physical-page, native/OCR, layout, reconciliation and
/// visual/deferred provenance. Segment Text is composed only from text-flow
/// elements; non-text evidence can still belong to the structural unit without
/// becoming narrative text.
/// </summary>
public sealed class HybridDocumentSegment
{
    public HybridDocumentSegment(
        string id,
        int ordinal,
        IReadOnlyList<NormalizedHybridDocumentElement> sourceElements,
        string? headingText = null)
    {
        if (string.IsNullOrWhiteSpace(
                id))
        {
            throw new ArgumentException(
                "Segment identifier cannot be empty.",
                nameof(id));
        }

        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ordinal));
        }

        ArgumentNullException.ThrowIfNull(
            sourceElements);

        if (sourceElements.Count == 0)
        {
            throw new ArgumentException(
                "A hybrid segment must retain at least one source element.",
                nameof(sourceElements));
        }

        if (sourceElements.Any(
                element =>
                    element.IsExcluded))
        {
            throw new ArgumentException(
                "Excluded text-flow evidence cannot enter a structural segment.",
                nameof(sourceElements));
        }

        ValidateSourceOrder(
            sourceElements);

        var textElements =
            sourceElements
                .Where(
                    element =>
                        element.IsTextFlowElement)
                .ToArray();

        if (textElements.Length == 0)
        {
            throw new ArgumentException(
                "A hybrid structural segment requires at least one text-flow element.",
                nameof(sourceElements));
        }

        var normalizedHeading =
            string.IsNullOrWhiteSpace(
                headingText)
                ? null
                : headingText.Trim();

        if (normalizedHeading is not null &&
            !string.Equals(
                textElements[0].Text,
                normalizedHeading,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Heading text must be the first text-flow element in the segment.",
                nameof(headingText));
        }

        Id =
            id.Trim();

        Ordinal =
            ordinal;

        SourceElements =
            sourceElements.ToArray();

        TextElements =
            textElements;

        FirstPhysicalPageNumber =
            SourceElements[0]
                .PhysicalPageNumber;

        LastPhysicalPageNumber =
            SourceElements[^1]
                .PhysicalPageNumber;

        HeadingText =
            normalizedHeading;

        Text =
            string.Join(
                "\n\n",
                TextElements.Select(
                    element =>
                        element.Text!));

        TextOrigins =
            TextElements
                .Select(
                    element =>
                        element.TextOrigin)
                .Where(
                    origin =>
                        origin !=
                        TextSelectionOrigin.None)
                .Distinct()
                .ToArray();
    }

    /// <summary>
    /// Deterministic identifier scoped to this segmentation result.
    /// </summary>
    public string Id { get; }

    public int Ordinal { get; }

    public int FirstPhysicalPageNumber { get; }

    public int LastPhysicalPageNumber { get; }

    public string? HeadingText { get; }

    public string Text { get; }

    /// <summary>
    /// All non-excluded hybrid evidence assigned to this structural unit.
    /// May include Visual, Deferred and UnresolvedText elements.
    /// </summary>
    public IReadOnlyList<NormalizedHybridDocumentElement> SourceElements { get; }

    /// <summary>
    /// Ordered authoritative text elements contributing to Segment.Text.
    /// </summary>
    public IReadOnlyList<NormalizedHybridDocumentElement> TextElements { get; }

    public IReadOnlyList<TextSelectionOrigin> TextOrigins { get; }

    public bool IsMixedTextOrigin =>
        TextOrigins.Count > 1;

    public IReadOnlyList<NormalizedHybridDocumentElement> VisualElements =>
        SourceElements
            .Where(
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Visual)
            .ToArray();

    public bool HasUnresolvedEvidence =>
        SourceElements.Any(
            element =>
                element.Kind is
                    HybridDocumentElementKind.UnresolvedText or
                    HybridDocumentElementKind.Deferred);

    private static void ValidateSourceOrder(
        IReadOnlyList<NormalizedHybridDocumentElement> sourceElements)
    {
        for (var index = 1;
             index < sourceElements.Count;
             index++)
        {
            var previous =
                sourceElements[index - 1];

            var current =
                sourceElements[index];

            if (current.PhysicalPageNumber <
                previous.PhysicalPageNumber)
            {
                throw new ArgumentException(
                    "Hybrid segment source elements must preserve physical-page order.",
                    nameof(sourceElements));
            }

            if (current.PhysicalPageNumber ==
                    previous.PhysicalPageNumber &&
                current.ReadingOrder <=
                    previous.ReadingOrder)
            {
                throw new ArgumentException(
                    "Hybrid segment source elements must preserve strict page-local reading order.",
                    nameof(sourceElements));
            }
        }
    }
}
