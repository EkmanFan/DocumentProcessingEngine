using DocumentProcessing.Core.DocumentModel;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
namespace DocumentProcessing.Core.Results;

/// <summary>
/// Canonical format-neutral result returned by document processing.
/// </summary>
/// <remarks>
/// This is the canonical public consumer result. It contains documentary
/// structure, source custody, processing evidence, preserved-visual custody, and
/// non-duplicating quality observations without requiring physical pages.
///
/// The Engine returns this type through the consumer-facing Host. The current
/// paged/hybrid Engine strategy may still use
/// <see cref="DocumentIngestionResult"/> internally during migration, but
/// portable projection occurs inside the Engine.
/// </remarks>
public sealed record DocumentProcessingResult
{
    #region Variables and Constants

    /// <summary>
    /// Stable schema identifier for the first portable processing-result
    /// contract.
    /// </summary>
    public const string SchemaVersionId =
        "document-processing-result-v2";

    #endregion

    #region Properties

    /// <summary>
    /// Gets the portable result schema identifier.
    /// </summary>
    public string SchemaVersion =>
        SchemaVersionId;

    /// <summary>
    /// Gets source identity and descriptive metadata.
    /// </summary>
    public DocumentSourceDescriptor Source { get; }

    /// <summary>
    /// Gets optional format-appropriate structural source custody.
    /// </summary>
    public DocumentSourceStructure? SourceStructure { get; }

    /// <summary>
    /// Gets deterministic processing-component custody.
    /// </summary>
    public DocumentProcessingManifest ProcessingManifest { get; }

    /// <summary>
    /// Gets document elements in exact document-wide order.
    /// </summary>
    public IReadOnlyList<DocumentElement> Elements { get; }

    /// <summary>
    /// Gets processing evidence for document elements.
    /// </summary>
    /// <remarks>
    /// Evidence is mandatory for non-visual elements and optional for visual
    /// elements. Visual evidence is used when a processor has neutral layout,
    /// exclusion, or resolved-state custody to retain.
    /// </remarks>
    public IReadOnlyList<DocumentElementProcessingEvidence>
        ElementProcessingEvidence { get; }

    /// <summary>
    /// Gets structural document segments in exact document-wide order.
    /// </summary>
    public IReadOnlyList<DocumentStructuralSegment> StructuralSegments { get; }

    /// <summary>
    /// Gets processing evidence for every structural segment.
    /// </summary>
    public IReadOnlyList<DocumentSegmentProcessingEvidence>
        SegmentProcessingEvidence { get; }

    /// <summary>
    /// Gets preserved visual assets. Binary bytes remain caller-owned.
    /// </summary>
    public IReadOnlyList<DocumentVisualAsset> VisualAssets { get; }

    /// <summary>
    /// Gets semantic footnotes projected outside the primary reading flow.
    /// </summary>
    public IReadOnlyList<DocumentFootnote> Footnotes { get; }

    /// <summary>
    /// Gets non-duplicating quality observations.
    /// </summary>
    public DocumentProcessingQualityObservations QualityObservations { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one complete portable processing result.
    /// </summary>
    public DocumentProcessingResult(
        DocumentSourceDescriptor source,
        DocumentProcessingManifest processingManifest,
        IReadOnlyList<DocumentElement> elements,
        IReadOnlyList<DocumentElementProcessingEvidence>
            elementProcessingEvidence,
        IReadOnlyList<DocumentStructuralSegment> structuralSegments,
        IReadOnlyList<DocumentSegmentProcessingEvidence>
            segmentProcessingEvidence,
        IReadOnlyList<DocumentVisualAsset> visualAssets,
        DocumentProcessingQualityObservations qualityObservations,
        DocumentSourceStructure? sourceStructure = null,
        IReadOnlyList<DocumentFootnote>? footnotes = null)
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
            elements);

        ArgumentNullException.ThrowIfNull(
            elementProcessingEvidence);

        ArgumentNullException.ThrowIfNull(
            structuralSegments);

        ArgumentNullException.ThrowIfNull(
            segmentProcessingEvidence);

        ArgumentNullException.ThrowIfNull(
            visualAssets);

        QualityObservations =
            qualityObservations ??
            throw new ArgumentNullException(
                nameof(qualityObservations));

        SourceStructure =
            sourceStructure;

        var elementArray =
            CopyWithoutNulls(
                elements,
                nameof(elements));

        var elementEvidenceArray =
            CopyWithoutNulls(
                elementProcessingEvidence,
                nameof(elementProcessingEvidence));

        var segmentArray =
            CopyWithoutNulls(
                structuralSegments,
                nameof(structuralSegments));

        var segmentEvidenceArray =
            CopyWithoutNulls(
                segmentProcessingEvidence,
                nameof(segmentProcessingEvidence));

        var visualAssetArray =
            CopyWithoutNulls(
                visualAssets,
                nameof(visualAssets));

        var footnoteArray =
            CopyWithoutNulls(
                footnotes ??
                    Array.Empty<DocumentFootnote>(),
                nameof(footnotes));

        ValidateUniqueIds(
            elementArray,
            element =>
                element.ElementId,
            "element",
            nameof(elements));

        ValidateContiguousOrdinals(
            elementArray,
            element =>
                element.Ordinal,
            "element",
            nameof(elements));

        ValidateUniqueIds(
            segmentArray,
            segment =>
                segment.SegmentId,
            "structural segment",
            nameof(structuralSegments));

        ValidateContiguousOrdinals(
            segmentArray,
            segment =>
                segment.Ordinal,
            "structural segment",
            nameof(structuralSegments));
        ValidateUniqueIds(
            footnoteArray,
            footnote =>
                footnote.FootnoteId,
            "footnote",
            nameof(footnotes));

        ValidateContiguousOrdinals(
            footnoteArray,
            footnote =>
                footnote.Ordinal,
            "footnote",
            nameof(footnotes));

        var elementsById =
            elementArray.ToDictionary(
                element =>
                    element.ElementId,
                StringComparer.Ordinal);

        var segmentsById =
            segmentArray.ToDictionary(
                segment =>
                    segment.SegmentId,
                StringComparer.Ordinal);

        ValidateSourceStructure(
            SourceStructure,
            elementArray);
        ValidateFootnotes(
            footnoteArray,
            elementsById,
            SourceStructure);

        ValidateStructuralMembership(
            elementArray,
            segmentsById,
            elementsById);

        ValidateElementEvidence(
            elementArray,
            elementEvidenceArray,
            elementsById,
            ProcessingManifest);

        var elementEvidenceById =
            elementEvidenceArray.ToDictionary(
                evidence =>
                    evidence.ElementId,
                StringComparer.Ordinal);

        ValidateSegmentEvidence(
            segmentArray,
            segmentEvidenceArray,
            elementEvidenceById);

        ValidateVisualAssets(
            visualAssetArray,
            elementsById,
            ProcessingManifest);

        ValidateQualityObservations(
            QualityObservations,
            elementsById,
            elementEvidenceById);

        Elements =
            elementArray;

        ElementProcessingEvidence =
            elementEvidenceArray;

        StructuralSegments =
            segmentArray;

        SegmentProcessingEvidence =
            segmentEvidenceArray;

        VisualAssets =
            visualAssetArray;


        Footnotes =
            footnoteArray;
    }

    #endregion

    #region Methods Validation

    private static T[] CopyWithoutNulls<T>(
        IReadOnlyList<T> values,
        string parameterName)
        where T : class
    {
        var copy =
            values.ToArray();

        if (copy.Any(
                value =>
                    value is null))
        {
            throw new ArgumentException(
                "Portable result collections cannot contain null values.",
                parameterName);
        }

        return copy;
    }

    private static void ValidateUniqueIds<T>(
        IReadOnlyList<T> values,
        Func<T, string> idSelector,
        string description,
        string parameterName)
    {
        if (values
                .Select(
                    idSelector)
                .Distinct(
                    StringComparer.Ordinal)
                .Count() !=
            values.Count)
        {
            throw new ArgumentException(
                $"Portable result contains duplicate {description} IDs.",
                parameterName);
        }
    }

    private static void ValidateContiguousOrdinals<T>(
        IReadOnlyList<T> values,
        Func<T, int> ordinalSelector,
        string description,
        string parameterName)
    {
        for (var index = 0;
             index < values.Count;
             index++)
        {
            if (ordinalSelector(
                    values[index]) !=
                index)
            {
                throw new ArgumentException(
                    $"Portable result {description} ordinals must be contiguous and match collection order starting at zero.",
                    parameterName);
            }
        }
    }

    private static void ValidateSourceStructure(
        DocumentSourceStructure? sourceStructure,
        IReadOnlyList<DocumentElement> elements)
    {
        if (sourceStructure is not
            PagedDocumentSourceStructure paged)
        {
            return;
        }

        foreach (var element in
                 elements)
        {
            if (element.Location is not
                PagedDocumentSourceLocation pagedLocation)
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' must use a paged source location when the result retains a paged source structure.",
                    nameof(elements));
            }

            if (pagedLocation.PhysicalPageNumber >
                paged.PhysicalPageCount)
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' references physical page {pagedLocation.PhysicalPageNumber}, outside the retained paged source structure.",
                    nameof(elements));
            }
        }
    }

    private static void ValidateStructuralMembership(
        IReadOnlyList<DocumentElement> elements,
        IReadOnlyDictionary<string, DocumentStructuralSegment> segmentsById,
        IReadOnlyDictionary<string, DocumentElement> elementsById)
    {
        foreach (var element in
                 elements)
        {
            if (element.SegmentId is null)
            {
                continue;
            }

            if (!segmentsById.TryGetValue(
                    element.SegmentId,
                    out var segment))
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' references unknown structural segment '{element.SegmentId}'.",
                    nameof(elements));
            }

            if (!segment.SourceElementIds.Contains(
                    element.ElementId,
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' references segment '{segment.SegmentId}' but that segment does not retain the element as a source member.",
                    nameof(elements));
            }
        }

        foreach (var segment in
                 segmentsById.Values)
        {
            foreach (var elementId in
                     segment.SourceElementIds)
            {
                if (!elementsById.TryGetValue(
                        elementId,
                        out var element))
                {
                    throw new ArgumentException(
                        $"Structural segment '{segment.SegmentId}' references unknown element '{elementId}'.",
                        nameof(segmentsById));
                }

                if (!string.Equals(
                        element.SegmentId,
                        segment.SegmentId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Structural segment '{segment.SegmentId}' source element '{elementId}' does not point back to that segment.",
                        nameof(segmentsById));
                }
            }
        }
    }

    private static void ValidateElementEvidence(
        IReadOnlyList<DocumentElement> elements,
        IReadOnlyList<DocumentElementProcessingEvidence> evidence,
        IReadOnlyDictionary<string, DocumentElement> elementsById,
        DocumentProcessingManifest manifest)
    {
        ValidateUniqueIds(
            evidence,
            item =>
                item.ElementId,
            "element-processing evidence",
            nameof(evidence));

        var evidenceById =
            evidence.ToDictionary(
                item =>
                    item.ElementId,
                StringComparer.Ordinal);

        foreach (var item in
                 evidence)
        {
            if (!elementsById.TryGetValue(
                    item.ElementId,
                    out var element))
            {
                throw new ArgumentException(
                    $"Element processing evidence references unknown element '{item.ElementId}'.",
                    nameof(evidence));
            }

            var hasLayoutEvidence =
                item.LayoutCandidateSequence.HasValue;

            if (hasLayoutEvidence &&
                (manifest.Rasterization is null ||
                 manifest.LayoutAnalysis is null))
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' retains layout evidence without rasterization/layout identities in the processing manifest.",
                    nameof(evidence));
            }

            var hasOcrIdentity =
                item.OcrBackendId is not null &&
                item.OcrProfileId is not null;

            if (hasOcrIdentity)
            {
                var observedIdentity =
                    new ProcessingComponentIdentity(
                        item.OcrBackendId!,
                        item.OcrProfileId!);

                if (!manifest.Ocr.Contains(
                        observedIdentity))
                {
                    throw new ArgumentException(
                        $"Element '{element.ElementId}' references OCR identity not present in the processing manifest.",
                        nameof(evidence));
                }
            }

            var hasReconciliationEvidence =
                item.ReconciliationDecision.HasValue ||
                item.TextsEquivalent.HasValue ||
                item.HasReconciliationDivergence ||
                item.SelectedTextPreparation is not null ||
                hasOcrIdentity;

            if (hasReconciliationEvidence &&
                manifest.Reconciliation is null)
            {
                throw new ArgumentException(
                    $"Element '{element.ElementId}' retains reconciliation evidence without a reconciliation identity in the processing manifest.",
                    nameof(evidence));
            }

            if (element.Text is not null &&
                item.SelectedSourceText is not null)
            {
                var actualNormalizationChangedText =
                    !string.Equals(
                        item.SelectedSourceText,
                        element.Text,
                        StringComparison.Ordinal);

                if (item.NormalizationChangedText !=
                    actualNormalizationChangedText)
                {
                    throw new ArgumentException(
                        $"Element '{element.ElementId}' normalization-change evidence does not match selected-source/final-text comparison.",
                        nameof(evidence));
                }
            }
        }

        foreach (var element in
                 elements.Where(
                     element =>
                         element.Kind !=
                         DocumentElementKind.Visual))
        {
            if (!evidenceById.ContainsKey(
                    element.ElementId))
            {
                throw new ArgumentException(
                    $"Non-visual element '{element.ElementId}' has no processing evidence.",
                    nameof(evidence));
            }
        }
    }

    private static void ValidateSegmentEvidence(
        IReadOnlyList<DocumentStructuralSegment> segments,
        IReadOnlyList<DocumentSegmentProcessingEvidence> evidence,
        IReadOnlyDictionary<string, DocumentElementProcessingEvidence>
            elementEvidenceById)
    {
        ValidateUniqueIds(
            evidence,
            item =>
                item.SegmentId,
            "segment-processing evidence",
            nameof(evidence));

        var evidenceById =
            evidence.ToDictionary(
                item =>
                    item.SegmentId,
                StringComparer.Ordinal);

        foreach (var segment in
                 segments)
        {
            if (!evidenceById.TryGetValue(
                    segment.SegmentId,
                    out var segmentEvidence))
            {
                throw new ArgumentException(
                    $"Structural segment '{segment.SegmentId}' has no processing evidence.",
                    nameof(evidence));
            }

            var sourceEvidence =
                segment.SourceElementIds
                    .Select(
                        elementId =>
                            elementEvidenceById.TryGetValue(
                                elementId,
                                out var elementEvidence)
                                ? elementEvidence
                                : null)
                    .Where(
                        item =>
                            item is not null)
                    .Cast<DocumentElementProcessingEvidence>()
                    .ToArray();

            var expectedSources =
                sourceEvidence
                    .Select(
                        item =>
                            item.TextSource)
                    .Where(
                        source =>
                            source !=
                            DocumentTextSourceKind.None)
                    .Distinct()
                    .OrderBy(
                        source =>
                            source)
                    .ToArray();

            var actualSources =
                segmentEvidence.TextSources
                    .OrderBy(
                        source =>
                            source)
                    .ToArray();

            if (!actualSources.SequenceEqual(
                    expectedSources))
            {
                throw new ArgumentException(
                    $"Structural segment '{segment.SegmentId}' text-source evidence does not match its source elements.",
                    nameof(evidence));
            }

            var expectedHasUnresolved =
                sourceEvidence.Any(
                    item =>
                        !item.IsResolved);

            if (segmentEvidence.HasUnresolvedEvidence !=
                expectedHasUnresolved)
            {
                throw new ArgumentException(
                    $"Structural segment '{segment.SegmentId}' unresolved-evidence flag does not match its source elements.",
                    nameof(evidence));
            }
        }

        foreach (var item in
                 evidence)
        {
            if (!segments.Any(
                    segment =>
                        string.Equals(
                            segment.SegmentId,
                            item.SegmentId,
                            StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    $"Segment processing evidence references unknown segment '{item.SegmentId}'.",
                    nameof(evidence));
            }
        }
    }

    private static void ValidateVisualAssets(
        IReadOnlyList<DocumentVisualAsset> visualAssets,
        IReadOnlyDictionary<string, DocumentElement> elementsById,
        DocumentProcessingManifest manifest)
    {
        ValidateUniqueIds(
            visualAssets,
            asset =>
                asset.AssetId,
            "visual asset",
            nameof(visualAssets));

        var visualElementIds =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var asset in
                 visualAssets)
        {
            if (!elementsById.TryGetValue(
                    asset.ElementId,
                    out var element))
            {
                throw new ArgumentException(
                    $"Visual asset '{asset.AssetId}' references unknown element '{asset.ElementId}'.",
                    nameof(visualAssets));
            }

            if (element.Kind !=
                DocumentElementKind.Visual)
            {
                throw new ArgumentException(
                    $"Visual asset '{asset.AssetId}' must reference a Visual document element.",
                    nameof(visualAssets));
            }

            if (!visualElementIds.Add(
                    asset.ElementId))
            {
                throw new ArgumentException(
                    $"Visual element '{asset.ElementId}' cannot own more than one preserved asset.",
                    nameof(visualAssets));
            }

            if (!manifest.VisualPreservationProfileIds.Contains(
                    asset.PreservationProfileId,
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Visual asset '{asset.AssetId}' references preservation profile not present in the processing manifest.",
                    nameof(visualAssets));
            }
        }
    }

    private static void ValidateFootnotes(
        IReadOnlyList<DocumentFootnote> footnotes,
        IReadOnlyDictionary<string, DocumentElement> elementsById,
        DocumentSourceStructure? sourceStructure)
    {
        foreach (var footnote in
                 footnotes)
        {
            foreach (var reference in
                     footnote.References)
            {
                var provenance =
                    reference.Provenance;

                if (!elementsById.TryGetValue(
                        provenance.ElementId,
                        out var referencedElement))
                {
                    throw new ArgumentException(
                        $"Footnote '{footnote.FootnoteId}' references unknown document element '{provenance.ElementId}'.",
                        nameof(footnotes));
                }

                ValidateFootnoteLocation(
                    provenance.Location,
                    sourceStructure,
                    footnote.FootnoteId,
                    nameof(footnotes));

                ValidateFootnoteReferenceLocation(
                    provenance.Location,
                    referencedElement.Location,
                    footnote.FootnoteId,
                    provenance.ElementId,
                    nameof(footnotes));
            }

            foreach (var location in
                     footnote.SourceLocations)
            {
                ValidateFootnoteLocation(
                    location,
                    sourceStructure,
                    footnote.FootnoteId,
                    nameof(footnotes));
            }
        }
    }

    private static void ValidateFootnoteLocation(
        DocumentSourceLocation location,
        DocumentSourceStructure? sourceStructure,
        string footnoteId,
        string parameterName)
    {
        if (sourceStructure is not
            PagedDocumentSourceStructure paged)
        {
            return;
        }

        if (location is not
            PagedDocumentSourceLocation pagedLocation)
        {
            throw new ArgumentException(
                $"Footnote '{footnoteId}' must use paged source locations when the result retains a paged source structure.",
                parameterName);
        }

        if (pagedLocation.PhysicalPageNumber >
            paged.PhysicalPageCount)
        {
            throw new ArgumentException(
                $"Footnote '{footnoteId}' references physical page {pagedLocation.PhysicalPageNumber}, outside the retained paged source structure.",
                parameterName);
        }
    }

    private static void ValidateFootnoteReferenceLocation(
        DocumentSourceLocation referenceLocation,
        DocumentSourceLocation elementLocation,
        string footnoteId,
        string elementId,
        string parameterName)
    {
        if (referenceLocation is
                PagedDocumentSourceLocation referencePage &&
            elementLocation is
                PagedDocumentSourceLocation elementPage)
        {
            if (referencePage.PhysicalPageNumber !=
                elementPage.PhysicalPageNumber)
            {
                throw new ArgumentException(
                    $"Footnote '{footnoteId}' reference '{elementId}' is on physical page {referencePage.PhysicalPageNumber}, but the referenced document element is on physical page {elementPage.PhysicalPageNumber}.",
                    parameterName);
            }

            return;
        }

        if (referenceLocation is
                PagedDocumentSourceLocation ||
            elementLocation is
                PagedDocumentSourceLocation)
        {
            throw new ArgumentException(
                $"Footnote '{footnoteId}' reference '{elementId}' must use paged locations consistently with its referenced element.",
                parameterName);
        }
    }

    private static void ValidateQualityObservations(
        DocumentProcessingQualityObservations quality,
        IReadOnlyDictionary<string, DocumentElement> elementsById,
        IReadOnlyDictionary<string, DocumentElementProcessingEvidence>
            elementEvidenceById)
    {
        foreach (var observation in
                 quality.OcrConfidenceObservations)
        {
            if (!elementsById.ContainsKey(
                    observation.ElementId))
            {
                throw new ArgumentException(
                    $"OCR quality observation references unknown element '{observation.ElementId}'.",
                    nameof(quality));
            }

            if (!elementEvidenceById.TryGetValue(
                    observation.ElementId,
                    out var evidence) ||
                evidence.OcrBackendId is null ||
                evidence.OcrProfileId is null)
            {
                throw new ArgumentException(
                    $"OCR confidence cannot be attached to element '{observation.ElementId}' because that element has no OCR processing identity.",
                    nameof(quality));
            }
        }
    }

    #endregion
}
