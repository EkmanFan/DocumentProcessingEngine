using UglyToad.PdfPig.Content;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.IntegrationTests.Pdf;

public sealed class PdfPigDocumentExtractorTests
{
    [Fact]
    public async Task ExtractAsync_ExtractsNativeTextFromGeneratedPdf()
    {
        var pdfBytes = CreateSinglePagePdf("Hello PDF");
        await using var stream = new MemoryStream(pdfBytes);
        var source = new DocumentSource(stream, "fixture.pdf", "application/pdf");
        var extractor = new PdfPigDocumentExtractor();

        var result = await extractor.ExtractAsync(source, DocumentFormatId.Pdf);

        Assert.Equal(DocumentFormatId.Pdf, result.Format);
        var page = Assert.Single(result.Pages);
        Assert.Equal(1, page.PhysicalPageNumber);
        Assert.Contains("Hello PDF", page.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAsync_RestoresSeekableCallerStreamPosition()
    {
        var pdfBytes = CreateSinglePagePdf("Stream ownership");
        await using var stream = new MemoryStream(pdfBytes);
        stream.Position = 7;
        var source = new DocumentSource(stream);
        var extractor = new PdfPigDocumentExtractor();

        _ = await extractor.ExtractAsync(source, DocumentFormatId.Pdf);

        Assert.Equal(7, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task ExtractAsync_RejectsUnsupportedFormat()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var source = new DocumentSource(stream);
        var extractor = new PdfPigDocumentExtractor();

        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await extractor.ExtractAsync(source, new DocumentFormatId("epub")));
    }

    private static byte[] CreateSinglePagePdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(text, 12, new PdfPoint(72, 720), font);
        return builder.Build();
    }
}
