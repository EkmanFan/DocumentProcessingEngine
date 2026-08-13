using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.UnitTests.Documents;

public sealed class DocumentFormatIdTests
{
    [Fact]
    public void Constructor_TrimsValue()
    {
        var format = new DocumentFormatId("  custom-format  ");

        Assert.Equal("custom-format", format.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_RejectsBlankValue(string value)
    {
        Assert.Throws<ArgumentException>(() => new DocumentFormatId(value));
    }

    [Fact]
    public void Pdf_HasStableIdentifier()
    {
        Assert.Equal("pdf", DocumentFormatId.Pdf.Value);
    }
}
