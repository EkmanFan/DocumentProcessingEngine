namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Receives non-authoritative H.4D.4B.1 projection evidence.
///
/// Ordinary observer failures are best-effort. Caller cancellation and
/// OutOfMemoryException propagate.
/// </summary>
public interface IDocumentControlledCandidatePortableProjectionObserver
{
    ValueTask ObserveAsync(
        DocumentControlledCandidatePortableProjectionReport report,
        CancellationToken cancellationToken = default);
}
