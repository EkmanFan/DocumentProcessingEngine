using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.UnitTests.Planning;

public sealed class DefaultPageProcessingRoutingTests
{
    private readonly DefaultPageProcessingAssessor _assessor =
        new();

    private readonly DefaultPageProcessingPolicy _policy =
        new();

    [Fact]
    public void MissingNativeText_RoutesToRecovery()
    {
        var decision =
            Decide(
                CreatePage(
                    wordCount:
                        0,
                    sourceText:
                        string.Empty,
                    rasterImageCount:
                        1,
                    largestRasterImageAreaRatio:
                        0.90,
                    withBlock:
                        false));

        Assert.Equal(
            NativeTextStatus.Missing,
            decision.Assessment.NativeTextStatus);

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrRecovery,
            decision.Plan.Route);
    }

    [Fact]
    public void CleanBornDigitalNativeText_RoutesNativeOnly()
    {
        var decision =
            Decide(
                CreatePage(
                    wordCount:
                        3,
                    sourceText:
                        "Alpha beta gamma.",
                    rasterImageCount:
                        0,
                    largestRasterImageAreaRatio:
                        0,
                    withBlock:
                        true));

        Assert.Equal(
            NativeTextStatus.Healthy,
            decision.Assessment.NativeTextStatus);

        Assert.Equal(
            PageProcessingRoute.NativeOnly,
            decision.Plan.Route);
    }

    [Fact]
    public void ImageBackedNativeText_IsUnverifiedNotSuspicious()
    {
        var decision =
            Decide(
                CreatePage(
                    wordCount:
                        3,
                    sourceText:
                        "Alpha beta gamma.",
                    rasterImageCount:
                        1,
                    largestRasterImageAreaRatio:
                        0.72,
                    withBlock:
                        true));

        Assert.Equal(
            NativeTextStatus.Unverified,
            decision.Assessment.NativeTextStatus);

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            decision.Plan.Route);
    }

    [Fact]
    public void ExplicitNativeCorruption_IsSuspicious()
    {
        var decision =
            Decide(
                CreatePage(
                    wordCount:
                        3,
                    sourceText:
                        "Alpha \uFFFD gamma.",
                    rasterImageCount:
                        0,
                    largestRasterImageAreaRatio:
                        0,
                    withBlock:
                        true));

        Assert.Equal(
            NativeTextStatus.Suspicious,
            decision.Assessment.NativeTextStatus);

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            decision.Plan.Route);
    }

    [Fact]
    public void NativeWordsWithoutBlocks_AreSuspiciousBeforeImageBackingCheck()
    {
        var decision =
            Decide(
                CreatePage(
                    wordCount:
                        3,
                    sourceText:
                        "Alpha beta gamma.",
                    rasterImageCount:
                        1,
                    largestRasterImageAreaRatio:
                        0.90,
                    withBlock:
                        false));

        Assert.Equal(
            NativeTextStatus.Suspicious,
            decision.Assessment.NativeTextStatus);
    }

    [Theory]
    [InlineData(
        NativeTextStatus.Healthy,
        PageProcessingRoute.NativeOnly)]
    [InlineData(
        NativeTextStatus.Missing,
        PageProcessingRoute.LayoutWithTargetedOcrRecovery)]
    [InlineData(
        NativeTextStatus.Suspicious,
        PageProcessingRoute.LayoutWithTargetedOcrReconciliation)]
    [InlineData(
        NativeTextStatus.Unverified,
        PageProcessingRoute.LayoutWithTargetedOcrReconciliation)]
    public void DefaultPolicy_MapsEveryStatusDeterministically(
        NativeTextStatus status,
        PageProcessingRoute expectedRoute)
    {
        var plan =
            _policy.Decide(
                new PageProcessingAssessment(
                    1,
                    status));

        Assert.Equal(
            expectedRoute,
            plan.Route);
    }

    [Fact]
    public void Planner_ComposesAssessmentAndPolicyWithoutHarnessDecisionLogic()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    CreatePage(
                        physicalPageNumber:
                            1,
                        wordCount:
                            3,
                        sourceText:
                            "Native text.",
                        rasterImageCount:
                            0,
                        largestRasterImageAreaRatio:
                            0,
                        withBlock:
                            true),
                    CreatePage(
                        physicalPageNumber:
                            2,
                        wordCount:
                            0,
                        sourceText:
                            string.Empty,
                        rasterImageCount:
                            1,
                        largestRasterImageAreaRatio:
                            0.9,
                        withBlock:
                            false),
                    CreatePage(
                        physicalPageNumber:
                            3,
                        wordCount:
                            3,
                        sourceText:
                            "Image backed text.",
                        rasterImageCount:
                            1,
                        largestRasterImageAreaRatio:
                            0.8,
                        withBlock:
                            true)
                ]);

        var planner =
            DocumentPageProcessingPlanner
                .CreateDefault();

        var decisions =
            planner.Plan(
                extraction);

        Assert.Collection(
            decisions,
            first =>
            {
                Assert.Equal(
                    NativeTextStatus.Healthy,
                    first.Assessment.NativeTextStatus);

                Assert.Equal(
                    PageProcessingRoute.NativeOnly,
                    first.Plan.Route);
            },
            second =>
            {
                Assert.Equal(
                    NativeTextStatus.Missing,
                    second.Assessment.NativeTextStatus);

                Assert.Equal(
                    PageProcessingRoute.LayoutWithTargetedOcrRecovery,
                    second.Plan.Route);
            },
            third =>
            {
                Assert.Equal(
                    NativeTextStatus.Unverified,
                    third.Assessment.NativeTextStatus);

                Assert.Equal(
                    PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
                    third.Plan.Route);
            });
    }

    [Fact]
    public void Planner_RejectsAssessorReturningWrongPhysicalPage()
    {
        var planner =
            new DocumentPageProcessingPlanner(
                new WrongPageAssessor(),
                _policy);

        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    CreatePage(
                        physicalPageNumber:
                            1,
                        wordCount:
                            3,
                        sourceText:
                            "Native text.",
                        rasterImageCount:
                            0,
                        largestRasterImageAreaRatio:
                            0,
                        withBlock:
                            true)
                ]);

        Assert.Throws<InvalidDataException>(
            () =>
                planner.Plan(
                    extraction));
    }

    private PageProcessingDecision Decide(
        DocumentExtractionPage page)
    {
        var assessment =
            _assessor.Assess(
                page);

        var plan =
            _policy.Decide(
                assessment);

        return new PageProcessingDecision(
            assessment,
            plan);
    }

    private static DocumentExtractionPage CreatePage(
        int physicalPageNumber = 1,
        int wordCount = 3,
        string sourceText = "Alpha beta gamma.",
        int rasterImageCount = 0,
        double largestRasterImageAreaRatio = 0,
        bool withBlock = true)
    {
        var words =
            wordCount ==
                0
                ? []
                : Enumerable
                    .Range(
                        0,
                        wordCount)
                    .Select(
                        index =>
                            new DocumentWord(
                                index,
                                $"w{index}",
                                new NormalizedRectangle(
                                    0.1 +
                                    index *
                                    0.05,
                                    0.2,
                                    0.14 +
                                    index *
                                    0.05,
                                    0.24),
                                "Body",
                                10))
                    .ToArray();

        IReadOnlyList<DocumentTextBlock> blocks =
            !withBlock
                ? []
                :
                [
                    new DocumentTextBlock(
                        sourceSequence:
                            0,
                        readingOrder:
                            0,
                        sourceText,
                        new NormalizedRectangle(
                            0.1,
                            0.2,
                            0.9,
                            0.4),
                        words,
                        dominantFontName:
                            "Body",
                        medianPointSize:
                            10,
                        lineCount:
                            1)
                ];

        return new DocumentExtractionPage(
            physicalPageNumber,
            sourceText,
            new NormalizedRectangle(
                0,
                0,
                1,
                1),
            wordCount,
            rasterImageCount,
            largestRasterImageAreaRatio,
            sourceWidth:
                612,
            sourceHeight:
                792,
            words,
            blocks);
    }

    private sealed class WrongPageAssessor
        : IPageProcessingAssessor
    {
        public PageProcessingAssessment Assess(
            DocumentExtractionPage page) =>
            new(
                page.PhysicalPageNumber +
                1,
                NativeTextStatus.Healthy);
    }
}
