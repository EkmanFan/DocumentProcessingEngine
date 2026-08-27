using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Submissions;

/// <summary>
/// Durable idempotent outcome of registering and enqueueing a submission.
/// </summary>
public sealed record DocumentSubmissionRegistration
{
    #region Properties

    /// <summary>
    /// Gets the durable canonical submission manifest.
    /// </summary>
    public DocumentSubmission Submission { get; }

    /// <summary>
    /// Gets the durable processing units associated with the submission.
    /// </summary>
    public IReadOnlyList<ProcessingUnitId> ProcessingUnitIds { get; }

    /// <summary>
    /// Gets whether this call created the durable registration.
    /// </summary>
    public bool Created { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a durable submission-registration outcome.
    /// </summary>
    public DocumentSubmissionRegistration(
        DocumentSubmission submission,
        IEnumerable<ProcessingUnitId> processingUnitIds,
        bool created)
    {
        ArgumentNullException.ThrowIfNull(
            processingUnitIds);

        var unitIds =
            processingUnitIds.ToArray();

        if (unitIds.Length ==
                0 ||
            unitIds.Any(
                unitId =>
                    unitId.Value ==
                    Guid.Empty) ||
            unitIds.Distinct().Count() !=
                unitIds.Length)
        {
            throw new ArgumentException(
                "Submission registration requires distinct non-empty processing units.",
                nameof(processingUnitIds));
        }

        Submission =
            submission ??
            throw new ArgumentNullException(
                nameof(submission));

        ProcessingUnitIds =
            unitIds;

        Created =
            created;
    }

    #endregion
}
