using DocumentProcessing.Core.DocumentModel;
using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Hybrid.Segmentation;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Quality;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Provenance;
using DocumentProcessing.Engine.Quality;

namespace DocumentProcessing.Engine.Results;

/// <summary>
/// Deterministically projects a completed normalized/segmented hybrid document
/// into the canonical portable <see cref="DocumentIngestionResult"/>.
///
/// The builder deliberately has one narrow input boundary:
///
///   completed segmentation + run-level provenance context
///
/// It reuses the already-proven Phase 19 provenance and quality projections
/// internally so callers cannot accidentally combine mutually inconsistent
/// provenance/quality graphs.
///
/// This component performs no extraction, rasterization, layout analysis, OCR,
/// reconciliation, normalization, segmentation, persistence or serialization.
/// </summary>
public static class DocumentIngestionResultBuilder
{
    #region Variables and Constants

    public const string ProjectionProfileId =
        "document-ingestion-result-projection-v1";

    #endregion


    #region Methods

    public static DocumentIngestionResult Build(
        HybridDocumentSegmentationResult segmentation,
        DocumentProcessingProvenanceContext provenanceContext)
    {
        ArgumentNullException.ThrowIfNull(
            segmentation);

        ArgumentNullException.ThrowIfNull(
            provenanceContext);

        var provenance =
            DocumentProcessingProvenanceBuilder
                .Build(
                    segmentation,
                    provenanceContext);

        var quality =
            DocumentQualityObservationsBuilder
                .Build(
                    segmentation,
                    provenance);

        var pages =
            ProjectPages(
                segmentation,
                provenance);

        var finalQuality =
            ProjectFinalQuality(
                provenance,
                quality);

        return new DocumentIngestionResult(
            provenance.Source,
            provenance.ProcessingManifest,
            pages,
            provenance.Elements,
            provenance.Segments,
            finalQuality);
    }

    internal static IReadOnlyList<DocumentNote> BuildNotes(
        DocumentIngestionResult ingestionResult,
        IReadOnlyList<NativeDocumentNote> notes)
    {
        ArgumentNullException.ThrowIfNull(
            ingestionResult);

        ArgumentNullException.ThrowIfNull(
            notes);

        var pagedNotes =
            notes
                .Select(
                    note =>
                        note as PagedNativeDocumentNote ??
                        throw new InvalidDataException(
                            $"Paged processing received unsupported native note evidence '{note.GetType().FullName}'."))
                .ToArray();

        var elementsByNativeBlock =
            ingestionResult.Elements
                .Where(
                    element =>
                        element.NativeBlockSourceSequence.HasValue)
                .GroupBy(
                    element =>
                        (
                            element.PhysicalPageNumber,
                            SourceSequence:
                                element.NativeBlockSourceSequence!.Value
                        ))
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.ToArray());

        var projectedNotes =
            new List<DocumentNote>(
                pagedNotes.Length);

        for (var ordinal = 0;
             ordinal <
             pagedNotes.Length;
             ordinal++)
        {
            var entry =
                pagedNotes[ordinal];

            var references =
                new List<DocumentNoteReference>(
                    entry.References.Count);

            foreach (var reference in
                     entry.References)
            {
                var key =
                    (
                        reference.PhysicalPageNumber,
                        SourceSequence:
                            reference.SourceBlockSequence
                    );

                if (!elementsByNativeBlock.TryGetValue(
                        key,
                        out var candidates) ||
                    candidates.Length !=
                        1)
                {
                    throw new InvalidDataException(
                        $"Note reference '{entry.Label}' at p{reference.PhysicalPageNumber}/b{reference.SourceBlockSequence} does not resolve to exactly one stable ingestion element.");
                }

                var owner =
                    candidates[0];

                if (owner.IsExcluded)
                {
                    throw new InvalidDataException(
                        $"Note reference '{entry.Label}' resolves to excluded element '{owner.ElementId}'.");
                }

                references.Add(
                    new DocumentNoteReference(
                        new DocumentNoteProvenance(
                            owner.ElementId,
                            new PagedDocumentSourceLocation(
                                reference.PhysicalPageNumber,
                                reference.Bounds))));
            }

            var sourceLocations =
                entry.PayloadLines
                    .Select(
                        line =>
                            (DocumentSourceLocation)
                                new PagedDocumentSourceLocation(
                                    line.PhysicalPageNumber,
                                    line.Bounds))
                    .ToArray();

            projectedNotes.Add(
                new DocumentNote(
                    noteId:
                        $"note-{ordinal:D6}",
                    ordinal,
                    entry.Label,
                    entry.Text,
                    ProvenanceTextHashing.ComputeUtf8Sha256(
                        entry.Text),
                    sourceLocations,
                    references));
        }

        return Array.AsReadOnly(
            projectedNotes.ToArray());
    }

    private static IReadOnlyList<DocumentIngestionPage>
        ProjectPages(
        HybridDocumentSegmentationResult segmentation,
        DocumentProcessingProvenance provenance)
    {
        var elementsBySourcePosition =
            provenance.Elements.ToDictionary(
                element =>
                    (
                        element.PhysicalPageNumber,
                        element.ReadingOrder
                    ));

        var projectedPages =
            new List<DocumentIngestionPage>(
                segmentation.SourceNormalization
                    .Pages.Count);

        foreach (var page in
                 segmentation.SourceNormalization.Pages)
        {
            var orderedElementIds =
                new string[
                    page.Elements.Count];

            for (var index = 0;
                 index <
                 page.Elements.Count;
                 index++)
            {
                var sourceElement =
                    page.Elements[index];

                var key =
                    (
                        sourceElement.PhysicalPageNumber,
                        sourceElement.ReadingOrder
                    );

                if (!elementsBySourcePosition.TryGetValue(
                        key,
                        out var element))
                {
                    throw new InvalidOperationException(
                        $"Result projection is missing provenance for normalized element p{key.PhysicalPageNumber}/r{key.ReadingOrder}.");
                }

                orderedElementIds[index] =
                    element.ElementId;
            }

            projectedPages.Add(
                new DocumentIngestionPage(
                    page.PhysicalPageNumber,
                    page.SourcePage.ContentViewport,
                    orderedElementIds));
        }

        return projectedPages;
    }

    private static DocumentIngestionQualityObservations
        ProjectFinalQuality(
        DocumentProcessingProvenance provenance,
        DocumentQualityObservations quality)
    {
        if (!string.Equals(
                provenance.Source.Sha256,
                quality.SourceDocumentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Result projection requires provenance and quality observations for the same source document.");
        }

        if (quality.Elements.Count !=
            provenance.Elements.Count)
        {
            throw new InvalidOperationException(
                "Result projection requires quality observations for every provenance element.");
        }

        var qualityByElementId =
            quality.Elements.ToDictionary(
                observation =>
                    observation.ElementId,
                StringComparer.Ordinal);

        var confidenceObservations =
            new List<DocumentElementOcrQualityObservation>();

        foreach (var element in
                 provenance.Elements)
        {
            if (!qualityByElementId.TryGetValue(
                    element.ElementId,
                    out var observation))
            {
                throw new InvalidOperationException(
                    $"Result projection is missing quality observations for element '{element.ElementId}'.");
            }

            ValidateQualityCorrespondence(
                element,
                observation);

            if (observation.OcrConfidence is not null)
            {
                confidenceObservations.Add(
                    new DocumentElementOcrQualityObservation(
                        element.ElementId,
                        observation.OcrConfidence));
            }
        }

        ValidateSegmentQualityCorrespondence(
            provenance,
            quality);

        return new DocumentIngestionQualityObservations(
            confidenceObservations);
    }

    private static void ValidateQualityCorrespondence(
        DocumentElementProvenance element,
        DocumentElementQualityObservations quality)
    {
        var expectedHasAuthoritativeText =
            element.NormalizedText is not null;

        var expectedHasOcrEvidence =
            element.OcrBackendId is not null;

        if (!string.Equals(
                element.SegmentId,
                quality.SegmentId,
                StringComparison.Ordinal) ||
            element.Kind !=
                quality.Kind ||
            element.TextOrigin !=
                quality.TextOrigin ||
            expectedHasAuthoritativeText !=
                quality.HasAuthoritativeText ||
            element.IsResolved !=
                quality.IsResolved ||
            element.IsExcluded !=
                quality.IsExcluded ||
            element.HasReconciliationDivergence !=
                quality.HasReconciliationDivergence ||
            element.NormalizationChangedText !=
                quality.NormalizationChangedText ||
            (element.PreservedVisual is not null) !=
                quality.HasPreservedVisual ||
            expectedHasOcrEvidence !=
                quality.HasOcrEvidence)
        {
            throw new InvalidOperationException(
                $"Result projection quality/provenance mismatch for element '{element.ElementId}'.");
        }
    }

    private static void ValidateSegmentQualityCorrespondence(
        DocumentProcessingProvenance provenance,
        DocumentQualityObservations quality)
    {
        if (quality.Segments.Count !=
            provenance.Segments.Count)
        {
            throw new InvalidOperationException(
                "Result projection requires quality observations for every provenance segment.");
        }

        var qualityBySegmentId =
            quality.Segments.ToDictionary(
                observation =>
                    observation.SegmentId,
                StringComparer.Ordinal);

        var elementsById =
            provenance.Elements.ToDictionary(
                element =>
                    element.ElementId,
                StringComparer.Ordinal);

        var elementQualityById =
            quality.Elements.ToDictionary(
                observation =>
                    observation.ElementId,
                StringComparer.Ordinal);

        foreach (var segment in
                 provenance.Segments)
        {
            if (!qualityBySegmentId.TryGetValue(
                    segment.SegmentId,
                    out var observation))
            {
                throw new InvalidOperationException(
                    $"Result projection is missing quality observations for segment '{segment.SegmentId}'.");
            }

            var sourceElementQuality =
                segment.SourceElementIds
                    .Select(
                        elementId =>
                            elementQualityById[elementId])
                    .ToArray();

            var expectedSourceElementCount =
                segment.SourceElementIds.Count;

            var expectedAuthoritativeTextElementCount =
                sourceElementQuality.Count(
                    item =>
                        item.HasAuthoritativeText);

            var expectedNativeTextElementCount =
                sourceElementQuality.Count(
                    item =>
                        item.HasAuthoritativeText &&
                        item.TextOrigin ==
                            Core.Reconciliation.TextSelectionOrigin.Native);

            var expectedOcrTextElementCount =
                sourceElementQuality.Count(
                    item =>
                        item.HasAuthoritativeText &&
                        item.TextOrigin ==
                            Core.Reconciliation.TextSelectionOrigin.Ocr);

            var expectedVisualElementCount =
                sourceElementQuality.Count(
                    item =>
                        item.Kind ==
                        Core.Hybrid.HybridDocumentElementKind.Visual);

            var expectedUnresolvedTextElementCount =
                sourceElementQuality.Count(
                    item =>
                        item.Kind ==
                        Core.Hybrid.HybridDocumentElementKind.UnresolvedText);

            var expectedDeferredElementCount =
                sourceElementQuality.Count(
                    item =>
                        item.Kind ==
                        Core.Hybrid.HybridDocumentElementKind.Deferred);

            var expectedExcludedElementCount =
                sourceElementQuality.Count(
                    item =>
                        item.IsExcluded);

            var expectedDivergenceCount =
                sourceElementQuality.Count(
                    item =>
                        item.HasReconciliationDivergence);

            var expectedNormalizationChangedCount =
                sourceElementQuality.Count(
                    item =>
                        item.NormalizationChangedText);

            var expectedOcrEvidenceCount =
                sourceElementQuality.Count(
                    item =>
                        item.HasOcrEvidence);

            var expectedOcrEvidenceWithoutConfidenceCount =
                sourceElementQuality.Count(
                    item =>
                        item.HasOcrEvidence &&
                        !item.HasOcrConfidenceObservations);

            if (observation.SourceElementCount !=
                    expectedSourceElementCount ||
                observation.AuthoritativeTextElementCount !=
                    expectedAuthoritativeTextElementCount ||
                observation.NativeTextElementCount !=
                    expectedNativeTextElementCount ||
                observation.OcrTextElementCount !=
                    expectedOcrTextElementCount ||
                observation.VisualElementCount !=
                    expectedVisualElementCount ||
                observation.UnresolvedTextElementCount !=
                    expectedUnresolvedTextElementCount ||
                observation.DeferredElementCount !=
                    expectedDeferredElementCount ||
                observation.ExcludedElementCount !=
                    expectedExcludedElementCount ||
                observation.ReconciliationDivergenceElementCount !=
                    expectedDivergenceCount ||
                observation.NormalizationChangedTextElementCount !=
                    expectedNormalizationChangedCount ||
                observation.OcrEvidenceElementCount !=
                    expectedOcrEvidenceCount ||
                observation.OcrEvidenceWithoutConfidenceObservationElementCount !=
                    expectedOcrEvidenceWithoutConfidenceCount ||
                observation.IsMixedTextOrigin !=
                    segment.IsMixedTextOrigin ||
                observation.HasUnresolvedEvidence !=
                    segment.HasUnresolvedEvidence)
            {
                throw new InvalidOperationException(
                    $"Result projection quality/provenance mismatch for segment '{segment.SegmentId}'.");
            }

            foreach (var elementId in
                     segment.SourceElementIds)
            {
                if (!elementsById.ContainsKey(
                        elementId))
                {
                    throw new InvalidOperationException(
                        $"Result projection segment '{segment.SegmentId}' references unknown provenance element '{elementId}'.");
                }
            }
        }
    }

    #endregion
}
