using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Converts the current authoritative PDF result into the format-neutral
/// <see cref="DocumentProcessingResult"/> contract.
/// </summary>
/// <remarks>
/// This is a migration bridge, not a second PDF processing path.
///
/// The adapter is deliberately fail-closed. Facts that the new result derives
/// rather than stores directly, such as per-page reading-order numbers and
/// segment page spans, must be exactly reconstructible from the legacy graph.
/// </remarks>
public static class PdfDocumentProcessingResultAdapter
{
    #region Methods Adaptation

    /// <summary>
    /// Converts one validated legacy PDF result without intentionally dropping
    /// documentary or processing custody.
    /// </summary>
    /// <param name="legacyResult">
    /// Current authoritative PDF-shaped result.
    /// </param>
    /// <returns>
    /// Equivalent format-neutral processing result.
    /// </returns>
    public static DocumentProcessingResult Adapt(
        DocumentIngestionResult legacyResult)
    {
        ArgumentNullException.ThrowIfNull(
            legacyResult);

        if (legacyResult.Source.Format !=
            DocumentFormatId.Pdf)
        {
            throw new InvalidOperationException(
                "The PDF result adapter can only convert PDF source results.");
        }

        var legacyElementsById =
            legacyResult.Elements.ToDictionary(
                element =>
                    element.ElementId,
                StringComparer.Ordinal);

        ValidateCanonicalPageReadingOrder(
            legacyResult.Pages,
            legacyElementsById);

        var orderedLegacyElements =
            legacyResult.Pages
                .SelectMany(
                    page =>
                        page.OrderedElementIds)
                .Select(
                    elementId =>
                        legacyElementsById[elementId])
                .ToArray();

        if (orderedLegacyElements.Length !=
            legacyResult.Elements.Count)
        {
            throw new InvalidOperationException(
                "Legacy page membership does not cover the complete element collection exactly once.");
        }

        var orderedLegacySegments =
            legacyResult.StructuralSegments
                .OrderBy(
                    segment =>
                        segment.Ordinal)
                .ToArray();

        ValidateCanonicalSegmentOrdinals(
            orderedLegacySegments);

        var elements =
            orderedLegacyElements
                .Select(
                    (element, ordinal) =>
                        AdaptElement(
                            element,
                            ordinal))
                .ToArray();

        var elementEvidence =
            orderedLegacyElements
                .Select(
                    AdaptElementEvidence)
                .ToArray();

        var structuralSegments =
            orderedLegacySegments
                .Select(
                    AdaptSegment)
                .ToArray();

        var segmentEvidence =
            orderedLegacySegments
                .Select(
                    AdaptSegmentEvidence)
                .ToArray();

        var visualAssets =
            orderedLegacyElements
                .Where(
                    element =>
                        element.PreservedVisual is not null)
                .Select(
                    AdaptVisualAsset)
                .ToArray();

        var source =
            new DocumentSourceDescriptor(
                legacyResult.Source.Format,
                legacyResult.Source.Sha256,
                legacyResult.Source.ByteLength,
                legacyResult.Source.FileName,
                legacyResult.Source.DeclaredMediaType);

        var sourceStructure =
            new PagedDocumentSourceStructure(
                legacyResult.Pages
                    .Select(
                        page =>
                            new PagedDocumentPageDescriptor(
                                page.PhysicalPageNumber,
                                page.ContentViewport))
                    .ToArray());

        var quality =
            new DocumentProcessingQualityObservations(
                legacyResult.QualityObservations
                    .OcrConfidenceObservations);

        return new DocumentProcessingResult(
            source,
            legacyResult.ProcessingManifest,
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

    private static DocumentElement AdaptElement(
        DocumentElementProvenance legacyElement,
        int ordinal) =>
        new(
            legacyElement.ElementId,
            ordinal,
            MapElementKind(
                legacyElement.Kind),
            new PagedDocumentSourceLocation(
                legacyElement.PhysicalPageNumber,
                legacyElement.Bounds),
            legacyElement.SegmentId,
            legacyElement.NormalizedText,
            legacyElement.NormalizedTextSha256);

    private static DocumentElementProcessingEvidence AdaptElementEvidence(
        DocumentElementProvenance legacyElement) =>
        new(
            legacyElement.ElementId,
            MapTextSource(
                legacyElement.TextOrigin),
            legacyElement.SelectedSourceText,
            legacyElement.SelectedSourceTextSha256,
            legacyElement.NativeBlockSourceSequence,
            legacyElement.LayoutObservationSequence,
            legacyElement.OcrBackendId,
            legacyElement.OcrProfileId,
            legacyElement.ReconciliationDecision,
            legacyElement.TextsEquivalent,
            legacyElement.HasReconciliationDivergence,
            legacyElement.SelectedTextPreparation,
            legacyElement.NormalizationDehyphenation,
            legacyElement.NormalizationChangedText,
            legacyElement.ExclusionReason,
            legacyElement.IsResolved,
            legacyElement.LayoutKind);

    private static DocumentStructuralSegment AdaptSegment(
        DocumentSegmentProvenance legacySegment) =>
        new(
            legacySegment.SegmentId,
            legacySegment.Ordinal,
            legacySegment.Text,
            legacySegment.TextSha256,
            legacySegment.HeadingText,
            legacySegment.SourceElementIds);

    private static DocumentSegmentProcessingEvidence AdaptSegmentEvidence(
        DocumentSegmentProvenance legacySegment)
    {
        if (legacySegment.TextOrigins.Any(
                origin =>
                    origin ==
                    TextSelectionOrigin.None))
        {
            throw new InvalidOperationException(
                $"Legacy segment '{legacySegment.SegmentId}' contains a None text origin that cannot represent authoritative segment text.");
        }

        if (legacySegment.TextOrigins
                .Distinct()
                .Count() !=
            legacySegment.TextOrigins.Count)
        {
            throw new InvalidOperationException(
                $"Legacy segment '{legacySegment.SegmentId}' contains duplicate text origins; exact legacy segment-origin semantics would be ambiguous after migration.");
        }

        return new DocumentSegmentProcessingEvidence(
            legacySegment.SegmentId,
            legacySegment.TextOrigins
                .Select(
                    MapTextSource)
                .ToArray(),
            legacySegment.HasUnresolvedEvidence);
    }

    private static DocumentVisualAsset AdaptVisualAsset(
        DocumentElementProvenance legacyElement)
    {
        if (legacyElement.Kind !=
            HybridDocumentElementKind.Visual)
        {
            throw new InvalidOperationException(
                $"Element '{legacyElement.ElementId}' contains preserved visual custody but is not a Visual element.");
        }

        var visual =
            legacyElement.PreservedVisual ??
            throw new InvalidOperationException(
                $"Visual element '{legacyElement.ElementId}' has no preserved visual custody.");

        var rasterDerivation =
            new DocumentRasterVisualDerivationEvidence(
                visual.SourceRasterPixelWidth,
                visual.SourceRasterPixelHeight,
                visual.Crop);

        return new DocumentVisualAsset(
            assetId:
                $"{legacyElement.ElementId}:preserved-visual",
            elementId:
                legacyElement.ElementId,
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
                    $"Unsupported legacy PDF element kind '{kind}'.")
        };

    private static DocumentTextSourceKind MapTextSource(
        TextSelectionOrigin origin) =>
        origin switch
        {
            TextSelectionOrigin.None =>
                DocumentTextSourceKind.None,
            TextSelectionOrigin.NativePdf =>
                DocumentTextSourceKind.Native,
            TextSelectionOrigin.Ocr =>
                DocumentTextSourceKind.Ocr,
            _ =>
                throw new InvalidOperationException(
                    $"Unsupported legacy PDF text origin '{origin}'.")
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
                        $"Legacy page {page.PhysicalPageNumber} references unknown element '{elementId}'.");
                }

                if (element.ReadingOrder !=
                    index)
                {
                    throw new InvalidOperationException(
                        $"Legacy page {page.PhysicalPageNumber} element '{element.ElementId}' has reading-order value {element.ReadingOrder}, but portable migration can reconstruct exact per-page reading order only when values are contiguous from zero.");
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
                    $"Legacy structural segment '{segments[index].SegmentId}' has ordinal {segments[index].Ordinal}; portable migration requires exact contiguous segment ordinals from zero.");
            }
        }
    }

    #endregion
}
