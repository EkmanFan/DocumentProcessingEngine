using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Submissions;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound read-only port for durable document-submission manifests.
/// </summary>
public interface IDocumentSubmissionReader
{
    /// <summary>
    /// Reads one submission manifest, or returns <see langword="null"/>.
    /// </summary>
    ValueTask<DocumentSubmission?> GetAsync(
        DocumentSubmissionId submissionId,
        CancellationToken cancellationToken = default);
}
