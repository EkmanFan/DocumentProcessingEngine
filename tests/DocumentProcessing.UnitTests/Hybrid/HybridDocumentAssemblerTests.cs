using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Hybrid;

public sealed class HybridDocumentAssemblerTests
{
    [Fact]
    public void AssemblePage_OrdersMixedContentAndKeepsFigureTextless()
    {
        var heading =
            Layout(
                sequence: 2,
                readingOrder: 2,
                LayoutObservationKind.Heading,
                0.10,
                0.10,
                0.90,
                0.18);

        var leftBody =
            Layout(
                sequence: 3,
                readingOrder: 3,
                LayoutObservationKind.Text,
                0.10,
                0.20,
                0.45,
                0.60);

        var figure =
            Layout(
                sequence: 4,
                readingOrder: 4,
                LayoutObservationKind.Figure,
                0.25,
                0.40,
                0.60,
                0.85);

        var caption =
            Layout(
                sequence: 5,
                readingOrder: 5,
                LayoutObservationKind.Caption,
                0.20,
                0.86,
                0.70,
                0.92);

        var rightBody =
            Layout(
                sequence: 6,
                readingOrder: 6,
                LayoutObservationKind.Text,
                0.55,
                0.20,
                0.90,
                0.60);

        var elements =
            new[]
            {
                HybridDocumentElementFactory.FromReconciliation(
                    MissingOcr(
                        rightBody,
                        "right body")),

                HybridDocumentElementFactory.FromPreservedVisual(
                    Visual(
                        figure)),

                HybridDocumentElementFactory.FromReconciliation(
                    MissingOcr(
                        heading,
                        "SECTION TITLE")),

                HybridDocumentElementFactory.FromReconciliation(
                    MissingOcr(
                        caption,
                        "Figure caption")),

                HybridDocumentElementFactory.FromReconciliation(
                    MissingOcr(
                        leftBody,
                        "left body"))
            };

        var page =
            HybridDocumentAssembler.AssemblePage(
                physicalPageNumber: 233,
                elements);

        Assert.Equal(
            new[]
            {
                HybridDocumentElementKind.Heading,
                HybridDocumentElementKind.Text,
                HybridDocumentElementKind.Visual,
                HybridDocumentElementKind.Caption,
                HybridDocumentElementKind.Text
            },
            page.Elements.Select(
                element =>
                    element.Kind));

        Assert.Equal(
            new[]
            {
                2,
                3,
                4,
                5,
                6
            },
            page.Elements.Select(
                element =>
                    element.ReadingOrder));

        var visual =
            page.Elements.Single(
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Visual);

        Assert.Null(
            visual.Text);

        Assert.False(
            visual.HasAuthoritativeText);

        Assert.Equal(
            TextSelectionOrigin.None,
            visual.TextOrigin);

        Assert.NotNull(
            visual.PreservedVisual);

        Assert.Equal(
            4,
            page.AuthoritativeTextElements.Count);
    }

    [Fact]
    public void FromReconciliation_ConflictBecomesTextlessUnresolvedElement()
    {
        var layout =
            Layout(
                sequence: 7,
                readingOrder: 7,
                LayoutObservationKind.Text,
                0.10,
                0.20,
                0.90,
                0.40);

        var native =
            Block(
                sourceSequence: 9,
                readingOrder: 7,
                "conversion",
                Word(
                    0,
                    "conversion",
                    0.10,
                    0.20,
                    0.30,
                    0.25));

        var ocr =
            Ocr(
                layout,
                "conversior");

        var conflict =
            NativeOcrTextReconciler.Reconcile(
                new TextReconciliationInput(
                    physicalPageNumber: 233,
                    NativeTextStatus.Suspicious,
                    native,
                    ocr));

        var element =
            HybridDocumentElementFactory
                .FromReconciliation(
                    conflict);

        Assert.Equal(
            HybridDocumentElementKind.UnresolvedText,
            element.Kind);

        Assert.Null(
            element.Text);

        Assert.Equal(
            TextSelectionOrigin.None,
            element.TextOrigin);

        Assert.False(
            element.HasAuthoritativeText);

        Assert.Same(
            conflict,
            element.Reconciliation);
    }

    [Fact]
    public void FromDeferred_PreservesUnknownWithoutMakingItText()
    {
        var unknown =
            Layout(
                sequence: 1,
                readingOrder: 1,
                LayoutObservationKind.Unknown,
                0.05,
                0.05,
                0.20,
                0.10);

        var element =
            HybridDocumentElementFactory.FromDeferred(
                unknown);

        Assert.Equal(
            HybridDocumentElementKind.Deferred,
            element.Kind);

        Assert.Null(
            element.Text);

        Assert.False(
            element.HasAuthoritativeText);

        Assert.Same(
            unknown,
            element.LayoutObservation);
    }

    [Fact]
    public void FromNative_UsesNativeReadingOrderAndFallsBackToSourceSequence()
    {
        var withReadingOrder =
            Block(
                sourceSequence: 8,
                readingOrder: 3,
                "native one",
                Word(
                    0,
                    "native",
                    0.10,
                    0.10,
                    0.20,
                    0.15));

        var fallback =
            Block(
                sourceSequence: 6,
                readingOrder: null,
                "native two",
                Word(
                    0,
                    "native",
                    0.10,
                    0.20,
                    0.20,
                    0.25));

        var first =
            HybridDocumentElementFactory.FromNative(
                12,
                withReadingOrder);

        var second =
            HybridDocumentElementFactory.FromNative(
                12,
                fallback);

        Assert.Equal(
            3,
            first.ReadingOrder);

        Assert.Equal(
            6,
            second.ReadingOrder);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            first.TextOrigin);

        Assert.True(
            first.HasAuthoritativeText);
    }

    [Fact]
    public void AssemblePage_RejectsDuplicateLayoutObservation()
    {
        var layout =
            Layout(
                sequence: 3,
                readingOrder: 3,
                LayoutObservationKind.Text,
                0.10,
                0.10,
                0.90,
                0.20);

        var first =
            HybridDocumentElementFactory.FromReconciliation(
                MissingOcr(
                    layout,
                    "alpha"));

        var duplicate =
            HybridDocumentElementFactory.FromReconciliation(
                MissingOcr(
                    layout,
                    "alpha"));

        Assert.Throws<InvalidOperationException>(
            () =>
                HybridDocumentAssembler.AssemblePage(
                    233,
                    new[]
                    {
                        first,
                        duplicate
                    }));
    }

    [Fact]
    public void AssemblePage_RejectsStandaloneNativePlusReconciledSameBlock()
    {
        var words =
            new[]
            {
                Word(0, "alpha", 0.10, 0.20, 0.20, 0.25),
                Word(1, "beta", 0.22, 0.20, 0.30, 0.25)
            };

        var block =
            Block(
                sourceSequence: 5,
                readingOrder: 1,
                "alpha beta",
                words);

        var layout =
            Layout(
                sequence: 8,
                readingOrder: 2,
                LayoutObservationKind.Text,
                0.08,
                0.18,
                0.35,
                0.28,
                physicalPageNumber: 20);

        var ocr =
            Ocr(
                layout,
                "alpha beta");

        var input =
            new TextReconciliationInput(
                20,
                NativeTextStatus.Healthy,
                block,
                ocr);

        var extent =
            NativeTextExtentProjector.Project(
                block,
                layout) ??
            throw new InvalidOperationException(
                "Expected comparable extent.");

        var reconciled =
            NativeOcrTextReconciler.ReconcileComparable(
                input,
                extent);

        var nativeElement =
            HybridDocumentElementFactory.FromNative(
                20,
                block);

        var reconciledElement =
            HybridDocumentElementFactory.FromReconciliation(
                reconciled);

        Assert.Throws<InvalidOperationException>(
            () =>
                HybridDocumentAssembler.AssemblePage(
                    20,
                    new[]
                    {
                        nativeElement,
                        reconciledElement
                    }));
    }

    [Fact]
    public void AssemblePage_AllowsNonOverlappingComparableExtentsFromSameNativeBlock()
    {
        var words =
            new[]
            {
                Word(0, "alpha", 0.10, 0.20, 0.18, 0.25),
                Word(1, "beta", 0.20, 0.20, 0.28, 0.25),
                Word(2, "gamma", 0.55, 0.20, 0.65, 0.25),
                Word(3, "delta", 0.67, 0.20, 0.77, 0.25)
            };

        var block =
            Block(
                sourceSequence: 5,
                readingOrder: 0,
                "alpha beta gamma delta",
                words);

        var left =
            Layout(
                sequence: 10,
                readingOrder: 1,
                LayoutObservationKind.Text,
                0.08,
                0.18,
                0.32,
                0.28,
                physicalPageNumber: 21);

        var right =
            Layout(
                sequence: 11,
                readingOrder: 2,
                LayoutObservationKind.Text,
                0.52,
                0.18,
                0.80,
                0.28,
                physicalPageNumber: 21);

        var first =
            ReconcileComparable(
                physicalPageNumber: 21,
                block,
                left,
                "alpha beta");

        var second =
            ReconcileComparable(
                physicalPageNumber: 21,
                block,
                right,
                "gamma delta");

        var page =
            HybridDocumentAssembler.AssemblePage(
                21,
                new[]
                {
                    HybridDocumentElementFactory.FromReconciliation(
                        second),
                    HybridDocumentElementFactory.FromReconciliation(
                        first)
                });

        Assert.Equal(
            2,
            page.Elements.Count);

        Assert.Equal(
            new[]
            {
                "alpha beta",
                "gamma delta"
            },
            page.AuthoritativeTextElements.Select(
                element =>
                    element.Text));
    }

    [Fact]
    public void AssemblePage_RejectsAmbiguousReadingOrder()
    {
        var first =
            HybridDocumentElementFactory.FromNative(
                30,
                Block(
                    sourceSequence: 1,
                    readingOrder: 5,
                    "one",
                    Word(
                        0,
                        "one",
                        0.10,
                        0.10,
                        0.20,
                        0.15)));

        var second =
            HybridDocumentElementFactory.FromNative(
                30,
                Block(
                    sourceSequence: 2,
                    readingOrder: 5,
                    "two",
                    Word(
                        0,
                        "two",
                        0.10,
                        0.20,
                        0.20,
                        0.25)));

        Assert.Throws<InvalidOperationException>(
            () =>
                HybridDocumentAssembler.AssemblePage(
                    30,
                    new[]
                    {
                        first,
                        second
                    }));
    }

    [Fact]
    public void AssembleDocument_OrdersPhysicalPagesAndRetainsUnresolvedSignal()
    {
        var page2 =
            HybridDocumentAssembler.AssemblePage(
                2,
                new[]
                {
                    HybridDocumentElementFactory.FromDeferred(
                        Layout(
                            sequence: 0,
                            readingOrder: 0,
                            LayoutObservationKind.Table,
                            0.10,
                            0.10,
                            0.90,
                            0.50,
                            physicalPageNumber: 2))
                });

        var page1 =
            HybridDocumentAssembler.AssemblePage(
                1,
                new[]
                {
                    HybridDocumentElementFactory.FromNative(
                        1,
                        Block(
                            sourceSequence: 0,
                            readingOrder: 0,
                            "native",
                            Word(
                                0,
                                "native",
                                0.10,
                                0.10,
                                0.20,
                                0.15)))
                });

        var result =
            HybridDocumentAssembler.AssembleDocument(
                new[]
                {
                    page2,
                    page1
                });

        Assert.Equal(
            HybridDocumentAssembler.AssemblyProfileId,
            result.AssemblyProfileId);

        Assert.Equal(
            new[]
            {
                1,
                2
            },
            result.Pages.Select(
                page =>
                    page.PhysicalPageNumber));

        Assert.True(
            result.HasUnresolvedEvidence);
    }

    [Fact]
    public void FromPreservedVisual_RejectsNonFigureLayout()
    {
        var text =
            Layout(
                sequence: 1,
                readingOrder: 1,
                LayoutObservationKind.Text,
                0.10,
                0.10,
                0.90,
                0.20);

        Assert.Throws<InvalidOperationException>(
            () =>
                HybridDocumentElementFactory.FromPreservedVisual(
                    Visual(
                        text)));
    }

    private static TextReconciliationResult MissingOcr(
        LayoutObservation layout,
        string text)
    {
        var ocr =
            Ocr(
                layout,
                text);

        return NativeOcrTextReconciler.Reconcile(
            new TextReconciliationInput(
                layout.PhysicalPageNumber,
                NativeTextStatus.Missing,
                nativeBlock: null,
                ocr));
    }

    private static TextReconciliationResult ReconcileComparable(
        int physicalPageNumber,
        DocumentTextBlock block,
        LayoutObservation layout,
        string ocrText)
    {
        var ocr =
            Ocr(
                layout,
                ocrText);

        var input =
            new TextReconciliationInput(
                physicalPageNumber,
                NativeTextStatus.Healthy,
                block,
                ocr);

        var extent =
            NativeTextExtentProjector.Project(
                block,
                layout) ??
            throw new InvalidOperationException(
                "Expected comparable extent.");

        return NativeOcrTextReconciler.ReconcileComparable(
            input,
            extent);
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

    private static OcrRegionResult Ocr(
        LayoutObservation layout,
        string text) =>
        new(
            "paddleocr-general-ocr",
            "test-profile-v1",
            layout,
            new[]
            {
                new OcrTextObservation(
                    layout.PhysicalPageNumber,
                    layout.ObservationSequence,
                    observationSequence: 0,
                    text,
                    confidence: 0.99,
                    layout.Bounds)
            });

    private static LayoutObservation Layout(
        int sequence,
        int readingOrder,
        LayoutObservationKind kind,
        double left,
        double top,
        double right,
        double bottom,
        int physicalPageNumber = 233) =>
        new(
            physicalPageNumber,
            sequence,
            readingOrder,
            kind,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            rawLabel: kind.ToString());

    private static DocumentTextBlock Block(
        int sourceSequence,
        int? readingOrder,
        string text,
        params DocumentWord[] words) =>
        new(
            sourceSequence,
            readingOrder,
            text,
            new NormalizedRectangle(
                words.Min(
                    word =>
                        word.Bounds.Left),
                words.Min(
                    word =>
                        word.Bounds.Top),
                words.Max(
                    word =>
                        word.Bounds.Right),
                words.Max(
                    word =>
                        word.Bounds.Bottom)),
            words);

    private static DocumentWord Word(
        int sourceSequence,
        string text,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            sourceSequence,
            text,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom));
}
