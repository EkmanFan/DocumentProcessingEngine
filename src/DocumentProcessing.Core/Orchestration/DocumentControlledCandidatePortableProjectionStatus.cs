namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Outcome of H.4D.4B.1 candidate portable-output/provenance projection.
/// </summary>
public enum DocumentControlledCandidatePortableProjectionStatus
{
    /// <summary>
    /// A candidate canonical document and neutral visual sidecars were built.
    /// This does not authorize cutover.
    /// </summary>
    Completed,

    /// <summary>
    /// Candidate execution did not provide the complete page stream required
    /// for projection.
    /// </summary>
    InputUnavailable,

    /// <summary>
    /// An ordinary projection/provenance failure was isolated from authority.
    /// </summary>
    Failed
}
