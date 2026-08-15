using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit runtime dependencies required only when a page plan selects a
/// raster/layout/OCR hybrid route.
///
/// This is a small composition object, not a plugin registry. Route selection
/// remains owned by <see cref="DocumentPageProcessingPlanner"/>, while the two
/// concrete page executors retain the already-proven recovery and
/// reconciliation behavior.
/// </summary>
public sealed class DocumentHybridExecutionDependencies
{
    public DocumentHybridExecutionDependencies(
        IDocumentRasterizer documentRasterizer,
        MissingNativeHybridPageExecutor missingNativeExecutor,
        NativePresentHybridPageExecutor nativePresentExecutor,
        ProcessingComponentIdentity layoutAnalysisIdentity,
        ProcessingComponentIdentity reconciliationIdentity)
    {
        DocumentRasterizer =
            documentRasterizer ??
            throw new ArgumentNullException(
                nameof(documentRasterizer));

        MissingNativeExecutor =
            missingNativeExecutor ??
            throw new ArgumentNullException(
                nameof(missingNativeExecutor));

        NativePresentExecutor =
            nativePresentExecutor ??
            throw new ArgumentNullException(
                nameof(nativePresentExecutor));

        LayoutAnalysisIdentity =
            layoutAnalysisIdentity ??
            throw new ArgumentNullException(
                nameof(layoutAnalysisIdentity));

        ReconciliationIdentity =
            reconciliationIdentity ??
            throw new ArgumentNullException(
                nameof(reconciliationIdentity));
    }

    public IDocumentRasterizer DocumentRasterizer { get; }

    public MissingNativeHybridPageExecutor MissingNativeExecutor { get; }

    public NativePresentHybridPageExecutor NativePresentExecutor { get; }

    /// <summary>
    /// Versioned identity of the configured layout-analysis service/model.
    /// The current narrow IPageLayoutAnalyzer boundary intentionally does not
    /// carry a profile identifier, so run-level custody receives it here.
    /// </summary>
    public ProcessingComponentIdentity LayoutAnalysisIdentity { get; }

    /// <summary>
    /// Versioned identity of the deterministic reconciliation policy used by
    /// the configured native-present executor.
    /// </summary>
    public ProcessingComponentIdentity ReconciliationIdentity { get; }
}
