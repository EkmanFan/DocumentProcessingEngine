namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Concrete engine mechanism selected for the text axis.
///
/// Unlike <see cref="TextProcessingRequirement"/>, these values describe the
/// current execution capability used to satisfy that requirement.
/// </summary>
public enum TextExecutionMode
{
    /// <summary>
    /// Consume trusted native text without secondary OCR.
    /// </summary>
    NativeText,

    /// <summary>
    /// Rasterize, analyze layout and use targeted OCR to recover missing native
    /// text.
    /// </summary>
    TargetedOcrRecovery,

    /// <summary>
    /// Rasterize, analyze layout and obtain targeted OCR as secondary evidence
    /// to verify native text.
    /// </summary>
    TargetedOcrVerification,

    /// <summary>
    /// Rasterize, analyze layout and obtain targeted OCR to reconcile explicit
    /// native-text corruption.
    /// </summary>
    TargetedOcrReconciliation
}
