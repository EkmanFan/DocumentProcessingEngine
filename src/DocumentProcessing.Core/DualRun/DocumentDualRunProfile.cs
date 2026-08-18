namespace DocumentProcessing.Core.DualRun;

/// <summary>
/// Document-level Dual Run activation profile snapshotted before processing.
/// </summary>
public enum DocumentDualRunProfile
{
    /// <summary>
    /// No Dual Run source snapshot, dispatch, queue, worker, or candidate work.
    /// </summary>
    Disabled,

    /// <summary>
    /// Execute deterministic candidate planning/comparison only.
    /// </summary>
    PlanningOnly,

    /// <summary>
    /// Select a deterministic source-hash cohort. Selected documents execute
    /// the Full mode; unselected documents perform no Dual Run work.
    /// </summary>
    Sampled,

    /// <summary>
    /// Execute complete Dual Run planning and candidate execution.
    /// </summary>
    Full
}
