using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Requeues technical failures while the configured attempt bound permits it.
/// </summary>
public sealed class BoundedProcessingFailurePolicy
    : IProcessingFailurePolicy
{
    #region Properties

    /// <summary>
    /// Gets the maximum number of processing attempts, including the first.
    /// </summary>
    public int MaximumAttempts { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a bounded technical-failure policy.
    /// </summary>
    public BoundedProcessingFailurePolicy(
        int maximumAttempts)
    {
        if (maximumAttempts <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAttempts),
                maximumAttempts,
                "Maximum processing attempts must be positive.");
        }

        MaximumAttempts =
            maximumAttempts;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public ProcessingFailureDisposition Decide(
        ProcessingWorkItem workItem,
        ProcessingFailure failure)
    {
        ArgumentNullException.ThrowIfNull(
            workItem);

        ArgumentNullException.ThrowIfNull(
            failure);

        return workItem.AttemptNumber <
               MaximumAttempts
            ? ProcessingFailureDisposition.Requeue
            : ProcessingFailureDisposition.Fail;
    }

    #endregion
}
