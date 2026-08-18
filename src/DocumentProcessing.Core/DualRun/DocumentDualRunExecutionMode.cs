namespace DocumentProcessing.Core.DualRun;

/// <summary>
/// Resolved worker execution requested for one selected document.
/// Sampling is resolved by the parent before worker dispatch.
/// </summary>
public enum DocumentDualRunExecutionMode
{
    PlanningOnly,
    Full
}
