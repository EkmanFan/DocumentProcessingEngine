namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Receives non-authoritative H.4D.3B controlled candidate visual-execution
/// evidence.
///
/// Observer failures are best-effort except caller cancellation and
/// <see cref="OutOfMemoryException"/>, which propagate.
/// </summary>
public interface IDocumentControlledCandidateVisualExecutionObserver
{
    ValueTask ObserveAsync(
        DocumentControlledCandidateVisualExecutionReport report,
        CancellationToken cancellationToken = default);
}
