namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Temporarily removes one ready pending processing unit from dispatch eligibility.
/// </summary>
public sealed record ShelveProcessingUnitCommand
{
    #region Properties

    /// <summary>Gets the processing-unit identity to shelve.</summary>
    public ProcessingUnitId UnitId { get; }

    /// <summary>Gets the expected queue version used for optimistic concurrency.</summary>
    public long ExpectedQueueVersion { get; }

    #endregion

    #region ctor

    /// <summary>Creates one processing-unit shelving command.</summary>
    public ShelveProcessingUnitCommand(
        ProcessingUnitId unitId,
        long expectedQueueVersion)
    {
        if (unitId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-unit identifier cannot be empty.",
                nameof(unitId));
        }

        if (expectedQueueVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedQueueVersion),
                expectedQueueVersion,
                "Queue version cannot be negative.");
        }

        UnitId = unitId;
        ExpectedQueueVersion = expectedQueueVersion;
    }

    #endregion
}
