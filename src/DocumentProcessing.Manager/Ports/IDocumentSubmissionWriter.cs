using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Submissions;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound write port for atomic submission registration and queue intake.
/// </summary>
public interface IDocumentSubmissionWriter
{
    /// <summary>
    /// Atomically persists a custody manifest and its initial processing units.
    /// </summary>
    ValueTask<DocumentSubmissionRegistration> RegisterAndEnqueueAsync(
        DocumentSubmission submission,
        IReadOnlyCollection<ProcessingWorkItem> processingUnits,
        CancellationToken cancellationToken = default);
}
