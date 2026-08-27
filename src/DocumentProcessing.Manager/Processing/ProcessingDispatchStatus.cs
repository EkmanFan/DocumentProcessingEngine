namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Terminal status of one attempt to dispatch the next queued unit.
/// </summary>
public enum ProcessingDispatchStatus
{
    /// <summary>
    /// No pending processing unit was available.
    /// </summary>
    QueueEmpty,

    /// <summary>
    /// The claimed unit completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The claimed unit reached a terminal failure.
    /// </summary>
    Failed,

    /// <summary>
    /// A technical failure was recorded and the unit was requeued.
    /// </summary>
    RetryScheduled,

    /// <summary>
    /// The claimed unit was interrupted and requeued.
    /// </summary>
    Interrupted,

    /// <summary>
    /// The worker lost its durable lease and did not finalize the unit.
    /// </summary>
    LeaseLost
}
