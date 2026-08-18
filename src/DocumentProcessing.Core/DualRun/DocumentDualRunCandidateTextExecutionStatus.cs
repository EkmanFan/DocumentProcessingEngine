namespace DocumentProcessing.Core.DualRun;
/// <summary>
/// Overall outcome of the Dual Run candidate text-execution experiment.
///
/// The report is comparison evidence only. It never authorizes candidate output
/// to replace the authoritative result.
/// </summary>
public enum DocumentDualRunCandidateTextExecutionStatus
{
    /// <summary>
    /// Dual Run planning completed and every page was either executed in the
    /// H.4D.1 NativeText capability or explicitly deferred because its
    /// candidate text mode is not yet executable in this increment.
    /// </summary>
    Completed,

    /// <summary>
    /// H.4C did not produce a completed candidate plan, so H.4D.1 performed no
    /// candidate execution.
    /// </summary>
    PlanningUnavailable,

    /// <summary>
    /// An ordinary non-fatal candidate-execution failure was isolated from the
    /// authoritative result.
    /// </summary>
    Failed
}
