using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Quality;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Results;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.UnitTests.Formats.Pdf;

/// <summary>
/// Verifies lossless/fail-closed migration from the current PDF result to the
/// portable processing-result contract.
/// </summary>
public sealed class PdfDocumentProcessingResultAdapterTests
{
    #region Variables and Constants

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string VisualSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    #endregion

    #region Methods Tests

    [Fact]
    public void Adapt_PreservesTextPageSegmentAndQualityCustody()
    {
        var legacy =
            CreateTextLegacyResult();

        var result =
            PdfDocumentProcessingResultAdapter.Adapt(
                legacy);

        Assert.Equal(
            DocumentProcessingResult.SchemaVersionId,
            result.SchemaVersion);

        Assert.Equal(
            legacy.Source.Sha256,
            result.Source.Sha256);

        Assert.Equal(
            legacy.Source.ByteLength,
            result.Source.ByteLength);

        Assert.Same(
            legacy.ProcessingManifest,
            result.ProcessingManifest);

        var structure =
            Assert.IsType<PagedDocumentSourceStructure>(
                result.SourceStructure);

        Assert.Equal(
            2,
            structure.PhysicalPageCount);

        Assert.Equal(
            legacy.Pages[1].ContentViewport,
            structure.Pages[1].ContentViewport);

        Assert.Equal(
            [
                "p000001-e000000",
                "p000002-e000000"
            ],
            result.Elements
                .Select(
                    element =>
                        element.ElementId)
                .ToArray());

        Assert.Equal(
            [0, 1],
            result.Elements
                .Select(
                    element =>
                        element.Ordinal)
                .ToArray());

        Assert.Equal(
            1,
            Assert.IsType<PagedDocumentSourceLocation>(
                    result.Elements[0].Location)
                .PhysicalPageNumber);

        Assert.Equal(
            DocumentTextSourceKind.Native,
            result.ElementProcessingEvidence[0]
                .TextSource);

        Assert.Equal(
            DocumentTextSourceKind.Ocr,
            result.ElementProcessingEvidence[1]
                .TextSource);

        Assert.Equal(
            LayoutObservationKind.Text,
            result.ElementProcessingEvidence[1]
                .LayoutKind);

        var segment =
            Assert.Single(
                result.StructuralSegments);

        Assert.Equal(
            legacy.StructuralSegments[0].Text,
            segment.Text);

        var segmentEvidence =
            Assert.Single(
                result.SegmentProcessingEvidence);

        Assert.Contains(
            DocumentTextSourceKind.Native,
            segmentEvidence.TextSources);

        Assert.Contains(
            DocumentTextSourceKind.Ocr,
            segmentEvidence.TextSources);

        var quality =
            Assert.Single(
                result.QualityObservations
                    .OcrConfidenceObservations);

        Assert.Equal(
            "p000002-e000000",
            quality.ElementId);

        Assert.Equal(
            3,
            quality.Confidence.ObservationCount);
    }

    [Fact]
    public void Adapt_PreservesVisualBinaryAndRasterCustody()
    {
        var legacy =
            CreateVisualLegacyResult();

        var result =
            PdfDocumentProcessingResultAdapter.Adapt(
                legacy);

        var element =
            Assert.Single(
                result.Elements);

        Assert.Equal(
            DocumentElementKind.Visual,
            element.Kind);

        var evidence =
            Assert.Single(
                result.ElementProcessingEvidence);

        Assert.Equal(
            LayoutObservationKind.Figure,
            evidence.LayoutKind);

        var asset =
            Assert.Single(
                result.VisualAssets);

        Assert.Equal(
            "visual-1:preserved-visual",
            asset.AssetId);

        Assert.Equal(
            "visual-profile-v1",
            asset.PreservationProfileId);

        Assert.Equal(
            VisualSha,
            asset.ContentSha256);

        var raster =
            Assert.IsType<DocumentRasterVisualDerivationEvidence>(
                asset.RasterDerivation);

        Assert.Equal(
            1000,
            raster.SourcePixelWidth);

        Assert.Equal(
            new PixelRectangle(
                10,
                20,
                410,
                520),
            raster.Crop);
    }

    [Fact]
    public void Adapt_RejectsNonCanonicalLegacyReadingOrder()
    {
        var legacy =
            CreateSingleTextLegacyResult(
                DocumentFormatId.Pdf,
                readingOrder:
                    7);

        var error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    PdfDocumentProcessingResultAdapter.Adapt(
                        legacy));

        Assert.Contains(
            "contiguous from zero",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Adapt_RejectsNonPdfLegacyResult()
    {
        var legacy =
            CreateSingleTextLegacyResult(
                new DocumentFormatId(
                    "epub"),
                readingOrder:
                    0);

        var error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    PdfDocumentProcessingResultAdapter.Adapt(
                        legacy));

        Assert.Contains(
            "only convert PDF",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_MatchesPdfAdapter_ForTextFixture()
    {
        var ingestion =
            CreateTextLegacyResult();

        var expected =
            PdfDocumentProcessingResultAdapter.Adapt(
                ingestion);

        var actual =
            DocumentProcessingResultProjector.Project(
                ingestion);

        Assert.Equivalent(
            expected,
            actual,
            strict:
                true);
    }

    [Fact]
    public void Project_MatchesPdfAdapter_ForVisualFixture()
    {
        var ingestion =
            CreateVisualLegacyResult();

        var expected =
            PdfDocumentProcessingResultAdapter.Adapt(
                ingestion);

        var actual =
            DocumentProcessingResultProjector.Project(
                ingestion);

        Assert.Equivalent(
            expected,
            actual,
            strict:
                true);
    }

    [Fact]
    public void Project_RejectsNonCanonicalReadingOrder()
    {
        var ingestion =
            CreateSingleTextLegacyResult(
                DocumentFormatId.Pdf,
                readingOrder:
                    7);

        var error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    DocumentProcessingResultProjector.Project(
                        ingestion));

        Assert.Contains(
            "contiguous from zero",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_AcceptsNonPdfIngestionResult()
    {
        var format =
            new DocumentFormatId(
                "epub");

        var ingestion =
            CreateSingleTextLegacyResult(
                format,
                readingOrder:
                    0);

        var result =
            DocumentProcessingResultProjector.Project(
                ingestion);

        Assert.Equal(
            format,
            result.Source.Format);
    }

    #endregion

    #region Methods Fixtures

    private static DocumentIngestionResult CreateTextLegacyResult()
    {
        const string segmentId =
            "segment-1";

        var native =
            CreateTextElement(
                elementId:
                    "p000001-e000000",
                physicalPageNumber:
                    1,
                readingOrder:
                    0,
                text:
                    "Native text.",
                TextSelectionOrigin.NativePdf,
                segmentId);

        var ocr =
            CreateTextElement(
                elementId:
                    "p000002-e000000",
                physicalPageNumber:
                    2,
                readingOrder:
                    0,
                text:
                    "OCR text.",
                TextSelectionOrigin.Ocr,
                segmentId);

        var segmentText =
            $"{native.NormalizedText}\n\n{ocr.NormalizedText}";

        var segment =
            new DocumentSegmentProvenance(
                SourceSha,
                segmentId,
                ordinal:
                    0,
                segmentText,
                ProvenanceTextHashing.ComputeUtf8Sha256(
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
                        TextSelectionOrigin.NativePdf,
                        TextSelectionOrigin.Ocr
                    ],
                hasUnresolvedEvidence:
                    false);

        var manifest =
            CreateManifest(
                includeOcr:
                    true,
                includeVisual:
                    false);

        var quality =
            new DocumentIngestionQualityObservations(
                [
                    new DocumentElementOcrQualityObservation(
                        ocr.ElementId,
                        new OcrConfidenceSummary(
                            observationCount:
                                3,
                            minimum:
                                0.70d,
                            arithmeticMean:
                                0.80d,
                            maximum:
                                0.90d))
                ]);

        return new DocumentIngestionResult(
            new DocumentSourceIdentity(
                DocumentFormatId.Pdf,
                SourceSha,
                byteLength:
                    2048,
                physicalPageCount:
                    2,
                fileName:
                    "fixture.pdf",
                declaredMediaType:
                    "application/pdf"),
            manifest,
            [
                new DocumentIngestionPage(
                    1,
                    FullViewport(),
                    [native.ElementId]),
                new DocumentIngestionPage(
                    2,
                    new NormalizedRectangle(
                        0.05,
                        0.10,
                        0.95,
                        0.90),
                    [ocr.ElementId])
            ],
            [
                native,
                ocr
            ],
            [segment],
            quality);
    }

    private static DocumentIngestionResult CreateVisualLegacyResult()
    {
        var visual =
            new DocumentElementProvenance(
                SourceSha,
                elementId:
                    "visual-1",
                physicalPageNumber:
                    1,
                readingOrder:
                    0,
                HybridDocumentElementKind.Visual,
                new NormalizedRectangle(
                    0.10,
                    0.20,
                    0.80,
                    0.90),
                segmentId:
                    null,
                selectedSourceText:
                    null,
                selectedSourceTextSha256:
                    null,
                normalizedText:
                    null,
                normalizedTextSha256:
                    null,
                TextSelectionOrigin.None,
                nativeBlockSourceSequence:
                    null,
                layoutObservationSequence:
                    3,
                layoutKind:
                    LayoutObservationKind.Figure,
                ocrBackendId:
                    null,
                ocrProfileId:
                    null,
                reconciliationDecision:
                    null,
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
                    new PreservedVisualProvenance(
                        profileId:
                            "visual-profile-v1",
                        mediaType:
                            "image/png",
                        sourceRasterPixelWidth:
                            1000,
                        sourceRasterPixelHeight:
                            1200,
                        new PixelRectangle(
                            10,
                            20,
                            410,
                            520),
                        contentLength:
                            1234,
                        VisualSha));

        return new DocumentIngestionResult(
            new DocumentSourceIdentity(
                DocumentFormatId.Pdf,
                SourceSha,
                byteLength:
                    4096,
                physicalPageCount:
                    1,
                fileName:
                    "visual.pdf",
                declaredMediaType:
                    "application/pdf"),
            CreateManifest(
                includeOcr:
                    false,
                includeVisual:
                    true),
            [
                new DocumentIngestionPage(
                    1,
                    FullViewport(),
                    [visual.ElementId])
            ],
            [visual],
            structuralSegments:
                [],
            DocumentIngestionQualityObservations.Empty);
    }

    private static DocumentIngestionResult CreateSingleTextLegacyResult(
        DocumentFormatId format,
        int readingOrder)
    {
        var element =
            CreateTextElement(
                elementId:
                    "element-1",
                physicalPageNumber:
                    1,
                readingOrder,
                text:
                    "Text.",
                TextSelectionOrigin.NativePdf,
                segmentId:
                    null);

        return new DocumentIngestionResult(
            new DocumentSourceIdentity(
                format,
                SourceSha,
                byteLength:
                    100,
                physicalPageCount:
                    1),
            CreateManifest(
                includeOcr:
                    false,
                includeVisual:
                    false),
            [
                new DocumentIngestionPage(
                    1,
                    FullViewport(),
                    [element.ElementId])
            ],
            [element],
            structuralSegments:
                [],
            DocumentIngestionQualityObservations.Empty);
    }

    private static DocumentElementProvenance CreateTextElement(
        string elementId,
        int physicalPageNumber,
        int readingOrder,
        string text,
        TextSelectionOrigin origin,
        string? segmentId)
    {
        var hash =
            ProvenanceTextHashing.ComputeUtf8Sha256(
                text);

        var isOcr =
            origin ==
            TextSelectionOrigin.Ocr;

        return new DocumentElementProvenance(
            SourceSha,
            elementId,
            physicalPageNumber,
            readingOrder,
            HybridDocumentElementKind.Text,
            new NormalizedRectangle(
                0.10,
                0.10,
                0.90,
                0.20),
            segmentId,
            selectedSourceText:
                text,
            selectedSourceTextSha256:
                hash,
            normalizedText:
                text,
            normalizedTextSha256:
                hash,
            origin,
            nativeBlockSourceSequence:
                isOcr
                    ? null
                    : 0,
            layoutObservationSequence:
                isOcr
                    ? 0
                    : null,
            layoutKind:
                isOcr
                    ? LayoutObservationKind.Text
                    : null,
            ocrBackendId:
                isOcr
                    ? "paddleocr-general-ocr"
                    : null,
            ocrProfileId:
                isOcr
                    ? "ocr-profile-v1"
                    : null,
            reconciliationDecision:
                isOcr
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

    private static DocumentProcessingManifest CreateManifest(
        bool includeOcr,
        bool includeVisual) =>
        new(
            engineVersion:
                "test-engine",
            nativeExtraction:
                new ProcessingComponentIdentity(
                    "pdfpig",
                    "native-v1"),
            rasterization:
                includeOcr ||
                includeVisual
                    ? new ProcessingComponentIdentity(
                        "pdftoppm",
                        "raster-v1")
                    : null,
            layoutAnalysis:
                includeOcr ||
                includeVisual
                    ? new ProcessingComponentIdentity(
                        "pp-structurev3",
                        "layout-v1")
                    : null,
            ocr:
                includeOcr
                    ? [
                        new ProcessingComponentIdentity(
                            "paddleocr-general-ocr",
                            "ocr-profile-v1")
                    ]
                    : [],
            reconciliation:
                includeOcr
                    ? new ProcessingComponentIdentity(
                        "native-ocr-reconciler",
                        "reconciliation-v1")
                    : null,
            visualPreservationProfileIds:
                includeVisual
                    ? ["visual-profile-v1"]
                    : [],
            assemblyProfileId:
                "assembly-v1",
            normalizationProfileId:
                "normalization-v1",
            segmentationProfileId:
                "segmentation-v1");

    private static NormalizedRectangle FullViewport() =>
        new(
            0,
            0,
            1,
            1);

    #endregion
}
