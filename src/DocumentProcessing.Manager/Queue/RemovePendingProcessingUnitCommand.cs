namespace DocumentProcessing.Manager.Queue;

/// <summary>Removes one unit that has not started processing.</summary>
public sealed record RemovePendingProcessingUnitCommand
{
    public ProcessingUnitId UnitId { get; }

    public long ExpectedQueueVersion { get; }

    public RemovePendingProcessingUnitCommand(
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
}
