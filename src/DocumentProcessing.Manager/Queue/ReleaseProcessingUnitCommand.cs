namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Makes one shelved pending processing unit eligible for dispatch.
/// </summary>
public sealed record ReleaseProcessingUnitCommand
{
    #region Properties

    /// <summary>
    /// Gets the processing-unit identity to release.
    /// </summary>
    public ProcessingUnitId UnitId { get; }

    /// <summary>
    /// Gets the expected queue version used for optimistic concurrency.
    /// </summary>
    public long ExpectedQueueVersion { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one processing-unit release command.
    /// </summary>
    public ReleaseProcessingUnitCommand(
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
