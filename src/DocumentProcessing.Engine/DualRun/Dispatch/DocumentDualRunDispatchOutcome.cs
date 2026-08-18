namespace DocumentProcessing.Engine.DualRun.Dispatch;

/// <summary>
/// Producer-side result of a non-blocking Dual Run dispatch attempt.
/// </summary>
public enum DocumentDualRunDispatchOutcome
{
    /// <summary>
    /// Ownership transferred to the dispatcher queue.
    /// </summary>
    Enqueued,

    /// <summary>
    /// Queue capacity was exhausted. Ownership remains with the caller.
    /// </summary>
    QueueFull,

    /// <summary>
    /// Dispatcher no longer accepts work. Ownership remains with the caller.
    /// </summary>
    Stopped
}
