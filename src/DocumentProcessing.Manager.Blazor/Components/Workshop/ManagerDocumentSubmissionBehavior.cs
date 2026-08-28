namespace DocumentProcessing.Manager.Blazor.Components.Workshop;

/// <summary>
/// Selects how a received document enters the Manager queue.
/// </summary>
public enum ManagerDocumentSubmissionBehavior
{
    /// <summary>
    /// Keeps the document ordered but ineligible until explicitly released.
    /// </summary>
    Shelve =
        0,

    /// <summary>
    /// Makes the document immediately eligible for sequential processing.
    /// </summary>
    Run =
        1
}
