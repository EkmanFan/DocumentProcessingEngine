using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Submissions;

/// <summary>
/// Reports reuse of a submission identity for different immutable content or metadata.
/// </summary>
public sealed class DocumentSubmissionConflictException
    : InvalidOperationException
{
    #region Properties

    /// <summary>
    /// Gets the conflicting submission identity.
    /// </summary>
    public DocumentSubmissionId SubmissionId { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a document-submission idempotency conflict.
    /// </summary>
    public DocumentSubmissionConflictException(
        DocumentSubmissionId submissionId)
        : base(
            $"Submission identifier '{submissionId}' is already registered for different source custody metadata.")
    {
        SubmissionId =
            submissionId;
    }

    #endregion
}
