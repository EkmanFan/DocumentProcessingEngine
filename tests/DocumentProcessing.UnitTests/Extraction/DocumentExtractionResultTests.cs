using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.UnitTests.Extraction;

public sealed class DocumentExtractionResultTests
{
    [Fact]
    public void Constructor_PreservesFormat()
    {
        var format = new DocumentFormatId("epub");

        var result = new DocumentExtractionResult(format);

        Assert.Equal(format, result.Format);
    }
}
