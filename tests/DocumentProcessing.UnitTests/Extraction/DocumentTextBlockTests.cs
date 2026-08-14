using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.UnitTests.Extraction;

public sealed class DocumentTextBlockTests
{
    [Fact]
    public void Constructor_KeepsSourceSequenceIndependentFromReadingOrder()
    {
        var bounds = new NormalizedRectangle(0.1, 0.2, 0.8, 0.4);
        var word = new DocumentWord(
            3,
            "text",
            new NormalizedRectangle(0.1, 0.2, 0.2, 0.3));

        var block = new DocumentTextBlock(
            sourceSequence: 7,
            readingOrder: 2,
            text: "text",
            bounds,
            [word]);

        Assert.Equal(7, block.SourceSequence);
        Assert.Equal(2, block.ReadingOrder!.Value);
        Assert.Equal("text", block.Text);
        Assert.Equal(bounds, block.Bounds);
        Assert.Same(word, Assert.Single(block.Words));
    }

    [Fact]
    public void Constructor_AllowsUnknownReadingOrder()
    {
        var block = new DocumentTextBlock(
            0,
            null,
            "text",
            new NormalizedRectangle(0.1, 0.2, 0.8, 0.4));

        Assert.Null(block.ReadingOrder);
    }

    [Fact]
    public void Constructor_RejectsInvalidValues()
    {
        var bounds = new NormalizedRectangle(0.1, 0.2, 0.8, 0.4);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DocumentTextBlock(-1, 0, "text", bounds));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DocumentTextBlock(0, -1, "text", bounds));

        Assert.Throws<ArgumentException>(
            () => new DocumentTextBlock(0, 0, " ", bounds));
    }
}
