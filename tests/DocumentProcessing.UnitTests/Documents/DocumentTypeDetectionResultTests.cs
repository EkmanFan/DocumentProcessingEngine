using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.UnitTests.Documents;

public sealed class DocumentTypeDetectionResultTests
{
    [Fact]
    public void Unknown_IsUnsupportedAndHasNoClaimedFormat()
    {
        var result = DocumentTypeDetectionResult.Unknown;

        Assert.False(result.IsSupported);
        Assert.Null(result.Format);
        Assert.Null(result.DetectedMediaType);
    }

    [Fact]
    public void Result_CanRepresentFutureFormatWithoutChangingCoreEnum()
    {
        var format = new DocumentFormatId("epub");

        var result = new DocumentTypeDetectionResult(
            format,
            "application/epub+zip",
            IsSupported: true);

        Assert.Equal("epub", result.Format?.Value);
        Assert.True(result.IsSupported);
    }
}
