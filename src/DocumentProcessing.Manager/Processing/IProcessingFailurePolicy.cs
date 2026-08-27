using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Strategy that selects the queue disposition of technical failures.
/// </summary>
public interface IProcessingFailurePolicy
{
    /// <summary>
    /// Selects whether the failed work item is terminal or retried.
    /// </summary>
    ProcessingFailureDisposition Decide(
        ProcessingWorkItem workItem,
        ProcessingFailure failure);
}
