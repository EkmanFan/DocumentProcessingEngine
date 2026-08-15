namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Explicit upstream assessment of native PDF text for one reconciliation
/// candidate.
///
/// This enum does not attempt to infer quality itself. The caller must supply
/// the status from deterministic preflight/page/region evidence.
/// </summary>
public enum NativeTextStatus
{
    /// <summary>
    /// No usable native text evidence is present.
    /// </summary>
    Missing,

    /// <summary>
    /// Available native text has enough deterministic evidence to be trusted
    /// without secondary OCR verification under the active policy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Available native text contains explicit deterministic evidence of
    /// corruption or structural inconsistency.
    /// </summary>
    Suspicious,

    /// <summary>
    /// Native text exists, but current deterministic native evidence is
    /// insufficient to establish whether it is faithful to the visible page.
    ///
    /// A common V1 case is an image-backed PDF page with a native/hidden text
    /// layer. "Unverified" is deliberately distinct from "Suspicious": lack of
    /// proof is not evidence of corruption.
    /// </summary>
    Unverified
}
