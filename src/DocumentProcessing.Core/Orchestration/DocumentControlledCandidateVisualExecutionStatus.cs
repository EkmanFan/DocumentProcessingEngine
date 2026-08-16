namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Overall outcome of controlled non-authoritative candidate visual execution.
/// </summary>
public enum DocumentControlledCandidateVisualExecutionStatus
{
    /// <summary>
    /// H.4C planning completed and every candidate visual action was executed.
    /// </summary>
    Completed,

    /// <summary>
    /// H.4C did not produce a completed plan, so no candidate visual action was
    /// executed.
    /// </summary>
    PlanningUnavailable,

    /// <summary>
    /// An ordinary candidate visual-execution failure was isolated from the
    /// authoritative legacy result.
    /// </summary>
    Failed
}
