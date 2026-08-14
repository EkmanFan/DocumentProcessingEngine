using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Reconciliation;

public sealed class NativeTextExtentProjectorTests
{
    [Fact]
    public void Project_UsesContiguousBlockWordSpanAndPreservesBlockOrder()
    {
        var first =
            Word(
                sourceSequence: 9,
                text: "first",
                left: 0.10,
                top: 0.10,
                right: 0.20,
                bottom: 0.20);

        var middle =
            Word(
                sourceSequence: 1,
                text: "middle",
                left: 0.70,
                top: 0.70,
                right: 0.80,
                bottom: 0.80);

        var last =
            Word(
                sourceSequence: 7,
                text: "last",
                left: 0.25,
                top: 0.10,
                right: 0.35,
                bottom: 0.20);

        var block =
            new DocumentTextBlock(
                sourceSequence: 3,
                readingOrder: 0,
                text: "first middle last",
                new NormalizedRectangle(
                    0.05,
                    0.05,
                    0.85,
                    0.85),
                new[]
                {
                    first,
                    middle,
                    last
                });

        var layout =
            TextObservation(
                new NormalizedRectangle(
                    0.08,
                    0.08,
                    0.40,
                    0.25));

        var extent =
            NativeTextExtentProjector.Project(
                block,
                layout);

        Assert.NotNull(extent);
        Assert.Equal(
            0,
            extent.FirstWordIndex);
        Assert.Equal(
            2,
            extent.LastWordIndex);
        Assert.Equal(
            2,
            extent.IntersectingWordCount);
        Assert.Equal(
            3,
            extent.WordCount);
        Assert.Equal(
            "first middle last",
            extent.Text);

        Assert.Same(
            first,
            extent.Words[0]);

        Assert.Same(
            middle,
            extent.Words[1]);

        Assert.Same(
            last,
            extent.Words[2]);
    }

    [Fact]
    public void Project_ReturnsNullWhenBlockIntersectsButNoNativeWordIntersects()
    {
        var word =
            Word(
                sourceSequence: 0,
                text: "outside",
                left: 0.70,
                top: 0.70,
                right: 0.80,
                bottom: 0.80);

        var block =
            new DocumentTextBlock(
                sourceSequence: 0,
                readingOrder: 0,
                text: "outside",
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.90,
                    0.90),
                new[]
                {
                    word
                });

        var layout =
            TextObservation(
                new NormalizedRectangle(
                    0.20,
                    0.20,
                    0.30,
                    0.30));

        Assert.Null(
            NativeTextExtentProjector.Project(
                block,
                layout));
    }

    [Fact]
    public void Project_ReturnsNullWhenBlockDoesNotIntersectLayoutRegion()
    {
        var word =
            Word(
                sourceSequence: 0,
                text: "native",
                left: 0.10,
                top: 0.10,
                right: 0.20,
                bottom: 0.20);

        var block =
            new DocumentTextBlock(
                sourceSequence: 0,
                readingOrder: 0,
                text: "native",
                word.Bounds,
                new[]
                {
                    word
                });

        var layout =
            TextObservation(
                new NormalizedRectangle(
                    0.70,
                    0.70,
                    0.80,
                    0.80));

        Assert.Null(
            NativeTextExtentProjector.Project(
                block,
                layout));
    }

    [Fact]
    public void Project_RejectsFigureBeforeProjectingNativeText()
    {
        var word =
            Word(
                sourceSequence: 0,
                text: "not-a-figure-caption",
                left: 0.10,
                top: 0.10,
                right: 0.20,
                bottom: 0.20);

        var block =
            new DocumentTextBlock(
                sourceSequence: 0,
                readingOrder: 0,
                text: word.Text,
                word.Bounds,
                new[]
                {
                    word
                });

        var figure =
            new LayoutObservation(
                physicalPageNumber: 233,
                observationSequence: 4,
                readingOrder: 4,
                LayoutObservationKind.Figure,
                word.Bounds,
                rawLabel: "image");

        Assert.Throws<InvalidOperationException>(
            () =>
                NativeTextExtentProjector.Project(
                    block,
                    figure));
    }

    [Fact]
    public void Project_RetainsSourceEvidenceAndComputesWordUnionBounds()
    {
        var first =
            Word(
                sourceSequence: 0,
                text: "alpha",
                left: 0.20,
                top: 0.30,
                right: 0.30,
                bottom: 0.40);

        var second =
            Word(
                sourceSequence: 1,
                text: "beta",
                left: 0.35,
                top: 0.25,
                right: 0.50,
                bottom: 0.45);

        var block =
            new DocumentTextBlock(
                sourceSequence: 2,
                readingOrder: 1,
                text: "alpha beta",
                new NormalizedRectangle(
                    0.20,
                    0.25,
                    0.50,
                    0.45),
                new[]
                {
                    first,
                    second
                });

        var layout =
            TextObservation(
                new NormalizedRectangle(
                    0.15,
                    0.20,
                    0.55,
                    0.50));

        var extent =
            Assert.IsType<ComparableNativeTextExtent>(
                NativeTextExtentProjector.Project(
                    block,
                    layout));

        Assert.Same(
            block,
            extent.SourceBlock);

        Assert.Same(
            layout,
            extent.SourceLayoutObservation);

        Assert.Equal(
            new NormalizedRectangle(
                0.20,
                0.25,
                0.50,
                0.45),
            extent.Bounds);
    }

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

    private static LayoutObservation TextObservation(
        NormalizedRectangle bounds) =>
        new(
            physicalPageNumber: 1,
            observationSequence: 0,
            readingOrder: 0,
            LayoutObservationKind.Text,
            bounds,
            rawLabel: "text");
}
