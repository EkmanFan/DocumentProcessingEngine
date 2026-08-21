using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Results;

/// <summary>
/// Projects the current canonical ingestion result into the format-neutral
/// consumer-facing <see cref="DocumentProcessingResult"/> contract.
/// </summary>
/// <remarks>
/// This migration projector is format-neutral. It performs no extraction,
/// assessment, planning, enrichment, reconciliation, persistence or routing.
///
/// Facts that the portable result derives rather than stores directly, such as
/// per-page reading-order numbers and segment ordinals, must remain exactly
/// reconstructible from the ingestion graph.
/// </remarks>
internal static class DocumentProcessingResultProjector
{
    #region Methods Projection

    public static DocumentProcessingResult Project(
        DocumentIngestionResult ingestionResult)
    {
        ArgumentNullException.ThrowIfNull(
            ingestionResult);

        var elementsById =
            ingestionResult.Elements.ToDictionary(
                element =>
                    element.ElementId,
                StringComparer.Ordinal);

        ValidateCanonicalPageReadingOrder(
            ingestionResult.Pages,
            elementsById);

        var orderedElements =
            ingestionResult.Pages
                .SelectMany(
                    page =>
                        page.OrderedElementIds)
                .Select(
                    elementId =>
                        elementsById[elementId])
                .ToArray();

        if (orderedElements.Length !=
            ingestionResult.Elements.Count)
        {
            throw new InvalidOperationException(
                "Ingestion page membership does not cover the complete element collection exactly once.");
        }

        var orderedSegments =
            ingestionResult.StructuralSegments
                .OrderBy(
                    segment =>
                        segment.Ordinal)
                .ToArray();

        ValidateCanonicalSegmentOrdinals(
            orderedSegments);

        var elements =
            orderedElements
                .Select(
                    (element, ordinal) =>
                        ProjectElement(
                            element,
                            ordinal))
                .ToArray();

        var elementEvidence =
            orderedElements
                .Select(
                    ProjectElementEvidence)
                .ToArray();

        var structuralSegments =
            orderedSegments
                .Select(
                    ProjectSegment)
                .ToArray();

        var segmentEvidence =
            orderedSegments
                .Select(
                    ProjectSegmentEvidence)
                .ToArray();

        var visualAssets =
            orderedElements
                .Where(
                    element =>
                        element.PreservedVisual is not null)
                .Select(
                    ProjectVisualAsset)
                .ToArray();

        var source =
            new DocumentSourceDescriptor(
                ingestionResult.Source.Format,
                ingestionResult.Source.Sha256,
                ingestionResult.Source.ByteLength,
                ingestionResult.Source.FileName,
                ingestionResult.Source.DeclaredMediaType);

        var sourceStructure =
            new PagedDocumentSourceStructure(
                ingestionResult.Pages
                    .Select(
                        page =>
                            new PagedDocumentPageDescriptor(
                                page.PhysicalPageNumber,
                                page.ContentViewport))
                    .ToArray());

        var quality =
            new DocumentProcessingQualityObservations(
                ingestionResult.QualityObservations
                    .OcrConfidenceObservations);

        return new DocumentProcessingResult(
            source,
            ingestionResult.ProcessingManifest,
            elements,
            elementEvidence,
            structuralSegments,
            segmentEvidence,
            visualAssets,
            quality,
            sourceStructure);
    }

    #endregion

    #region Methods Mapping

    private static DocumentElement ProjectElement(
        DocumentElementProvenance ingestionElement,
        int ordinal) =>
        new(
            ingestionElement.ElementId,
            ordinal,
            MapElementKind(
                ingestionElement.Kind),
            new PagedDocumentSourceLocation(
                ingestionElement.PhysicalPageNumber,
                ingestionElement.Bounds),
            ingestionElement.SegmentId,
            ingestionElement.NormalizedText,
            ingestionElement.NormalizedTextSha256);

    private static DocumentElementProcessingEvidence ProjectElementEvidence(
        DocumentElementProvenance ingestionElement) =>
        new(
            ingestionElement.ElementId,
            MapTextSource(
                ingestionElement.TextOrigin),
            ingestionElement.SelectedSourceText,
            ingestionElement.SelectedSourceTextSha256,
            ingestionElement.NativeBlockSourceSequence,
            ingestionElement.LayoutObservationSequence,
            ingestionElement.OcrBackendId,
            ingestionElement.OcrProfileId,
            ingestionElement.ReconciliationDecision,
            ingestionElement.TextsEquivalent,
            ingestionElement.HasReconciliationDivergence,
            ingestionElement.SelectedTextPreparation,
            ingestionElement.NormalizationDehyphenation,
            ingestionElement.NormalizationChangedText,
            ingestionElement.ExclusionReason,
            ingestionElement.IsResolved,
            ingestionElement.LayoutKind);

    private static DocumentStructuralSegment ProjectSegment(
        DocumentSegmentProvenance ingestionSegment) =>
        new(
            ingestionSegment.SegmentId,
            ingestionSegment.Ordinal,
            ingestionSegment.Text,
            ingestionSegment.TextSha256,
            ingestionSegment.HeadingText,
            ingestionSegment.SourceElementIds);

    private static DocumentSegmentProcessingEvidence ProjectSegmentEvidence(
        DocumentSegmentProvenance ingestionSegment)
    {
        if (ingestionSegment.TextOrigins.Any(
                origin =>
                    origin ==
                    TextSelectionOrigin.None))
        {
            throw new InvalidOperationException(
                $"Ingestion segment '{ingestionSegment.SegmentId}' contains a None text origin that cannot represent authoritative segment text.");
        }

        if (ingestionSegment.TextOrigins
                .Distinct()
                .Count() !=
            ingestionSegment.TextOrigins.Count)
        {
            throw new InvalidOperationException(
                $"Ingestion segment '{ingestionSegment.SegmentId}' contains duplicate text origins; exact segment-origin semantics would be ambiguous after portable projection.");
        }

        return new DocumentSegmentProcessingEvidence(
            ingestionSegment.SegmentId,
            ingestionSegment.TextOrigins
                .Select(
                    MapTextSource)
                .ToArray(),
            ingestionSegment.HasUnresolvedEvidence);
    }

    private static DocumentVisualAsset ProjectVisualAsset(
        DocumentElementProvenance ingestionElement)
    {
        if (ingestionElement.Kind !=
            HybridDocumentElementKind.Visual)
        {
            throw new InvalidOperationException(
                $"Element '{ingestionElement.ElementId}' contains preserved visual custody but is not a Visual element.");
        }

        var visual =
            ingestionElement.PreservedVisual ??
            throw new InvalidOperationException(
                $"Visual element '{ingestionElement.ElementId}' has no preserved visual custody.");

        var rasterDerivation =
            new DocumentRasterVisualDerivationEvidence(
                visual.SourceRasterPixelWidth,
                visual.SourceRasterPixelHeight,
                visual.Crop);

        return new DocumentVisualAsset(
            assetId:
                $"{ingestionElement.ElementId}:preserved-visual",
            elementId:
                ingestionElement.ElementId,
            preservationProfileId:
                visual.ProfileId,
            mediaType:
                visual.MediaType,
            contentLength:
                visual.ContentLength,
            contentSha256:
                visual.ContentSha256,
            rasterDerivation);
    }

    private static DocumentElementKind MapElementKind(
        HybridDocumentElementKind kind) =>
        kind switch
        {
            HybridDocumentElementKind.Text =>
                DocumentElementKind.Text,
            HybridDocumentElementKind.Heading =>
                DocumentElementKind.Heading,
            HybridDocumentElementKind.Caption =>
                DocumentElementKind.Caption,
            HybridDocumentElementKind.Visual =>
                DocumentElementKind.Visual,
            HybridDocumentElementKind.UnresolvedText =>
                DocumentElementKind.UnresolvedText,
            HybridDocumentElementKind.Deferred =>
                DocumentElementKind.Deferred,
            _ =>
                throw new InvalidOperationException(
                    $"Unsupported ingestion element kind '{kind}'.")
        };

    private static DocumentTextSourceKind MapTextSource(
        TextSelectionOrigin origin) =>
        origin switch
        {
            TextSelectionOrigin.None =>
                DocumentTextSourceKind.None,
            TextSelectionOrigin.Native =>
                DocumentTextSourceKind.Native,
            TextSelectionOrigin.Ocr =>
                DocumentTextSourceKind.Ocr,
            _ =>
                throw new InvalidOperationException(
                    $"Unsupported ingestion text origin '{origin}'.")
        };

    #endregion

    #region Methods Validation

    private static void ValidateCanonicalPageReadingOrder(
        IReadOnlyList<DocumentIngestionPage> pages,
        IReadOnlyDictionary<string, DocumentElementProvenance> elementsById)
    {
        foreach (var page in
                 pages)
        {
            for (var index = 0;
                 index < page.OrderedElementIds.Count;
                 index++)
            {
                var elementId =
                    page.OrderedElementIds[index];

                if (!elementsById.TryGetValue(
                        elementId,
                        out var element))
                {
                    throw new InvalidOperationException(
                        $"Ingestion page {page.PhysicalPageNumber} references unknown element '{elementId}'.");
                }

                if (element.ReadingOrder !=
                    index)
                {
                    throw new InvalidOperationException(
                        $"Ingestion page {page.PhysicalPageNumber} element '{element.ElementId}' has reading-order value {element.ReadingOrder}, but portable projection can reconstruct exact per-page reading order only when values are contiguous from zero.");
                }
            }
        }
    }

    private static void ValidateCanonicalSegmentOrdinals(
        IReadOnlyList<DocumentSegmentProvenance> segments)
    {
        for (var index = 0;
             index < segments.Count;
             index++)
        {
            if (segments[index].Ordinal !=
                index)
            {
                throw new InvalidOperationException(
                    $"Ingestion structural segment '{segments[index].SegmentId}' has ordinal {segments[index].Ordinal}; portable projection requires exact contiguous segment ordinals from zero.");
            }
        }
    }

    #endregion
}
