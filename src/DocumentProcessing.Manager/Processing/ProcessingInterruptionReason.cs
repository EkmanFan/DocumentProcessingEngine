namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Reason why an active processing unit must return to the pending queue.
/// </summary>
public enum ProcessingInterruptionReason
{
    /// <summary>
    /// The Manager received an explicit stop command.
    /// </summary>
    ManagerStop,

    /// <summary>
    /// The process host is shutting down.
    /// </summary>
    HostShutdown,

    /// <summary>
    /// This process lost exclusive ownership of the global Manager runtime.
    /// </summary>
    RuntimeLeaseLost
}
