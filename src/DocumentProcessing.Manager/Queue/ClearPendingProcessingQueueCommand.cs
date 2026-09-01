namespace DocumentProcessing.Manager.Queue;

/// <summary>Removes every unit that has not started processing.</summary>
public sealed record ClearPendingProcessingQueueCommand
{
    public long ExpectedQueueVersion { get; }

    public ClearPendingProcessingQueueCommand(
        long expectedQueueVersion)
    {
        if (expectedQueueVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedQueueVersion),
                expectedQueueVersion,
                "Queue version cannot be negative.");
        }

        ExpectedQueueVersion = expectedQueueVersion;
    }
}
