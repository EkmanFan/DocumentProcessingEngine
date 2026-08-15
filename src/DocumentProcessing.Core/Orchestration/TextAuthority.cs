namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Policy-facing interpretation of native text evidence.
///
/// Text authority is intentionally independent from visual evidence. A page may
/// contain trusted native text and still contain a meaningful visual that must
/// be preserved. Likewise, visual presentation evidence must never override a
/// missing or corrupted native-text state.
/// </summary>
public enum TextAuthority
{
    /// <summary>
    /// No usable native text is available.
    /// </summary>
    Missing,

    /// <summary>
    /// Deterministic native evidence is sufficient to trust the native text.
    /// </summary>
    Trusted,

    /// <summary>
    /// Native text exists, but deterministic evidence is not yet sufficient to
    /// establish visual fidelity.
    /// </summary>
    NeedsVerification,

    /// <summary>
    /// Explicit deterministic evidence indicates native-text corruption or
    /// structural inconsistency.
    /// </summary>
    Corrupted
}
