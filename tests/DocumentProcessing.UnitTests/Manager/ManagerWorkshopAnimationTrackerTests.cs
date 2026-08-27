using DocumentProcessing.Manager.Blazor.ManagerApi;
using DocumentProcessing.Manager.Blazor.Workshop;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerWorkshopAnimationTrackerTests
{
    #region Tests

    [Fact]
    public void Observe_CelebratesOnlyNewSuccessfulUnitsAfterInitialProjection()
    {
        var initialSuccess =
            CreateCompletedItem(
                ManagerQueueItemStatus.Succeeded);

        var tracker =
            new ManagerWorkshopAnimationTracker();

        tracker.Observe(
            CreateSnapshot(
                initialSuccess));

        Assert.Equal(
            0,
            tracker.CelebrationSequence);

        tracker.Observe(
            CreateSnapshot(
                initialSuccess,
                CreateCompletedItem(
                    ManagerQueueItemStatus.Failed)));

        Assert.Equal(
            0,
            tracker.CelebrationSequence);

        var newSuccess =
            CreateCompletedItem(
                ManagerQueueItemStatus.Succeeded);

        tracker.Observe(
            CreateSnapshot(
                initialSuccess,
                newSuccess));

        Assert.Equal(
            1,
            tracker.CelebrationSequence);

        tracker.Observe(
            CreateSnapshot(
                initialSuccess,
                newSuccess));

        Assert.Equal(
            1,
            tracker.CelebrationSequence);
    }

    [Fact]
    public void Observe_DoesNotReplayAPreviouslyObservedSuccess()
    {
        var success =
            CreateCompletedItem(
                ManagerQueueItemStatus.Succeeded);

        var tracker =
            new ManagerWorkshopAnimationTracker();

        tracker.Observe(
            CreateSnapshot(
                success));

        tracker.Observe(
            CreateSnapshot());

        tracker.Observe(
            CreateSnapshot(
                success));

        Assert.Equal(
            0,
            tracker.CelebrationSequence);
    }

    #endregion

    #region Methods

    private static ManagerWorkshopSnapshot CreateSnapshot(
        params ManagerWorkItemView[] completedItems) =>
        new(
            ManagerHostState.Running,
            StateVersion:
                1,
            QueueVersion:
                1,
            PendingItems:
                [],
            ActiveItem:
                null,
            completedItems);

    private static ManagerWorkItemView CreateCompletedItem(
        ManagerQueueItemStatus status) =>
        new(
            Guid.NewGuid(),
            "document",
            "document.pdf",
            "Document entier",
            AttemptNumber:
                1,
            status,
            QueuePosition:
                null,
            ResultReference:
                status ==
                ManagerQueueItemStatus.Succeeded
                    ? "manager-result:test"
                    : null,
            FailureMessage:
                status ==
                ManagerQueueItemStatus.Failed
                    ? "Test failure"
                    : null,
            DateTimeOffset.UtcNow);

    #endregion
}
