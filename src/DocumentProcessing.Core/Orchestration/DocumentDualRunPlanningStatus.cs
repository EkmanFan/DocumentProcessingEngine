namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Outcome of the non-authoritative document shadow-planning path.
/// </summary>
public enum DocumentDualRunPlanningStatus
{
    /// <summary>
    /// The complete candidate planning chain ran successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// No configured visual-observation source supports the detected format.
    /// Legacy execution remains authoritative.
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// Shadow evidence/planning failed without changing the authoritative
    /// legacy execution path.
    /// </summary>
    Failed
}
