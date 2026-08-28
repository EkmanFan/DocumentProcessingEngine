using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Segmentation;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Segmentation;
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
    [Theory]
    [InlineData("habermas-p0070.pdf", "Chapter 7", "Even if all")]
    [InlineData("habermas-p0078.pdf", "Chapter 8", "Naturalism views")]
    public async Task ExtractAsync_ReconstructsQualifiedDropCapsAfterChapterHeading(
        string fileName,
        string expectedHeading,
        string expectedParagraphPrefix)
    {
        var path =
            Path.Combine(
                FindRepositoryRoot(),
                "tests",
                "document_corpus",
                "pdf",
                "pages",
                fileName);

        if (!File.Exists(path))
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                $"Qualified drop-cap fixture '{fileName}' is unavailable.");
        }

        await using var stream =
            File.OpenRead(path);
        var source =
            new DocumentSource(
                stream,
                fileName,
                "application/pdf");
        var extraction =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    source,
                    DocumentFormatId.Pdf);
        var page =
            Assert.Single(
                extraction.Pages);
        var heading =
            Assert.Single(
                page.Blocks,
                block =>
                    string.Equals(
                        block.Text,
                        expectedHeading,
                        StringComparison.Ordinal));
        var paragraph =
            Assert.Single(
                page.Blocks,
                block =>
                    block.Text.StartsWith(
                        expectedParagraphPrefix,
                        StringComparison.Ordinal));

        Assert.True(
            heading.ReadingOrder <
            paragraph.ReadingOrder);
        Assert.Equal(
            expectedParagraphPrefix[0].ToString(),
            paragraph.Words[0].Text);
        Assert.StartsWith(
            expectedParagraphPrefix[1..].Split(' ')[0],
            paragraph.Words[1].Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            page.Blocks,
            block =>
                string.Equals(
                    block.Text,
                    expectedParagraphPrefix[0].ToString(),
                    StringComparison.Ordinal));

        var segmentation =
            new HeuristicDocumentSegmenter()
                .Segment(
                    new DocumentTextNormalizer()
                        .Normalize(
                            extraction),
                    new DocumentSegmentationOptions(
                        [expectedHeading]));
        var chapter =
            Assert.Single(
                segmentation.Segments,
                segment =>
                    string.Equals(
                        segment.HeadingText,
                        expectedHeading,
                        StringComparison.Ordinal));
        var paragraphSegment =
            Assert.Single(
                segmentation.Segments,
                segment =>
                    segment.Text.Contains(
                        expectedParagraphPrefix,
                        StringComparison.Ordinal));

        Assert.True(
            paragraphSegment.Ordinal >=
            chapter.Ordinal);
        Assert.NotNull(
            paragraphSegment.HeadingText);
        Assert.DoesNotContain(
            segmentation.Segments.Where(segment =>
                segment.Ordinal < chapter.Ordinal),
            segment =>
                segment.Text.Contains(
                    expectedParagraphPrefix,
                    StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ehrman-p0079.pdf")]
    [InlineData("decretis-p0512.pdf")]
    public async Task ExtractAsync_DoesNotInventDropCapsInQualifiedNegativeCorpora(
        string fileName)
    {
        var path =
            Path.Combine(
                FindRepositoryRoot(),
                "tests",
                "document_corpus",
                "pdf",
                "pages",
                fileName);

        if (!File.Exists(path))
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                $"Qualified negative fixture '{fileName}' is unavailable.");
        }

        await using var stream =
            File.OpenRead(path);
        var page =
            Assert.Single(
                (await new PdfPigDocumentExtractor()
                    .ExtractAsync(
                        new DocumentSource(
                            stream,
                            fileName,
                            "application/pdf"),
                        DocumentFormatId.Pdf))
                .Pages);

        Assert.DoesNotContain(
            page.Blocks,
            block =>
                block.Words.Count >= 2 &&
                block.Words[0].Text.Length == 1 &&
                char.IsUpper(
                    block.Words[0].Text[0]) &&
                block.Words[0].MedianPointSize is { } initialSize &&
                block.Words[1].MedianPointSize is { } bodySize &&
                initialSize >= bodySize * 1.75 &&
                block.Text.StartsWith(
                    block.Words[0].Text +
                    block.Words[1].Text,
                    StringComparison.Ordinal));
    }

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

    private static string FindRepositoryRoot()
    {
        var current =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "DocumentProcessingEngine.sln")))
            {
                return current.FullName;
            }

            current =
                current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root from the integration-test output directory.");
    }

}
