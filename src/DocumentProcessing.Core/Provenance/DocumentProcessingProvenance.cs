namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Custody-complete portable projection over the completed hybrid document
/// structure.
///
/// Aggregate construction validates the bidirectional element/segment custody
/// graph so a portable result cannot contain dangling or contradictory lineage.
/// </summary>
public sealed record DocumentProcessingProvenance
{
    public DocumentProcessingProvenance(
        DocumentSourceIdentity source,
        DocumentProcessingManifest processingManifest,
        IReadOnlyList<DocumentElementProvenance> elements,
        IReadOnlyList<DocumentSegmentProvenance> segments)
    {
        Source =
            source ??
            throw new ArgumentNullException(nameof(source));

        ProcessingManifest =
            processingManifest ??
            throw new ArgumentNullException(nameof(processingManifest));

        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(segments);

        var elementArray = elements.ToArray();
        var segmentArray = segments.ToArray();

        if (elementArray.Any(
                element =>
                    !string.Equals(
                        element.SourceDocumentSha256,
                        Source.Sha256,
                        StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every element must belong to the declared source document.",
                nameof(elements));
        }

        if (segmentArray.Any(
                segment =>
                    !string.Equals(
                        segment.SourceDocumentSha256,
                        Source.Sha256,
                        StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every segment must belong to the declared source document.",
                nameof(segments));
        }

        var elementsById =
            BuildUniqueElementIndex(
                elementArray);

        var segmentsById =
            BuildUniqueSegmentIndex(
                segmentArray);

        ValidateBidirectionalMembership(
            elementsById,
            segmentsById);

        Elements = elementArray;
        Segments = segmentArray;
    }

    public DocumentSourceIdentity Source { get; }
    public DocumentProcessingManifest ProcessingManifest { get; }
    public IReadOnlyList<DocumentElementProvenance> Elements { get; }
    public IReadOnlyList<DocumentSegmentProvenance> Segments { get; }

    private static IReadOnlyDictionary<
        string,
        DocumentElementProvenance> BuildUniqueElementIndex(
        IReadOnlyList<DocumentElementProvenance> elements)
    {
        var result =
            new Dictionary<
                string,
                DocumentElementProvenance>(
                StringComparer.Ordinal);

        foreach (var element in elements)
        {
            if (!result.TryAdd(
                    element.ElementId,
                    element))
            {
                throw new ArgumentException(
                    $"Duplicate element provenance ID '{element.ElementId}'.",
                    nameof(elements));
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<
        string,
        DocumentSegmentProvenance> BuildUniqueSegmentIndex(
        IReadOnlyList<DocumentSegmentProvenance> segments)
    {
        var result =
            new Dictionary<
                string,
                DocumentSegmentProvenance>(
                StringComparer.Ordinal);

        foreach (var segment in segments)
        {
            if (!result.TryAdd(
                    segment.SegmentId,
                    segment))
            {
                throw new ArgumentException(
                    $"Duplicate segment provenance ID '{segment.SegmentId}'.",
                    nameof(segments));
            }
        }

        return result;
    }

    private static void ValidateBidirectionalMembership(
        IReadOnlyDictionary<
            string,
            DocumentElementProvenance> elementsById,
        IReadOnlyDictionary<
            string,
            DocumentSegmentProvenance> segmentsById)
    {
        foreach (var element in elementsById.Values)
        {
            if (element.SegmentId is not null &&
                !segmentsById.ContainsKey(element.SegmentId))
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' references unknown segment '{element.SegmentId}'.",
                    "elements");
            }
        }

        var memberships =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        foreach (var segment in segmentsById.Values)
        {
            foreach (var elementId in segment.SourceElementIds)
            {
                if (!elementsById.TryGetValue(
                        elementId,
                        out var element))
                {
                    throw new ArgumentException(
                        $"Segment '{segment.SegmentId}' references unknown element '{elementId}'.",
                        "segments");
                }

                if (!string.Equals(
                        element.SegmentId,
                        segment.SegmentId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Segment '{segment.SegmentId}' references element '{elementId}', but that element declares segment '{element.SegmentId ?? "<none>"}'.",
                        "segments");
                }

                if (!memberships.TryAdd(
                        elementId,
                        segment.SegmentId))
                {
                    throw new ArgumentException(
                        $"Element '{elementId}' is referenced by more than one structural segment.",
                        "segments");
                }
            }
        }

        foreach (var element in elementsById.Values)
        {
            if (element.SegmentId is null)
            {
                continue;
            }

            if (!memberships.TryGetValue(
                    element.ElementId,
                    out var membershipSegmentId))
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' declares segment '{element.SegmentId}' but is absent from that segment's source-element membership.",
                    "elements");
            }

            if (!string.Equals(
                    membershipSegmentId,
                    element.SegmentId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' has contradictory segment membership.",
                    "elements");
            }
        }
    }
}
