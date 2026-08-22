using System.Reflection;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Quality;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Results;

namespace DocumentProcessing.UnitTests.Results;

public sealed class DocumentIngestionResultTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Constructor_AcceptsCompletePortableGraph()
    {
        var fixture =
            CreateFixture();

        var result =
            new DocumentIngestionResult(
                fixture.Source,
                fixture.Manifest,
                fixture.Pages,
                fixture.Elements,
                fixture.Segments,
                fixture.Quality);

        Assert.Equal(
            DocumentIngestionResult.SchemaVersionId,
            result.SchemaVersion);

        Assert.Equal(
            "document-ingestion-result-v1",
            result.SchemaVersion);

        Assert.Equal(
            SourceSha,
            result.Source.Sha256);

        Assert.Equal(
            2,
            result.Pages.Count);

        Assert.Equal(
            "p000001",
            result.Pages[0].PageId);

        Assert.Equal(
            "p000002",
            result.Pages[1].PageId);

        Assert.Equal(
            2,
            result.Elements.Count);

        Assert.Single(
            result.StructuralSegments);

        var quality =
            Assert.Single(
                result.QualityObservations
                    .OcrConfidenceObservations);

        Assert.Equal(
            fixture.Elements[1].ElementId,
            quality.ElementId);

        Assert.Equal(
            3,
            quality.Confidence.ObservationCount);
    }

    [Fact]
    public void Constructor_DefensivelyCopiesRootCollections()
    {
        var fixture =
            CreateFixture();

        var pages =
            fixture.Pages.ToList();

        var elements =
            fixture.Elements.ToList();

        var segments =
            fixture.Segments.ToList();

        var result =
            new DocumentIngestionResult(
                fixture.Source,
                fixture.Manifest,
                pages,
                elements,
                segments,
                fixture.Quality);

        pages.Clear();
        elements.Clear();
        segments.Clear();

        Assert.Equal(
            2,
            result.Pages.Count);

        Assert.Equal(
            2,
            result.Elements.Count);

        Assert.Single(
            result.StructuralSegments);
    }

    [Fact]
    public void PortableProjection_AcceptsUniqueNonContiguousPageReadingOrder()
    {
        var fixture =
            CreateFixture();

        var first =
            CopyWithReadingOrder(
                fixture.Elements[0],
                readingOrder:
                    5);

        var ingestion =
            new DocumentIngestionResult(
                fixture.Source,
                fixture.Manifest,
                fixture.Pages,
                [
                    first,
                    fixture.Elements[1]
                ],
                fixture.Segments,
                fixture.Quality);

        var portable =
            DocumentProcessingResultProjector
                .Project(
                    ingestion);

        Assert.Equal(
            [
                first.ElementId,
                fixture.Elements[1].ElementId
            ],
            portable.Elements.Select(
                element =>
                    element.ElementId));

        Assert.Equal(
            [0, 1],
            portable.Elements.Select(
                element =>
                    element.Ordinal));
    }

    [Fact]
    public void Constructor_RejectsMissingPhysicalPage()
    {
        var fixture =
            CreateFixture();

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentIngestionResult(
                        fixture.Source,
                        fixture.Manifest,
                        [fixture.Pages[0]],
                        fixture.Elements,
                        fixture.Segments,
                        fixture.Quality));

        Assert.Contains(
            "exactly one entry",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsPageElementMembershipMismatch()
    {
        var fixture =
            CreateFixture();

        var brokenPages =
            new[]
            {
                new DocumentIngestionPage(
                    1,
                    FullViewport(),
                    []),
                fixture.Pages[1]
            };

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentIngestionResult(
                        fixture.Source,
                        fixture.Manifest,
                        brokenPages,
                        fixture.Elements,
                        fixture.Segments,
                        fixture.Quality));

        Assert.Contains(
            "ordered element membership",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsSegmentPageSpanContradictingMembership()
    {
        var fixture =
            CreateFixture();

        var segment =
            fixture.Segments.Single();

        var brokenSegment =
            new DocumentSegmentProvenance(
                segment.SourceDocumentSha256,
                segment.SegmentId,
                segment.Ordinal,
                segment.Text,
                segment.TextSha256,
                segment.HeadingText,
                firstPhysicalPageNumber:
                    1,
                lastPhysicalPageNumber:
                    1,
                segment.SourceElementIds,
                segment.TextOrigins,
                segment.HasUnresolvedEvidence);

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentIngestionResult(
                        fixture.Source,
                        fixture.Manifest,
                        fixture.Pages,
                        fixture.Elements,
                        [brokenSegment],
                        fixture.Quality));

        Assert.Contains(
            "page span",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsOcrQualityForUnknownElement()
    {
        var fixture =
            CreateFixture();

        var quality =
            new DocumentIngestionQualityObservations(
                [
                    new DocumentElementOcrQualityObservation(
                        "missing-element",
                        Confidence())
                ]);

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentIngestionResult(
                        fixture.Source,
                        fixture.Manifest,
                        fixture.Pages,
                        fixture.Elements,
                        fixture.Segments,
                        quality));

        Assert.Contains(
            "unknown element",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsOcrQualityForNativeOnlyElement()
    {
        var fixture =
            CreateFixture();

        var quality =
            new DocumentIngestionQualityObservations(
                [
                    new DocumentElementOcrQualityObservation(
                        fixture.Elements[0].ElementId,
                        Confidence())
                ]);

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentIngestionResult(
                        fixture.Source,
                        fixture.Manifest,
                        fixture.Pages,
                        fixture.Elements,
                        fixture.Segments,
                        quality));

        Assert.Contains(
            "no OCR evidence",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsOcrEvidenceWithoutLayoutEvidence()
    {
        var fixture =
            CreateFixture();

        var validOcr =
            fixture.Elements[1];

        var brokenOcr =
            new DocumentElementProvenance(
                validOcr.SourceDocumentSha256,
                validOcr.ElementId,
                validOcr.PhysicalPageNumber,
                validOcr.ReadingOrder,
                validOcr.Kind,
                validOcr.Bounds,
                validOcr.SegmentId,
                validOcr.SelectedSourceText,
                validOcr.SelectedSourceTextSha256,
                validOcr.NormalizedText,
                validOcr.NormalizedTextSha256,
                validOcr.TextOrigin,
                validOcr.NativeBlockSourceSequence,
                layoutObservationSequence:
                    null,
                layoutKind:
                    null,
                validOcr.OcrBackendId,
                validOcr.OcrProfileId,
                validOcr.ReconciliationDecision,
                validOcr.TextsEquivalent,
                validOcr.HasReconciliationDivergence,
                validOcr.SelectedTextPreparation,
                validOcr.NormalizationDehyphenation,
                validOcr.NormalizationChangedText,
                validOcr.ExclusionReason,
                validOcr.IsResolved,
                validOcr.PreservedVisual);

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentIngestionResult(
                        fixture.Source,
                        fixture.Manifest,
                        fixture.Pages,
                        [
                            fixture.Elements[0],
                            brokenOcr
                        ],
                        fixture.Segments,
                        fixture.Quality));

        Assert.Contains(
            "source layout observation",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsOcrIdentityAbsentFromManifest()
    {
        var fixture =
            CreateFixture();

        var manifestWithoutOcr =
            new DocumentProcessingManifest(
                fixture.Manifest.EngineVersion,
                fixture.Manifest.NativeExtraction,
                fixture.Manifest.Rasterization,
                fixture.Manifest.LayoutAnalysis,
                Array.Empty<ProcessingComponentIdentity>(),
                fixture.Manifest.Reconciliation,
                fixture.Manifest.VisualPreservationProfileIds,
                fixture.Manifest.AssemblyProfileId,
                fixture.Manifest.NormalizationProfileId,
                fixture.Manifest.SegmentationProfileId);

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentIngestionResult(
                        fixture.Source,
                        manifestWithoutOcr,
                        fixture.Pages,
                        fixture.Elements,
                        fixture.Segments,
                        fixture.Quality));

        Assert.Contains(
            "OCR identity",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QualityObservations_RejectDuplicateElementConfidence()
    {
        var observation =
            new DocumentElementOcrQualityObservation(
                "e1",
                Confidence());

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentIngestionQualityObservations(
                    [
                        observation,
                        observation
                    ]));
    }

    [Fact]
    public void PublicResultSurface_AvoidsDuplicateAggregateTruth()
    {
        var properties =
            typeof(DocumentIngestionResult)
                .GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public)
                .Select(
                    property =>
                        property.Name)
                .ToArray();

        Assert.DoesNotContain(
            "Provenance",
            properties);

        Assert.DoesNotContain(
            "VisualAssets",
            properties);

        Assert.DoesNotContain(
            "PreservedVisualReferences",
            properties);

        Assert.Contains(
            "Elements",
            properties);

        Assert.Contains(
            "StructuralSegments",
            properties);

        Assert.Equal(
            typeof(IReadOnlyList<DocumentElementProvenance>),
            typeof(DocumentIngestionResult)
                .GetProperty(
                    nameof(DocumentIngestionResult.Elements))!
                .PropertyType);

        Assert.Equal(
            typeof(IReadOnlyList<DocumentSegmentProvenance>),
            typeof(DocumentIngestionResult)
                .GetProperty(
                    nameof(DocumentIngestionResult.StructuralSegments))!
                .PropertyType);
    }

    [Fact]
    public void PublicResultTypes_AreReadOnlyAndHaveNoJsonAttributes()
    {
        var resultTypes =
            new[]
            {
                typeof(DocumentIngestionResult),
                typeof(DocumentIngestionPage),
                typeof(DocumentIngestionQualityObservations),
                typeof(DocumentElementOcrQualityObservation)
            };

        foreach (var type in
                 resultTypes)
        {
            Assert.All(
                type.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public),
                property =>
                    Assert.Null(
                        property.SetMethod));

            var attributeNamespaces =
                type.GetCustomAttributesData()
                    .Select(
                        attribute =>
                            attribute.AttributeType.Namespace)
                    .Where(
                        value =>
                            value is not null)
                    .ToArray();

            Assert.DoesNotContain(
                attributeNamespaces,
                value =>
                    value!.StartsWith(
                        "System.Text.Json",
                        StringComparison.Ordinal));
        }
    }

    private static Fixture CreateFixture()
    {
        var source =
            new DocumentSourceIdentity(
                DocumentFormatId.Pdf,
                SourceSha,
                byteLength:
                    123456,
                physicalPageCount:
                    2,
                fileName:
                    "fixture.pdf",
                declaredMediaType:
                    "application/pdf");

        var native =
            TextElement(
                elementId:
                    "p000001-e000000",
                physicalPageNumber:
                    1,
                readingOrder:
                    0,
                text:
                    "Native text.",
                TextSelectionOrigin.Native,
                ocrBackendId:
                    null,
                ocrProfileId:
                    null);

        var ocr =
            TextElement(
                elementId:
                    "p000002-e000000",
                physicalPageNumber:
                    2,
                readingOrder:
                    0,
                text:
                    "OCR text.",
                TextSelectionOrigin.Ocr,
                ocrBackendId:
                    "paddleocr-general-ocr",
                ocrProfileId:
                    "ocr-profile-v1");

        var segmentText =
            $"{native.NormalizedText}\n\n{ocr.NormalizedText}";

        var segment =
            new DocumentSegmentProvenance(
                SourceSha,
                segmentId:
                    "p000001-s000000",
                ordinal:
                    0,
                segmentText,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        segmentText),
                headingText:
                    null,
                firstPhysicalPageNumber:
                    1,
                lastPhysicalPageNumber:
                    2,
                sourceElementIds:
                    [
                        native.ElementId,
                        ocr.ElementId
                    ],
                textOrigins:
                    [
                        TextSelectionOrigin.Native,
                        TextSelectionOrigin.Ocr
                    ],
                hasUnresolvedEvidence:
                    false);

        native =
            TextElement(
                native.ElementId,
                1,
                0,
                "Native text.",
                TextSelectionOrigin.Native,
                ocrBackendId:
                    null,
                ocrProfileId:
                    null,
                segmentId:
                    segment.SegmentId);

        ocr =
            TextElement(
                ocr.ElementId,
                2,
                0,
                "OCR text.",
                TextSelectionOrigin.Ocr,
                ocrBackendId:
                    "paddleocr-general-ocr",
                ocrProfileId:
                    "ocr-profile-v1",
                segmentId:
                    segment.SegmentId);

        // Recreate the segment after assigning final element membership.
        segment =
            new DocumentSegmentProvenance(
                SourceSha,
                "p000001-s000000",
                0,
                segmentText,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        segmentText),
                headingText:
                    null,
                1,
                2,
                [
                    native.ElementId,
                    ocr.ElementId
                ],
                [
                    TextSelectionOrigin.Native,
                    TextSelectionOrigin.Ocr
                ],
                hasUnresolvedEvidence:
                    false);

        var manifest =
            new DocumentProcessingManifest(
                engineVersion:
                    "test-engine",
                nativeExtraction:
                    new ProcessingComponentIdentity(
                        "pdfpig",
                        "native-v1"),
                rasterization:
                    new ProcessingComponentIdentity(
                        "pdftoppm",
                        "raster-v1"),
                layoutAnalysis:
                    new ProcessingComponentIdentity(
                        "pp-structurev3",
                        "layout-v1"),
                ocr:
                    [
                        new ProcessingComponentIdentity(
                            "paddleocr-general-ocr",
                            "ocr-profile-v1")
                    ],
                reconciliation:
                    new ProcessingComponentIdentity(
                        "native-ocr-reconciler",
                        "reconciliation-v1"),
                visualPreservationProfileIds:
                    Array.Empty<string>(),
                assemblyProfileId:
                    "assembly-v1",
                normalizationProfileId:
                    "normalization-v1",
                segmentationProfileId:
                    "segmentation-v1");

        var pages =
            new[]
            {
                new DocumentIngestionPage(
                    1,
                    FullViewport(),
                    [native.ElementId]),
                new DocumentIngestionPage(
                    2,
                    FullViewport(),
                    [ocr.ElementId])
            };

        var quality =
            new DocumentIngestionQualityObservations(
                [
                    new DocumentElementOcrQualityObservation(
                        ocr.ElementId,
                        Confidence())
                ]);

        return new Fixture(
            source,
            manifest,
            pages,
            [native, ocr],
            [segment],
            quality);
    }

    private static DocumentElementProvenance TextElement(
        string elementId,
        int physicalPageNumber,
        int readingOrder,
        string text,
        TextSelectionOrigin textOrigin,
        string? ocrBackendId,
        string? ocrProfileId,
        string? segmentId = null)
    {
        var hash =
            ProvenanceTextHashing
                .ComputeUtf8Sha256(
                    text);

        return new DocumentElementProvenance(
            SourceSha,
            elementId,
            physicalPageNumber,
            readingOrder,
            HybridDocumentElementKind.Text,
            new NormalizedRectangle(
                0.1,
                0.1,
                0.9,
                0.2),
            segmentId,
            text,
            hash,
            text,
            hash,
            textOrigin,
            nativeBlockSourceSequence:
                textOrigin ==
                TextSelectionOrigin.Native
                    ? 0
                    : null,
            layoutObservationSequence:
                textOrigin ==
                TextSelectionOrigin.Ocr
                    ? 0
                    : null,
            layoutKind:
                textOrigin ==
                TextSelectionOrigin.Ocr
                    ? LayoutObservationKind.Text
                    : null,
            ocrBackendId,
            ocrProfileId,
            reconciliationDecision:
                textOrigin ==
                TextSelectionOrigin.Ocr
                    ? TextReconciliationDecision.OcrOnly
                    : null,
            textsEquivalent:
                null,
            hasReconciliationDivergence:
                false,
            selectedTextPreparation:
                null,
            normalizationDehyphenation:
                null,
            normalizationChangedText:
                false,
            exclusionReason:
                null,
            isResolved:
                true,
            preservedVisual:
                null);
    }

    private static DocumentElementProvenance CopyWithReadingOrder(
        DocumentElementProvenance source,
        int readingOrder) =>
        new(
            source.SourceDocumentSha256,
            source.ElementId,
            source.PhysicalPageNumber,
            readingOrder,
            source.Kind,
            source.Bounds,
            source.SegmentId,
            source.SelectedSourceText,
            source.SelectedSourceTextSha256,
            source.NormalizedText,
            source.NormalizedTextSha256,
            source.TextOrigin,
            source.NativeBlockSourceSequence,
            source.LayoutObservationSequence,
            source.LayoutKind,
            source.OcrBackendId,
            source.OcrProfileId,
            source.ReconciliationDecision,
            source.TextsEquivalent,
            source.HasReconciliationDivergence,
            source.SelectedTextPreparation,
            source.NormalizationDehyphenation,
            source.NormalizationChangedText,
            source.ExclusionReason,
            source.IsResolved,
            source.PreservedVisual);

    private static OcrConfidenceSummary Confidence() =>
        new(
            observationCount:
                3,
            minimum:
                0.70d,
            arithmeticMean:
                0.80d,
            maximum:
                0.90d);

    private static NormalizedRectangle FullViewport() =>
        new(
            0d,
            0d,
            1d,
            1d);

    private sealed record Fixture(
        DocumentSourceIdentity Source,
        DocumentProcessingManifest Manifest,
        IReadOnlyList<DocumentIngestionPage> Pages,
        IReadOnlyList<DocumentElementProvenance> Elements,
        IReadOnlyList<DocumentSegmentProvenance> Segments,
        DocumentIngestionQualityObservations Quality);
}
