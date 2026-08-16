using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit opt-in H.4D.4 comparison composition.
///
/// The comparison stage has no execution capability. It only combines already
/// produced planning/text/visual evidence with the authoritative result.
/// </summary>
public sealed class DocumentControlledCandidateComparisonDependencies
{
    public DocumentControlledCandidateComparisonDependencies(
        IDocumentControlledCandidateComparisonObserver observer)
    {
        Observer =
            observer ??
            throw new ArgumentNullException(
                nameof(observer));
    }

    public IDocumentControlledCandidateComparisonObserver Observer { get; }
}
