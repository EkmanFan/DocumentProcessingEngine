using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class RetryFailedProcessingUnitCommandTests
{
    [Fact]
    public void Constructor_RejectsEmptyUnitId()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new RetryFailedProcessingUnitCommand(
                    new ProcessingUnitId(
                        Guid.Empty),
                    expectedQueueVersion:
                        0));
    }

    [Fact]
    public void Constructor_RejectsNegativeQueueVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new RetryFailedProcessingUnitCommand(
                    ProcessingUnitId.New(),
                    expectedQueueVersion:
                        -1));
    }
}
