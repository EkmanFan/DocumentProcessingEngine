using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.DualRun.InProcess;

/// <summary>
/// Explicit opt-in dependencies for in-process Dual Run planning.
///
/// The visual source is format-specific and injected through the Core
/// capability boundary. All remaining default components are deterministic and
/// live in the Engine layer.
/// </summary>
public sealed class DocumentDualRunPlanningDependencies
{
    #region Variables and Constants

    #endregion

    #region Properties

    public IVisualRasterObservationSource VisualRasterObservationSource { get; }

    public IDocumentDualRunPlanningObserver Observer { get; }

    public DocumentTextNormalizer NativeTextNormalizer { get; }

    public DefaultVisualStructuralEvidenceEnricher StructuralEvidenceEnricher { get; }

    public GuardedDocumentPageExecutionPlanner GuardedPlanner { get; }

    #endregion

    #region ctor

    public DocumentDualRunPlanningDependencies(
        IVisualRasterObservationSource visualRasterObservationSource,
        IDocumentDualRunPlanningObserver observer,
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

    #endregion

    #region Methods

    #endregion
}
