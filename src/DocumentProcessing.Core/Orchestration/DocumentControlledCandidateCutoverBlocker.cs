namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Explicit evidence gaps or divergences that prevent guarded authority cutover.
///
/// H.4D.4A intentionally includes blockers that cannot be cleared until a real
/// candidate portable result and provenance graph exist.
/// </summary>
public enum DocumentControlledCandidateCutoverBlocker
{
    TextExecutionUnavailable,

    VisualExecutionUnavailable,

    TextExecutionIncomplete,

    SelectedTextSequenceDivergence,

    TextProjectionDivergence,

    CandidateVisualPersistenceNotCompared,

    PortableOutputNotCompared,

    ProvenanceNotCompared
}
