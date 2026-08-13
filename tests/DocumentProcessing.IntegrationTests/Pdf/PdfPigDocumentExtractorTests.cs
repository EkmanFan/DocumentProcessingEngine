using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.IntegrationTests.Pdf;

public sealed class PdfPigDocumentExtractorTests
{
    [Fact]
    public async Task ExtractAsync_ExtractsNativeTextAndStructuredWordsFromGeneratedPdf()
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
        Assert.True(page.SourceWidth > 0);
        Assert.True(page.SourceHeight > 0);
        Assert.NotEmpty(page.Words);
        Assert.Equal(page.Words.Count, page.WordCount);
        Assert.Equal(0, page.RasterImageCount);
        Assert.Equal(0, page.LargestRasterImageAreaRatio);

        Assert.Equal(
            Enumerable.Range(0, page.Words.Count),
            page.Words.Select(word => word.SourceSequence));

        Assert.Contains(
            page.Words,
            word => word.Text.Contains("Hello", StringComparison.Ordinal));

        Assert.All(
            page.Words,
            word =>
            {
                Assert.True(double.IsFinite(word.Bounds.Left));
                Assert.True(double.IsFinite(word.Bounds.Top));
                Assert.True(double.IsFinite(word.Bounds.Right));
                Assert.True(double.IsFinite(word.Bounds.Bottom));
                Assert.True(word.Bounds.Right >= word.Bounds.Left);
                Assert.True(word.Bounds.Bottom >= word.Bounds.Top);
            });
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
