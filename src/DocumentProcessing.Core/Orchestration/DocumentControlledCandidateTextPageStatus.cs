namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// H.4D.1 text-axis disposition for one candidate page.
/// </summary>
public enum DocumentControlledCandidateTextPageStatus
{
    /// <summary>
    /// The candidate NativeText mode was actually executed from extracted
    /// native blocks and compared with the already-computed legacy page.
    /// </summary>
    ExecutedNativeText,

    /// <summary>
    /// The candidate requires an OCR-backed text mode. H.4D.1 deliberately does
    /// not execute that mode yet.
    /// </summary>
    DeferredNonNativeTextMode
}
