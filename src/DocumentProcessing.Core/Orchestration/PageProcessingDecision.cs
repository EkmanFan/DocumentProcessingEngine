namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Immutable association between deterministic page evidence assessment and the
/// processing route selected from that assessment.
/// </summary>
public sealed record PageProcessingDecision
{
    public PageProcessingDecision(
        PageProcessingAssessment assessment,
        PageProcessingPlan plan)
    {
        Assessment =
            assessment ??
            throw new ArgumentNullException(
                nameof(assessment));

        Plan =
            plan ??
            throw new ArgumentNullException(
                nameof(plan));
    }

    public int PhysicalPageNumber =>
        Assessment.PhysicalPageNumber;

    public PageProcessingAssessment Assessment { get; }

    public PageProcessingPlan Plan { get; }
}
