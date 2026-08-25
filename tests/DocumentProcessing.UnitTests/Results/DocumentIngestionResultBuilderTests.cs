using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Hybrid.Segmentation;
using DocumentProcessing.Engine.Results;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Results;

public sealed class DocumentIngestionResultBuilderTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly NormalizedRectangle ExpectedViewport =
        new(
            left:
                0.05,
            top:
                0.10,
            right:
                0.95,
            bottom:
                0.90);

    [Fact]
    public void Build_ProjectsCompletedGraphIntoPortableResult()
    {
        var segmentation =
            BuildRepresentativeSegmentation(
                SourceSha);

        var result =
            DocumentIngestionResultBuilder
                .Build(
                    segmentation,
                    CompleteContext(
                        SourceSha));

        Assert.Equal(
            DocumentIngestionResult.SchemaVersionId,
            result.SchemaVersion);

        Assert.Equal(
            SourceSha,
            result.Source.Sha256);

        Assert.Equal(
            "0.1.0-test",
            result.ProcessingManifest.EngineVersion);

        Assert.Equal(
            HybridDocumentAssembler.AssemblyProfileId,
            result.ProcessingManifest.AssemblyProfileId);

        Assert.Equal(
            HybridDocumentNormalizer.NormalizationProfileId,
            result.ProcessingManifest.NormalizationProfileId);

        Assert.Equal(
            HybridDocumentSegmenter.SegmentationProfileId,
            result.ProcessingManifest.SegmentationProfileId);

        var page =
            Assert.Single(
                result.Pages);

        Assert.Equal(
            "p000001",
            page.PageId);

        Assert.Equal(
            1,
            page.PhysicalPageNumber);

        Assert.Equal(
            ExpectedViewport,
            page.ContentViewport);

        Assert.Equal(
            new[]
            {
                "p000001-e000000",
                "p000001-e000001",
                "p000001-e000002",
                "p000001-e000003"
            },
            page.OrderedElementIds);

        Assert.Equal(
            4,
            result.Elements.Count);

        var segment =
            Assert.Single(
                result.StructuralSegments);

        Assert.Equal(
            "p000001-s000000",
            segment.SegmentId);

        Assert.Equal(
            page.OrderedElementIds,
            segment.SourceElementIds);

        Assert.True(
            segment.IsMixedTextOrigin);

        Assert.True(
            segment.HasUnresolvedEvidence);

        var ocrQuality =
            Assert.Single(
                result.QualityObservations
                    .OcrConfidenceObservations);

        Assert.Equal(
            "p000001-e000000",
            ocrQuality.ElementId);

        Assert.Equal(
            1,
            ocrQuality.Confidence.ObservationCount);

        Assert.Equal(
            0.98d,
            ocrQuality.Confidence.Minimum);

        Assert.Equal(
            0.98d,
            ocrQuality.Confidence.ArithmeticMean);

        Assert.Equal(
            0.98d,
            ocrQuality.Confidence.Maximum);

        var visual =
            Assert.Single(
                result.Elements,
                element =>
                    element.PreservedVisual is not null);

        Assert.Equal(
            "visual-profile-v1",
            visual.PreservedVisual!.ProfileId);

        Assert.Equal(
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            visual.PreservedVisual.ContentSha256);
    }

    [Fact]
    public void Build_IsSemanticallyDeterministicForSameCompletedEvidence()
    {
        var segmentation =
            BuildRepresentativeSegmentation(
                SourceSha);

        var context =
            CompleteContext(
                SourceSha);

        var first =
            DocumentIngestionResultBuilder
                .Build(
                    segmentation,
                    context);

        var second =
            DocumentIngestionResultBuilder
                .Build(
                    segmentation,
                    context);

        Assert.Equal(
            first.SchemaVersion,
            second.SchemaVersion);

        Assert.Equal(
            first.Source.Sha256,
            second.Source.Sha256);

        Assert.Equal(
            first.Pages
                .Select(
                    page =>
                        page.PageId),
            second.Pages
                .Select(
                    page =>
                        page.PageId));

        Assert.Equal(
            first.Pages
                .SelectMany(
                    page =>
                        page.OrderedElementIds),
            second.Pages
                .SelectMany(
                    page =>
                        page.OrderedElementIds));

        Assert.Equal(
            first.Elements
                .Select(
                    element =>
                        (
                            element.ElementId,
                            element.SelectedSourceTextSha256,
                            element.NormalizedTextSha256,
                            element.PreservedVisual
                                ?.ContentSha256
                        )),
            second.Elements
                .Select(
                    element =>
                        (
                            element.ElementId,
                            element.SelectedSourceTextSha256,
                            element.NormalizedTextSha256,
                            element.PreservedVisual
                                ?.ContentSha256
                        )));

        Assert.Equal(
            first.StructuralSegments
                .Select(
                    segment =>
                        (
                            segment.SegmentId,
                            segment.TextSha256
                        )),
            second.StructuralSegments
                .Select(
                    segment =>
                        (
                            segment.SegmentId,
                            segment.TextSha256
                        )));

        Assert.Equal(
            first.QualityObservations
                .OcrConfidenceObservations
                .Select(
                    item =>
                        (
                            item.ElementId,
                            item.Confidence.ObservationCount,
                            item.Confidence.Minimum,
                            item.Confidence.ArithmeticMean,
                            item.Confidence.Maximum
                        )),
            second.QualityObservations
                .OcrConfidenceObservations
                .Select(
                    item =>
                        (
                            item.ElementId,
                            item.Confidence.ObservationCount,
                            item.Confidence.Minimum,
                            item.Confidence.ArithmeticMean,
                            item.Confidence.Maximum
                        )));
    }

    [Fact]
    public void BuildAndPortableProjection_RetainUnqualifiedVisualStatus()
    {
        var ingestion =
            DocumentIngestionResultBuilder
                .Build(
                    BuildRepresentativeSegmentation(
                        SourceSha,
                        DocumentVisualQualification.Unqualified),
                    CompleteContext(
                        SourceSha));

        var preserved =
            Assert.Single(
                ingestion.Elements,
                element =>
                    element.PreservedVisual is not null)
                .PreservedVisual!;

        Assert.Equal(
            DocumentVisualQualification.Unqualified,
            preserved.Qualification);

        var portable =
            DocumentProcessingResultProjector
                .Project(
                    ingestion,
                    []);

        Assert.Equal(
            DocumentVisualQualification.Unqualified,
            Assert.Single(
                    portable.VisualAssets)
                .Qualification);
    }

    [Fact]
    public void Build_RejectsMissingReconciliationIdentity()
    {
        var segmentation =
            BuildRepresentativeSegmentation(
                SourceSha);

        var context =
            new DocumentProcessingProvenanceContext(
                Source(
                    SourceSha),
                "0.1.0-test",
                NativeExtraction(),
                Rasterization(),
                LayoutAnalysis(),
                reconciliation:
                    null);

        var error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    DocumentIngestionResultBuilder
                        .Build(
                            segmentation,
                            context));

        Assert.Contains(
            "reconciliation processing identity",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_RejectsSourcePageCountThatContradictsCompletedGraph()
    {
        var segmentation =
            BuildRepresentativeSegmentation(
                SourceSha);

        var context =
            new DocumentProcessingProvenanceContext(
                new DocumentSourceIdentity(
                    DocumentFormatId.Pdf,
                    SourceSha,
                    byteLength:
                        12345,
                    physicalPageCount:
                        2,
                    fileName:
                        "source.pdf",
                    declaredMediaType:
                        "application/pdf"),
                "0.1.0-test",
                NativeExtraction(),
                Rasterization(),
                LayoutAnalysis(),
                Reconciliation());

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    DocumentIngestionResultBuilder
                        .Build(
                            segmentation,
                            context));

        Assert.Contains(
            "exactly one entry",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicBuilder_HasSingleNarrowBuildBoundary()
    {
        var buildMethods =
            typeof(DocumentIngestionResultBuilder)
                .GetMethods()
                .Where(
                    method =>
                        method.IsPublic &&
                        method.IsStatic &&
                        string.Equals(
                            method.Name,
                            nameof(DocumentIngestionResultBuilder.Build),
                            StringComparison.Ordinal))
                .ToArray();

        var build =
            Assert.Single(
                buildMethods);

        var parameters =
            build.GetParameters();

        Assert.Equal(
            2,
            parameters.Length);

        Assert.Equal(
            typeof(DocumentProcessing.Core.Hybrid.Segmentation
                .HybridDocumentSegmentationResult),
            parameters[0].ParameterType);

        Assert.Equal(
            typeof(DocumentProcessingProvenanceContext),
            parameters[1].ParameterType);

        Assert.Equal(
            typeof(DocumentIngestionResult),
            build.ReturnType);
    }

    private static DocumentProcessing.Core.Hybrid.Segmentation
        .HybridDocumentSegmentationResult
        BuildRepresentativeSegmentation(
        string sourceShaForVisual,
        DocumentVisualQualification visualQualification =
            DocumentVisualQualification.Meaningful)
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
                            "Heading",
                        confidence:
                            0.98,
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
                        sourceShaForVisual,
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
                            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                        qualification:
                            visualQualification));

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

        var page =
            HybridDocumentAssembler
                .AssemblePage(
                    1,
                    ExpectedViewport,
                    new[]
                    {
                        heading,
                        native,
                        visual,
                        deferred
                    });

        var assembly =
            HybridDocumentAssembler
                .AssembleDocument(
                    new[]
                    {
                        page
                    });

        var normalization =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        return new HybridDocumentSegmenter()
            .Segment(
                normalization);
    }

    private static DocumentProcessingProvenanceContext
        CompleteContext(
        string sourceSha) =>
        new(
            Source(
                sourceSha),
            "0.1.0-test",
            NativeExtraction(),
            Rasterization(),
            LayoutAnalysis(),
            Reconciliation());

    private static DocumentSourceIdentity Source(
        string sourceSha) =>
        new(
            DocumentFormatId.Pdf,
            sourceSha,
            byteLength:
                12345,
            physicalPageCount:
                1,
            fileName:
                "source.pdf",
            declaredMediaType:
                "application/pdf");

    private static ProcessingComponentIdentity
        NativeExtraction() =>
        new(
            "pdfpig",
            "pdfpig-native-v1");

    private static ProcessingComponentIdentity
        Rasterization() =>
        new(
            "pdftoppm",
            "pdftoppm-300dpi-v1");

    private static ProcessingComponentIdentity
        LayoutAnalysis() =>
        new(
            "pp-structurev3",
            "pp-structurev3-v1");

    private static ProcessingComponentIdentity
        Reconciliation() =>
        new(
            "native-ocr-reconciler",
            "native-ocr-reconciliation-v1");

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
