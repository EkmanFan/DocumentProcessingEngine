namespace DocumentProcessing.Manager.Custody;

/// <summary>
/// Stable semantic kind of an append-only custody event.
/// </summary>
public enum CustodyEventKind
{
    /// <summary>
    /// Exact source bytes and their immutable manifest were registered.
    /// </summary>
    SourceRegistered =
        0
}
