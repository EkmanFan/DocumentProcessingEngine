namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Stage at which a non-fatal Dual Run planning failure occurred.
/// </summary>
public enum DocumentDualRunPlanningFailureStage
{
    Capability,
    NativeNormalization,
    RasterObservation,
    StructuralEnrichment,
    CandidatePlanning
}
