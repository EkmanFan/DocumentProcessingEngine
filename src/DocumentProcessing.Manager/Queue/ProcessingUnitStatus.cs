namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Durable lifecycle status of one processing unit.
/// </summary>
public enum ProcessingUnitStatus
{
    /// <summary>
    /// The unit is waiting in the global queue.
    /// </summary>
    Pending =
        0,

    /// <summary>
    /// One globally fenced worker currently owns the unit.
    /// </summary>
    Active =
        1,

    /// <summary>
    /// The unit produced a durable registered result.
    /// </summary>
    Succeeded =
        2,

    /// <summary>
    /// The unit reached a terminal processing failure.
    /// </summary>
    Failed =
        3
}
