namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Receives non-authoritative H.4D.4 cross-axis comparison evidence.
///
/// Ordinary observer failures are best-effort. Caller cancellation and
/// <see cref="OutOfMemoryException"/> propagate.
/// </summary>
public interface IDocumentControlledCandidateComparisonObserver
{
    ValueTask ObserveAsync(
        DocumentControlledCandidateComparisonReport report,
        CancellationToken cancellationToken = default);
}
