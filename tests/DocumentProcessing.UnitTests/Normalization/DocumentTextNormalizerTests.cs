using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Engine.Normalization;

namespace DocumentProcessing.UnitTests.Normalization;

public sealed class DocumentTextNormalizerTests
{
    [Fact]
    public void Normalize_NormalizesTextWithoutLosingSourceEvidence()
    {
        var sourceText =
            "Cafe\u0301   inter-\nnational\r\nstudy";

        var sourceBlock =
            CreateBlock(
                sourceSequence: 7,
                readingOrder: 2,
                sourceText);

        var extraction =
            CreateExtraction(
                CreatePage(
                    physicalPageNumber: 1,
                    sourceBlock));

        var result =
            new DocumentTextNormalizer()
                .Normalize(extraction);

        var page =
            Assert.Single(result.Pages);

        var block =
            Assert.Single(page.Blocks);

        Assert.Same(
            extraction,
            result.SourceExtraction);

        Assert.Same(
            sourceBlock,
            block.SourceBlock);

        Assert.Equal(
            sourceText,
            block.SourceText);

        Assert.Equal(
            "Café international study",
            block.Text);

        Assert.False(
            block.IsExcluded);

        Assert.Null(
            block.ExclusionReason);

        Assert.Equal(
            DocumentTextNormalizer.NormalizationProfileId,
            result.NormalizationProfileId);
    }

    [Fact]
    public void Normalize_DehyphenatesOnlyLowercaseLineContinuation()
    {
        var extraction =
            CreateExtraction(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        0,
                        "lower-\ncase"),
                    CreateBlock(
                        1,
                        1,
                        "Upper-\nCase"),
                    CreateBlock(
                        2,
                        2,
                        "well-being")));

        var result =
            new DocumentTextNormalizer()
                .Normalize(extraction);

        var blocks =
            Assert.Single(result.Pages)
                .Blocks;

        Assert.Equal(
            "lowercase",
            blocks[0].Text);

        Assert.Equal(
            "Upper- Case",
            blocks[1].Text);

        Assert.Equal(
            "well-being",
            blocks[2].Text);
    }

    [Fact]
    public void Normalize_PreservesExtractionPageAndBlockOrder()
    {
        var pageTwo =
            CreatePage(
                2,
                CreateBlock(
                    sourceSequence: 30,
                    readingOrder: null,
                    text: "unknown order"),
                CreateBlock(
                    sourceSequence: 20,
                    readingOrder: 1,
                    text: "second"),
                CreateBlock(
                    sourceSequence: 10,
                    readingOrder: 0,
                    text: "first"));

        var pageOne =
            CreatePage(
                1,
                CreateBlock(
                    sourceSequence: 5,
                    readingOrder: 0,
                    text: "page one"));

        var extraction =
            CreateExtraction(
                pageTwo,
                pageOne);

        var result =
            new DocumentTextNormalizer()
                .Normalize(extraction);

        Assert.Equal(
            [2, 1],
            result.Pages
                .Select(page =>
                    page.PhysicalPageNumber));

        Assert.Equal(
            [30, 20, 10],
            result.Pages[0]
                .Blocks
                .Select(block =>
                    block.SourceBlock.SourceSequence));
    }

    [Fact]
    public void Normalize_ExcludesRecurringHeadersAndCanonicalizesDigits()
    {
        var extraction =
            CreateExtraction(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        0,
                        "CHAPTER 1",
                        HeaderBounds)),
                CreatePage(
                    2,
                    CreateBlock(
                        0,
                        0,
                        "CHAPTER 2",
                        HeaderBounds)),
                CreatePage(
                    3,
                    CreateBlock(
                        0,
                        0,
                        "CHAPTER 3",
                        HeaderBounds)));

        var result =
            new DocumentTextNormalizer()
                .Normalize(extraction);

        var blocks =
            result.Pages
                .SelectMany(page =>
                    page.Blocks)
                .ToArray();

        Assert.All(
            blocks,
            block =>
            {
                Assert.True(
                    block.IsExcluded);

                Assert.Equal(
                    DocumentBlockExclusionReason.RepeatedHeader,
                    block.ExclusionReason);
            });
    }

    [Fact]
    public void Normalize_ExcludesRecurringFooters()
    {
        var extraction =
            CreateExtraction(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        0,
                        "Copyright notice",
                        FooterBounds)),
                CreatePage(
                    2,
                    CreateBlock(
                        0,
                        0,
                        "Copyright notice",
                        FooterBounds)),
                CreatePage(
                    3,
                    CreateBlock(
                        0,
                        0,
                        "Copyright notice",
                        FooterBounds)));

        var result =
            new DocumentTextNormalizer()
                .Normalize(extraction);

        Assert.All(
            result.Pages.SelectMany(page =>
                page.Blocks),
            block =>
                Assert.Equal(
                    DocumentBlockExclusionReason.RepeatedFooter,
                    block.ExclusionReason));
    }

    [Fact]
    public void Normalize_DoesNotExcludeNonRecurringOrBodyBlocks()
    {
        var extraction =
            CreateExtraction(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        0,
                        "Only twice",
                        HeaderBounds),
                    CreateBlock(
                        1,
                        1,
                        "Repeated body",
                        BodyBounds)),
                CreatePage(
                    2,
                    CreateBlock(
                        0,
                        0,
                        "Only twice",
                        HeaderBounds),
                    CreateBlock(
                        1,
                        1,
                        "Repeated body",
                        BodyBounds)),
                CreatePage(
                    3,
                    CreateBlock(
                        0,
                        0,
                        "Different header",
                        HeaderBounds),
                    CreateBlock(
                        1,
                        1,
                        "Repeated body",
                        BodyBounds)));

        var result =
            new DocumentTextNormalizer()
                .Normalize(extraction);

        Assert.All(
            result.Pages.SelectMany(page =>
                page.Blocks),
            block =>
            {
                Assert.False(
                    block.IsExcluded);

                Assert.Null(
                    block.ExclusionReason);
            });
    }

    [Fact]
    public void Normalize_HonorsCancellation()
    {
        var extraction =
            CreateExtraction(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        0,
                        "text")));

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () =>
                new DocumentTextNormalizer()
                    .Normalize(
                        extraction,
                        cancellation.Token));
    }

    private static readonly NormalizedRectangle HeaderBounds =
        new(
            0.1,
            0.02,
            0.9,
            0.07);

    private static readonly NormalizedRectangle BodyBounds =
        new(
            0.1,
            0.30,
            0.9,
            0.40);

    private static readonly NormalizedRectangle FooterBounds =
        new(
            0.1,
            0.90,
            0.9,
            0.96);

    private static DocumentExtractionResult
        CreateExtraction(
            params DocumentExtractionPage[] pages) =>
        new(
            DocumentFormatId.Pdf,
            pages);

    private static DocumentExtractionPage
        CreatePage(
            int physicalPageNumber,
            params DocumentTextBlock[] blocks) =>
        new(
            physicalPageNumber,
            sourceText: string.Join(
                "\n",
                blocks.Select(block =>
                    block.Text)),
            wordCount: blocks.Sum(block =>
                block.Words.Count),
            sourceWidth: 600,
            sourceHeight: 800,
            blocks: blocks);

    private static DocumentTextBlock
        CreateBlock(
            int sourceSequence,
            int? readingOrder,
            string text,
            NormalizedRectangle? bounds = null) =>
        new(
            sourceSequence,
            readingOrder,
            text,
            bounds ??
            new NormalizedRectangle(
                0.1,
                0.20,
                0.9,
                0.30));
}
