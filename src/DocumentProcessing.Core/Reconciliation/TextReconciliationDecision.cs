namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Deterministic outcome of native/OCR reconciliation for one paired text
/// candidate.
/// </summary>
public enum TextReconciliationDecision
{
    /// <summary>
    /// Healthy native evidence exists and no usable OCR text is available.
    /// </summary>
    NativeOnly,

    /// <summary>
    /// Native text is missing and usable OCR evidence was recovered.
    /// </summary>
    OcrOnly,

    /// <summary>
    /// Native and OCR text agree under the conservative V1 comparison rule.
    /// Native text remains selected while OCR is retained as verifying evidence.
    /// </summary>
    Agreement,

    /// <summary>
    /// Healthy native text and OCR disagree. The deterministic V1 policy keeps
    /// native text authoritative but records the divergence explicitly.
    /// </summary>
    HealthyNativePreferred,

    /// <summary>
    /// Native text is suspicious and no usable OCR text is available to verify it.
    /// No authoritative text is selected.
    /// </summary>
    SuspiciousNativeUnverified,

    /// <summary>
    /// Suspicious native text and OCR text disagree. The conflict remains
    /// unresolved rather than being hidden behind a heuristic choice.
    /// </summary>
    Conflict,

    /// <summary>
    /// Native text is missing and OCR produced no usable text.
    /// </summary>
    NoTextRecovered
}
