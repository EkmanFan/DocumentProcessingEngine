using DocumentProcessing.Core.Hybrid.Normalization;

namespace DocumentProcessing.Core.Hybrid.Segmentation;

/// <summary>
/// Structural segmentation result over the unified normalized hybrid stream.
///
/// Every non-excluded authoritative text-flow element must belong to exactly one
/// segment. Non-text evidence may remain unassigned when no text-led structural
/// unit exists for it.
/// </summary>
public sealed class HybridDocumentSegmentationResult
{
    public HybridDocumentSegmentationResult(
        HybridDocumentNormalizationResult sourceNormalization,
        string segmentationProfileId,
        IReadOnlyList<HybridDocumentSegment>? segments = null)
    {
        SourceNormalization =
            sourceNormalization ??
            throw new ArgumentNullException(
                nameof(sourceNormalization));

        if (string.IsNullOrWhiteSpace(
                segmentationProfileId))
        {
            throw new ArgumentException(
                "Segmentation profile identifier cannot be empty.",
                nameof(segmentationProfileId));
        }

        var resolved =
            segments ??
            Array.Empty<HybridDocumentSegment>();

        for (var index = 0;
             index < resolved.Count;
             index++)
        {
            if (resolved[index].Ordinal !=
                index)
            {
                throw new ArgumentException(
                    "Hybrid segment ordinals must be contiguous and zero-based.",
                    nameof(segments));
            }
        }

        if (resolved
            .Select(
                segment =>
                    segment.Id)
            .Distinct(
                StringComparer.Ordinal)
            .Count() !=
            resolved.Count)
        {
            throw new ArgumentException(
                "Hybrid segment identifiers must be unique.",
                nameof(segments));
        }

        var sourceElements =
            SourceNormalization.Pages
                .SelectMany(
                    page =>
                        page.Elements)
                .ToHashSet(
                    ReferenceEqualityComparer.Instance);

        var assignedElements =
            new HashSet<NormalizedHybridDocumentElement>(
                ReferenceEqualityComparer.Instance);

        foreach (var segment in resolved)
        {
            foreach (var element in segment.SourceElements)
            {
                if (!sourceElements.Contains(
                        element))
                {
                    throw new ArgumentException(
                        "Segment source evidence must belong to the source normalization.",
                        nameof(segments));
                }

                if (!assignedElements.Add(
                        element))
                {
                    throw new ArgumentException(
                        "A normalized hybrid element cannot belong to multiple structural segments.",
                        nameof(segments));
                }
            }
        }

        var expectedTextFlow =
            SourceNormalization.Pages
                .SelectMany(
                    page =>
                        page.Elements)
                .Where(
                    element =>
                        element.IsTextFlowElement)
                .ToHashSet(
                    ReferenceEqualityComparer.Instance);

        var actualTextFlow =
            resolved
                .SelectMany(
                    segment =>
                        segment.TextElements)
                .ToHashSet(
                    ReferenceEqualityComparer.Instance);

        if (!expectedTextFlow.SetEquals(
                actualTextFlow))
        {
            throw new ArgumentException(
                "Every non-excluded hybrid text-flow element must be segmented exactly once.",
                nameof(segments));
        }

        SegmentationProfileId =
            segmentationProfileId.Trim();

        Segments =
            resolved.ToArray();
    }

    public HybridDocumentNormalizationResult SourceNormalization { get; }

    public string SegmentationProfileId { get; }

    public IReadOnlyList<HybridDocumentSegment> Segments { get; }

    public bool HasUnresolvedEvidence =>
        SourceNormalization.HasUnresolvedEvidence;

    public bool HasMixedTextOriginSegments =>
        Segments.Any(
            segment =>
                segment.IsMixedTextOrigin);
}
