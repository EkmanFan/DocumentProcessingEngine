using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Pdf.Notes;

namespace DocumentProcessing.UnitTests.Pdf;

public sealed class PdfRaisedNumericReferenceFinderTests
{
    #region Tests

    [Fact]
    public void Find_UsesBlockTypographyWhenAnchorUsesSmallCapitals()
    {
        var block =
            Block(
                sourceSequence:
                    0,
                medianPointSize:
                    12,
                [
                    Word(0, "90", 0.10, 0.30, 0.15, 0.32, 12),
                    Word(1, "CE.", 0.16, 0.30, 0.20, 0.32, 9),
                    Word(2, "27", 0.20, 0.285, 0.22, 0.305, 9)
                ]);

        var reference =
            Assert.Single(
                PdfRaisedNumericReferenceFinder.Find(
                    1,
                    [block]));

        Assert.Equal(
            "27",
            reference.Value);
    }

    [Fact]
    public void Find_UsesUniqueSpatialAnchorAcrossNativeBlocks()
    {
        var markerBlock =
            Block(
                sourceSequence:
                    0,
                medianPointSize:
                    12,
                [
                    Word(1, "unrelated", 0.70, 0.30, 0.80, 0.32, 12),
                    Word(2, "29", 0.20, 0.285, 0.22, 0.305, 9)
                ]);

        var anchorBlock =
            Block(
                sourceSequence:
                    1,
                medianPointSize:
                    9,
                [
                    Word(0, "CE.", 0.16, 0.30, 0.20, 0.32, 9)
                ]);

        var reference =
            Assert.Single(
                PdfRaisedNumericReferenceFinder.Find(
                    1,
                    [
                        markerBlock,
                        anchorBlock
                    ]));

        Assert.Equal(
            "29",
            reference.Value);
    }

    [Fact]
    public void Find_UsesUniqueSpatialAnchorWhenMarkerStartsNativeBlock()
    {
        var markerBlock =
            Block(
                sourceSequence:
                    0,
                medianPointSize:
                    10,
                [
                    Word(14, "5", 0.7032, 0.4091, 0.7102, 0.4151, 7.5)
                ]);

        var anchorBlock =
            Block(
                sourceSequence:
                    1,
                medianPointSize:
                    10,
                [
                    Word(13, "church.", 0.6500, 0.4087, 0.7032, 0.4175, 10)
                ]);

        var reference =
            Assert.Single(
                PdfRaisedNumericReferenceFinder.Find(
                    25,
                    [
                        markerBlock,
                        anchorBlock
                    ]));

        Assert.Equal(
            "5",
            reference.Value);
    }

    [Fact]
    public void Find_DoesNotUseAmbiguousSpatialAnchorForFirstBlockWord()
    {
        var markerBlock =
            Block(
                sourceSequence:
                    0,
                medianPointSize:
                    10,
                [
                    Word(14, "5", 0.7032, 0.4091, 0.7102, 0.4151, 7.5)
                ]);

        var firstAnchorBlock =
            Block(
                sourceSequence:
                    1,
                medianPointSize:
                    10,
                [
                    Word(13, "church.", 0.6500, 0.4087, 0.7032, 0.4175, 10)
                ]);

        var secondAnchorBlock =
            Block(
                sourceSequence:
                    2,
                medianPointSize:
                    10,
                [
                    Word(15, "duplicate", 0.6400, 0.4087, 0.7032, 0.4175, 10)
                ]);

        var references =
            PdfRaisedNumericReferenceFinder.Find(
                25,
                [
                    markerBlock,
                    firstAnchorBlock,
                    secondAnchorBlock
                ]);

        Assert.Empty(
            references);
    }

    [Fact]
    public void Find_DoesNotUseUnraisedSpatialAnchorForFirstBlockWord()
    {
        var markerBlock =
            Block(
                sourceSequence:
                    0,
                medianPointSize:
                    10,
                [
                    Word(14, "5", 0.7032, 0.4087, 0.7102, 0.4175, 7.5)
                ]);

        var anchorBlock =
            Block(
                sourceSequence:
                    1,
                medianPointSize:
                    10,
                [
                    Word(13, "church.", 0.6500, 0.4087, 0.7032, 0.4175, 10)
                ]);

        var references =
            PdfRaisedNumericReferenceFinder.Find(
                25,
                [
                    markerBlock,
                    anchorBlock
                ]);

        Assert.Empty(
            references);
    }

    #endregion

    #region Helpers

    private static DocumentTextBlock Block(
        int sourceSequence,
        double medianPointSize,
        IReadOnlyList<DocumentWord> words) =>
        new(
            sourceSequence,
            readingOrder:
                sourceSequence,
            string.Join(
                " ",
                words.Select(
                    word =>
                        word.Text)),
            new NormalizedRectangle(
                0.10,
                0.20,
                0.90,
                0.40),
            words,
            medianPointSize:
                medianPointSize,
            lineCount:
                1);

    private static DocumentWord Word(
        int sourceSequence,
        string text,
        double left,
        double top,
        double right,
        double bottom,
        double pointSize) =>
        new(
            sourceSequence,
            text,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            fontName:
                "Test",
            medianPointSize:
                pointSize);

    #endregion
}
