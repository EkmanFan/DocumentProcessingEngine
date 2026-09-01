using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Custody;

/// <summary>Permanently removes one terminal unit and its unshared custody chain.</summary>
public sealed record PurgeTerminalProcessingUnitCommand
{
    public ProcessingUnitId UnitId { get; }

    public long ExpectedQueueVersion { get; }

    public PurgeTerminalProcessingUnitCommand(
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
