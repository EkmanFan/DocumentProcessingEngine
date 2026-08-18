using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit runtime dependencies required when authoritative page execution
/// needs raster/layout-backed hybrid work.
///
/// Legacy OCR recovery/reconciliation remain unchanged. The optional
/// authoritative visual-planning pair enables only the independently proven
/// Healthy + NativeText + resolved meaningful-visual preservation branch.
/// </summary>
public sealed class DocumentHybridExecutionDependencies
{
    public DocumentHybridExecutionDependencies(
        IDocumentRasterizer documentRasterizer,
        MissingNativeHybridPageExecutor missingNativeExecutor,
        NativePresentHybridPageExecutor nativePresentExecutor,
        ProcessingComponentIdentity layoutAnalysisIdentity,
        ProcessingComponentIdentity reconciliationIdentity,
        DocumentAuthoritativeVisualPlanningDependencies?
            authoritativeVisualPlanning = null,
        HealthyNativeVisualPageExecutor?
            healthyNativeVisualExecutor = null)
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

        if ((authoritativeVisualPlanning is null) !=
            (healthyNativeVisualExecutor is null))
        {
            throw new ArgumentException(
                "Authoritative Healthy native visual execution requires both " +
                "planning dependencies and a page executor, or neither.");
        }

        AuthoritativeVisualPlanning =
            authoritativeVisualPlanning;

        HealthyNativeVisualExecutor =
            healthyNativeVisualExecutor;
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

    /// <summary>
    /// Optional authoritative deterministic source-visual evidence chain.
    /// This is distinct from shadow planning: failures propagate.
    /// </summary>
    public DocumentAuthoritativeVisualPlanningDependencies?
        AuthoritativeVisualPlanning { get; }

    /// <summary>
    /// Optional layout-only executor for the narrow proven Healthy-native
    /// meaningful-visual preservation branch.
    /// </summary>
    public HealthyNativeVisualPageExecutor?
        HealthyNativeVisualExecutor { get; }
}
