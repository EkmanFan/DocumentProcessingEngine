namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Closed V1 execution routes that the ingestion processor knows how to execute.
///
/// This enum describes supported engine capabilities. It is deliberately not an
/// extensibility/plugin registry. Policy implementations may choose differently
/// among these routes without changing the processor that consumes the plan.
/// Adding a genuinely new processing capability may require extending this enum
/// and the execution code.
/// </summary>
public enum PageProcessingRoute
{
    /// <summary>
    /// Trust the page's healthy native PDF text and do not invoke raster,
    /// layout, OCR or native/OCR reconciliation.
    /// </summary>
    NativeOnly,

    /// <summary>
    /// Rasterize and analyze layout, then recover OCR-authorized textual
    /// regions because authoritative native text is missing.
    ///
    /// Region-level visual/deferred handling remains owned by the existing
    /// deterministic layout treatment policy.
    /// </summary>
    LayoutWithTargetedOcrRecovery,

    /// <summary>
    /// Rasterize and analyze layout, then obtain OCR as secondary evidence and
    /// reconcile comparable native/OCR text deterministically.
    ///
    /// Region-level visual/deferred handling remains owned by the existing
    /// deterministic layout treatment policy.
    /// </summary>
    LayoutWithTargetedOcrReconciliation
}
