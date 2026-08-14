using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Hybrid.Segmentation;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Segmentation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Hybrid.Segmentation;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Hybrid.Segmentation;

public sealed class HybridDocumentSegmenterTests
{
    [Fact]
    public void Segment_UsesPageBoundedFallbackWhenNoHeadingExists()
    {
        var result =
            Segment(
                Page(
                    1,
                    Native(
                        1,
                        0,
                        0,
                        "Page one body.",
                        pointSize: 10,
                        wordCount: 8)),
                Page(
                    2,
                    Ocr(
                        2,
                        0,
                        0,
                        LayoutObservationKind.Text,
                        "Page two body.")));

        Assert.Equal(
            2,
            result.Segments.Count);

        Assert.All(
            result.Segments,
            segment =>
                Assert.Equal(
                    segment.FirstPhysicalPageNumber,
                    segment.LastPhysicalPageNumber));

        Assert.All(
            result.Segments,
            segment =>
                Assert.Null(
                    segment.HeadingText));
    }

    [Fact]
    public void Segment_AllowsNativeToNativeAcrossPages()
    {
        var result =
            Segment(
                Page(
                    1,
                    Native(
                        1,
                        0,
                        0,
                        "SECTION TITLE",
                        pointSize: 13,
                        wordCount: 2),
                    Native(
                        1,
                        1,
                        1,
                        "Native page one.",
                        pointSize: 10,
                        wordCount: 8)),
                Page(
                    2,
                    Native(
                        2,
                        0,
                        0,
                        "Native page two.",
                        pointSize: 10,
                        wordCount: 8)));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Equal(
            "SECTION TITLE",
            segment.HeadingText);

        Assert.Equal(
            (1, 2),
            (
                segment.FirstPhysicalPageNumber,
                segment.LastPhysicalPageNumber));

        Assert.Equal(
            new[]
            {
                TextSelectionOrigin.NativePdf
            },
            segment.TextOrigins);

        Assert.False(
            segment.IsMixedTextOrigin);
    }

    [Fact]
    public void Segment_AllowsOcrToOcrAcrossPages()
    {
        var result =
            Segment(
                Page(
                    1,
                    Ocr(
                        1,
                        0,
                        0,
                        LayoutObservationKind.Heading,
                        "SCANNED SECTION"),
                    Ocr(
                        1,
                        1,
                        1,
                        LayoutObservationKind.Text,
                        "OCR page one.")),
                Page(
                    2,
                    Ocr(
                        2,
                        0,
                        0,
                        LayoutObservationKind.Text,
                        "OCR page two.")));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Equal(
            (1, 2),
            (
                segment.FirstPhysicalPageNumber,
                segment.LastPhysicalPageNumber));

        Assert.Equal(
            new[]
            {
                TextSelectionOrigin.Ocr
            },
            segment.TextOrigins);

        Assert.Contains(
            "OCR page two.",
            segment.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Segment_AllowsNativeToOcrAcrossPagesAndRetainsMixedOrigins()
    {
        var result =
            Segment(
                Page(
                    1,
                    Native(
                        1,
                        0,
                        0,
                        "NATIVE SECTION",
                        pointSize: 13,
                        wordCount: 2),
                    Native(
                        1,
                        1,
                        1,
                        "Native body.",
                        pointSize: 10,
                        wordCount: 8)),
                Page(
                    2,
                    Ocr(
                        2,
                        0,
                        0,
                        LayoutObservationKind.Text,
                        "OCR continuation.")));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Equal(
            new[]
            {
                TextSelectionOrigin.NativePdf,
                TextSelectionOrigin.Ocr
            },
            segment.TextOrigins);

        Assert.True(
            segment.IsMixedTextOrigin);

        Assert.Equal(
            new[]
            {
                1,
                1,
                2
            },
            segment.TextElements.Select(
                element =>
                    element.PhysicalPageNumber));
    }

    [Fact]
    public void Segment_AllowsOcrToNativeAcrossPagesAndRetainsMixedOrigins()
    {
        var result =
            Segment(
                Page(
                    1,
                    Ocr(
                        1,
                        0,
                        0,
                        LayoutObservationKind.Heading,
                        "OCR SECTION"),
                    Ocr(
                        1,
                        1,
                        1,
                        LayoutObservationKind.Text,
                        "OCR body.")),
                Page(
                    2,
                    Native(
                        2,
                        0,
                        0,
                        "Native continuation.",
                        pointSize: 10,
                        wordCount: 8)));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Equal(
            new[]
            {
                TextSelectionOrigin.Ocr,
                TextSelectionOrigin.NativePdf
            },
            segment.TextOrigins);

        Assert.True(
            segment.IsMixedTextOrigin);
    }

    [Fact]
    public void Segment_RetainsVisualDeferredAndConflictEvidenceWithoutAddingNarrativeText()
    {
        var heading =
            Ocr(
                1,
                0,
                0,
                LayoutObservationKind.Heading,
                "MIXED SECTION");

        var bodyOne =
            Ocr(
                1,
                1,
                1,
                LayoutObservationKind.Text,
                "First body.");

        var figureLayout =
            Layout(
                1,
                sequence: 2,
                readingOrder: 2,
                LayoutObservationKind.Figure);

        var figure =
            HybridDocumentElementFactory
                .FromPreservedVisual(
                    Visual(
                        figureLayout));

        var deferred =
            HybridDocumentElementFactory
                .FromDeferred(
                    Layout(
                        1,
                        sequence: 3,
                        readingOrder: 3,
                        LayoutObservationKind.Unknown));

        var conflict =
            Conflict(
                Layout(
                    1,
                    sequence: 4,
                    readingOrder: 4,
                    LayoutObservationKind.Text));

        var bodyTwo =
            Ocr(
                1,
                5,
                5,
                LayoutObservationKind.Text,
                "Second body.");

        var result =
            Segment(
                Page(
                    1,
                    heading,
                    bodyOne,
                    figure,
                    deferred,
                    conflict,
                    bodyTwo));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Equal(
            6,
            segment.SourceElements.Count);

        Assert.Equal(
            3,
            segment.TextElements.Count);

        Assert.Single(
            segment.VisualElements);

        Assert.True(
            segment.HasUnresolvedEvidence);

        Assert.DoesNotContain(
            "conversion",
            segment.Text,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "conversior",
            segment.Text,
            StringComparison.Ordinal);

        Assert.Equal(
            "MIXED SECTION\n\nFirst body.\n\nSecond body.",
            segment.Text);
    }

    [Fact]
    public void Segment_ExcludedRecurringMarginNeverEntersSegmentEvidence()
    {
        var pages =
            new[]
            {
                Page(
                    1,
                    Native(
                        1,
                        0,
                        0,
                        "RUNNING 1",
                        pointSize: 8,
                        wordCount: 2,
                        bounds:
                            HeaderBounds),
                    Ocr(
                        1,
                        1,
                        1,
                        LayoutObservationKind.Heading,
                        "SECTION"),
                    Ocr(
                        1,
                        2,
                        2,
                        LayoutObservationKind.Text,
                        "Body one.")),

                Page(
                    2,
                    Native(
                        2,
                        0,
                        0,
                        "RUNNING 2",
                        pointSize: 8,
                        wordCount: 2,
                        bounds:
                            HeaderBounds),
                    Ocr(
                        2,
                        1,
                        1,
                        LayoutObservationKind.Text,
                        "Body two.")),

                Page(
                    3,
                    Native(
                        3,
                        0,
                        0,
                        "RUNNING 3",
                        pointSize: 8,
                        wordCount: 2,
                        bounds:
                            HeaderBounds),
                    Ocr(
                        3,
                        1,
                        1,
                        LayoutObservationKind.Text,
                        "Body three."))
            };

        var normalization =
            Normalize(
                pages);

        Assert.All(
            normalization.Pages,
            page =>
                Assert.True(
                    page.Elements[0].IsExcluded));

        var result =
            new HybridDocumentSegmenter()
                .Segment(
                    normalization);

        var segment =
            Assert.Single(
                result.Segments);

        Assert.DoesNotContain(
            segment.SourceElements,
            element =>
                element.IsExcluded);

        Assert.DoesNotContain(
            "RUNNING",
            segment.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Segment_LayoutCaptionNeverBecomesHeading()
    {
        var result =
            Segment(
                Page(
                    1,
                    Ocr(
                        1,
                        0,
                        0,
                        LayoutObservationKind.Caption,
                        "INTRODUCTION"),
                    Ocr(
                        1,
                        1,
                        1,
                        LayoutObservationKind.Text,
                        "Body.")));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Null(
            segment.HeadingText);

        Assert.Equal(
            HybridDocumentElementKind.Caption,
            segment.TextElements[0].Kind);
    }

    [Fact]
    public void Segment_ExplicitHintCanPromoteLayoutTextForScannedDocument()
    {
        var normalization =
            Normalize(
                Page(
                    1,
                    Ocr(
                        1,
                        0,
                        0,
                        LayoutObservationKind.Text,
                        "WHAT DO YOU THI NK?"),
                    Ocr(
                        1,
                        1,
                        1,
                        LayoutObservationKind.Text,
                        "Body.")),
                Page(
                    2,
                    Ocr(
                        2,
                        0,
                        0,
                        LayoutObservationKind.Text,
                        "Continuation.")));

        var result =
            new HybridDocumentSegmenter()
                .Segment(
                    normalization,
                    new DocumentSegmentationOptions(
                        ["WHAT DO YOU THINK?"]));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Equal(
            "WHAT DO YOU THI NK?",
            segment.HeadingText);

        Assert.Equal(
            (1, 2),
            (
                segment.FirstPhysicalPageNumber,
                segment.LastPhysicalPageNumber));
    }

    [Fact]
    public void Segment_LayoutTextDoesNotGetOverriddenByNativeTypography()
    {
        var layout =
            Layout(
                1,
                sequence: 0,
                readingOrder: 0,
                LayoutObservationKind.Text);

        var nativeBlock =
            Block(
                sourceSequence: 0,
                readingOrder: 0,
                "Looks like heading",
                pointSize: 20,
                wordCount: 3);

        var ocr =
            new OcrRegionResult(
                "paddleocr-general-ocr",
                "test-profile-v1",
                layout,
                new[]
                {
                    new OcrTextObservation(
                        1,
                        0,
                        0,
                        "Looks like heading",
                        0.99,
                        layout.Bounds)
                });

        var reconciliation =
            NativeOcrTextReconciler.Reconcile(
                new TextReconciliationInput(
                    1,
                    NativeTextStatus.Healthy,
                    nativeBlock,
                    ocr));

        var reconciled =
            HybridDocumentElementFactory
                .FromReconciliation(
                    reconciliation);

        var body =
            Native(
                1,
                1,
                1,
                "Native body.",
                pointSize: 10,
                wordCount: 8);

        var result =
            Segment(
                Page(
                    1,
                    reconciled,
                    body));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Null(
            segment.HeadingText);
    }

    [Fact]
    public void Segment_LayoutlessNativeStrictTypographyMatchesLegacyHeadingBehavior()
    {
        var result =
            Segment(
                Page(
                    1,
                    Native(
                        1,
                        0,
                        0,
                        "Structured Topic",
                        pointSize: 12,
                        wordCount: 2),
                    Native(
                        1,
                        1,
                        1,
                        "Ordinary body with sufficient weight.",
                        pointSize: 10,
                        wordCount: 12)));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Equal(
            "Structured Topic",
            segment.HeadingText);
    }

    [Fact]
    public void Segment_DoesNotInferLayoutlessNativeHeadingWithoutTypography()
    {
        var result =
            Segment(
                Page(
                    1,
                    Native(
                        1,
                        0,
                        0,
                        "1. Introduction",
                        pointSize: null,
                        wordCount: 2),
                    Native(
                        1,
                        1,
                        1,
                        "Body.",
                        pointSize: null,
                        wordCount: 8)));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Null(
            segment.HeadingText);
    }

    [Fact]
    public void Segment_ProducesDeterministicIds()
    {
        var normalization =
            Normalize(
                Page(
                    5,
                    Ocr(
                        5,
                        0,
                        0,
                        LayoutObservationKind.Heading,
                        "FIRST"),
                    Ocr(
                        5,
                        1,
                        1,
                        LayoutObservationKind.Text,
                        "Body.")),
                Page(
                    6,
                    Ocr(
                        6,
                        0,
                        0,
                        LayoutObservationKind.Heading,
                        "SECOND"),
                    Ocr(
                        6,
                        1,
                        1,
                        LayoutObservationKind.Text,
                        "Body.")));

        var segmenter =
            new HybridDocumentSegmenter();

        var first =
            segmenter.Segment(
                normalization);

        var second =
            segmenter.Segment(
                normalization);

        Assert.Equal(
            first.Segments.Select(
                segment =>
                    segment.Id),
            second.Segments.Select(
                segment =>
                    segment.Id));

        Assert.Equal(
            new[]
            {
                "p000005-s000000",
                "p000006-s000001"
            },
            first.Segments.Select(
                segment =>
                    segment.Id));
    }

    [Fact]
    public void Segment_NoTextEvidenceProducesNoSegmentsButRetainsSourceNormalization()
    {
        var figure =
            HybridDocumentElementFactory
                .FromPreservedVisual(
                    Visual(
                        Layout(
                            1,
                            0,
                            0,
                            LayoutObservationKind.Figure)));

        var normalization =
            Normalize(
                Page(
                    1,
                    figure));

        var result =
            new HybridDocumentSegmenter()
                .Segment(
                    normalization);

        Assert.Empty(
            result.Segments);

        Assert.Same(
            normalization,
            result.SourceNormalization);

        Assert.Single(
            normalization.Pages[0].VisualElements);
    }

    [Fact]
    public void Segment_HonorsCancellation()
    {
        var normalization =
            Normalize(
                Page(
                    1,
                    Ocr(
                        1,
                        0,
                        0,
                        LayoutObservationKind.Text,
                        "Body.")));

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () =>
                new HybridDocumentSegmenter()
                    .Segment(
                        normalization,
                        cancellation.Token));
    }

    private static HybridDocumentSegmentationResult Segment(
        params HybridDocumentPage[] pages) =>
        new HybridDocumentSegmenter()
            .Segment(
                Normalize(
                    pages));

    private static HybridDocumentNormalizationResult Normalize(
        params HybridDocumentPage[] pages)
    {
        var assembly =
            HybridDocumentAssembler
                .AssembleDocument(
                    pages);

        return new HybridDocumentNormalizer()
            .Normalize(
                assembly);
    }

    private static HybridDocumentPage Page(
        int physicalPageNumber,
        params HybridDocumentElement[] elements) =>
        HybridDocumentAssembler
            .AssemblePage(
                physicalPageNumber,
                elements);

    private static HybridDocumentElement Native(
        int physicalPageNumber,
        int sourceSequence,
        int? readingOrder,
        string text,
        double? pointSize,
        int wordCount,
        NormalizedRectangle? bounds = null) =>
        HybridDocumentElementFactory
            .FromNative(
                physicalPageNumber,
                Block(
                    sourceSequence,
                    readingOrder,
                    text,
                    pointSize,
                    wordCount,
                    bounds));

    private static HybridDocumentElement Ocr(
        int physicalPageNumber,
        int sequence,
        int readingOrder,
        LayoutObservationKind kind,
        string text)
    {
        var layout =
            Layout(
                physicalPageNumber,
                sequence,
                readingOrder,
                kind);

        var ocr =
            new OcrRegionResult(
                "paddleocr-general-ocr",
                "test-profile-v1",
                layout,
                new[]
                {
                    new OcrTextObservation(
                        physicalPageNumber,
                        sequence,
                        observationSequence: 0,
                        text,
                        confidence: 0.99,
                        layout.Bounds)
                });

        var reconciliation =
            NativeOcrTextReconciler.Reconcile(
                new TextReconciliationInput(
                    physicalPageNumber,
                    NativeTextStatus.Missing,
                    nativeBlock: null,
                    ocr));

        return HybridDocumentElementFactory
            .FromReconciliation(
                reconciliation);
    }

    private static HybridDocumentElement Conflict(
        LayoutObservation layout)
    {
        var native =
            Block(
                sourceSequence: 99,
                readingOrder:
                    layout.ReadingOrder,
                "conversion",
                pointSize: 10,
                wordCount: 1,
                bounds:
                    layout.Bounds);

        var ocr =
            new OcrRegionResult(
                "paddleocr-general-ocr",
                "test-profile-v1",
                layout,
                new[]
                {
                    new OcrTextObservation(
                        layout.PhysicalPageNumber,
                        layout.ObservationSequence,
                        observationSequence: 0,
                        "conversior",
                        confidence: 0.99,
                        layout.Bounds)
                });

        var reconciliation =
            NativeOcrTextReconciler.Reconcile(
                new TextReconciliationInput(
                    layout.PhysicalPageNumber,
                    NativeTextStatus.Suspicious,
                    native,
                    ocr));

        return HybridDocumentElementFactory
            .FromReconciliation(
                reconciliation);
    }

    private static PreservedVisualEvidence Visual(
        LayoutObservation layout) =>
        new(
            new string(
                'a',
                64),
            "visual-test-v1",
            "image/png",
            layout,
            sourceRasterPixelWidth: 1000,
            sourceRasterPixelHeight: 1200,
            new PixelRectangle(
                100,
                200,
                600,
                900),
            contentLength: 1234,
            new string(
                'b',
                64));

    private static LayoutObservation Layout(
        int physicalPageNumber,
        int sequence,
        int readingOrder,
        LayoutObservationKind kind) =>
        new(
            physicalPageNumber,
            sequence,
            readingOrder,
            kind,
            BodyBounds,
            rawLabel:
                kind.ToString());

    private static DocumentTextBlock Block(
        int sourceSequence,
        int? readingOrder,
        string text,
        double? pointSize,
        int wordCount,
        NormalizedRectangle? bounds = null)
    {
        var resolvedBounds =
            bounds ??
            BodyBounds;

        var words =
            Enumerable
                .Range(
                    0,
                    wordCount)
                .Select(
                    index =>
                        new DocumentWord(
                            index,
                            $"w{index}",
                            resolvedBounds,
                            fontName:
                                "TestFont",
                            medianPointSize:
                                pointSize))
                .ToArray();

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder,
            text,
            resolvedBounds,
            words,
            dominantFontName:
                pointSize is null
                    ? null
                    : "TestFont",
            medianPointSize:
                pointSize,
            lineCount:
                1);
    }

    private static readonly NormalizedRectangle HeaderBounds =
        new(
            0.10,
            0.02,
            0.90,
            0.07);

    private static readonly NormalizedRectangle BodyBounds =
        new(
            0.10,
            0.20,
            0.90,
            0.80);
}
