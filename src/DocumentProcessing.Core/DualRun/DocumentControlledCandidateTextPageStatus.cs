namespace DocumentProcessing.Core.DualRun;
/// <summary>
/// Text-axis execution disposition for one controlled candidate page.
/// </summary>
public enum DocumentControlledCandidateTextPageStatus
{
    /// <summary>
    /// Candidate NativeText was executed from extracted native blocks.
    /// </summary>
    ExecutedNativeText,

    /// <summary>
    /// Candidate TargetedOcrRecovery was executed.
    /// </summary>
    ExecutedTargetedOcrRecovery,

    /// <summary>
    /// Candidate TargetedOcrVerification was executed.
    /// </summary>
    ExecutedTargetedOcrVerification,

    /// <summary>
    /// Candidate TargetedOcrReconciliation was executed.
    /// </summary>
    ExecutedTargetedOcrReconciliation,

    /// <summary>
    /// The candidate requires an OCR-backed text mode but this composition does
    /// not provide controlled OCR execution dependencies. This preserves the
    /// H.4D.1 NativeText-only opt-in behavior.
    /// </summary>
    DeferredNonNativeTextMode
}
