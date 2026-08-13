using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Segmentation;
using DocumentProcessing.Engine.Segmentation;

namespace DocumentProcessing.UnitTests.Segmentation;

public sealed class HeuristicDocumentSegmenterTests
{
    [Fact]
    public void Segment_UsesOnePageBoundedFallbackSegmentWithoutHeading()
    {
        var normalization =
            CreateNormalization(
                CreatePage(
                    1,
                    CreateNormalizedBlock(
                        0,
                        "First paragraph."),
                    CreateNormalizedBlock(
                        1,
                        "Second paragraph.")),
                CreatePage(
                    2,
                    CreateNormalizedBlock(
                        0,
                        "Third paragraph.")));

        var result =
            new HeuristicDocumentSegmenter()
                .Segment(normalization);

        Assert.Equal(
            2,
            result.Segments.Count);

        Assert.Equal(
            [1, 2],
            result.Segments
                .Select(segment =>
                    segment.FirstPhysicalPageNumber));

        Assert.All(
            result.Segments,
            segment =>
                Assert.Equal(
                    segment.FirstPhysicalPageNumber,
                    segment.LastPhysicalPageNumber));

        Assert.Null(
            result.Segments[0].HeadingText);

        Assert.Equal(
            "First paragraph.\n\nSecond paragraph.",
            result.Segments[0].Text);
    }

    [Fact]
    public void Segment_StartsNewSegmentOnObviousUppercaseHeading()
    {
        var first =
            CreateNormalizedBlock(
                0,
                "Preface text.");

        var heading =
            CreateNormalizedBlock(
                1,
                "HISTORICAL CONTEXT");

        var body =
            CreateNormalizedBlock(
                2,
                "The following discussion begins here.");

        var result =
            new HeuristicDocumentSegmenter()
                .Segment(
                    CreateNormalization(
                        CreatePage(
                            1,
                            first,
                            heading,
                            body)));

        Assert.Equal(
            2,
            result.Segments.Count);

        Assert.Null(
            result.Segments[0].HeadingText);

        Assert.Equal(
            "HISTORICAL CONTEXT",
            result.Segments[1].HeadingText);

        Assert.Equal(
            [heading, body],
            result.Segments[1].SourceBlocks);
    }

    [Fact]
    public void Segment_RecognizesExplicitNumberedHeading()
    {
        var heading =
            CreateNormalizedBlock(
                0,
                "1. Introduction");

        var body =
            CreateNormalizedBlock(
                1,
                "Ordinary body content follows.");

        var segment =
            Assert.Single(
                new HeuristicDocumentSegmenter()
                    .Segment(
                        CreateNormalization(
                            CreatePage(
                                1,
                                heading,
                                body)))
                    .Segments);

        Assert.Equal(
            "1. Introduction",
            segment.HeadingText);

        Assert.Equal(
            "1. Introduction\n\nOrdinary body content follows.",
            segment.Text);
    }

    [Fact]
    public void Segment_DoesNotPromoteOrdinarySentenceToHeading()
    {
        var normalization =
            CreateNormalization(
                CreatePage(
                    1,
                    CreateNormalizedBlock(
                        0,
                        "This is an ordinary sentence"),
                    CreateNormalizedBlock(
                        1,
                        "More body content.")));

        var segment =
            Assert.Single(
                new HeuristicDocumentSegmenter()
                    .Segment(normalization)
                    .Segments);

        Assert.Null(
            segment.HeadingText);

        Assert.Equal(
            2,
            segment.SourceBlocks.Count);
    }

    [Fact]
    public void Segment_IgnoresExcludedMarginBlocks()
    {
        var excludedHeader =
            CreateNormalizedBlock(
                0,
                "CHAPTER 7",
                DocumentBlockExclusionReason.RepeatedHeader);

        var body =
            CreateNormalizedBlock(
                1,
                "Body content.");

        var segment =
            Assert.Single(
                new HeuristicDocumentSegmenter()
                    .Segment(
                        CreateNormalization(
                            CreatePage(
                                1,
                                excludedHeader,
                                body)))
                    .Segments);

        Assert.Null(
            segment.HeadingText);

        Assert.Single(
            segment.SourceBlocks);

        Assert.Same(
            body,
            segment.SourceBlocks[0]);

        Assert.DoesNotContain(
            "CHAPTER 7",
            segment.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Segment_PreservesSourceBlockReferencesAndOrder()
    {
        var first =
            CreateNormalizedBlock(
                7,
                "FIRST HEADING");

        var second =
            CreateNormalizedBlock(
                3,
                "Body A.");

        var third =
            CreateNormalizedBlock(
                9,
                "Body B.");

        var segment =
            Assert.Single(
                new HeuristicDocumentSegmenter()
                    .Segment(
                        CreateNormalization(
                            CreatePage(
                                4,
                                first,
                                second,
                                third)))
                    .Segments);

        Assert.Equal(
            [first, second, third],
            segment.SourceBlocks);

        Assert.Same(
            first,
            segment.SourceBlocks[0]);

        Assert.Equal(
            4,
            segment.FirstPhysicalPageNumber);

        Assert.Equal(
            4,
            segment.LastPhysicalPageNumber);
    }

    [Fact]
    public void Segment_ProducesDeterministicDocumentLocalIds()
    {
        var normalization =
            CreateNormalization(
                CreatePage(
                    5,
                    CreateNormalizedBlock(
                        0,
                        "INTRODUCTION"),
                    CreateNormalizedBlock(
                        1,
                        "Body."),
                    CreateNormalizedBlock(
                        2,
                        "CONCLUSION"),
                    CreateNormalizedBlock(
                        3,
                        "Final body.")));

        var segmenter =
            new HeuristicDocumentSegmenter();

        var first =
            segmenter.Segment(
                normalization);

        var second =
            segmenter.Segment(
                normalization);

        Assert.Equal(
            first.Segments.Select(segment =>
                segment.Id),
            second.Segments.Select(segment =>
                segment.Id));

        Assert.Equal(
            ["p000005-s000000", "p000005-s000001"],
            first.Segments.Select(segment =>
                segment.Id));

        Assert.Equal(
            [0, 1],
            first.Segments.Select(segment =>
                segment.Ordinal));
    }

    [Fact]
    public void Segment_SkipsPageWithOnlyExcludedBlocks()
    {
        var normalization =
            CreateNormalization(
                CreatePage(
                    1,
                    CreateNormalizedBlock(
                        0,
                        "RUNNING HEADER",
                        DocumentBlockExclusionReason.RepeatedHeader)));

        var result =
            new HeuristicDocumentSegmenter()
                .Segment(normalization);

        Assert.Empty(
            result.Segments);
    }

    [Fact]
    public void Segment_HonorsCancellation()
    {
        var normalization =
            CreateNormalization(
                CreatePage(
                    1,
                    CreateNormalizedBlock(
                        0,
                        "Body.")));

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () =>
                new HeuristicDocumentSegmenter()
                    .Segment(
                        normalization,
                        cancellation.Token));
    }

    private static DocumentTextNormalizationResult
        CreateNormalization(
            params NormalizedDocumentPage[] pages)
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                pages.Select(page =>
                        page.SourcePage)
                    .ToArray());

        return new DocumentTextNormalizationResult(
            extraction,
            "test-normalization-profile",
            pages);
    }

    private static NormalizedDocumentPage
        CreatePage(
            int physicalPageNumber,
            params NormalizedDocumentTextBlock[] blocks)
    {
        var sourceBlocks =
            blocks.Select(block =>
                    block.SourceBlock)
                .ToArray();

        var sourcePage =
            new DocumentExtractionPage(
                physicalPageNumber,
                string.Join(
                    "\n",
                    sourceBlocks.Select(block =>
                        block.Text)),
                sourceWidth: 600,
                sourceHeight: 800,
                blocks: sourceBlocks);

        return new NormalizedDocumentPage(
            sourcePage,
            blocks);
    }

    private static NormalizedDocumentTextBlock
        CreateNormalizedBlock(
            int sourceSequence,
            string text,
            DocumentBlockExclusionReason? exclusionReason = null)
    {
        var sourceBlock =
            new DocumentTextBlock(
                sourceSequence,
                readingOrder: sourceSequence,
                text,
                new NormalizedRectangle(
                    0.1,
                    0.2,
                    0.9,
                    0.3));

        return new NormalizedDocumentTextBlock(
            sourceBlock,
            text,
            exclusionReason);
    }
}
