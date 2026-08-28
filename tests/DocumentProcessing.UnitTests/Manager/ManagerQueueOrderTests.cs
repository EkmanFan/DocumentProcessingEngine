using DocumentProcessing.Manager.Blazor.Workshop;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerQueueOrderTests
{
    #region Tests

    [Fact]
    public void MoveToTargetPosition_MovesEarlierUnitToLaterPosition()
    {
        var first =
            Guid.NewGuid();

        var second =
            Guid.NewGuid();

        var third =
            Guid.NewGuid();

        Assert.Equal(
            [second, third, first],
            ManagerQueueOrder.MoveToTargetPosition(
                [first, second, third],
                first,
                third));
    }

    [Fact]
    public void MoveToTargetPosition_MovesLaterUnitToEarlierPosition()
    {
        var first =
            Guid.NewGuid();

        var second =
            Guid.NewGuid();

        var third =
            Guid.NewGuid();

        Assert.Equal(
            [third, first, second],
            ManagerQueueOrder.MoveToTargetPosition(
                [first, second, third],
                third,
                first));
    }

    [Fact]
    public void MoveToTargetPosition_RejectsUnitOutsidePendingOrder()
    {
        Assert.Throws<ArgumentException>(
            () =>
                ManagerQueueOrder.MoveToTargetPosition(
                    [Guid.NewGuid()],
                    Guid.NewGuid(),
                    Guid.NewGuid()));
    }

    #endregion
}
