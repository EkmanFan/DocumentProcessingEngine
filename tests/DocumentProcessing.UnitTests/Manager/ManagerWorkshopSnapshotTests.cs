using DocumentProcessing.Manager.Blazor.ManagerApi;
using DocumentProcessing.Manager.Blazor.Workshop;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerWorkshopSnapshotTests
{
    #region Tests

    [Fact]
    public void Create_ProjectsPendingActiveAndCompletedWorkForTheWorkshop()
    {
        var now =
            DateTimeOffset.UtcNow;

        var pendingLater =
            CreateItem(
                "allison.pdf",
                ManagerQueueItemStatus.Pending,
                dispatchState:
                    ManagerQueueItemDispatchState.Shelved,
                queuePosition:
                    20,
                updatedAtUtc:
                    now);

        var completedEarlier =
            CreateItem(
                "decretis.pdf",
                ManagerQueueItemStatus.Succeeded,
                resultReference:
                    "manager-result:decretis",
                updatedAtUtc:
                    now.AddMinutes(
                        -5));

        var active =
            CreateItem(
                "bauckham.pdf",
                ManagerQueueItemStatus.Active,
                updatedAtUtc:
                    now.AddMinutes(
                        -2));

        var pendingFirst =
            CreateItem(
                "habermas.pdf",
                ManagerQueueItemStatus.Pending,
                queuePosition:
                    10,
                updatedAtUtc:
                    now.AddMinutes(
                        -3));

        var completedLatest =
            CreateItem(
                "ehrman.pdf",
                ManagerQueueItemStatus.Failed,
                failureMessage:
                    "Unsupported source",
                updatedAtUtc:
                    now);

        var workshop =
            ManagerWorkshopSnapshot.Create(
                new ManagerStateContract(
                    ManagerHostState.Running,
                    Version:
                        7),
                new ManagerQueueContract(
                    Version:
                        11,
                    [
                        pendingLater,
                        completedEarlier,
                        active,
                        pendingFirst,
                        completedLatest
                    ]));

        Assert.Equal(
            ManagerHostState.Running,
            workshop.State);

        Assert.Equal(
            ["habermas", "allison"],
            workshop.PendingItems
                .Select(
                    item =>
                        item.DocumentTitle));

        Assert.Equal(
            ManagerQueueItemDispatchState.Shelved,
            workshop.PendingItems[1].DispatchState);

        Assert.Equal(
            "bauckham",
            workshop.ActiveItem?.DocumentTitle);

        Assert.All(
            workshop.PendingItems,
            item =>
                Assert.Equal(
                    ManagerWorkItemScopeKind.WholeDocument,
                    item.Scope.Kind));

        Assert.Equal(
            ["ehrman", "decretis"],
            workshop.CompletedItems
                .Select(
                    item =>
                        item.DocumentTitle));
    }

    [Fact]
    public void Create_RejectsMultipleActiveUnits()
    {
        var queue =
            new ManagerQueueContract(
                Version:
                    1,
                [
                    CreateItem(
                        "first.pdf",
                        ManagerQueueItemStatus.Active),
                    CreateItem(
                        "second.pdf",
                        ManagerQueueItemStatus.Active)
                ]);

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    ManagerWorkshopSnapshot.Create(
                        new ManagerStateContract(
                            ManagerHostState.Running,
                            Version:
                                1),
                        queue));

        Assert.Contains(
            "multiple active",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_PreservesPageRangeSemanticsWithoutPresentationText()
    {
        var item =
            CreateItem(
                "bauckham.pdf",
                ManagerQueueItemStatus.Pending,
                queuePosition:
                    1) with
            {
                Scope =
                    new ManagerScopeContract(
                        "pageRange",
                        StartPhysicalPageNumber:
                            281,
                        EndPhysicalPageNumber:
                            326,
                        Title:
                            "Chapter 7")
            };

        var workshop =
            ManagerWorkshopSnapshot.Create(
                new ManagerStateContract(
                    ManagerHostState.Running,
                    Version:
                        1),
                new ManagerQueueContract(
                    Version:
                        1,
                    [item]));

        var scope =
            Assert.Single(
                    workshop.PendingItems)
                .Scope;

        Assert.Equal(
            ManagerWorkItemScopeKind.PageRange,
            scope.Kind);

        Assert.Equal(
            281,
            scope.StartPhysicalPageNumber);

        Assert.Equal(
            326,
            scope.EndPhysicalPageNumber);

        Assert.Equal(
            "Chapter 7",
            scope.Title);
    }

    #endregion

    #region Methods

    private static ManagerQueueItemContract CreateItem(
        string originalFileName,
        ManagerQueueItemStatus status,
        ManagerQueueItemDispatchState dispatchState =
            ManagerQueueItemDispatchState.Ready,
        long? queuePosition = null,
        string? resultReference = null,
        string? failureMessage = null,
        DateTimeOffset? updatedAtUtc = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            originalFileName,
            new ManagerScopeContract(
                "wholeDocument",
                StartPhysicalPageNumber:
                    null,
                EndPhysicalPageNumber:
                    null,
                Title:
                    null),
            AttemptNumber:
                1,
            status,
            dispatchState,
            queuePosition,
            resultReference,
            LastFailureCode:
                failureMessage is null
                    ? null
                    : "manager.failure",
            failureMessage,
            updatedAtUtc ??
            DateTimeOffset.UtcNow);

    #endregion
}
