
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.Core.DualRun;
/// <summary>
/// Side-by-side comparison of the decision that remains authoritative for
/// runtime execution and the candidate guarded Dual Run decision.
/// </summary>
public sealed record DocumentDualRunPageComparison
{
    public DocumentDualRunPageComparison(
        PageProcessingDecision authoritative,
        GuardedPagePlanningDecision dualRun)
    {
        Authoritative =
            authoritative ??
            throw new ArgumentNullException(
                nameof(authoritative));

        DualRun =
            dualRun ??
            throw new ArgumentNullException(
                nameof(dualRun));

        if (Authoritative.PhysicalPageNumber !=
            DualRun.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "Authoritative and Dual Run decisions must refer to the same physical page.");
        }
    }

    public int PhysicalPageNumber =>
        Authoritative.PhysicalPageNumber;

    /// <summary>
    /// Decision consumed by DocumentProcessor runtime execution.
    /// </summary>
    public PageProcessingDecision Authoritative { get; }

    /// <summary>
    /// Non-authoritative guarded candidate decision.
    /// </summary>
    public GuardedPagePlanningDecision DualRun { get; }

    /// <summary>
    /// Verifies that the authoritative branch recomputed inside the guarded planner
    /// agrees with the already-authoritative planner used by DocumentProcessor.
    /// </summary>
    public bool AuthoritativePlanningAgreement =>
        Authoritative.Assessment.NativeTextStatus ==
            DualRun.Authoritative.Assessment.NativeTextStatus &&
        Authoritative.Plan.Route ==
            DualRun.Authoritative.Plan.Route;

    public bool CandidateRemovesAuthoritativeTextMl =>
        Authoritative.Plan.Route !=
            PageProcessingRoute.NativeOnly &&
        DualRun.Candidate.Plan.TextMode ==
            TextExecutionMode.NativeText;

    public bool CandidateAddsIndependentVisualWorkToAuthoritativeNativePage =>
        Authoritative.Plan.Route ==
            PageProcessingRoute.NativeOnly &&
        DualRun.CandidateHasIndependentVisualWork;
}
