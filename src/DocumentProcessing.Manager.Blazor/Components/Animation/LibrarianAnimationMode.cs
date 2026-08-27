namespace DocumentProcessing.Manager.Blazor.Components.Animation;

/// <summary>
/// Describes the observable activity represented by the librarian scene.
/// </summary>
public enum LibrarianAnimationMode
{
    /// <summary>
    /// The workshop is establishing its first Manager connection.
    /// </summary>
    Connecting,

    /// <summary>
    /// The Manager is ready but no document is currently active.
    /// </summary>
    Waiting,

    /// <summary>
    /// A document is actively being processed.
    /// </summary>
    Reading,

    /// <summary>
    /// Processing has been paused.
    /// </summary>
    Paused,

    /// <summary>
    /// The Manager has been stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// The Manager Host cannot currently be reached.
    /// </summary>
    Unavailable
}
