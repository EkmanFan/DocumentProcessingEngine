namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// User-selected options for one document-processing request.
/// </summary>
public sealed record DocumentProcessingRequestOptions
{
    public static DocumentProcessingRequestOptions Default { get; } =
        new();

    public DocumentProcessingRequestOptions(
        bool qualifyUnresolvedVisuals = false)
    {
        QualifyUnresolvedVisuals =
            qualifyUnresolvedVisuals;
    }

    /// <summary>
    /// Gets whether visuals unresolved by deterministic format facts may be
    /// sent to the configured visual-analysis capability for qualification.
    /// </summary>
    public bool QualifyUnresolvedVisuals { get; }
}
