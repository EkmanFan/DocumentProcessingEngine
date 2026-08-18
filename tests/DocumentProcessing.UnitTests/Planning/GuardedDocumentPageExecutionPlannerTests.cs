using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.UnitTests.Planning;

public sealed class GuardedDocumentPageExecutionPlannerTests
{
    [Fact]
    public void Plan_MissingText_PreservesRecoverySafety()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Missing);

        var decision =
            Assert.Single(
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            1),
                    Observations(
                        BlankCanvas())));

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrRecovery,
            decision.Authoritative.Plan.Route);

        Assert.Equal(
            TextExecutionMode.TargetedOcrRecovery,
            decision.Candidate.Plan.TextMode);

        Assert.True(
            decision.Candidate.Plan.RequiresTargetedOcr);

        Assert.False(
            decision.CandidateRemovesAuthoritativeTextMl);
    }

    [Fact]
    public void Plan_SuspiciousText_PreservesReconciliationSafety()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Suspicious);

        var decision =
            Assert.Single(
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            1),
                    Observations(
                        BlankCanvas())));

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            decision.Authoritative.Plan.Route);

        Assert.Equal(
            TextExecutionMode.TargetedOcrReconciliation,
            decision.Candidate.Plan.TextMode);

        Assert.True(
            decision.Candidate.Plan.RequiresTargetedOcr);

        Assert.True(
            decision.Candidate.Plan.RequiresNativeOcrReconciliation);

        Assert.False(
            decision.CandidateRemovesAuthoritativeTextMl);
    }

    [Fact]
    public void Plan_UnverifiedPresentationOnlyVisual_ShowsIntendedTextMlOptimization()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Unverified);

        var decision =
            Assert.Single(
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            1),
                    Observations(
                        BlankCanvas())));

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            decision.Authoritative.Plan.Route);

        Assert.Equal(
            TextExecutionMode.NativeText,
            decision.Candidate.Plan.TextMode);

        Assert.Equal(
            VisualExecutionAction.NoAdditionalSemanticProcessing,
            Assert.Single(
                decision.Candidate.Plan.VisualElements).Action);

        Assert.True(
            decision.CandidateRemovesAuthoritativeTextMl);

        Assert.False(
            decision.Candidate.Plan.RequiresLayoutAnalysis);

        Assert.False(
            decision.Candidate.Plan.RequiresTargetedOcr);
    }

    [Fact]
    public void Plan_UnverifiedUnknownVisual_FailsClosed()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Unverified);

        var decision =
            Assert.Single(
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            1),
                    Observations(
                        UnknownVisual())));

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            decision.Authoritative.Plan.Route);

        Assert.Equal(
            TextExecutionMode.TargetedOcrVerification,
            decision.Candidate.Plan.TextMode);

        Assert.True(
            decision.Candidate.Plan.RequiresTargetedOcr);

        Assert.True(
            decision.Candidate.Plan.RequiresNativeOcrReconciliation);

        Assert.True(
            decision.Candidate.Plan.RequiresVisualAnalysis);

        Assert.False(
            decision.CandidateRemovesAuthoritativeTextMl);
    }

    [Fact]
    public void Plan_HealthyMeaningfulVisual_AddsPreservationWithoutTextMl()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Healthy);

        var decision =
            Assert.Single(
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            1),
                    Observations(
                        LargeIndependentVisual())));

        Assert.Equal(
            PageProcessingRoute.NativeOnly,
            decision.Authoritative.Plan.Route);

        Assert.Equal(
            TextExecutionMode.NativeText,
            decision.Candidate.Plan.TextMode);

        Assert.Equal(
            VisualExecutionAction.PreserveMeaningfulVisual,
            Assert.Single(
                decision.Candidate.Plan.VisualElements).Action);

        Assert.False(
            decision.Candidate.Plan.RequiresRasterization);

        Assert.False(
            decision.Candidate.Plan.RequiresLayoutAnalysis);

        Assert.False(
            decision.Candidate.Plan.RequiresTargetedOcr);

        Assert.True(
            decision.Candidate.Plan.RequiresMeaningfulVisualPreservation);

        Assert.True(
            decision.CandidateHasIndependentVisualWork);
    }

    [Fact]
    public void Plan_HealthyUnknownVisual_AddsVisualAnalysisWithoutOcr()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Healthy);

        var decision =
            Assert.Single(
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            1),
                    Observations(
                        UnknownVisual())));

        Assert.Equal(
            PageProcessingRoute.NativeOnly,
            decision.Authoritative.Plan.Route);

        Assert.Equal(
            TextExecutionMode.NativeText,
            decision.Candidate.Plan.TextMode);

        Assert.True(
            decision.Candidate.Plan.RequiresRasterization);

        Assert.True(
            decision.Candidate.Plan.RequiresLayoutAnalysis);

        Assert.False(
            decision.Candidate.Plan.RequiresTargetedOcr);

        Assert.True(
            decision.Candidate.Plan.RequiresVisualAnalysis);

        Assert.True(
            decision.CandidateHasIndependentVisualWork);
    }

    [Fact]
    public void Plan_PageWithoutVisuals_RequiresExplicitEmptyObservationSet()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Healthy);

        var decision =
            Assert.Single(
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            0),
                    Observations()));

        Assert.Empty(
            decision.Candidate.Evidence.VisualElements);

        Assert.Empty(
            decision.Candidate.Plan.VisualElements);

        Assert.False(
            decision.Candidate.Plan.HasAdditionalSemanticWork);
    }

    [Fact]
    public void Plan_RejectsMissingObservationSetForExtractionPage()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Healthy);

        Assert.Throws<InvalidDataException>(
            () =>
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            0),
                    []));
    }

    [Fact]
    public void Plan_RejectsIncompleteVisualObservationCoverage()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Healthy);

        Assert.Throws<InvalidDataException>(
            () =>
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            2),
                    Observations(
                        BlankCanvas(
                            sourceVisualIndex:
                                0))));
    }

    [Fact]
    public void Plan_RejectsNonContiguousVisualSourceIndexes()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Healthy);

        Assert.Throws<InvalidDataException>(
            () =>
                planner.Plan(
                    Extraction(
                        rasterImageCount:
                            2),
                    Observations(
                        BlankCanvas(
                            sourceVisualIndex:
                                0),
                        BlankCanvas(
                            sourceVisualIndex:
                                2))));
    }

    [Fact]
    public void Plan_RejectsVisualObservationPageMismatch()
    {
        var planner =
            CreatePlanner(
                NativeTextStatus.Healthy);

        Assert.Throws<InvalidDataException>(
            () =>
                planner.Plan(
                    Extraction(
                        physicalPageNumber:
                            1,
                        rasterImageCount:
                            0),
                    [
                        new PageVisualEvidenceObservations(
                            physicalPageNumber:
                                2,
                            visualElements:
                                [])
                    ]));
    }

    [Fact]
    public void Plan_MultiplePages_PreservesDocumentOrderAndPhysicalIdentity()
    {
        var planner =
            new GuardedDocumentPageExecutionPlanner(
                new PerPageAssessor(
                    new Dictionary<int, NativeTextStatus>
                    {
                        [1] =
                            NativeTextStatus.Healthy,
                        [2] =
                            NativeTextStatus.Missing
                    }),
                new DefaultPageProcessingPolicy(),
                new DefaultVisualEvidenceAssessor(),
                new DefaultPageProcessingRequirementsPolicy(),
                new DefaultPageExecutionPlanCompiler());

        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    Page(
                        physicalPageNumber:
                            1,
                        rasterImageCount:
                            0),
                    Page(
                        physicalPageNumber:
                            2,
                        rasterImageCount:
                            1)
                ]);

        var decisions =
            planner.Plan(
                extraction,
                [
                    new PageVisualEvidenceObservations(
                        physicalPageNumber:
                            1,
                        visualElements:
                            []),
                    new PageVisualEvidenceObservations(
                        physicalPageNumber:
                            2,
                        visualElements:
                        [
                            BlankCanvas()
                        ])
                ]);

        Assert.Equal(
            2,
            decisions.Count);

        Assert.Equal(
            1,
            decisions[0].PhysicalPageNumber);

        Assert.Equal(
            2,
            decisions[1].PhysicalPageNumber);

        Assert.Equal(
            TextExecutionMode.NativeText,
            decisions[0].Candidate.Plan.TextMode);

        Assert.Equal(
            TextExecutionMode.TargetedOcrRecovery,
            decisions[1].Candidate.Plan.TextMode);
    }

    [Fact]
    public void PageVisualEvidenceObservations_SnapshotsCallerCollection()
    {
        var source =
            new List<VisualEvidenceObservation>
            {
                BlankCanvas()
            };

        var observations =
            new PageVisualEvidenceObservations(
                physicalPageNumber:
                    1,
                visualElements:
                    source);

        source.Add(
            LargeIndependentVisual(
                sourceVisualIndex:
                    1));

        Assert.Single(
            observations.VisualElements);
    }

    [Fact]
    public void PageVisualEvidenceObservations_RejectDuplicateSourceIndexes()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PageVisualEvidenceObservations(
                    physicalPageNumber:
                        1,
                    visualElements:
                    [
                        BlankCanvas(
                            sourceVisualIndex:
                                0),
                        LargeIndependentVisual(
                            sourceVisualIndex:
                                0)
                    ]));
    }

    private static GuardedDocumentPageExecutionPlanner CreatePlanner(
        NativeTextStatus status) =>
        new(
            new PerPageAssessor(
                new Dictionary<int, NativeTextStatus>
                {
                    [1] =
                        status
                }),
            new DefaultPageProcessingPolicy(),
            new DefaultVisualEvidenceAssessor(),
            new DefaultPageProcessingRequirementsPolicy(),
            new DefaultPageExecutionPlanCompiler());

    private static DocumentExtractionResult Extraction(
        int physicalPageNumber = 1,
        int rasterImageCount = 1) =>
        new(
            DocumentFormatId.Pdf,
            [
                Page(
                    physicalPageNumber,
                    rasterImageCount)
            ]);

    private static DocumentExtractionPage Page(
        int physicalPageNumber,
        int rasterImageCount) =>
        new(
            physicalPageNumber,
            sourceText:
                "native text",
            wordCount:
                1,
            rasterImageCount:
                rasterImageCount,
            largestRasterImageAreaRatio:
                rasterImageCount > 0
                    ? 0.67
                    : 0);

    private static IReadOnlyList<PageVisualEvidenceObservations> Observations(
        params VisualEvidenceObservation[] visualElements) =>
        [
            new PageVisualEvidenceObservations(
                physicalPageNumber:
                    1,
                visualElements)
        ];

    private static VisualEvidenceObservation BlankCanvas(
        int sourceVisualIndex = 0) =>
        new(
            sourceVisualIndex,
            VisualForegroundState.BlankCanvas,
            foregroundPixelRatio:
                0,
            VisualPixelInteractionKind.BlankCanvas,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                0,
            effectiveVisualAreaRatio:
                null,
            HeadingAssociationEvidenceKind.NotMeasured,
            NativeTextContainmentEvidenceKind.NotMeasured,
            CaptionAssociationEvidenceKind.NotMeasured);

    private static VisualEvidenceObservation UnknownVisual(
        int sourceVisualIndex = 0) =>
        new(
            sourceVisualIndex,
            VisualForegroundState.Unavailable,
            foregroundPixelRatio:
                null,
            VisualPixelInteractionKind.NotMeasured,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                null,
            effectiveVisualAreaRatio:
                null,
            HeadingAssociationEvidenceKind.NotMeasured,
            NativeTextContainmentEvidenceKind.NotMeasured,
            CaptionAssociationEvidenceKind.NotMeasured);

    private static VisualEvidenceObservation LargeIndependentVisual(
        int sourceVisualIndex = 0) =>
        new(
            sourceVisualIndex,
            VisualForegroundState.Measured,
            foregroundPixelRatio:
                0.10,
            VisualPixelInteractionKind.NoForegroundWordIntersection,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                3,
            effectiveVisualAreaRatio:
                0.15,
            HeadingAssociationEvidenceKind.NoStrongAssociation,
            NativeTextContainmentEvidenceKind.NoContainedNativeText,
            CaptionAssociationEvidenceKind.NoStrongAssociation);

    private sealed class PerPageAssessor
        : IPageProcessingAssessor
    {
        private readonly IReadOnlyDictionary<int, NativeTextStatus> _statuses;

        public PerPageAssessor(
            IReadOnlyDictionary<int, NativeTextStatus> statuses)
        {
            _statuses =
                statuses;
        }

        public PageProcessingAssessment Assess(
            DocumentExtractionPage page)
        {
            if (!_statuses.TryGetValue(
                    page.PhysicalPageNumber,
                    out var status))
            {
                throw new InvalidOperationException(
                    $"No native-text status configured for physical page " +
                    $"{page.PhysicalPageNumber}.");
            }

            return new PageProcessingAssessment(
                page.PhysicalPageNumber,
                status);
        }
    }
}
