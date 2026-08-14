namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Run-level custody information that the current mature evidence objects do
/// not themselves retain.
///
/// Phase 21's deterministic orchestrator will eventually own construction of
/// this context from the actual configured run.
/// </summary>
public sealed record DocumentProcessingProvenanceContext
{
    public DocumentProcessingProvenanceContext(
        DocumentSourceIdentity source,
        string engineVersion,
        ProcessingComponentIdentity nativeExtraction,
        ProcessingComponentIdentity? rasterization = null,
        ProcessingComponentIdentity? layoutAnalysis = null,
        ProcessingComponentIdentity? reconciliation = null)
    {
        Source =
            source ??
            throw new ArgumentNullException(
                nameof(source));

        if (string.IsNullOrWhiteSpace(
                engineVersion))
        {
            throw new ArgumentException(
                "Engine version cannot be empty.",
                nameof(engineVersion));
        }

        NativeExtraction =
            nativeExtraction ??
            throw new ArgumentNullException(
                nameof(nativeExtraction));

        EngineVersion =
            engineVersion.Trim();

        Rasterization = rasterization;
        LayoutAnalysis = layoutAnalysis;
        Reconciliation = reconciliation;
    }

    public DocumentSourceIdentity Source { get; }

    public string EngineVersion { get; }

    public ProcessingComponentIdentity NativeExtraction { get; }

    public ProcessingComponentIdentity? Rasterization { get; }

    public ProcessingComponentIdentity? LayoutAnalysis { get; }

    public ProcessingComponentIdentity? Reconciliation { get; }
}
