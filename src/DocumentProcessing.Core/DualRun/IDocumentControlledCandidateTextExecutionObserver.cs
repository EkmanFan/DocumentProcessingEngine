namespace DocumentProcessing.Core.DualRun;
/// <summary>
/// Receives non-authoritative H.4D.1 controlled candidate execution evidence.
///
/// Observer failures are best-effort except caller cancellation and
/// <see cref="OutOfMemoryException"/>, which propagate.
/// </summary>
public interface IDocumentControlledCandidateTextExecutionObserver
{
    ValueTask ObserveAsync(
        DocumentControlledCandidateTextExecutionReport report,
        CancellationToken cancellationToken = default);
}
