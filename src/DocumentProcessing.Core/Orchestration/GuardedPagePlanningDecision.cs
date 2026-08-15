namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Side-by-side legacy and candidate planning result for one physical page.
///
/// Runtime execution still consumes <see cref="Legacy"/> during Phase
/// 21E.1H.3C. The candidate plan exists for guarded comparison and future
/// cutover only.
/// </summary>
public sealed record GuardedPagePlanningDecision
{
    public GuardedPagePlanningDecision(
        PageProcessingDecision legacy,
        PageExecutionPlanningDecision candidate)
    {
        Legacy =
            legacy ??
            throw new ArgumentNullException(
                nameof(legacy));

        Candidate =
            candidate ??
            throw new ArgumentNullException(
                nameof(candidate));

        if (Legacy.PhysicalPageNumber !=
            Candidate.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "Legacy and candidate decisions must refer to the same physical page.");
        }
    }

    public int PhysicalPageNumber =>
        Legacy.PhysicalPageNumber;

    public PageProcessingDecision Legacy { get; }

    public PageExecutionPlanningDecision Candidate { get; }

    /// <summary>
    /// True when the legacy route requests hybrid text processing but the
    /// candidate two-axis policy can safely consume native text.
    ///
    /// This is an intended optimization signal, not permission to cut over
    /// runtime execution during Phase 21E.1H.3C.
    /// </summary>
    public bool CandidateRemovesLegacyTextMl =>
        Legacy.Plan.Route !=
            PageProcessingRoute.NativeOnly &&
        Candidate.Plan.TextMode ==
            TextExecutionMode.NativeText;

    public bool CandidateHasIndependentVisualWork =>
        Candidate.Plan.RequiresVisualAnalysis ||
        Candidate.Plan.RequiresMeaningfulVisualPreservation;
}
