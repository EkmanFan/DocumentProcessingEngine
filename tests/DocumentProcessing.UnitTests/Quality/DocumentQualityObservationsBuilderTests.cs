using System.Reflection;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Quality;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Hybrid.Segmentation;
using DocumentProcessing.Engine.Provenance;
using DocumentProcessing.Engine.Quality;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Quality;

public sealed class DocumentQualityObservationsBuilderTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Build_ProjectsDeterministicElementSegmentAndDocumentFacts()
    {
        var segmentation =
            BuildRepresentativeSegmentation();

        var provenance =
            DocumentProcessingProvenanceBuilder
                .Build(
                    segmentation,
                    CompleteContext());

        var quality =
            DocumentQualityObservationsBuilder
                .Build(
                    segmentation,
                    provenance);

        Assert.Equal(
            SourceSha,
            quality.SourceDocumentSha256);

        Assert.Equal(
            4,
            quality.ElementCount);

        Assert.Equal(
            2,
            quality.AuthoritativeTextElementCount);

        Assert.Equal(
            1,
            quality.NativeTextElementCount);

        Assert.Equal(
            1,
            quality.OcrTextElementCount);

        Assert.Equal(
            1,
            quality.VisualElementCount);

        Assert.Equal(
            0,
            quality.UnresolvedTextElementCount);

        Assert.Equal(
            1,
            quality.DeferredElementCount);

        Assert.Equal(
            0,
            quality.ExcludedElementCount);

        Assert.Equal(
            0,
            quality.ReconciliationDivergenceElementCount);

        Assert.Equal(
            1,
            quality.NormalizationChangedTextElementCount);

        Assert.Equal(
            1,
            quality.OcrEvidenceElementCount);

        Assert.Equal(
            0,
            quality.OcrEvidenceWithoutConfidenceObservationElementCount);

        Assert.Equal(
            1,
            quality.SegmentCount);

        Assert.Equal(
            1,
            quality.MixedTextOriginSegmentCount);

        Assert.Equal(
            1,
            quality.SegmentsWithUnresolvedEvidenceCount);

        var ocr =
            Assert.Single(
                quality.Elements,
                element =>
                    element.TextOrigin ==
                    TextSelectionOrigin.Ocr);

        Assert.True(
            ocr.HasAuthoritativeText);

        Assert.True(
            ocr.HasOcrEvidence);

        Assert.NotNull(
            ocr.OcrConfidence);

        Assert.Equal(
            2,
            ocr.OcrConfidence!.ObservationCount);

        Assert.Equal(
            0.60d,
            ocr.OcrConfidence.Minimum,
            precision:
                10);

        Assert.Equal(
            0.70d,
            ocr.OcrConfidence.ArithmeticMean,
            precision:
                10);

        Assert.Equal(
            0.80d,
            ocr.OcrConfidence.Maximum,
            precision:
                10);

        var native =
            Assert.Single(
                quality.Elements,
                element =>
                    element.TextOrigin ==
                    TextSelectionOrigin.NativePdf);

        Assert.True(
            native.NormalizationChangedText);

        Assert.False(
            native.HasOcrEvidence);

        var segment =
            Assert.Single(
                quality.Segments);

        Assert.Equal(
            4,
            segment.SourceElementCount);

        Assert.Equal(
            2,
            segment.AuthoritativeTextElementCount);

        Assert.Equal(
            1,
            segment.NativeTextElementCount);

        Assert.Equal(
            1,
            segment.OcrTextElementCount);

        Assert.Equal(
            1,
            segment.VisualElementCount);

        Assert.Equal(
            1,
            segment.DeferredElementCount);

        Assert.Equal(
            1,
            segment.OcrEvidenceElementCount);

        Assert.Equal(
            0,
            segment.OcrEvidenceWithoutConfidenceObservationElementCount);

        Assert.True(
            segment.IsMixedTextOrigin);

        Assert.True(
            segment.HasUnresolvedEvidence);
    }

    [Fact]
    public void Build_RejectsProvenanceFromDifferentNormalizedElement()
    {
        var segmentation =
            BuildRepresentativeSegmentation();

        var provenance =
            DocumentProcessingProvenanceBuilder
                .Build(
                    segmentation,
                    CompleteContext());

        var first =
            provenance.Elements[0];

        var tampered =
            new DocumentElementProvenance(
                first.SourceDocumentSha256,
                first.ElementId,
                first.PhysicalPageNumber,
                first.ReadingOrder,
                HybridDocumentElementKind.Text,
                first.Bounds,
                first.SegmentId,
                first.SelectedSourceText,
                first.SelectedSourceTextSha256,
                first.NormalizedText,
                first.NormalizedTextSha256,
                first.TextOrigin,
                first.NativeBlockSourceSequence,
                first.LayoutObservationSequence,
                first.LayoutKind,
                first.OcrBackendId,
                first.OcrProfileId,
                first.ReconciliationDecision,
                first.TextsEquivalent,
                first.HasReconciliationDivergence,
                first.SelectedTextPreparation,
                first.NormalizationDehyphenation,
                first.NormalizationChangedText,
                first.ExclusionReason,
                first.IsResolved,
                first.PreservedVisual);

        var tamperedElements =
            provenance.Elements
                .ToArray();

        tamperedElements[0] =
            tampered;

        var tamperedProvenance =
            new DocumentProcessingProvenance(
                provenance.Source,
                provenance.ProcessingManifest,
                tamperedElements,
                provenance.Segments);

        var error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    DocumentQualityObservationsBuilder
                        .Build(
                            segmentation,
                            tamperedProvenance));

        Assert.Contains(
            "provenance mismatch",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OcrConfidenceSummary_RejectsInvalidValuesAndOrdering()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new OcrConfidenceSummary(
                    0,
                    0.5d,
                    0.5d,
                    0.5d));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new OcrConfidenceSummary(
                    1,
                    -0.1d,
                    0.5d,
                    0.5d));

        Assert.Throws<ArgumentException>(
            () =>
                new OcrConfidenceSummary(
                    2,
                    0.8d,
                    0.7d,
                    0.9d));
    }

    [Fact]
    public void PublicQualityTypes_DoNotExposePolicyOrOpaqueScoreProperties()
    {
        var publicTypes =
            new[]
            {
                typeof(DocumentQualityObservations),
                typeof(DocumentElementQualityObservations),
                typeof(DocumentSegmentQualityObservations),
                typeof(OcrConfidenceSummary)
            };

        var forbiddenTerms =
            new[]
            {
                "Score",
                "Severity",
                "Admissible",
                "Threshold",
                "Grade",
                "Rating"
            };

        foreach (var type in
                 publicTypes)
        {
            var propertyNames =
                type.GetProperties(
                        BindingFlags.Instance |
                        BindingFlags.Public)
                    .Select(
                        property =>
                            property.Name)
                    .ToArray();

            foreach (var forbiddenTerm in
                     forbiddenTerms)
            {
                Assert.DoesNotContain(
                    propertyNames,
                    propertyName =>
                        propertyName.Contains(
                            forbiddenTerm,
                            StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void ElementQuality_RejectsConfidenceWithoutOcrEvidence()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentElementQualityObservations(
                    SourceSha,
                    "e1",
                    segmentId:
                        null,
                    HybridDocumentElementKind.Text,
                    TextSelectionOrigin.NativePdf,
                    hasAuthoritativeText:
                        true,
                    isResolved:
                        true,
                    isExcluded:
                        false,
                    hasReconciliationDivergence:
                        false,
                    normalizationChangedText:
                        false,
                    hasPreservedVisual:
                        false,
                    hasOcrEvidence:
                        false,
                    new OcrConfidenceSummary(
                        1,
                        0.9d,
                        0.9d,
                        0.9d)));
    }

    private static DocumentProcessingProvenanceContext CompleteContext() =>
        new(
            new DocumentSourceIdentity(
                DocumentFormatId.Pdf,
                SourceSha,
                byteLength:
                    12345,
                physicalPageCount:
                    1,
                fileName:
                    "source.pdf",
                declaredMediaType:
                    "application/pdf"),
            engineVersion:
                "0.1.0-test",
            new ProcessingComponentIdentity(
                "pdfpig",
                "pdfpig-native-v1"),
            new ProcessingComponentIdentity(
                "pdftoppm",
                "pdftoppm-300dpi-v1"),
            new ProcessingComponentIdentity(
                "pp-structurev3",
                "pp-structurev3-v1"),
            new ProcessingComponentIdentity(
                "native-ocr-reconciler",
                "native-ocr-reconciliation-v1"));

    private static DocumentProcessing.Core.Hybrid.Segmentation
        .HybridDocumentSegmentationResult BuildRepresentativeSegmentation()
    {
        var headingLayout =
            Layout(
                sequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Heading,
                top:
                    0.10,
                bottom:
                    0.16,
                rawLabel:
                    "doc_title");

        var ocrRegion =
            new OcrRegionResult(
                "paddleocr-general-ocr",
                "ocr-profile-v1",
                headingLayout,
                new[]
                {
                    new OcrTextObservation(
                        physicalPageNumber:
                            1,
                        sourceLayoutObservationSequence:
                            0,
                        observationSequence:
                            0,
                        text:
                            "Head",
                        confidence:
                            0.60d,
                        bounds:
                            headingLayout.Bounds),
                    new OcrTextObservation(
                        physicalPageNumber:
                            1,
                        sourceLayoutObservationSequence:
                            0,
                        observationSequence:
                            1,
                        text:
                            "ing",
                        confidence:
                            0.80d,
                        bounds:
                            headingLayout.Bounds)
                });

        var reconciliation =
            NativeOcrTextReconciler
                .Reconcile(
                    new TextReconciliationInput(
                        physicalPageNumber:
                            1,
                        NativeTextStatus.Missing,
                        nativeBlock:
                            null,
                        ocrRegion));

        var heading =
            HybridDocumentElementFactory
                .FromReconciliation(
                    reconciliation);

        var native =
            HybridDocumentElementFactory
                .FromNative(
                    1,
                    new DocumentTextBlock(
                        sourceSequence:
                            7,
                        readingOrder:
                            1,
                        text:
                            "Native   body.",
                        bounds:
                            Rectangle(
                                top:
                                    0.20,
                                bottom:
                                    0.45)));

        var figureLayout =
            Layout(
                sequence:
                    2,
                readingOrder:
                    2,
                LayoutObservationKind.Figure,
                top:
                    0.50,
                bottom:
                    0.70,
                rawLabel:
                    "image");

        var visual =
            HybridDocumentElementFactory
                .FromPreservedVisual(
                    new PreservedVisualEvidence(
                        SourceSha,
                        "visual-profile-v1",
                        "image/png",
                        figureLayout,
                        sourceRasterPixelWidth:
                            1000,
                        sourceRasterPixelHeight:
                            1000,
                        new PixelRectangle(
                            100,
                            500,
                            900,
                            700),
                        contentLength:
                            500,
                        contentSha256:
                            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));

        var deferred =
            HybridDocumentElementFactory
                .FromDeferred(
                    Layout(
                        sequence:
                            3,
                        readingOrder:
                            3,
                        LayoutObservationKind.Unknown,
                        top:
                            0.75,
                        bottom:
                            0.80,
                        rawLabel:
                            "number"));

        var assembly =
            HybridDocumentAssembler
                .AssembleDocument(
                    new[]
                    {
                        HybridDocumentAssembler
                            .AssemblePage(
                                1,
                                new[]
                                {
                                    heading,
                                    native,
                                    visual,
                                    deferred
                                })
                    });

        var normalization =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        return new HybridDocumentSegmenter()
            .Segment(
                normalization);
    }

    private static LayoutObservation Layout(
        int sequence,
        int readingOrder,
        LayoutObservationKind kind,
        double top,
        double bottom,
        string rawLabel) =>
        new(
            physicalPageNumber:
                1,
            observationSequence:
                sequence,
            readingOrder,
            kind,
            Rectangle(
                top,
                bottom),
            rawLabel);

    private static NormalizedRectangle Rectangle(
        double top,
        double bottom) =>
        new(
            left:
                0.10,
            top,
            right:
                0.90,
            bottom);
}
