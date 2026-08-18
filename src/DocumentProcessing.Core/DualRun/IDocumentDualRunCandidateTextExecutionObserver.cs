namespace DocumentProcessing.Core.DualRun;
/// <summary>
/// Receives non-authoritative H.4D.1 Dual Run candidate execution evidence.
///
/// Observer failures are best-effort except caller cancellation and
/// <see cref="OutOfMemoryException"/>, which propagate.
/// </summary>
public interface IDocumentDualRunCandidateTextExecutionObserver
{
    ValueTask ObserveAsync(
        DocumentDualRunCandidateTextExecutionReport report,
        CancellationToken cancellationToken = default);
}
