namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Outcome of the non-authoritative document Dual Run planning path.
/// </summary>
public enum DocumentDualRunPlanningStatus
{
    /// <summary>
    /// The complete candidate planning chain ran successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// No configured visual-observation source supports the detected format.
    /// Authoritative execution remains unchanged.
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// Dual Run evidence/planning failed without changing the authoritative
    /// execution path.
    /// </summary>
    Failed
}
