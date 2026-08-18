using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.UnitTests.Hybrid;

public sealed class NativeLayoutVisualPageAssemblerTests
{
    [Fact]
    public void Assemble_FigureBeforeNativeText_ReindexesWithoutChangingNativeAuthority()
    {
        var first =
            CreateSingleWordBlock(
                sourceSequence:
                    0,
                readingOrder:
                    0,
                "Alpha",
                top:
                    0.30);

        var second =
            CreateSingleWordBlock(
                sourceSequence:
                    1,
                readingOrder:
                    1,
                "Beta",
                top:
                    0.60);

        var page =
            CreatePage(
                first,
                second);

        var figure =
            Figure(
                observationSequence:
                    0,
                readingOrder:
                    0,
                top:
                    0.05,
                bottom:
                    0.20);

        var layout =
            Layout(
                figure,
                Text(
                    observationSequence:
                        1,
                    readingOrder:
                        1,
                    top:
                        0.30,
                    bottom:
                        0.36),
                Text(
                    observationSequence:
                        2,
                    readingOrder:
                        2,
                    top:
                        0.60,
                    bottom:
                        0.66));

        var result =
            NativeLayoutVisualPageAssembler
                .Assemble(
                    page,
                    layout,
                    [
                        PreservedVisual(
                            figure)
                    ]);

        Assert.Equal(
            [
                HybridDocumentElementKind.Visual,
                HybridDocumentElementKind.Text,
                HybridDocumentElementKind.Text
            ],
            result.Elements
                .Select(
                    element =>
                        element.Kind));

        Assert.Equal(
            [
                0,
                1,
                2
            ],
            result.Elements
                .Select(
                    element =>
                        element.ReadingOrder));

        Assert.Same(
            first,
            result.Elements[1]
                .NativeBlock);

        Assert.Same(
            second,
            result.Elements[2]
                .NativeBlock);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            result.Elements[1]
                .TextOrigin);

        Assert.Equal(
            "Alpha",
            result.Elements[1]
                .Text);

        Assert.Equal(
            "Beta",
            result.Elements[2]
                .Text);
    }

    [Fact]
    public void Assemble_FigureBetweenNativeBlocks_PreservesNativeRelativeOrder()
    {
        var first =
            CreateSingleWordBlock(
                0,
                0,
                "Before",
                top:
                    0.10);

        var second =
            CreateSingleWordBlock(
                1,
                1,
                "After",
                top:
                    0.70);

        var page =
            CreatePage(
                first,
                second);

        var figure =
            Figure(
                observationSequence:
                    1,
                readingOrder:
                    1,
                top:
                    0.40,
                bottom:
                    0.60);

        var layout =
            Layout(
                Text(
                    0,
                    0,
                    0.10,
                    0.16),
                figure,
                Text(
                    2,
                    2,
                    0.70,
                    0.76));

        var result =
            NativeLayoutVisualPageAssembler
                .Assemble(
                    page,
                    layout,
                    [
                        PreservedVisual(
                            figure)
                    ]);

        Assert.Equal(
            [
                HybridDocumentElementKind.Text,
                HybridDocumentElementKind.Visual,
                HybridDocumentElementKind.Text
            ],
            result.Elements
                .Select(
                    element =>
                        element.Kind));

        Assert.Same(
            first,
            result.Elements[0]
                .NativeBlock);

        Assert.Same(
            second,
            result.Elements[2]
                .NativeBlock);

        Assert.Equal(
            [
                0,
                1,
                2
            ],
            result.Elements
                .Select(
                    element =>
                        element.ReadingOrder));
    }

    [Fact]
    public void Assemble_BlockStraddlesFigure_FailsClosed()
    {
        var block =
            CreateTwoWordBlock(
                sourceSequence:
                    0,
                readingOrder:
                    0,
                "Before",
                firstTop:
                    0.10,
                "After",
                secondTop:
                    0.80);

        var page =
            CreatePage(
                block);

        var figure =
            Figure(
                observationSequence:
                    1,
                readingOrder:
                    1,
                top:
                    0.40,
                bottom:
                    0.60);

        var layout =
            Layout(
                Text(
                    0,
                    0,
                    0.10,
                    0.16),
                figure,
                Text(
                    2,
                    2,
                    0.80,
                    0.86));

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    NativeLayoutVisualPageAssembler
                        .Assemble(
                            page,
                            layout,
                            [
                                PreservedVisual(
                                    figure)
                            ]));

        Assert.Contains(
            "straddles preserved visual",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_UnmappedNativeBlock_FailsClosed()
    {
        var mapped =
            CreateSingleWordBlock(
                0,
                0,
                "Mapped",
                top:
                    0.10);

        var unmapped =
            CreateSingleWordBlock(
                1,
                1,
                "Unmapped",
                top:
                    0.80);

        var page =
            CreatePage(
                mapped,
                unmapped);

        var figure =
            Figure(
                observationSequence:
                    1,
                readingOrder:
                    1,
                top:
                    0.40,
                bottom:
                    0.60);

        var layout =
            Layout(
                Text(
                    0,
                    0,
                    0.10,
                    0.16),
                figure);

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    NativeLayoutVisualPageAssembler
                        .Assemble(
                            page,
                            layout,
                            [
                                PreservedVisual(
                                    figure)
                            ]));

        Assert.Contains(
            "has no deterministic layout text ownership",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "1",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_AmbiguousNativeWordOwnership_FailsClosed()
    {
        var block =
            CreateSingleWordBlock(
                0,
                0,
                "Ambiguous",
                top:
                    0.10);

        var page =
            CreatePage(
                block);

        var figure =
            Figure(
                observationSequence:
                    2,
                readingOrder:
                    2,
                top:
                    0.70,
                bottom:
                    0.90);

        var layout =
            Layout(
                Text(
                    0,
                    0,
                    0.09,
                    0.17),
                Text(
                    1,
                    1,
                    0.08,
                    0.18),
                figure);

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    NativeLayoutVisualPageAssembler
                        .Assemble(
                            page,
                            layout,
                            [
                                PreservedVisual(
                                    figure)
                            ]));

        Assert.Contains(
            "ambiguous native word ownership",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_DuplicateVisualReadingOrder_FailsClosed()
    {
        var block =
            CreateSingleWordBlock(
                0,
                0,
                "Text",
                top:
                    0.70);

        var page =
            CreatePage(
                block);

        var firstFigure =
            Figure(
                observationSequence:
                    0,
                readingOrder:
                    0,
                top:
                    0.05,
                bottom:
                    0.20);

        var secondFigure =
            Figure(
                observationSequence:
                    1,
                readingOrder:
                    0,
                top:
                    0.30,
                bottom:
                    0.45);

        var layout =
            Layout(
                firstFigure,
                secondFigure,
                Text(
                    2,
                    1,
                    0.70,
                    0.76));

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    NativeLayoutVisualPageAssembler
                        .Assemble(
                            page,
                            layout,
                            [
                                PreservedVisual(
                                    firstFigure),
                                PreservedVisual(
                                    secondFigure)
                            ]));

        Assert.Contains(
            "duplicate layout reading order",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_NoVisuals_PreservesExistingNativeOnlyBehavior()
    {
        var first =
            CreateSingleWordBlock(
                0,
                0,
                "Alpha",
                top:
                    0.10);

        var second =
            CreateSingleWordBlock(
                1,
                1,
                "Beta",
                top:
                    0.30);

        var page =
            CreatePage(
                first,
                second);

        var result =
            NativeLayoutVisualPageAssembler
                .Assemble(
                    page,
                    Layout(),
                    []);

        Assert.Equal(
            [
                "Alpha",
                "Beta"
            ],
            result.Elements
                .Select(
                    element =>
                        element.Text));

        Assert.All(
            result.Elements,
            element =>
                Assert.Equal(
                    TextSelectionOrigin.NativePdf,
                    element.TextOrigin));
    }

    private static DocumentExtractionPage CreatePage(
        params DocumentTextBlock[] blocks)
    {
        var words =
            blocks
                .SelectMany(
                    block =>
                        block.Words)
                .ToArray();

        return new DocumentExtractionPage(
            physicalPageNumber:
                1,
            sourceText:
                string.Join(
                    " ",
                    blocks.Select(
                        block =>
                            block.Text)),
            wordCount:
                words.Length,
            rasterImageCount:
                1,
            largestRasterImageAreaRatio:
                0.40,
            sourceWidth:
                612,
            sourceHeight:
                792,
            words,
            blocks);
    }

    private static DocumentTextBlock CreateSingleWordBlock(
        int sourceSequence,
        int readingOrder,
        string text,
        double top)
    {
        var word =
            new DocumentWord(
                sourceSequence:
                    0,
                text,
                new NormalizedRectangle(
                    0.10,
                    top,
                    0.40,
                    top +
                    0.05));

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder,
            text,
            new NormalizedRectangle(
                0.10,
                top,
                0.40,
                top +
                0.05),
            [
                word
            ]);
    }

    private static DocumentTextBlock CreateTwoWordBlock(
        int sourceSequence,
        int readingOrder,
        string firstText,
        double firstTop,
        string secondText,
        double secondTop)
    {
        var first =
            new DocumentWord(
                sourceSequence:
                    0,
                firstText,
                new NormalizedRectangle(
                    0.10,
                    firstTop,
                    0.40,
                    firstTop +
                    0.05));

        var second =
            new DocumentWord(
                sourceSequence:
                    1,
                secondText,
                new NormalizedRectangle(
                    0.10,
                    secondTop,
                    0.40,
                    secondTop +
                    0.05));

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder,
            $"{firstText} {secondText}",
            new NormalizedRectangle(
                0.10,
                firstTop,
                0.40,
                secondTop +
                0.05),
            [
                first,
                second
            ]);
    }

    private static LayoutAnalysisResult Layout(
        params LayoutObservation[] observations) =>
        new(
            "fake-layout",
            physicalPageNumber:
                1,
            observations);

    private static LayoutObservation Text(
        int observationSequence,
        int readingOrder,
        double top,
        double bottom) =>
        new(
            physicalPageNumber:
                1,
            observationSequence,
            readingOrder,
            LayoutObservationKind.Text,
            new NormalizedRectangle(
                0.05,
                top,
                0.95,
                bottom));

    private static LayoutObservation Figure(
        int observationSequence,
        int readingOrder,
        double top,
        double bottom) =>
        new(
            physicalPageNumber:
                1,
            observationSequence,
            readingOrder,
            LayoutObservationKind.Figure,
            new NormalizedRectangle(
                0.05,
                top,
                0.95,
                bottom));

    private static HybridDocumentElement PreservedVisual(
        LayoutObservation figure)
    {
        var evidence =
            new PreservedVisualEvidence(
                sourceDocumentSha256:
                    new string(
                        'a',
                        64),
                profileId:
                    "test-visual-v1",
                mediaType:
                    "image/png",
                sourceLayoutObservation:
                    figure,
                sourceRasterPixelWidth:
                    1000,
                sourceRasterPixelHeight:
                    1000,
                crop:
                    new PixelRectangle(
                        50,
                        50,
                        950,
                        950),
                contentLength:
                    4,
                contentSha256:
                    new string(
                        'b',
                        64));

        return HybridDocumentElementFactory
            .FromPreservedVisual(
                evidence);
    }
}
