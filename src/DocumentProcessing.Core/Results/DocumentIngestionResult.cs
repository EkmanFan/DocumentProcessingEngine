using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.Results;

/// <summary>
/// Canonical strongly typed portable result returned by the document-processing
/// engine.
///
/// This is a deliberate public boundary over the already-proven Phase 19
/// information model. It does not expose the hybrid runtime graph, backend wire
/// payloads, retrieval chunks, embeddings or consumer-specific semantics.
///
/// V1 reuses the Phase 19 portable element/segment provenance records as the
/// authoritative documentary element/segment representation rather than
/// creating a second copy of the same facts.
/// </summary>
public sealed record DocumentIngestionResult
{
    public const string SchemaVersionId =
        "document-ingestion-result-v1";

    public DocumentIngestionResult(
        DocumentSourceIdentity source,
        DocumentProcessingManifest processingManifest,
        IReadOnlyList<DocumentIngestionPage> pages,
        IReadOnlyList<DocumentElementProvenance> elements,
        IReadOnlyList<DocumentSegmentProvenance> structuralSegments,
        DocumentIngestionQualityObservations qualityObservations)
    {
        Source =
            source ??
            throw new ArgumentNullException(
                nameof(source));

        ProcessingManifest =
            processingManifest ??
            throw new ArgumentNullException(
                nameof(processingManifest));

        ArgumentNullException.ThrowIfNull(
            pages);

        ArgumentNullException.ThrowIfNull(
            elements);

        ArgumentNullException.ThrowIfNull(
            structuralSegments);

        QualityObservations =
            qualityObservations ??
            throw new ArgumentNullException(
                nameof(qualityObservations));

        var pageArray =
            pages.ToArray();

        var elementArray =
            elements.ToArray();

        var segmentArray =
            structuralSegments.ToArray();

        if (pageArray.Any(
                page =>
                    page is null))
        {
            throw new ArgumentException(
                "Pages cannot contain null values.",
                nameof(pages));
        }

        if (elementArray.Any(
                element =>
                    element is null))
        {
            throw new ArgumentException(
                "Elements cannot contain null values.",
                nameof(elements));
        }

        if (segmentArray.Any(
                segment =>
                    segment is null))
        {
            throw new ArgumentException(
                "Structural segments cannot contain null values.",
                nameof(structuralSegments));
        }

        // Reuse the Phase 19 custody aggregate as the invariant authority for:
        // - source SHA consistency;
        // - unique element/segment IDs;
        // - bidirectional element/segment membership.
        _ =
            new DocumentProcessingProvenance(
                Source,
                ProcessingManifest,
                elementArray,
                segmentArray);

        var elementsById =
            elementArray.ToDictionary(
                element =>
                    element.ElementId,
                StringComparer.Ordinal);

        ValidatePages(
            Source,
            pageArray,
            elementArray);

        ValidateSegmentPageSpans(
            segmentArray,
            elementsById);

        ValidateObservedProcessingIdentities(
            ProcessingManifest,
            elementArray);

        ValidateQualityObservations(
            QualityObservations,
            elementsById);

        Pages =
            pageArray;

        Elements =
            elementArray;

        StructuralSegments =
            segmentArray;
    }

    /// <summary>
    /// Portable contract schema identifier, distinct from EngineVersion in the
    /// processing manifest.
    /// </summary>
    public string SchemaVersion =>
        SchemaVersionId;

    public DocumentSourceIdentity Source { get; }

    public DocumentProcessingManifest ProcessingManifest { get; }

    public IReadOnlyList<DocumentIngestionPage> Pages { get; }

    /// <summary>
    /// Authoritative final element content + custody representation.
    /// </summary>
    public IReadOnlyList<DocumentElementProvenance> Elements { get; }

    /// <summary>
    /// Authoritative structural-segment content + custody representation.
    /// </summary>
    public IReadOnlyList<DocumentSegmentProvenance> StructuralSegments { get; }

    /// <summary>
    /// Quality evidence not already represented authoritatively by the element
    /// and segment graph.
    /// </summary>
    public DocumentIngestionQualityObservations QualityObservations { get; }

    private static void ValidatePages(
        DocumentSourceIdentity source,
        IReadOnlyList<DocumentIngestionPage> pages,
        IReadOnlyList<DocumentElementProvenance> elements)
    {
        if (pages.Count !=
            source.PhysicalPageCount)
        {
            throw new ArgumentException(
                "Result pages must contain exactly one entry for every physical source page.",
                nameof(pages));
        }

        for (var index = 0;
             index <
             pages.Count;
             index++)
        {
            var expectedPhysicalPageNumber =
                index +
                1;

            if (pages[index].PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new ArgumentException(
                    "Result pages must be in exact physical-page order starting at page 1.",
                    nameof(pages));
            }
        }

        if (elements.Any(
                element =>
                    element.PhysicalPageNumber >
                    source.PhysicalPageCount))
        {
            throw new ArgumentException(
                "An element references a physical page outside the source document.",
                nameof(elements));
        }

        foreach (var page in
                 pages)
        {
            var pageElements =
                elements
                    .Where(
                        element =>
                            element.PhysicalPageNumber ==
                            page.PhysicalPageNumber)
                    .OrderBy(
                        element =>
                            element.ReadingOrder)
                    .ToArray();

            if (pageElements
                    .Select(
                        element =>
                            element.ReadingOrder)
                    .Distinct()
                    .Count() !=
                pageElements.Length)
            {
                throw new ArgumentException(
                    $"Physical page {page.PhysicalPageNumber} contains duplicate reading-order values.",
                    nameof(elements));
            }

            var expectedElementIds =
                pageElements
                    .Select(
                        element =>
                            element.ElementId)
                    .ToArray();

            if (!page.OrderedElementIds.SequenceEqual(
                    expectedElementIds,
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Physical page {page.PhysicalPageNumber} ordered element membership does not match the authoritative element collection.",
                    nameof(pages));
            }
        }
    }

    private static void ValidateSegmentPageSpans(
        IReadOnlyList<DocumentSegmentProvenance> segments,
        IReadOnlyDictionary<string, DocumentElementProvenance> elementsById)
    {
        foreach (var segment in
                 segments)
        {
            var sourcePages =
                segment.SourceElementIds
                    .Select(
                        elementId =>
                            elementsById[elementId]
                                .PhysicalPageNumber)
                    .ToArray();

            var expectedFirstPage =
                sourcePages.Min();

            var expectedLastPage =
                sourcePages.Max();

            if (segment.FirstPhysicalPageNumber !=
                    expectedFirstPage ||
                segment.LastPhysicalPageNumber !=
                    expectedLastPage)
            {
                throw new ArgumentException(
                    $"Structural segment '{segment.SegmentId}' page span does not match its source-element membership.",
                    nameof(segments));
            }
        }
    }

    private static void ValidateObservedProcessingIdentities(
        DocumentProcessingManifest manifest,
        IReadOnlyList<DocumentElementProvenance> elements)
    {
        foreach (var element in
                 elements)
        {
            if ((element.OcrBackendId is null) !=
                (element.OcrProfileId is null))
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' must retain OCR backend/profile identity together.",
                    nameof(elements));
            }

            if (element.OcrBackendId is not null)
            {
                var observedOcrIdentity =
                    new ProcessingComponentIdentity(
                        element.OcrBackendId,
                        element.OcrProfileId!);

                if (!manifest.Ocr.Contains(
                        observedOcrIdentity))
                {
                    throw new ArgumentException(
                        $"Element '{element.ElementId}' references OCR identity not present in the processing manifest.",
                        nameof(elements));
                }
            }

            if (element.LayoutObservationSequence.HasValue !=
                element.LayoutKind.HasValue)
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' must retain layout observation sequence and neutral layout kind together.",
                    nameof(elements));
            }

            var hasLayoutEvidence =
                element.LayoutObservationSequence.HasValue &&
                element.LayoutKind.HasValue;

            if (element.OcrBackendId is not null &&
                !hasLayoutEvidence)
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' retains OCR evidence without its source layout observation.",
                    nameof(elements));
            }

            if (hasLayoutEvidence &&
                (manifest.Rasterization is null ||
                 manifest.LayoutAnalysis is null))
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' retains layout evidence without rasterization/layout manifest identity.",
                    nameof(elements));
            }

            var hasReconciliationEvidence =
                element.ReconciliationDecision.HasValue ||
                element.TextsEquivalent.HasValue ||
                element.HasReconciliationDivergence ||
                element.SelectedTextPreparation is not null ||
                element.OcrBackendId is not null;

            if (hasReconciliationEvidence &&
                manifest.Reconciliation is null)
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' retains reconciliation evidence without reconciliation manifest identity.",
                    nameof(elements));
            }

            if (element.PreservedVisual is not null &&
                !manifest.VisualPreservationProfileIds.Contains(
                    element.PreservedVisual.ProfileId,
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' references a visual-preservation profile not present in the processing manifest.",
                    nameof(elements));
            }
        }
    }

    private static void ValidateQualityObservations(
        DocumentIngestionQualityObservations quality,
        IReadOnlyDictionary<string, DocumentElementProvenance> elementsById)
    {
        foreach (var observation in
                 quality.OcrConfidenceObservations)
        {
            if (!elementsById.TryGetValue(
                    observation.ElementId,
                    out var element))
            {
                throw new ArgumentException(
                    $"OCR quality observation references unknown element '{observation.ElementId}'.",
                    nameof(quality));
            }

            if (element.OcrBackendId is null ||
                element.OcrProfileId is null)
            {
                throw new ArgumentException(
                    $"OCR confidence cannot be attached to element '{observation.ElementId}' because that element has no OCR evidence identity.",
                    nameof(quality));
            }
        }
    }
}
