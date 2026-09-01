namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Makes one terminally failed processing unit eligible for another attempt.
/// </summary>
public sealed record RetryFailedProcessingUnitCommand
{
    #region Properties

    /// <summary>
    /// Gets the processing-unit identity to retry.
    /// </summary>
    public ProcessingUnitId UnitId { get; }

    /// <summary>
    /// Gets the expected queue version used for optimistic concurrency.
    /// </summary>
    public long ExpectedQueueVersion { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one failed processing-unit retry command.
    /// </summary>
    public RetryFailedProcessingUnitCommand(
        ProcessingUnitId unitId,
        long expectedQueueVersion)
    {
        if (unitId.Value ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-unit identifier cannot be empty.",
                nameof(unitId));
        }

        if (expectedQueueVersion <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedQueueVersion),
                expectedQueueVersion,
                "Queue version cannot be negative.");
        }

        UnitId =
            unitId;

        ExpectedQueueVersion =
            expectedQueueVersion;
    }

    #endregion
}
