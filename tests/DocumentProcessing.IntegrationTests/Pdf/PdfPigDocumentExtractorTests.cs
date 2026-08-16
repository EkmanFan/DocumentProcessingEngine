using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.IntegrationTests.Pdf;

public sealed class PdfPigDocumentExtractorTests
{
    [Fact]
    public async Task ExtractAsync_ExtractsNativeTextWordsAndLayoutBlocksFromGeneratedPdf()
    {
        var pdfBytes =
            CreateSinglePagePdf(
                "Hello PDF");

        await using var stream =
            new MemoryStream(pdfBytes);

        var source =
            new DocumentSource(
                stream,
                "fixture.pdf",
                "application/pdf");

        var extractor =
            new PdfPigDocumentExtractor();

        var result =
            await extractor.ExtractAsync(
                source,
                DocumentFormatId.Pdf);

        Assert.Equal(
            DocumentFormatId.Pdf,
            result.Format);

        var page =
            Assert.Single(
                result.Pages);

        Assert.Equal(
            1,
            page.PhysicalPageNumber);

        Assert.Contains(
            "Hello PDF",
            page.SourceText,
            StringComparison.Ordinal);

        Assert.True(
            page.SourceWidth > 0);

        Assert.True(
            page.SourceHeight > 0);

        Assert.NotEmpty(
            page.Words);

        Assert.Equal(
            page.Words.Count,
            page.WordCount);

        Assert.Equal(
            0,
            page.RasterImageCount);

        Assert.Equal(
            0,
            page.LargestRasterImageAreaRatio);

        Assert.Equal(
            Enumerable.Range(
                0,
                page.Words.Count),
            page.Words.Select(word =>
                word.SourceSequence));

        Assert.Contains(
            page.Words,
            word =>
                word.Text.Contains(
                    "Hello",
                    StringComparison.Ordinal));

        Assert.All(
            page.Words,
            word =>
            {
                Assert.True(
                    double.IsFinite(
                        word.Bounds.Left));

                Assert.True(
                    double.IsFinite(
                        word.Bounds.Top));

                Assert.True(
                    double.IsFinite(
                        word.Bounds.Right));

                Assert.True(
                    double.IsFinite(
                        word.Bounds.Bottom));

                Assert.True(
                    word.Bounds.Right >=
                    word.Bounds.Left);

                Assert.True(
                    word.Bounds.Bottom >=
                    word.Bounds.Top);

                Assert.False(
                    string.IsNullOrWhiteSpace(
                        word.FontName));

                Assert.True(
                    word.MedianPointSize is > 0);
            });

        Assert.NotEmpty(
            page.Blocks);

        Assert.Equal(
            Enumerable.Range(
                0,
                page.Blocks.Count),
            page.Blocks.Select(block =>
                block.ReadingOrder!.Value));

        Assert.Equal(
            Enumerable.Range(
                0,
                page.Blocks.Count),
            page.Blocks
                .Select(block =>
                    block.SourceSequence)
                .OrderBy(sourceSequence =>
                    sourceSequence));

        Assert.All(
            page.Blocks,
            block =>
            {
                Assert.NotNull(
                    block.ReadingOrder);

                Assert.False(
                    string.IsNullOrWhiteSpace(
                        block.Text));

                Assert.NotEmpty(
                    block.Words);

                Assert.True(
                    block.Bounds.Right >=
                    block.Bounds.Left);

                Assert.True(
                    block.Bounds.Bottom >=
                    block.Bounds.Top);

                Assert.False(
                    string.IsNullOrWhiteSpace(
                        block.DominantFontName));

                Assert.True(
                    block.MedianPointSize is > 0);

                Assert.True(
                    block.LineCount > 0);

                Assert.Equal(
                    block.Words.Count,
                    block.WordCount);

                Assert.All(
                    block.Words,
                    word =>
                        Assert.Contains(
                            word,
                            page.Words));
            });
    }

    [Fact]
    public async Task ExtractAsync_PreservesRelativePointSizeEvidence()
    {
        var pdfBytes =
            CreateTypographyFixturePdf();

        await using var stream =
            new MemoryStream(pdfBytes);

        var source =
            new DocumentSource(
                stream,
                "typography-fixture.pdf",
                "application/pdf");

        var page =
            Assert.Single(
                (await new PdfPigDocumentExtractor()
                    .ExtractAsync(
                        source,
                        DocumentFormatId.Pdf))
                .Pages);

        var headingWord =
            Assert.Single(
                page.Words,
                word =>
                    string.Equals(
                        word.Text,
                        "HEADING",
                        StringComparison.Ordinal));

        var bodyWord =
            Assert.Single(
                page.Words,
                word =>
                    string.Equals(
                        word.Text,
                        "ordinary",
                        StringComparison.Ordinal));

        Assert.NotNull(
            headingWord.MedianPointSize);

        Assert.NotNull(
            bodyWord.MedianPointSize);

        Assert.True(
            headingWord.MedianPointSize >
            bodyWord.MedianPointSize);

        Assert.All(
            page.Blocks,
            block =>
            {
                Assert.True(
                    block.MedianPointSize is > 0);

                Assert.True(
                    block.LineCount > 0);

                Assert.Equal(
                    block.Words.Count,
                    block.WordCount);
            });
    }

    [Fact]
    public async Task ExtractAsync_UsesDeterministicWordOrderForMixedOrientations()
    {
        var pdfBytes =
            CreateMixedOrientationFixturePdf();

        using (var fixture =
               PdfDocument.Open(
                   pdfBytes))
        {
            var letters =
                fixture
                    .GetPage(1)
                    .Letters;

            Assert.Contains(
                letters,
                letter =>
                    letter.TextOrientation ==
                    TextOrientation.Horizontal);

            Assert.Contains(
                letters,
                letter =>
                    letter.TextOrientation ==
                    TextOrientation.Other);
        }

        var sequences =
            new List<string>();

        for (var iteration = 0;
             iteration < 24;
             iteration++)
        {
            await using var stream =
                new MemoryStream(
                    pdfBytes);

            var page =
                Assert.Single(
                    (await new PdfPigDocumentExtractor()
                        .ExtractAsync(
                            new DocumentSource(
                                stream,
                                "mixed-orientation-fixture.pdf",
                                "application/pdf"),
                            DocumentFormatId.Pdf))
                    .Pages);

            sequences.Add(
                string.Join(
                    "\u001F",
                    page.Words.Select(word =>
                        word.Text)));
        }

        var sequence =
            Assert.Single(
                sequences
                    .Distinct(
                        StringComparer.Ordinal));

        Assert.StartsWith(
            "HORIZONTAL\u001FALPHA",
            sequence,
            StringComparison.Ordinal);

        Assert.EndsWith(
            "DIAGONAL",
            sequence,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAsync_RestoresSeekableCallerStreamPosition()
    {
        var pdfBytes =
            CreateSinglePagePdf(
                "Stream ownership");

        await using var stream =
            new MemoryStream(pdfBytes);

        stream.Position = 7;

        var source =
            new DocumentSource(stream);

        var extractor =
            new PdfPigDocumentExtractor();

        _ = await extractor.ExtractAsync(
            source,
            DocumentFormatId.Pdf);

        Assert.Equal(
            7,
            stream.Position);

        Assert.True(
            stream.CanRead);
    }

    [Fact]
    public async Task ExtractAsync_RejectsUnsupportedFormat()
    {
        await using var stream =
            new MemoryStream(
                [1, 2, 3]);

        var source =
            new DocumentSource(stream);

        var extractor =
            new PdfPigDocumentExtractor();

        await Assert.ThrowsAsync<NotSupportedException>(
            async () =>
                await extractor.ExtractAsync(
                    source,
                    new DocumentFormatId(
                        "epub")));
    }

    private static byte[] CreateSinglePagePdf(
        string text)
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        var page =
            builder.AddPage(
                PageSize.A4);

        page.AddText(
            text,
            12,
            new PdfPoint(
                72,
                720),
            font);

        return builder.Build();
    }

    private static byte[] CreateTypographyFixturePdf()
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        var page =
            builder.AddPage(
                PageSize.A4);

        page.AddText(
            "HEADING",
            18,
            new PdfPoint(
                72,
                760),
            font);

        page.AddText(
            "ordinary body text",
            11,
            new PdfPoint(
                72,
                700),
            font);

        return builder.Build();
    }
    private static byte[] CreateMixedOrientationFixturePdf()
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        var page =
            builder.AddPage(
                PageSize.A4);

        page.AddText(
            "HORIZONTAL ALPHA",
            12,
            new PdfPoint(
                72,
                720),
            font);

        page.CurrentStream.Operations.Add(
            Push.Value);

        const double angle =
            Math.PI /
            4.0;

        page.CurrentStream.Operations.Add(
            new ModifyCurrentTransformationMatrix(
                [
                    Math.Cos(angle),
                    Math.Sin(angle),
                    -Math.Sin(angle),
                    Math.Cos(angle),
                    0,
                    0
                ]));

        page.AddText(
            "DIAGONAL",
            12,
            new PdfPoint(
                260,
                260),
            font);

        page.CurrentStream.Operations.Add(
            Pop.Value);

        return builder.Build();
    }

}
