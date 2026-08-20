namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Side-by-side authoritative and candidate planning result for one physical page.
///
/// Runtime execution consumes <see cref="Authoritative"/>.
/// The candidate plan exists for guarded comparison and future
/// cutover only.
/// </summary>
public sealed record GuardedPagePlanningDecision
{
    public GuardedPagePlanningDecision(
        PageProcessingDecision authoritative,
        PageExecutionPlanningDecision candidate)
    {
        Authoritative =
            authoritative ??
            throw new ArgumentNullException(
                nameof(authoritative));

        Candidate =
            candidate ??
            throw new ArgumentNullException(
                nameof(candidate));

        if (Authoritative.PhysicalPageNumber !=
            Candidate.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "Authoritative and candidate decisions must refer to the same physical page.");
        }
    }

    public int PhysicalPageNumber =>
        Authoritative.PhysicalPageNumber;

    public PageProcessingDecision Authoritative { get; }

    public PageExecutionPlanningDecision Candidate { get; }

    /// <summary>
    /// True when the authoritative route requests hybrid text processing but the
    /// candidate two-axis policy can safely consume native text.
    ///
    /// This is an intended optimization signal, not permission to change
    /// authoritative runtime execution.
    /// </summary>
    public bool CandidateRemovesAuthoritativeTextMl =>
        Authoritative.Plan.Route !=
            PageProcessingRoute.NativeOnly &&
        Candidate.Plan.TextMode ==
            TextExecutionMode.NativeText;

    public bool CandidateHasIndependentVisualWork =>
        Candidate.Plan.RequiresVisualAnalysis ||
        Candidate.Plan.RequiresMeaningfulVisualPreservation;
}
