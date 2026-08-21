using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Hybrid.Segmentation;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Quality;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Engine.Quality;

/// <summary>
/// Deterministically derives neutral quality observations from the completed
/// hybrid evidence graph and its custody-complete provenance projection.
///
/// It does not assign severity, thresholds, admissibility, or a single quality
/// score.
/// </summary>
public static class DocumentQualityObservationsBuilder
{
    #region Variables and Constants

    public const string QualityProfileId =
        "deterministic-document-quality-observations-v1";

    #endregion


    #region Methods

    public static DocumentQualityObservations Build(
        HybridDocumentSegmentationResult segmentation,
        DocumentProcessingProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(
            segmentation);

        ArgumentNullException.ThrowIfNull(
            provenance);

        var normalizedElements =
            segmentation.SourceNormalization.Pages
                .SelectMany(
                    page =>
                        page.Elements)
                .ToArray();

        var provenanceBySourcePosition =
            BuildProvenanceIndex(
                provenance.Elements);

        if (normalizedElements.Length !=
            provenanceBySourcePosition.Count)
        {
            throw new InvalidOperationException(
                "Quality projection requires provenance for every normalized hybrid element.");
        }

        var elementObservations =
            normalizedElements
                .Select(
                    element =>
                        ProjectElement(
                            element,
                            GetMatchingProvenance(
                                element,
                                provenanceBySourcePosition),
                            provenance.Source.Sha256))
                .ToArray();

        var elementObservationsById =
            elementObservations.ToDictionary(
                observation =>
                    observation.ElementId,
                StringComparer.Ordinal);

        var segmentObservations =
            provenance.Segments
                .Select(
                    segment =>
                        ProjectSegment(
                            segment,
                            elementObservationsById,
                            provenance.Source.Sha256))
                .ToArray();

        ValidateSegmentSet(
            segmentation,
            provenance);

        return new DocumentQualityObservations(
            provenance.Source.Sha256,
            elementObservations,
            segmentObservations);
    }

    private static IReadOnlyDictionary<
        (int PhysicalPageNumber, int ReadingOrder),
        DocumentElementProvenance> BuildProvenanceIndex(
        IReadOnlyList<DocumentElementProvenance> elements)
    {
        var result =
            new Dictionary<
                (int PhysicalPageNumber, int ReadingOrder),
                DocumentElementProvenance>();

        foreach (var element in
                 elements)
        {
            var key =
                (
                    element.PhysicalPageNumber,
                    element.ReadingOrder
                );

            if (!result.TryAdd(
                    key,
                    element))
            {
                throw new InvalidOperationException(
                    $"Duplicate provenance source position p{element.PhysicalPageNumber}/r{element.ReadingOrder}.");
            }
        }

        return result;
    }

    private static DocumentElementProvenance GetMatchingProvenance(
        NormalizedHybridDocumentElement element,
        IReadOnlyDictionary<
            (int PhysicalPageNumber, int ReadingOrder),
            DocumentElementProvenance> provenanceBySourcePosition)
    {
        var key =
            (
                element.PhysicalPageNumber,
                element.ReadingOrder
            );

        if (!provenanceBySourcePosition.TryGetValue(
                key,
                out var provenance))
        {
            throw new InvalidOperationException(
                $"Missing provenance for normalized element p{element.PhysicalPageNumber}/r{element.ReadingOrder}.");
        }

        ValidateElementCorrespondence(
            element,
            provenance);

        return provenance;
    }

    private static void ValidateElementCorrespondence(
        NormalizedHybridDocumentElement element,
        DocumentElementProvenance provenance)
    {
        var expectedNormalizationChanged =
            element.SourceText is not null &&
            element.Text is not null &&
            !string.Equals(
                element.SourceText,
                element.Text,
                StringComparison.Ordinal);

        if (element.Kind !=
                provenance.Kind ||
            element.TextOrigin !=
                provenance.TextOrigin ||
            element.IsResolved !=
                provenance.IsResolved ||
            element.IsExcluded !=
                provenance.IsExcluded ||
            expectedNormalizationChanged !=
                provenance.NormalizationChangedText ||
            (element.PreservedVisual is not null) !=
                (provenance.PreservedVisual is not null) ||
            !string.Equals(
                element.SourceText,
                provenance.SelectedSourceText,
                StringComparison.Ordinal) ||
            !string.Equals(
                element.Text,
                provenance.NormalizedText,
                StringComparison.Ordinal) ||
            (element.Reconciliation
                 ?.HasDivergence ??
             false) !=
                provenance.HasReconciliationDivergence)
        {
            throw new InvalidOperationException(
                $"Quality projection provenance mismatch for element '{provenance.ElementId}'.");
        }
    }

    private static DocumentElementQualityObservations ProjectElement(
        NormalizedHybridDocumentElement element,
        DocumentElementProvenance provenance,
        string sourceDocumentSha256)
    {
        var ocrRegion =
            element.Reconciliation
                ?.Input.OcrRegion;

        var ocrConfidence =
            ocrRegion is null
                ? null
                : BuildOcrConfidenceSummary(
                    ocrRegion.TextObservations
                        .Select(
                            observation =>
                                observation.Confidence)
                        .ToArray());

        return new DocumentElementQualityObservations(
            sourceDocumentSha256,
            provenance.ElementId,
            provenance.SegmentId,
            provenance.Kind,
            provenance.TextOrigin,
            element.HasAuthoritativeText,
            provenance.IsResolved,
            provenance.IsExcluded,
            provenance.HasReconciliationDivergence,
            provenance.NormalizationChangedText,
            provenance.PreservedVisual is not null,
            ocrRegion is not null,
            ocrConfidence);
    }

    private static OcrConfidenceSummary? BuildOcrConfidenceSummary(
        IReadOnlyList<double> confidenceValues)
    {
        if (confidenceValues.Count == 0)
        {
            return null;
        }

        return new OcrConfidenceSummary(
            confidenceValues.Count,
            confidenceValues.Min(),
            confidenceValues.Average(),
            confidenceValues.Max());
    }

    private static DocumentSegmentQualityObservations ProjectSegment(
        DocumentSegmentProvenance segment,
        IReadOnlyDictionary<
            string,
            DocumentElementQualityObservations> elementObservationsById,
        string sourceDocumentSha256)
    {
        var sourceElements =
            segment.SourceElementIds
                .Select(
                    elementId =>
                    {
                        if (!elementObservationsById.TryGetValue(
                                elementId,
                                out var observation))
                        {
                            throw new InvalidOperationException(
                                $"Segment '{segment.SegmentId}' references element '{elementId}' without quality observations.");
                        }

                        return observation;
                    })
                .ToArray();

        return new DocumentSegmentQualityObservations(
            sourceDocumentSha256,
            segment.SegmentId,
            sourceElements.Length,
            sourceElements.Count(
                element =>
                    element.HasAuthoritativeText),
            sourceElements.Count(
                element =>
                    element.HasAuthoritativeText &&
                    element.TextOrigin ==
                        TextSelectionOrigin.Native),
            sourceElements.Count(
                element =>
                    element.HasAuthoritativeText &&
                    element.TextOrigin ==
                        TextSelectionOrigin.Ocr),
            sourceElements.Count(
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Visual),
            sourceElements.Count(
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.UnresolvedText),
            sourceElements.Count(
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Deferred),
            sourceElements.Count(
                element =>
                    element.IsExcluded),
            sourceElements.Count(
                element =>
                    element.HasReconciliationDivergence),
            sourceElements.Count(
                element =>
                    element.NormalizationChangedText),
            sourceElements.Count(
                element =>
                    element.HasOcrEvidence),
            sourceElements.Count(
                element =>
                    element.HasOcrEvidence &&
                    !element.HasOcrConfidenceObservations),
            segment.IsMixedTextOrigin,
            segment.HasUnresolvedEvidence);
    }

    private static void ValidateSegmentSet(
        HybridDocumentSegmentationResult segmentation,
        DocumentProcessingProvenance provenance)
    {
        var sourceSegmentIds =
            segmentation.Segments
                .Select(
                    segment =>
                        segment.Id)
                .OrderBy(
                    value =>
                        value,
                    StringComparer.Ordinal)
                .ToArray();

        var provenanceSegmentIds =
            provenance.Segments
                .Select(
                    segment =>
                        segment.SegmentId)
                .OrderBy(
                    value =>
                        value,
                    StringComparer.Ordinal)
                .ToArray();

        if (!sourceSegmentIds.SequenceEqual(
                provenanceSegmentIds,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Quality projection requires provenance for the exact structural segment set.");
        }
    }

    #endregion
}
