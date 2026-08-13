using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.UnitTests.Extraction;

public sealed class DocumentExtractionPageTests
{
    [Fact]
    public void Constructor_PreservesPageNumberAndText()
    {
        var page = new DocumentExtractionPage(3, "sample text");
        Assert.Equal(3, page.PhysicalPageNumber);
        Assert.Equal("sample text", page.SourceText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsInvalidPhysicalPageNumber(int pageNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DocumentExtractionPage(pageNumber, "text"));
    }
}
