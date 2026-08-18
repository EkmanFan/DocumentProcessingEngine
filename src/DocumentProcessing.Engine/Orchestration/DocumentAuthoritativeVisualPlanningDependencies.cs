using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Normalization;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit opt-in dependencies for authoritative source-visual planning.
///
/// Unlike shadow planning, failures from this capability are authoritative and
/// propagate to the caller. The source observer and structural enricher produce
/// evidence only; the guarded planner remains the deterministic policy boundary.
/// </summary>
public sealed class DocumentAuthoritativeVisualPlanningDependencies
{
    public DocumentAuthoritativeVisualPlanningDependencies(
        IVisualRasterObservationSource visualRasterObservationSource,
        DocumentTextNormalizer? nativeTextNormalizer = null,
        DefaultVisualStructuralEvidenceEnricher? structuralEvidenceEnricher = null,
        GuardedDocumentPageExecutionPlanner? guardedPlanner = null)
    {
        VisualRasterObservationSource =
            visualRasterObservationSource ??
            throw new ArgumentNullException(
                nameof(visualRasterObservationSource));

        NativeTextNormalizer =
            nativeTextNormalizer ??
            new DocumentTextNormalizer();

        StructuralEvidenceEnricher =
            structuralEvidenceEnricher ??
            new DefaultVisualStructuralEvidenceEnricher();

        GuardedPlanner =
            guardedPlanner ??
            GuardedDocumentPageExecutionPlanner
                .CreateDefault();
    }

    public IVisualRasterObservationSource VisualRasterObservationSource { get; }

    public DocumentTextNormalizer NativeTextNormalizer { get; }

    public DefaultVisualStructuralEvidenceEnricher StructuralEvidenceEnricher { get; }

    public GuardedDocumentPageExecutionPlanner GuardedPlanner { get; }
}
