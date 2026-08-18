namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Immutable trace of the candidate two-axis planning chain for one page.
///
/// The trace deliberately retains evidence and requirements so Dual Run
/// validation can explain why an execution plan differs from the authoritative route.
/// </summary>
public sealed record PageExecutionPlanningDecision
{
    public PageExecutionPlanningDecision(
        PageProcessingAssessment nativeAssessment,
        PageProcessingEvidence evidence,
        PageProcessingRequirements requirements,
        PageExecutionPlan plan)
    {
        NativeAssessment =
            nativeAssessment ??
            throw new ArgumentNullException(
                nameof(nativeAssessment));

        Evidence =
            evidence ??
            throw new ArgumentNullException(
                nameof(evidence));

        Requirements =
            requirements ??
            throw new ArgumentNullException(
                nameof(requirements));

        Plan =
            plan ??
            throw new ArgumentNullException(
                nameof(plan));

        var physicalPageNumber =
            NativeAssessment.PhysicalPageNumber;

        if (Evidence.PhysicalPageNumber !=
                physicalPageNumber ||
            Requirements.PhysicalPageNumber !=
                physicalPageNumber ||
            Plan.PhysicalPageNumber !=
                physicalPageNumber)
        {
            throw new ArgumentException(
                "Candidate planning artifacts must refer to the same physical page.");
        }
    }

    public int PhysicalPageNumber =>
        NativeAssessment.PhysicalPageNumber;

    public PageProcessingAssessment NativeAssessment { get; }

    public PageProcessingEvidence Evidence { get; }

    public PageProcessingRequirements Requirements { get; }

    public PageExecutionPlan Plan { get; }
}
