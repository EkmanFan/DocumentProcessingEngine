using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.UnitTests.Extraction;

public sealed class TypographicEvidenceTests
{
    [Fact]
    public void DocumentWord_PreservesOptionalTypography()
    {
        var word =
            new DocumentWord(
                0,
                "Heading",
                Bounds,
                "Helvetica-Bold",
                18);

        Assert.Equal(
            "Helvetica-Bold",
            word.FontName);

        Assert.Equal(
            18,
            word.MedianPointSize);
    }

    [Fact]
    public void DocumentWord_RejectsInvalidPointSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentWord(
                    0,
                    "Word",
                    Bounds,
                    medianPointSize: 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentWord(
                    0,
                    "Word",
                    Bounds,
                    medianPointSize:
                        double.NaN));
    }

    [Fact]
    public void DocumentTextBlock_PreservesTypographyAndDerivesWordCount()
    {
        var words =
            new[]
            {
                new DocumentWord(
                    0,
                    "Chapter",
                    Bounds,
                    "Helvetica-Bold",
                    18),
                new DocumentWord(
                    1,
                    "One",
                    Bounds,
                    "Helvetica-Bold",
                    18)
            };

        var block =
            new DocumentTextBlock(
                0,
                0,
                "Chapter One",
                Bounds,
                words,
                "Helvetica-Bold",
                18,
                lineCount: 1);

        Assert.Equal(
            "Helvetica-Bold",
            block.DominantFontName);

        Assert.Equal(
            18,
            block.MedianPointSize);

        Assert.Equal(
            1,
            block.LineCount);

        Assert.Equal(
            2,
            block.WordCount);
    }

    [Fact]
    public void DocumentTextBlock_RejectsInvalidTypographyMetadata()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentTextBlock(
                    0,
                    0,
                    "Block",
                    Bounds,
                    medianPointSize: -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentTextBlock(
                    0,
                    0,
                    "Block",
                    Bounds,
                    lineCount: -1));
    }

    private static readonly NormalizedRectangle Bounds =
        new(
            0.1,
            0.1,
            0.9,
            0.2);
}
