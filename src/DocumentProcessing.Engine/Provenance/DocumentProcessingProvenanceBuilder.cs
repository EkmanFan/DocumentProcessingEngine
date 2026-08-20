using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Hybrid.Segmentation;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Engine.Provenance;

/// <summary>
/// Deterministically projects the completed hybrid evidence graph into the
/// custody-complete default provenance model.
///
/// This component does not perform extraction, rasterization, layout, OCR,
/// reconciliation, normalization, segmentation, quality scoring or consumer
/// persistence.
/// </summary>
public static class DocumentProcessingProvenanceBuilder
{
    #region Variables and Constants

    public const string ProjectionProfileId =
        "custody-complete-provenance-projection-v1";

    #endregion


    #region Methods

    public static DocumentProcessingProvenance Build(
        HybridDocumentSegmentationResult segmentation,
        DocumentProcessingProvenanceContext context)
    {
        ArgumentNullException.ThrowIfNull(
            segmentation);

        ArgumentNullException.ThrowIfNull(
            context);

        var normalization =
            segmentation.SourceNormalization;

        var assembly =
            normalization.SourceAssembly;

        var allElements =
            normalization.Pages
                .SelectMany(
                    page =>
                        page.Elements)
                .ToArray();

        ValidateSourcePageBounds(
            allElements,
            context);

        ValidateRequiredRunIdentities(
            allElements,
            context);

        ValidateEvidenceCompleteness(
            allElements,
            context);

        var elementIds =
            new Dictionary<
                NormalizedHybridDocumentElement,
                string>(
                ReferenceEqualityComparer.Instance);

        foreach (var element in
                 allElements)
        {
            elementIds.Add(
                element,
                CreateElementId(
                    element));
        }

        var segmentAssignments =
            BuildSegmentAssignments(
                segmentation);

        var elements =
            allElements
                .Select(
                    element =>
                        ProjectElement(
                            element,
                            elementIds[element],
                            segmentAssignments,
                            context.Source.Sha256))
                .ToArray();

        var segments =
            segmentation.Segments
                .Select(
                    segment =>
                        ProjectSegment(
                            segment,
                            elementIds,
                            context.Source.Sha256))
                .ToArray();

        var ocrComponents =
            allElements
                .Select(
                    element =>
                        element.Reconciliation
                            ?.Input.OcrRegion)
                .Where(
                    region =>
                        region is not null)
                .Select(
                    region =>
                        new ProcessingComponentIdentity(
                            region!.BackendId,
                            region.ProfileId))
                .Distinct()
                .ToArray();

        var visualProfiles =
            allElements
                .Select(
                    element =>
                        element.PreservedVisual
                            ?.ProfileId)
                .Where(
                    profileId =>
                        profileId is not null)
                .Select(
                    profileId =>
                        profileId!)
                .Distinct(
                    StringComparer.Ordinal)
                .ToArray();

        var manifest =
            new DocumentProcessingManifest(
                context.EngineVersion,
                context.NativeExtraction,
                context.Rasterization,
                context.LayoutAnalysis,
                ocrComponents,
                context.Reconciliation,
                visualProfiles,
                assembly.AssemblyProfileId,
                normalization.NormalizationProfileId,
                segmentation.SegmentationProfileId);

        return new DocumentProcessingProvenance(
            context.Source,
            manifest,
            elements,
            segments);
    }

    private static DocumentElementProvenance ProjectElement(
        NormalizedHybridDocumentElement element,
        string elementId,
        IReadOnlyDictionary<
            NormalizedHybridDocumentElement,
            string> segmentAssignments,
        string sourceDocumentSha256)
    {
        segmentAssignments.TryGetValue(
            element,
            out var segmentId);

        var selectedSourceText =
            element.SourceText;

        var normalizedText =
            element.Text;

        var reconciliation =
            element.Reconciliation;

        var ocrRegion =
            reconciliation
                ?.Input.OcrRegion;

        var selectedTextPreparation =
            GetSelectedTextPreparation(
                element);

        var normalizationDehyphenation =
            ToPublicDehyphenation(
                element.NormalizationDehyphenation);

        var normalizationChangedText =
            selectedSourceText is not null &&
            normalizedText is not null &&
            !string.Equals(
                selectedSourceText,
                normalizedText,
                StringComparison.Ordinal);

        var visual =
            element.PreservedVisual is null
                ? null
                : new PreservedVisualProvenance(
                    element.PreservedVisual.ProfileId,
                    element.PreservedVisual.MediaType,
                    element.PreservedVisual.SourceRasterPixelWidth,
                    element.PreservedVisual.SourceRasterPixelHeight,
                    element.PreservedVisual.Crop,
                    element.PreservedVisual.ContentLength,
                    element.PreservedVisual.ContentSha256);

        return new DocumentElementProvenance(
            sourceDocumentSha256,
            elementId,
            element.PhysicalPageNumber,
            element.ReadingOrder,
            element.Kind,
            element.Bounds,
            segmentId,
            selectedSourceText,
            selectedSourceText is null
                ? null
                : ProvenanceTextHashing.ComputeUtf8Sha256(
                    selectedSourceText),
            normalizedText,
            normalizedText is null
                ? null
                : ProvenanceTextHashing.ComputeUtf8Sha256(
                    normalizedText),
            element.TextOrigin,
            element.NativeBlock
                ?.SourceSequence,
            element.LayoutObservation
                ?.ObservationSequence,
            element.LayoutObservation
                ?.Kind,
            ocrRegion
                ?.BackendId,
            ocrRegion
                ?.ProfileId,
            reconciliation
                ?.Decision,
            reconciliation
                ?.TextsEquivalent,
            reconciliation
                ?.HasDivergence ??
            false,
            selectedTextPreparation,
            normalizationDehyphenation,
            normalizationChangedText,
            element.ExclusionReason,
            element.IsResolved,
            visual);
    }

    private static DocumentSegmentProvenance ProjectSegment(
        HybridDocumentSegment segment,
        IReadOnlyDictionary<
            NormalizedHybridDocumentElement,
            string> elementIds,
        string sourceDocumentSha256) =>
        new(
            sourceDocumentSha256,
            segment.Id,
            segment.Ordinal,
            segment.Text,
            ProvenanceTextHashing.ComputeUtf8Sha256(
                segment.Text),
            segment.HeadingText,
            segment.FirstPhysicalPageNumber,
            segment.LastPhysicalPageNumber,
            segment.SourceElements
                .Select(
                    element =>
                        elementIds[element])
                .ToArray(),
            segment.TextOrigins,
            segment.HasUnresolvedEvidence);

    private static Dictionary<
        NormalizedHybridDocumentElement,
        string> BuildSegmentAssignments(
        HybridDocumentSegmentationResult segmentation)
    {
        var assignments =
            new Dictionary<
                NormalizedHybridDocumentElement,
                string>(
                ReferenceEqualityComparer.Instance);

        foreach (var segment in
                 segmentation.Segments)
        {
            foreach (var element in
                     segment.SourceElements)
            {
                if (!assignments.TryAdd(
                        element,
                        segment.Id))
                {
                    throw new InvalidOperationException(
                        "One normalized element was assigned to more than one provenance segment.");
                }
            }
        }

        return assignments;
    }

    private static TextDehyphenationProvenance?
        GetSelectedTextPreparation(
        NormalizedHybridDocumentElement element)
    {
        var reconciliation =
            element.Reconciliation;

        if (reconciliation is null)
        {
            return null;
        }

        var preparation =
            element.TextOrigin switch
            {
                TextSelectionOrigin.NativePdf =>
                    reconciliation.NativeTextPreparation,

                TextSelectionOrigin.Ocr =>
                    reconciliation.OcrTextPreparation,

                _ =>
                    null
            };

        return ToPublicDehyphenation(
            preparation);
    }

    private static TextDehyphenationProvenance?
        ToPublicDehyphenation(
        TextDehyphenationResult? result)
    {
        if (result is null ||
            !result.Changed)
        {
            return null;
        }

        return new TextDehyphenationProvenance(
            result.SoftHyphenRemovalCount,
            result.BoundaryJoinCount);
    }

    private static string CreateElementId(
        NormalizedHybridDocumentElement element) =>
        $"p{element.PhysicalPageNumber:D6}-e{element.ReadingOrder:D6}";

    private static void ValidateSourcePageBounds(
        IEnumerable<NormalizedHybridDocumentElement> elements,
        DocumentProcessingProvenanceContext context)
    {
        var outOfRange =
            elements.FirstOrDefault(
                element =>
                    element.PhysicalPageNumber >
                    context.Source.PhysicalPageCount);

        if (outOfRange is not null)
        {
            throw new InvalidOperationException(
                $"Element page {outOfRange.PhysicalPageNumber} exceeds source physical page count {context.Source.PhysicalPageCount}.");
        }
    }

    private static void ValidateRequiredRunIdentities(
        IReadOnlyCollection<NormalizedHybridDocumentElement> elements,
        DocumentProcessingProvenanceContext context)
    {
        var hasLayoutBackedEvidence =
            elements.Any(
                element =>
                    element.LayoutObservation is not null);

        if (hasLayoutBackedEvidence &&
            context.Rasterization is null)
        {
            throw new InvalidOperationException(
                "Layout-backed evidence requires an explicit rasterization processing identity.");
        }

        if (hasLayoutBackedEvidence &&
            context.LayoutAnalysis is null)
        {
            throw new InvalidOperationException(
                "Layout-backed evidence requires an explicit layout processing identity.");
        }

        var hasReconciliationEvidence =
            elements.Any(
                element =>
                    element.Reconciliation is not null);

        if (hasReconciliationEvidence &&
            context.Reconciliation is null)
        {
            throw new InvalidOperationException(
                "Reconciliation evidence requires an explicit reconciliation processing identity.");
        }
    }

    private static void ValidateEvidenceCompleteness(
        IEnumerable<NormalizedHybridDocumentElement> elements,
        DocumentProcessingProvenanceContext context)
    {
        foreach (var element in
                 elements)
        {
            if (element.TextOrigin ==
                    TextSelectionOrigin.Ocr &&
                element.Reconciliation
                    ?.Input.OcrRegion is null)
            {
                throw new InvalidOperationException(
                    $"OCR-authoritative element on page {element.PhysicalPageNumber} lacks explicit OCR-region provenance.");
            }

            var visual =
                element.PreservedVisual;

            if (visual is not null &&
                !string.Equals(
                    visual.SourceDocumentSha256,
                    context.Source.Sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Preserved visual on page {element.PhysicalPageNumber} belongs to a different source document.");
            }
        }
    }

    #endregion
}
