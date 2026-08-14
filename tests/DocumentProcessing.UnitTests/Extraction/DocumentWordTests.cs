using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.UnitTests.Extraction;

public sealed class DocumentWordTests
{
    [Fact]
    public void Constructor_PreservesSourceSequenceTextAndGeometry()
    {
        var bounds = new NormalizedRectangle(0.1, 0.2, 0.3, 0.4);

        var word = new DocumentWord(7, "text", bounds);

        Assert.Equal(7, word.SourceSequence);
        Assert.Equal("text", word.Text);
        Assert.Equal(bounds, word.Bounds);
    }

    [Fact]
    public void Constructor_RejectsInvalidValues()
    {
        var bounds = new NormalizedRectangle(0.1, 0.2, 0.3, 0.4);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DocumentWord(-1, "text", bounds));

        Assert.Throws<ArgumentException>(
            () => new DocumentWord(0, " ", bounds));
    }
}
