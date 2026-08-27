namespace DocumentProcessing.Manager.Control;

/// <summary>
/// Durable operating state of the document-processing Manager.
/// </summary>
public enum ManagerOperatingState
{
    /// <summary>
    /// The dispatcher must not start work and any active unit is interrupted.
    /// </summary>
    Stopped,

    /// <summary>
    /// The dispatcher may claim and process queued units.
    /// </summary>
    Running,

    /// <summary>
    /// The active unit may finish, but no subsequent unit may start.
    /// </summary>
    Paused
}
