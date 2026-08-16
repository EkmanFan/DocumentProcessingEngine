using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Guarded shadow integration of the legacy route planner and the new two-axis
/// planning chain.
///
/// The planner requires complete visual-observation coverage for every source
/// visual occurrence. It produces both the legacy decision and the candidate
/// execution plan, then enforces text-safety invariants before returning.
///
/// Phase 21E.1H.4C wires this planner into <see cref="DocumentProcessor"/>
/// only through non-authoritative shadow planning. Runtime execution remains
/// driven exclusively by the legacy planner.
/// </summary>
public sealed class GuardedDocumentPageExecutionPlanner
{
    private readonly IPageProcessingAssessor _nativeAssessor;
    private readonly IPageProcessingPolicy _legacyPolicy;
    private readonly DefaultVisualEvidenceAssessor _visualAssessor;
    private readonly IPageProcessingRequirementsPolicy _requirementsPolicy;
    private readonly DefaultPageExecutionPlanCompiler _executionPlanCompiler;

    public GuardedDocumentPageExecutionPlanner(
        IPageProcessingAssessor nativeAssessor,
        IPageProcessingPolicy legacyPolicy,
        DefaultVisualEvidenceAssessor visualAssessor,
        IPageProcessingRequirementsPolicy requirementsPolicy,
        DefaultPageExecutionPlanCompiler executionPlanCompiler)
    {
        _nativeAssessor =
            nativeAssessor ??
            throw new ArgumentNullException(
                nameof(nativeAssessor));

        _legacyPolicy =
            legacyPolicy ??
            throw new ArgumentNullException(
                nameof(legacyPolicy));

        _visualAssessor =
            visualAssessor ??
            throw new ArgumentNullException(
                nameof(visualAssessor));

        _requirementsPolicy =
            requirementsPolicy ??
            throw new ArgumentNullException(
                nameof(requirementsPolicy));

        _executionPlanCompiler =
            executionPlanCompiler ??
            throw new ArgumentNullException(
                nameof(executionPlanCompiler));
    }

    public IReadOnlyList<GuardedPagePlanningDecision> Plan(
        DocumentExtractionResult extraction,
        IReadOnlyList<PageVisualEvidenceObservations> visualObservations)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            visualObservations);

        if (visualObservations.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Guarded planner received {visualObservations.Count} visual-observation " +
                $"sets for {extraction.Pages.Count} extracted pages.");
        }

        var decisions =
            new GuardedPagePlanningDecision[
                extraction.Pages.Count];

        for (var index = 0;
             index <
             extraction.Pages.Count;
             index++)
        {
            var page =
                extraction.Pages[index];

            var pageVisualObservations =
                visualObservations[index];

            ValidateObservationCoverage(
                page,
                pageVisualObservations,
                index);

            var nativeAssessment =
                _nativeAssessor.Assess(
                    page);

            if (nativeAssessment.PhysicalPageNumber !=
                page.PhysicalPageNumber)
            {
                throw new InvalidDataException(
                    $"Native assessor returned physical page " +
                    $"{nativeAssessment.PhysicalPageNumber} for extraction page " +
                    $"{page.PhysicalPageNumber}.");
            }

            var legacyPlan =
                _legacyPolicy.Decide(
                    nativeAssessment);

            var legacyDecision =
                new PageProcessingDecision(
                    nativeAssessment,
                    legacyPlan);

            var visualEvidence =
                pageVisualObservations
                    .VisualElements
                    .Select(
                        _visualAssessor.Assess)
                    .ToArray();

            var evidence =
                new PageProcessingEvidence(
                    page.PhysicalPageNumber,
                    TextAuthorityMapper
                        .FromNativeTextStatus(
                            nativeAssessment.NativeTextStatus),
                    visualEvidence);

            var requirements =
                _requirementsPolicy.Decide(
                    evidence);

            var candidatePlan =
                _executionPlanCompiler.Compile(
                    requirements);

            var candidateDecision =
                new PageExecutionPlanningDecision(
                    nativeAssessment,
                    evidence,
                    requirements,
                    candidatePlan);

            EnforceTextSafetyGuard(
                nativeAssessment.NativeTextStatus,
                candidatePlan);

            decisions[index] =
                new GuardedPagePlanningDecision(
                    legacyDecision,
                    candidateDecision);
        }

        return decisions;
    }

    public static GuardedDocumentPageExecutionPlanner CreateDefault() =>
        new(
            new DefaultPageProcessingAssessor(),
            new DefaultPageProcessingPolicy(),
            new DefaultVisualEvidenceAssessor(),
            new DefaultPageProcessingRequirementsPolicy(),
            new DefaultPageExecutionPlanCompiler());

    private static void ValidateObservationCoverage(
        DocumentExtractionPage page,
        PageVisualEvidenceObservations observations,
        int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(
            observations);

        if (observations.PhysicalPageNumber !=
            page.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                $"Visual-observation set at index {pageIndex} refers to physical page " +
                $"{observations.PhysicalPageNumber}; expected {page.PhysicalPageNumber}.");
        }

        if (observations.VisualElements.Count !=
            page.RasterImageCount)
        {
            throw new InvalidDataException(
                $"Physical page {page.PhysicalPageNumber} reports " +
                $"{page.RasterImageCount} source raster image occurrence(s), but the guarded " +
                $"planner received {observations.VisualElements.Count} visual observation(s).");
        }

        var indexes =
            observations.VisualElements
                .Select(
                    observation =>
                        observation.SourceVisualIndex)
                .OrderBy(
                    sourceVisualIndex =>
                        sourceVisualIndex)
                .ToArray();

        for (var expectedIndex = 0;
             expectedIndex <
             indexes.Length;
             expectedIndex++)
        {
            if (indexes[expectedIndex] !=
                expectedIndex)
            {
                throw new InvalidDataException(
                    $"Physical page {page.PhysicalPageNumber} visual observations must " +
                    $"cover source indexes 0..{Math.Max(0, page.RasterImageCount - 1)} " +
                    $"exactly once.");
            }
        }
    }

    private static void EnforceTextSafetyGuard(
        NativeTextStatus nativeTextStatus,
        PageExecutionPlan candidatePlan)
    {
        var allowed =
            nativeTextStatus switch
            {
                NativeTextStatus.Healthy =>
                    candidatePlan.TextMode ==
                    TextExecutionMode.NativeText,

                NativeTextStatus.Missing =>
                    candidatePlan.TextMode ==
                    TextExecutionMode.TargetedOcrRecovery,

                NativeTextStatus.Suspicious =>
                    candidatePlan.TextMode ==
                    TextExecutionMode.TargetedOcrReconciliation,

                NativeTextStatus.Unverified =>
                    candidatePlan.TextMode is
                        TextExecutionMode.NativeText or
                        TextExecutionMode.TargetedOcrVerification,

                _ =>
                    false
            };

        if (!allowed)
        {
            throw new InvalidDataException(
                $"Candidate page plan text mode '{candidatePlan.TextMode}' is not " +
                $"permitted for native-text status '{nativeTextStatus}'.");
        }
    }
}
