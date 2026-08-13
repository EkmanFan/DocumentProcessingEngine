using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Segmentation;
using DocumentProcessing.Engine.Segmentation;

namespace DocumentProcessing.UnitTests.Segmentation;

public sealed class HeuristicDocumentSegmenterTests
{
    [Fact]
    public void Segment_UsesPageBoundedFallbackWhenNoHeadingExists()
    {
        var result =
            Segment(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        "First paragraph."),
                    CreateBlock(
                        1,
                        "Second paragraph.")),
                CreatePage(
                    2,
                    CreateBlock(
                        0,
                        "Third paragraph.")));

        Assert.Equal(
            2,
            result.Segments.Count);

        Assert.All(
            result.Segments,
            segment =>
                Assert.Equal(
                    segment.FirstPhysicalPageNumber,
                    segment.LastPhysicalPageNumber));

        Assert.All(
            result.Segments,
            segment =>
                Assert.Null(
                    segment.HeadingText));
    }

    [Fact]
    public void Segment_AllowsHeadingLedStructureToSpanPages()
    {
        var result =
            Segment(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        "INTRODUCTION",
                        pointSize: 13,
                        wordCount: 2),
                    CreateBlock(
                        1,
                        "Body on page one.",
                        pointSize: 10,
                        wordCount: 8)),
                CreatePage(
                    2,
                    CreateBlock(
                        0,
                        "Body continuing on page two.",
                        pointSize: 10,
                        wordCount: 10)),
                CreatePage(
                    3,
                    CreateBlock(
                        0,
                        "NEXT SECTION",
                        pointSize: 13,
                        wordCount: 2),
                    CreateBlock(
                        1,
                        "Second section body.",
                        pointSize: 10,
                        wordCount: 8)));

        Assert.Equal(
            2,
            result.Segments.Count);

        var first =
            result.Segments[0];

        Assert.Equal(
            "INTRODUCTION",
            first.HeadingText);

        Assert.Equal(
            1,
            first.FirstPhysicalPageNumber);

        Assert.Equal(
            2,
            first.LastPhysicalPageNumber);

        Assert.Contains(
            "Body continuing on page two.",
            first.Text,
            StringComparison.Ordinal);

        var second =
            result.Segments[1];

        Assert.Equal(
            3,
            second.FirstPhysicalPageNumber);

        Assert.Equal(
            3,
            second.LastPhysicalPageNumber);
    }

    [Fact]
    public void Segment_KeepsPreHeadingContentAsPageBoundedFallback()
    {
        var result =
            Segment(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        "Preface body.",
                        pointSize: 10,
                        wordCount: 8)),
                CreatePage(
                    2,
                    CreateBlock(
                        0,
                        "More unstructured body.",
                        pointSize: 10,
                        wordCount: 8),
                    CreateBlock(
                        1,
                        "STRUCTURED SECTION",
                        pointSize: 13,
                        wordCount: 2),
                    CreateBlock(
                        2,
                        "Structured body.",
                        pointSize: 10,
                        wordCount: 8)));

        Assert.Equal(
            3,
            result.Segments.Count);

        Assert.Null(
            result.Segments[0].HeadingText);

        Assert.Equal(
            (1, 1),
            (
                result.Segments[0].FirstPhysicalPageNumber,
                result.Segments[0].LastPhysicalPageNumber));

        Assert.Null(
            result.Segments[1].HeadingText);

        Assert.Equal(
            (2, 2),
            (
                result.Segments[1].FirstPhysicalPageNumber,
                result.Segments[1].LastPhysicalPageNumber));

        Assert.Equal(
            "STRUCTURED SECTION",
            result.Segments[2].HeadingText);
    }


    [Fact]
    public void Segment_DoesNotInferHeadingWithoutTypography()
    {
        var segment =
            Assert.Single(
                Segment(
                    CreatePage(
                        1,
                        CreateBlock(
                            0,
                            "1. Introduction"),
                        CreateBlock(
                            1,
                            "Body.")))
                .Segments);

        Assert.Null(
            segment.HeadingText);

        Assert.Equal(
            2,
            segment.SourceBlocks.Count);
    }

    [Fact]
    public void Segment_RejectsBareLeadingNumberAtBodyFontSize()
    {
        var result =
            Segment(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        "749 ’; and all that they may be constant to",
                        pointSize: 11,
                        wordCount: 9),
                    CreateBlock(
                        1,
                        "Ordinary body.",
                        pointSize: 11,
                        wordCount: 8)));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Null(
            segment.HeadingText);

        Assert.Equal(
            2,
            segment.SourceBlocks.Count);
    }

    [Fact]
    public void Segment_RejectsExplicitStructuralLabelWhenTypographyContradictsIt()
    {
        var result =
            Segment(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        "Chapter 1: Running label",
                        pointSize: 8,
                        wordCount: 4),
                    CreateBlock(
                        1,
                        "Ordinary body with several words here.",
                        pointSize: 10,
                        wordCount: 12)));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Null(
            segment.HeadingText);
    }


    [Fact]
    public void Segment_RejectsUppercaseLabelBelowTypographyThreshold()
    {
        var segment =
            Assert.Single(
                Segment(
                    CreatePage(
                        1,
                        CreateBlock(
                            0,
                            "ANOTHER GLIMPSE INTO THE PAST",
                            pointSize: 11.5,
                            wordCount: 5),
                        CreateBlock(
                            1,
                            "Ordinary body with sufficient weight.",
                            pointSize: 10,
                            wordCount: 12)))
                .Segments);

        Assert.Null(
            segment.HeadingText);
    }

    [Fact]
    public void Segment_RejectsTypographyWithTooFewLetters()
    {
        var segment =
            Assert.Single(
                Segment(
                    CreatePage(
                        1,
                        CreateBlock(
                            0,
                            "eox 6.2",
                            pointSize: 13,
                            wordCount: 2),
                        CreateBlock(
                            1,
                            "Ordinary body with sufficient weight.",
                            pointSize: 10,
                            wordCount: 12)))
                .Segments);

        Assert.Null(
            segment.HeadingText);
    }

    [Fact]
    public void Segment_RejectsTypographyBelowAlphaNumericRatio()
    {
        var segment =
            Assert.Single(
                Segment(
                    CreatePage(
                        1,
                        CreateBlock(
                            0,
                            "l) J'(l l) N J).f",
                            pointSize: 14,
                            wordCount: 6),
                        CreateBlock(
                            1,
                            "Ordinary body with sufficient weight.",
                            pointSize: 10,
                            wordCount: 12)))
                .Segments);

        Assert.Null(
            segment.HeadingText);
    }

    [Fact]
    public void Segment_AcceptsStrictTypographyHeading()
    {
        var result =
            Segment(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        "Structured Topic",
                        pointSize: 12,
                        wordCount: 2),
                    CreateBlock(
                        1,
                        "Ordinary body with sufficient weight.",
                        pointSize: 10,
                        wordCount: 12)));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Equal(
            "Structured Topic",
            segment.HeadingText);
    }

    [Fact]
    public void Segment_RejectsCorruptedLargeTypography()
    {
        var result =
            Segment(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        ". i';�-� _ J -��",
                        pointSize: 70,
                        wordCount: 3),
                    CreateBlock(
                        1,
                        "Ordinary body with sufficient weight.",
                        pointSize: 10,
                        wordCount: 12)));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Null(
            segment.HeadingText);
    }

    [Fact]
    public void Segment_RejectsSentenceLikeBlockAtSubsectionRatio()
    {
        var result =
            Segment(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        "This looks like an ordinary sentence.",
                        pointSize: 12,
                        wordCount: 7),
                    CreateBlock(
                        1,
                        "Body body body body body body body body.",
                        pointSize: 10,
                        wordCount: 12)));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.Null(
            segment.HeadingText);
    }

    [Fact]
    public void Segment_IgnoresExcludedMarginBlocks()
    {
        var excluded =
            CreateBlock(
                0,
                "RUNNING HEADER",
                pointSize: 20,
                wordCount: 2,
                exclusionReason:
                    DocumentBlockExclusionReason.RepeatedHeader);

        var body =
            CreateBlock(
                1,
                "Body content.",
                pointSize: 10,
                wordCount: 8);

        var segment =
            Assert.Single(
                Segment(
                    CreatePage(
                        1,
                        excluded,
                        body))
                .Segments);

        Assert.Null(
            segment.HeadingText);

        Assert.Single(
            segment.SourceBlocks);

        Assert.Same(
            body,
            segment.SourceBlocks[0]);
    }

    [Fact]
    public void Segment_PreservesCrossPageSourceBlockOrder()
    {
        var heading =
            CreateBlock(
                0,
                "SECTION TITLE",
                pointSize: 13,
                wordCount: 2);

        var pageOneBody =
            CreateBlock(
                1,
                "Page one.",
                pointSize: 10,
                wordCount: 8);

        var pageTwoBody =
            CreateBlock(
                0,
                "Page two.",
                pointSize: 10,
                wordCount: 8);

        var segment =
            Assert.Single(
                Segment(
                    CreatePage(
                        1,
                        heading,
                        pageOneBody),
                    CreatePage(
                        2,
                        pageTwoBody))
                .Segments);

        Assert.Equal(
            [heading, pageOneBody, pageTwoBody],
            segment.SourceBlocks);
    }

    [Fact]
    public void Segment_ProducesDeterministicIdsAfterCrossPageGrouping()
    {
        var normalization =
            CreateNormalization(
                CreatePage(
                    5,
                    CreateBlock(
                        0,
                        "FIRST SECTION",
                        pointSize: 13,
                        wordCount: 2),
                    CreateBlock(
                        1,
                        "Body.",
                        pointSize: 10,
                        wordCount: 8)),
                CreatePage(
                    6,
                    CreateBlock(
                        0,
                        "Continuation.",
                        pointSize: 10,
                        wordCount: 8)),
                CreatePage(
                    7,
                    CreateBlock(
                        0,
                        "SECOND SECTION",
                        pointSize: 13,
                        wordCount: 2),
                    CreateBlock(
                        1,
                        "Final body.",
                        pointSize: 10,
                        wordCount: 8)));

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
            ["p000005-s000000", "p000007-s000001"],
            first.Segments.Select(segment =>
                segment.Id));
    }

    [Fact]
    public void Segment_SkipsPageWithOnlyExcludedBlocks()
    {
        var result =
            Segment(
                CreatePage(
                    1,
                    CreateBlock(
                        0,
                        "RUNNING HEADER",
                        exclusionReason:
                            DocumentBlockExclusionReason.RepeatedHeader)));

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
                    CreateBlock(
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

    private static DocumentSegmentationResult Segment(
        params NormalizedDocumentPage[] pages) =>
        new HeuristicDocumentSegmenter()
            .Segment(
                CreateNormalization(
                    pages));

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

    private static NormalizedDocumentPage CreatePage(
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

    private static NormalizedDocumentTextBlock CreateBlock(
        int sourceSequence,
        string text,
        double? pointSize = null,
        int wordCount = 0,
        DocumentBlockExclusionReason? exclusionReason = null)
    {
        var words =
            Enumerable.Range(
                    0,
                    wordCount)
                .Select(index =>
                    new DocumentWord(
                        index,
                        $"w{index}",
                        Bounds,
                        pointSize is null
                            ? null
                            : "TestFont",
                        pointSize))
                .ToArray();

        var sourceBlock =
            new DocumentTextBlock(
                sourceSequence,
                readingOrder:
                    sourceSequence,
                text,
                Bounds,
                words,
                pointSize is null
                    ? null
                    : "TestFont",
                pointSize,
                lineCount:
                    pointSize is null
                        ? 0
                        : 1);

        return new NormalizedDocumentTextBlock(
            sourceBlock,
            text,
            exclusionReason);
    }

    private static readonly NormalizedRectangle Bounds =
        new(
            0.1,
            0.2,
            0.9,
            0.3);
}
