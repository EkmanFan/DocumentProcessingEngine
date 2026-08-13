using System.Text;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.IntegrationTests.Pdf;

public sealed class PdfDocumentTypeDetectorTests
{
    [Fact]
    public async Task DetectAsync_DetectsPdfSignatureAndRestoresStreamPosition()
    {
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\nfixture"));
        stream.Position = 3;
        var source = new DocumentSource(stream, "fixture.bin", "application/octet-stream");
        var detector = new PdfDocumentTypeDetector();

        var result = await detector.DetectAsync(source);

        Assert.True(result.IsSupported);
        Assert.Equal(DocumentFormatId.Pdf, result.Format);
        Assert.Equal("application/pdf", result.DetectedMediaType);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public async Task DetectAsync_DoesNotTrustFileNameOrDeclaredMediaType()
    {
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not a pdf"));
        var source = new DocumentSource(stream, "fixture.pdf", "application/pdf");
        var detector = new PdfDocumentTypeDetector();

        var result = await detector.DetectAsync(source);

        Assert.Equal(DocumentTypeDetectionResult.Unknown, result);
    }
}
