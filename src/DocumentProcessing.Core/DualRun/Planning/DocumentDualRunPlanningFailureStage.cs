namespace DocumentProcessing.Core.DualRun;
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
