namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Policy-facing text-processing requirement selected from deterministic
/// <see cref="TextAuthority"/> plus the page's visual evidence.
///
/// This enum describes what must be achieved for the text axis. It does not
/// prescribe a concrete raster/layout/OCR execution route.
/// </summary>
public enum TextProcessingRequirement
{
    /// <summary>
    /// Native text may be consumed without secondary text verification.
    /// </summary>
    UseNativeText,

    /// <summary>
    /// Authoritative native text is missing and text recovery remains required.
    /// </summary>
    RecoverMissingNativeText,

    /// <summary>
    /// Native text exists but deterministic evidence is still insufficient to
    /// remove the verification requirement.
    /// </summary>
    VerifyNativeText,

    /// <summary>
    /// Explicit deterministic corruption evidence requires native/OCR
    /// reconciliation or an equivalent conservative recovery mechanism.
    /// </summary>
    ReconcileCorruptedNativeText
}
