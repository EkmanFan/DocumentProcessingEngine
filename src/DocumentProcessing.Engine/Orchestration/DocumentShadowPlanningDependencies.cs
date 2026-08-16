using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Normalization;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit opt-in dependencies for true shadow planning.
///
/// The visual source is format-specific and injected through the Core
/// capability boundary. All remaining default components are deterministic and
/// live in the Engine layer.
/// </summary>
public sealed class DocumentShadowPlanningDependencies
{
    public DocumentShadowPlanningDependencies(
        IVisualRasterObservationSource visualRasterObservationSource,
        IDocumentShadowPlanningObserver observer,
        DocumentTextNormalizer? nativeTextNormalizer = null,
        DefaultVisualStructuralEvidenceEnricher? structuralEvidenceEnricher = null,
        GuardedDocumentPageExecutionPlanner? guardedPlanner = null)
    {
        VisualRasterObservationSource =
            visualRasterObservationSource ??
            throw new ArgumentNullException(
                nameof(visualRasterObservationSource));

        Observer =
            observer ??
            throw new ArgumentNullException(
                nameof(observer));

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

    public IDocumentShadowPlanningObserver Observer { get; }

    public DocumentTextNormalizer NativeTextNormalizer { get; }

    public DefaultVisualStructuralEvidenceEnricher StructuralEvidenceEnricher { get; }

    public GuardedDocumentPageExecutionPlanner GuardedPlanner { get; }
}
