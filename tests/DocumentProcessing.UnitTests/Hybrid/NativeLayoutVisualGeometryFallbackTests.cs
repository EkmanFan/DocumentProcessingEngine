using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.UnitTests.Hybrid;

public sealed class NativeLayoutVisualGeometryFallbackTests
{
    #region Variables and Constants

    #endregion

    #region ctor

    #endregion

    #region Methods Tests

    [Fact]
    public void Assemble_UnmappedBlockAboveVisual_UsesGeometryFallback()
    {
        var block =
            Block(
                0,
                0,
                "Above",
                top:
                    0.10,
                bottom:
                    0.15);

        var figure =
            Figure(
                sequence:
                    0,
                readingOrder:
                    0,
                top:
                    0.40,
                bottom:
                    0.60);

        var result =
            NativeLayoutVisualPageAssembler
                .Assemble(
                    Page(
                        block),
                    Layout(
                        figure),
                    [
                        PreservedVisual(
                            figure)
                    ]);

        Assert.Equal(
            [
                HybridDocumentElementKind.Text,
                HybridDocumentElementKind.Visual
            ],
            result.Elements
                .Select(
                    element =>
                        element.Kind));

        Assert.Same(
            block,
            result.Elements[0]
                .NativeBlock);
    }

    [Fact]
    public void Assemble_UnmappedBlockBelowVisual_UsesGeometryFallback()
    {
        var block =
            Block(
                0,
                0,
                "Below",
                top:
                    0.80,
                bottom:
                    0.85);

        var figure =
            Figure(
                sequence:
                    0,
                readingOrder:
                    0,
                top:
                    0.40,
                bottom:
                    0.60);

        var result =
            NativeLayoutVisualPageAssembler
                .Assemble(
                    Page(
                        block),
                    Layout(
                        figure),
                    [
                        PreservedVisual(
                            figure)
                    ]);

        Assert.Equal(
            [
                HybridDocumentElementKind.Visual,
                HybridDocumentElementKind.Text
            ],
            result.Elements
                .Select(
                    element =>
                        element.Kind));

        Assert.Same(
            block,
            result.Elements[1]
                .NativeBlock);
    }

    [Fact]
    public void Assemble_UnmappedBlockBetweenTwoVisuals_UsesMiddleGeometryBand()
    {
        var block =
            Block(
                0,
                0,
                "Middle",
                top:
                    0.45,
                bottom:
                    0.50);

        var firstFigure =
            Figure(
                sequence:
                    0,
                readingOrder:
                    0,
                top:
                    0.10,
                bottom:
                    0.30);

        var secondFigure =
            Figure(
                sequence:
                    1,
                readingOrder:
                    1,
                top:
                    0.70,
                bottom:
                    0.90);

        var result =
            NativeLayoutVisualPageAssembler
                .Assemble(
                    Page(
                        block),
                    Layout(
                        firstFigure,
                        secondFigure),
                    [
                        PreservedVisual(
                            firstFigure),
                        PreservedVisual(
                            secondFigure)
                    ]);

        Assert.Equal(
            [
                HybridDocumentElementKind.Visual,
                HybridDocumentElementKind.Text,
                HybridDocumentElementKind.Visual
            ],
            result.Elements
                .Select(
                    element =>
                        element.Kind));

        Assert.Same(
            block,
            result.Elements[1]
                .NativeBlock);
    }

    [Fact]
    public void Assemble_UnmappedBlockWithGeometryConflictingWithVisualOrder_FailsClosed()
    {
        var block =
            Block(
                0,
                0,
                "Middle",
                top:
                    0.45,
                bottom:
                    0.50);

        var firstInReadingOrderButBelow =
            Figure(
                sequence:
                    0,
                readingOrder:
                    0,
                top:
                    0.70,
                bottom:
                    0.90);

        var secondInReadingOrderButAbove =
            Figure(
                sequence:
                    1,
                readingOrder:
                    1,
                top:
                    0.10,
                bottom:
                    0.30);

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    NativeLayoutVisualPageAssembler
                        .Assemble(
                            Page(
                                block),
                            Layout(
                                firstInReadingOrderButBelow,
                                secondInReadingOrderButAbove),
                            [
                                PreservedVisual(
                                    firstInReadingOrderButBelow),
                                PreservedVisual(
                                    secondInReadingOrderButAbove)
                            ]));

        Assert.Contains(
            "conflicts with layout reading order",
            exception.Message,
            StringComparison.Ordinal);
    }

    #endregion

    #region Methods Helpers

    private static DocumentExtractionPage Page(
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

    private static DocumentTextBlock Block(
        int sourceSequence,
        int readingOrder,
        string text,
        double top,
        double bottom)
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
                    bottom));

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder,
            text,
            new NormalizedRectangle(
                0.10,
                top,
                0.40,
                bottom),
            [
                word
            ]);
    }

    private static LayoutAnalysisResult Layout(
        params LayoutObservation[] observations) =>
        new(
            "fake-layout",
            physicalPageNumber:
                1,
            observations);

    private static LayoutObservation Figure(
        int sequence,
        int readingOrder,
        double top,
        double bottom) =>
        new(
            physicalPageNumber:
                1,
            observationSequence:
                sequence,
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

    #endregion
}
