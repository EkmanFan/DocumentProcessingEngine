using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.History;

/// <summary>Hides one terminal unit from user-facing history without deleting custody.</summary>
public sealed record HideTerminalProcessingUnitCommand
{
    public ProcessingUnitId UnitId { get; }

    public long ExpectedQueueVersion { get; }

    public HideTerminalProcessingUnitCommand(
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
