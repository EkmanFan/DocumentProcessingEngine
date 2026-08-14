using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Hybrid.Normalization;

public sealed class HybridDocumentNormalizerTests
{
    [Fact]
    public void Normalize_NormalizesStandaloneNativeAndMatchesLegacyTextRules()
    {
        var sourceText =
            "Cafe\u0301   inter-\nnational\r\nstudy";

        var block =
            Block(
                sourceSequence: 7,
                readingOrder: 2,
                sourceText,
                BodyBounds);

        var sourceElement =
            HybridDocumentElementFactory.FromNative(
                physicalPageNumber: 1,
                block);

        var assembly =
            Assemble(
                Page(
                    1,
                    sourceElement));

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        var normalized =
            Assert.Single(
                Assert.Single(
                        result.Pages)
                    .Elements);

        Assert.Same(
            assembly,
            result.SourceAssembly);

        Assert.Same(
            sourceElement,
            normalized.SourceElement);

        Assert.Equal(
            sourceText,
            normalized.SourceText);

        Assert.Equal(
            "Café international study",
            normalized.Text);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            normalized.TextOrigin);

        Assert.Equal(
            HybridDocumentElementKind.Text,
            normalized.Kind);

        Assert.Null(
            normalized.NormalizationDehyphenation);

        Assert.False(
            normalized.IsExcluded);

        Assert.Equal(
            HybridDocumentNormalizer.NormalizationProfileId,
            result.NormalizationProfileId);

        var legacyExtraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                new[]
                {
                    new DocumentExtractionPage(
                        physicalPageNumber: 1,
                        sourceText,
                        sourceWidth: 600,
                        sourceHeight: 800,
                        blocks:
                            new[]
                            {
                                block
                            })
                });

        var legacy =
            new DocumentTextNormalizer()
                .Normalize(
                    legacyExtraction);

        Assert.Equal(
            Assert.Single(
                    Assert.Single(
                            legacy.Pages)
                        .Blocks)
                .Text,
            normalized.Text);
    }

    [Fact]
    public void Normalize_OcrOnlyUsesExplicitObservationBoundaryDehyphenation()
    {
        var sourceElement =
            OcrOnlyElement(
                physicalPageNumber: 1,
                sequence: 3,
                readingOrder: 3,
                LayoutObservationKind.Text,
                BodyBounds,
                "Cafe\u0301   compan-",
                "ions and well-being");

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    Assemble(
                        Page(
                            1,
                            sourceElement)));

        var normalized =
            Assert.Single(
                Assert.Single(
                        result.Pages)
                    .Elements);

        Assert.Equal(
            "Café companions and well-being",
            normalized.Text);

        Assert.Equal(
            TextSelectionOrigin.Ocr,
            normalized.TextOrigin);

        Assert.Equal(
            1,
            normalized
                .NormalizationDehyphenation
                ?.BoundaryJoinCount);

        Assert.Equal(
            0,
            normalized
                .NormalizationDehyphenation
                ?.SoftHyphenRemovalCount);

        Assert.Same(
            sourceElement.Reconciliation,
            normalized.Reconciliation);

        Assert.Null(
            normalized.NativeBlock);
    }

    [Fact]
    public void Normalize_OcrBoundaryBeforeUppercaseRemainsHardBoundary()
    {
        var sourceElement =
            OcrOnlyElement(
                physicalPageNumber: 1,
                sequence: 3,
                readingOrder: 3,
                LayoutObservationKind.Text,
                BodyBounds,
                "Upper-",
                "Case");

        var normalized =
            Assert.Single(
                Assert.Single(
                        new HybridDocumentNormalizer()
                            .Normalize(
                                Assemble(
                                    Page(
                                        1,
                                        sourceElement)))
                            .Pages)
                    .Elements);

        Assert.Equal(
            "Upper- Case",
            normalized.Text);

        Assert.Null(
            normalized.NormalizationDehyphenation);
    }

    [Fact]
    public void Normalize_PreservesHeadingAndCaptionKindsAndOcrOrigin()
    {
        var heading =
            OcrOnlyElement(
                1,
                sequence: 2,
                readingOrder: 2,
                LayoutObservationKind.Heading,
                new NormalizedRectangle(
                    0.10,
                    0.15,
                    0.90,
                    0.20),
                "  SECTION   TITLE  ");

        var caption =
            OcrOnlyElement(
                1,
                sequence: 5,
                readingOrder: 5,
                LayoutObservationKind.Caption,
                new NormalizedRectangle(
                    0.20,
                    0.70,
                    0.80,
                    0.78),
                "Figure   caption");

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    Assemble(
                        Page(
                            1,
                            heading,
                            caption)));

        Assert.Equal(
            new[]
            {
                HybridDocumentElementKind.Heading,
                HybridDocumentElementKind.Caption
            },
            result.Pages[0]
                .Elements
                .Select(
                    item =>
                        item.Kind));

        Assert.All(
            result.Pages[0].Elements,
            item =>
                Assert.Equal(
                    TextSelectionOrigin.Ocr,
                    item.TextOrigin));

        Assert.Equal(
            "SECTION TITLE",
            result.Pages[0].Elements[0].Text);

        Assert.Equal(
            "Figure caption",
            result.Pages[0].Elements[1].Text);
    }

    [Fact]
    public void Normalize_PreservesVisualDeferredAndConflictWithoutCreatingText()
    {
        var deferredLayout =
            Layout(
                physicalPageNumber: 1,
                sequence: 0,
                readingOrder: 0,
                LayoutObservationKind.Unknown,
                new NormalizedRectangle(
                    0.05,
                    0.05,
                    0.20,
                    0.10));

        var conflictLayout =
            Layout(
                physicalPageNumber: 1,
                sequence: 1,
                readingOrder: 1,
                LayoutObservationKind.Text,
                new NormalizedRectangle(
                    0.10,
                    0.20,
                    0.90,
                    0.35));

        var visualLayout =
            Layout(
                physicalPageNumber: 1,
                sequence: 2,
                readingOrder: 2,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.20,
                    0.40,
                    0.80,
                    0.80));

        var deferred =
            HybridDocumentElementFactory.FromDeferred(
                deferredLayout);

        var conflict =
            ConflictElement(
                conflictLayout);

        var visual =
            HybridDocumentElementFactory.FromPreservedVisual(
                Visual(
                    visualLayout));

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    Assemble(
                        Page(
                            1,
                            deferred,
                            conflict,
                            visual)));

        var elements =
            result.Pages[0].Elements;

        Assert.Equal(
            3,
            elements.Count);

        Assert.Equal(
            HybridDocumentElementKind.Deferred,
            elements[0].Kind);

        Assert.Equal(
            HybridDocumentElementKind.UnresolvedText,
            elements[1].Kind);

        Assert.Equal(
            HybridDocumentElementKind.Visual,
            elements[2].Kind);

        Assert.All(
            elements,
            element =>
            {
                Assert.Null(
                    element.Text);

                Assert.False(
                    element.HasAuthoritativeText);

                Assert.False(
                    element.IsTextFlowElement);
            });

        Assert.Same(
            visual.PreservedVisual,
            elements[2].PreservedVisual);

        Assert.True(
            result.HasUnresolvedEvidence);
    }

    [Fact]
    public void Normalize_ExcludesRecurringHeadersAndCanonicalizesDigits()
    {
        var assembly =
            Assemble(
                Page(
                    1,
                    NativeElement(
                        1,
                        0,
                        0,
                        "CHAPTER 1",
                        HeaderBounds)),
                Page(
                    2,
                    NativeElement(
                        2,
                        0,
                        0,
                        "CHAPTER 2",
                        HeaderBounds)),
                Page(
                    3,
                    NativeElement(
                        3,
                        0,
                        0,
                        "CHAPTER 3",
                        HeaderBounds)));

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        var elements =
            result.Pages
                .SelectMany(
                    page =>
                        page.Elements)
                .ToArray();

        Assert.All(
            elements,
            element =>
            {
                Assert.True(
                    element.IsExcluded);

                Assert.Equal(
                    DocumentBlockExclusionReason.RepeatedHeader,
                    element.ExclusionReason);

                Assert.False(
                    element.IsTextFlowElement);

                Assert.NotNull(
                    element.Text);
            });
    }

    [Fact]
    public void Normalize_ExcludesRecurringFooters()
    {
        var assembly =
            Assemble(
                Page(
                    1,
                    NativeElement(
                        1,
                        0,
                        0,
                        "Copyright notice",
                        FooterBounds)),
                Page(
                    2,
                    NativeElement(
                        2,
                        0,
                        0,
                        "Copyright notice",
                        FooterBounds)),
                Page(
                    3,
                    NativeElement(
                        3,
                        0,
                        0,
                        "Copyright notice",
                        FooterBounds)));

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        Assert.All(
            result.Pages
                .SelectMany(
                    page =>
                        page.Elements),
            element =>
                Assert.Equal(
                    DocumentBlockExclusionReason.RepeatedFooter,
                    element.ExclusionReason));
    }

    [Fact]
    public void Normalize_DoesNotExcludeRepeatedBodyOrNonRecurringHeader()
    {
        var assembly =
            Assemble(
                Page(
                    1,
                    NativeElement(
                        1,
                        0,
                        0,
                        "Only twice",
                        HeaderBounds),
                    NativeElement(
                        1,
                        1,
                        1,
                        "Repeated body",
                        BodyBounds)),
                Page(
                    2,
                    NativeElement(
                        2,
                        0,
                        0,
                        "Only twice",
                        HeaderBounds),
                    NativeElement(
                        2,
                        1,
                        1,
                        "Repeated body",
                        BodyBounds)),
                Page(
                    3,
                    NativeElement(
                        3,
                        0,
                        0,
                        "Different header",
                        HeaderBounds),
                    NativeElement(
                        3,
                        1,
                        1,
                        "Repeated body",
                        BodyBounds)));

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        Assert.All(
            result.Pages
                .SelectMany(
                    page =>
                        page.Elements),
            element =>
            {
                Assert.False(
                    element.IsExcluded);

                Assert.True(
                    element.IsTextFlowElement);
            });
    }

    [Fact]
    public void Normalize_PreservesPageElementAndSourceIdentityOrder()
    {
        var pageTwo =
            Page(
                2,
                NativeElement(
                    2,
                    0,
                    0,
                    "page two first",
                    BodyBounds),
                NativeElement(
                    2,
                    1,
                    1,
                    "page two second",
                    BodyBounds));

        var pageOne =
            Page(
                1,
                NativeElement(
                    1,
                    0,
                    0,
                    "page one",
                    BodyBounds));

        var assembly =
            new HybridDocumentAssemblyResult(
                HybridDocumentAssembler.AssemblyProfileId,
                new[]
                {
                    pageOne,
                    pageTwo
                });

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        Assert.Equal(
            new[]
            {
                1,
                2
            },
            result.Pages
                .Select(
                    page =>
                        page.PhysicalPageNumber));

        for (var pageIndex = 0;
             pageIndex < assembly.Pages.Count;
             pageIndex++)
        {
            Assert.Same(
                assembly.Pages[pageIndex],
                result.Pages[pageIndex].SourcePage);

            for (var elementIndex = 0;
                 elementIndex <
                 assembly.Pages[pageIndex].Elements.Count;
                 elementIndex++)
            {
                Assert.Same(
                    assembly.Pages[pageIndex].Elements[elementIndex],
                    result.Pages[pageIndex].Elements[elementIndex].SourceElement);
            }
        }
    }

    [Fact]
    public void Normalize_TextFlowExcludesMarginsButRetainsVisualAndDeferredEvidence()
    {
        var deferred =
            HybridDocumentElementFactory.FromDeferred(
                Layout(
                    1,
                    sequence: 0,
                    readingOrder: 0,
                    LayoutObservationKind.Unknown,
                    new NormalizedRectangle(
                        0.10,
                        0.20,
                        0.90,
                        0.50)));

        var body =
            NativeElement(
                1,
                sourceSequence: 1,
                readingOrder: 1,
                "body",
                BodyBounds);

        var page =
            Page(
                1,
                deferred,
                body);

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    Assemble(
                        page));

        Assert.Single(
            result.Pages[0].TextFlowElements);

        Assert.Same(
            body,
            result.Pages[0]
                .TextFlowElements[0]
                .SourceElement);

        Assert.True(
            result.Pages[0].HasUnresolvedEvidence);

        Assert.Equal(
            2,
            result.Pages[0].Elements.Count);
    }

    [Fact]
    public void Normalize_HonorsCancellation()
    {
        var assembly =
            Assemble(
                Page(
                    1,
                    NativeElement(
                        1,
                        0,
                        0,
                        "text",
                        BodyBounds)));

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () =>
                new HybridDocumentNormalizer()
                    .Normalize(
                        assembly,
                        cancellation.Token));
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
            0.30,
            0.90,
            0.40);

    private static readonly NormalizedRectangle FooterBounds =
        new(
            0.10,
            0.90,
            0.90,
            0.96);

    private static HybridDocumentAssemblyResult Assemble(
        params HybridDocumentPage[] pages) =>
        new(
            HybridDocumentAssembler.AssemblyProfileId,
            pages);

    private static HybridDocumentPage Page(
        int physicalPageNumber,
        params HybridDocumentElement[] elements) =>
        HybridDocumentAssembler.AssemblePage(
            physicalPageNumber,
            elements);

    private static HybridDocumentElement NativeElement(
        int physicalPageNumber,
        int sourceSequence,
        int? readingOrder,
        string text,
        NormalizedRectangle bounds) =>
        HybridDocumentElementFactory.FromNative(
            physicalPageNumber,
            Block(
                sourceSequence,
                readingOrder,
                text,
                bounds));

    private static HybridDocumentElement OcrOnlyElement(
        int physicalPageNumber,
        int sequence,
        int readingOrder,
        LayoutObservationKind kind,
        NormalizedRectangle bounds,
        params string[] fragments)
    {
        var layout =
            Layout(
                physicalPageNumber,
                sequence,
                readingOrder,
                kind,
                bounds);

        var observations =
            fragments
                .Select(
                    (text, index) =>
                        new OcrTextObservation(
                            physicalPageNumber,
                            sourceLayoutObservationSequence:
                                sequence,
                            observationSequence:
                                index,
                            text,
                            confidence:
                                0.99,
                            bounds))
                .ToArray();

        var ocr =
            new OcrRegionResult(
                "paddleocr-general-ocr",
                "test-profile-v1",
                layout,
                observations);

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

    private static HybridDocumentElement ConflictElement(
        LayoutObservation layout)
    {
        var block =
            Block(
                sourceSequence: 9,
                readingOrder:
                    layout.ReadingOrder,
                "conversion",
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
                    block,
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
            sourceRasterPixelWidth:
                1000,
            sourceRasterPixelHeight:
                1200,
            new PixelRectangle(
                100,
                200,
                600,
                900),
            contentLength:
                1234,
            new string(
                'b',
                64));

    private static LayoutObservation Layout(
        int physicalPageNumber,
        int sequence,
        int readingOrder,
        LayoutObservationKind kind,
        NormalizedRectangle bounds) =>
        new(
            physicalPageNumber,
            sequence,
            readingOrder,
            kind,
            bounds,
            rawLabel:
                kind.ToString());

    private static DocumentTextBlock Block(
        int sourceSequence,
        int? readingOrder,
        string text,
        NormalizedRectangle bounds) =>
        new(
            sourceSequence,
            readingOrder,
            text,
            bounds);
}
