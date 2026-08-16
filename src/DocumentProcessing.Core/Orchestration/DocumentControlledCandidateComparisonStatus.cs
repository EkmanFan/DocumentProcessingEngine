namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Outcome of deterministic H.4D.4 cross-axis candidate comparison.
/// </summary>
public enum DocumentControlledCandidateComparisonStatus
{
    /// <summary>
    /// Planning, text execution and visual execution were comparable page by
    /// page. Cutover may still be blocked by explicit evidence gaps.
    /// </summary>
    Completed,

    /// <summary>
    /// H.4C planning was unavailable, so no cross-axis comparison was possible.
    /// </summary>
    PlanningUnavailable,

    /// <summary>
    /// One or both controlled candidate execution axes did not complete.
    /// </summary>
    CandidateExecutionUnavailable,

    /// <summary>
    /// The comparison mechanism itself encountered an ordinary isolated failure.
    /// </summary>
    Failed
}
