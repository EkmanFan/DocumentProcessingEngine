namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Stage at which a non-fatal shadow-planning failure occurred.
/// </summary>
public enum DocumentShadowPlanningFailureStage
{
    Capability,
    NativeNormalization,
    RasterObservation,
    StructuralEnrichment,
    CandidatePlanning
}
