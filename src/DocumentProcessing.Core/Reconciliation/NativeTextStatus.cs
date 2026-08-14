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
    Missing,
    Healthy,
    Suspicious
}
