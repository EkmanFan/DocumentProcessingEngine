using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit H.4D.4B.1 composition.
///
/// Candidate processing identities are supplied explicitly because the current
/// narrow layout/reconciliation execution reports do not retain every run-level
/// profile identity needed by canonical provenance projection.
/// </summary>
public sealed class DocumentControlledCandidatePortableProjectionDependencies
{
    public DocumentControlledCandidatePortableProjectionDependencies(
        IDocumentControlledCandidatePortableProjectionObserver observer,
        ProcessingComponentIdentity? rasterizationIdentity = null,
        ProcessingComponentIdentity? layoutAnalysisIdentity = null,
        ProcessingComponentIdentity? reconciliationIdentity = null)
    {
        Observer =
            observer ??
            throw new ArgumentNullException(
                nameof(observer));

        RasterizationIdentity =
            rasterizationIdentity;

        LayoutAnalysisIdentity =
            layoutAnalysisIdentity;

        ReconciliationIdentity =
            reconciliationIdentity;
    }

    public IDocumentControlledCandidatePortableProjectionObserver Observer { get; }

    public ProcessingComponentIdentity? RasterizationIdentity { get; }

    public ProcessingComponentIdentity? LayoutAnalysisIdentity { get; }

    public ProcessingComponentIdentity? ReconciliationIdentity { get; }
}
