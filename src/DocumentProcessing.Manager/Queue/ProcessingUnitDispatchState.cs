namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Durable dispatch eligibility of one pending processing unit.
/// </summary>
public enum ProcessingUnitDispatchState
{
    /// <summary>
    /// The unit remains visible and ordered but requires an explicit release.
    /// </summary>
    Shelved =
        0,

    /// <summary>
    /// The unit may be claimed when the Manager is running.
    /// </summary>
    Ready =
        1
}
