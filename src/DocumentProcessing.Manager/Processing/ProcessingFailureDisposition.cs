namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Queue disposition selected for one technical processing failure.
/// </summary>
public enum ProcessingFailureDisposition
{
    /// <summary>
    /// The processing unit reaches a terminal failed state.
    /// </summary>
    Fail,

    /// <summary>
    /// The processing unit returns to the pending queue for another attempt.
    /// </summary>
    Requeue
}
