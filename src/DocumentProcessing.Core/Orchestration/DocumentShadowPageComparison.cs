namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Side-by-side comparison of the decision that remains authoritative for
/// runtime execution and the candidate guarded shadow decision.
/// </summary>
public sealed record DocumentShadowPageComparison
{
    public DocumentShadowPageComparison(
        PageProcessingDecision authoritativeLegacy,
        GuardedPagePlanningDecision shadow)
    {
        AuthoritativeLegacy =
            authoritativeLegacy ??
            throw new ArgumentNullException(
                nameof(authoritativeLegacy));

        Shadow =
            shadow ??
            throw new ArgumentNullException(
                nameof(shadow));

        if (AuthoritativeLegacy.PhysicalPageNumber !=
            Shadow.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "Authoritative and shadow decisions must refer to the same physical page.");
        }
    }

    public int PhysicalPageNumber =>
        AuthoritativeLegacy.PhysicalPageNumber;

    /// <summary>
    /// Decision consumed by DocumentProcessor runtime execution.
    /// </summary>
    public PageProcessingDecision AuthoritativeLegacy { get; }

    /// <summary>
    /// Non-authoritative guarded candidate decision.
    /// </summary>
    public GuardedPagePlanningDecision Shadow { get; }

    /// <summary>
    /// Verifies that the legacy branch recomputed inside the guarded planner
    /// agrees with the already-authoritative planner used by DocumentProcessor.
    /// </summary>
    public bool LegacyPlanningAgreement =>
        AuthoritativeLegacy.Assessment.NativeTextStatus ==
            Shadow.Legacy.Assessment.NativeTextStatus &&
        AuthoritativeLegacy.Plan.Route ==
            Shadow.Legacy.Plan.Route;

    public bool CandidateRemovesLegacyTextMl =>
        AuthoritativeLegacy.Plan.Route !=
            PageProcessingRoute.NativeOnly &&
        Shadow.Candidate.Plan.TextMode ==
            TextExecutionMode.NativeText;

    public bool CandidateAddsIndependentVisualWorkToLegacyNativePage =>
        AuthoritativeLegacy.Plan.Route ==
            PageProcessingRoute.NativeOnly &&
        Shadow.CandidateHasIndependentVisualWork;
}
