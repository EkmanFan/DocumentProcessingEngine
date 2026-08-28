using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Submissions;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound write port for atomic submission registration and processing intake.
/// </summary>
public interface IDocumentSubmissionWriter
{
    /// <summary>
    /// Atomically persists a custody manifest and its initial processing intake.
    /// </summary>
    ValueTask<DocumentSubmissionRegistration> RegisterAsync(
        DocumentSubmission submission,
        IReadOnlyCollection<ProcessingUnitIntake> processingUnits,
        CancellationToken cancellationToken = default);
}
