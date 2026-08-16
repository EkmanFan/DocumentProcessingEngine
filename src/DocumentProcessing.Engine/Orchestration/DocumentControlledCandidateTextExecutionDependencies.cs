using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit opt-in composition for H.4D.1 controlled candidate text execution.
///
/// This increment intentionally contains no raster, layout, OCR, reconciliation,
/// or visual-preservation dependency.
/// </summary>
public sealed class DocumentControlledCandidateTextExecutionDependencies
{
    public DocumentControlledCandidateTextExecutionDependencies(
        IDocumentControlledCandidateTextExecutionObserver observer)
    {
        Observer =
            observer ??
            throw new ArgumentNullException(
                nameof(observer));
    }

    public IDocumentControlledCandidateTextExecutionObserver Observer { get; }
}
