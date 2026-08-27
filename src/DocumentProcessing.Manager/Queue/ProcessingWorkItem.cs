namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Immutable atomic work item claimed from the global processing queue.
/// </summary>
public sealed record ProcessingWorkItem
{
    #region Properties

    /// <summary>
    /// Gets the processing-unit identity.
    /// </summary>
    public ProcessingUnitId UnitId { get; }

    /// <summary>
    /// Gets the owning document-submission identity.
    /// </summary>
    public DocumentSubmissionId SubmissionId { get; }

    /// <summary>
    /// Gets the immutable source scope.
    /// </summary>
    public ProcessingUnitScope Scope { get; }

    /// <summary>
    /// Gets the one-based attempt number.
    /// </summary>
    public int AttemptNumber { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one atomic work item.
    /// </summary>
    public ProcessingWorkItem(
        ProcessingUnitId unitId,
        DocumentSubmissionId submissionId,
        ProcessingUnitScope scope,
        int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(
            scope);

        if (attemptNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptNumber),
                attemptNumber,
                "Attempt number must be positive.");
        }

        UnitId =
            unitId;

        SubmissionId =
            submissionId;

        Scope =
            scope;

        AttemptNumber =
            attemptNumber;
    }

    #endregion
}
