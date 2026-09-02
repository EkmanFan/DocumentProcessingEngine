using DocumentProcessing.Manager.Blazor.ManagerApi;

namespace DocumentProcessing.Manager.Blazor.Workshop;

internal sealed record ManagerWorkshopSnapshot(
    ManagerHostState State,
    long StateVersion,
    long QueueVersion,
    IReadOnlyList<ManagerWorkItemView> PendingItems,
    ManagerWorkItemView? ActiveItem,
    IReadOnlyList<ManagerWorkItemView> CompletedItems)
{
    #region Methods Factory

    public static ManagerWorkshopSnapshot Create(
        ManagerStateContract state,
        ManagerQueueContract queue)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        ArgumentNullException.ThrowIfNull(
            queue);

        Validate(
            state,
            queue);

        var items =
            queue.Items
                .Select(
                    ManagerWorkItemView.Create)
                .ToArray();

        var activeItems =
            items
                .Where(
                    item =>
                        item.Status ==
                        ManagerQueueItemStatus.Active)
                .ToArray();

        if (activeItems.Length >
            1)
        {
            throw new InvalidDataException(
                "The sequential Manager cannot expose multiple active processing units.");
        }

        return new ManagerWorkshopSnapshot(
            state.State,
            state.Version,
            queue.Version,
            items
                .Where(
                    item =>
                        item.Status ==
                        ManagerQueueItemStatus.Pending)
                .OrderBy(
                    item =>
                        item.QueuePosition)
                .ToArray(),
            activeItems.SingleOrDefault(),
            items
                .Where(
                    item =>
                        item.Status is
                            ManagerQueueItemStatus.Succeeded or
                            ManagerQueueItemStatus.Failed)
                .OrderByDescending(
                    item =>
                        item.UpdatedAtUtc)
                .ToArray());
    }

    #endregion

    #region Methods Validation

    private static void Validate(
        ManagerStateContract state,
        ManagerQueueContract queue)
    {
        if (!Enum.IsDefined(
                state.State) ||
            state.Version <
                0)
        {
            throw new InvalidDataException(
                "The Manager returned an invalid operating-state snapshot.");
        }

        if (queue.Version <
                0 ||
            queue.Items is null ||
            queue.Items.Any(
                item =>
                    item is null))
        {
            throw new InvalidDataException(
                "The Manager returned an invalid queue snapshot.");
        }

        if (queue.Items
                .Select(
                    item =>
                        item.UnitId)
                .Distinct()
                .Count() !=
            queue.Items.Count)
        {
            throw new InvalidDataException(
                "The Manager returned duplicate processing units.");
        }
    }

    #endregion
}

internal sealed record ManagerWorkItemView(
    Guid UnitId,
    Guid SubmissionId,
    string DocumentTitle,
    string OriginalFileName,
    ManagerWorkItemScopeView Scope,
    int AttemptNumber,
    ManagerQueueItemStatus Status,
    ManagerQueueItemDispatchState DispatchState,
    long? QueuePosition,
    string? ResultReference,
    string? FailureMessage,
    DateTimeOffset UpdatedAtUtc,
    ManagerProcessingProgressView? Progress = null)
{
    #region Methods Factory

    public static ManagerWorkItemView Create(
        ManagerQueueItemContract item)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        if (item.UnitId ==
                Guid.Empty ||
            item.SubmissionId ==
                Guid.Empty ||
            string.IsNullOrWhiteSpace(
                item.OriginalFileName) ||
            item.AttemptNumber <=
                0 ||
            !Enum.IsDefined(
                item.Status) ||
            !Enum.IsDefined(
                item.DispatchState) ||
            (item.Status !=
                ManagerQueueItemStatus.Pending &&
             item.DispatchState !=
                ManagerQueueItemDispatchState.Ready) ||
            (item.Status ==
                ManagerQueueItemStatus.Pending) !=
            item.QueuePosition.HasValue ||
            (item.Status ==
                ManagerQueueItemStatus.Succeeded) !=
            !string.IsNullOrWhiteSpace(
                item.ResultReference) ||
            (item.Status ==
                ManagerQueueItemStatus.Failed &&
             string.IsNullOrWhiteSpace(
                 item.LastFailureMessage)))
        {
            throw new InvalidDataException(
                "The Manager returned an invalid processing unit.");
        }

        var normalizedFileName =
            item.OriginalFileName
                .Trim()
                .Replace(
                    '\\',
                    '/');

        var leafFileName =
            normalizedFileName[
                (normalizedFileName.LastIndexOf(
                    '/') + 1)..];

        if (string.IsNullOrWhiteSpace(
                leafFileName))
        {
            throw new InvalidDataException(
                "The Manager returned an invalid document filename.");
        }

        var documentTitle =
            Path.GetFileNameWithoutExtension(
                leafFileName);

        return new ManagerWorkItemView(
            item.UnitId,
            item.SubmissionId,
            string.IsNullOrWhiteSpace(
                documentTitle)
                ? leafFileName
                : documentTitle,
            leafFileName,
            ManagerWorkItemScopeView.Create(
                item.Scope),
            item.AttemptNumber,
            item.Status,
            item.DispatchState,
            item.QueuePosition,
            item.ResultReference,
            item.LastFailureMessage,
            item.UpdatedAtUtc.ToUniversalTime(),
            item.Progress is null
                ? null
                : ManagerProcessingProgressView.Create(
                    item.Progress));
    }

    #endregion
}

internal sealed record ManagerProcessingProgressView(
    ManagerProcessingProgressStage Stage,
    int CompletionPercentage,
    int? CompletedUnitCount,
    int? TotalUnitCount,
    DateTimeOffset UpdatedAtUtc)
{
    #region Methods Factory

    public static ManagerProcessingProgressView Create(
        ManagerProcessingProgressContract progress)
    {
        ArgumentNullException.ThrowIfNull(
            progress);

        if (!Enum.IsDefined(
                progress.Stage) ||
            progress.CompletionPercentage is <
                0 or >
                100 ||
            progress.CompletedUnitCount.HasValue !=
                progress.TotalUnitCount.HasValue ||
            progress.CompletedUnitCount is <
                0 ||
            progress.TotalUnitCount is <=
                0 ||
            progress.CompletedUnitCount >
                progress.TotalUnitCount)
        {
            throw new InvalidDataException(
                "The Manager returned invalid processing progress.");
        }

        return new ManagerProcessingProgressView(
            progress.Stage,
            progress.CompletionPercentage,
            progress.CompletedUnitCount,
            progress.TotalUnitCount,
            progress.UpdatedAtUtc.ToUniversalTime());
    }

    #endregion
}

internal sealed record ManagerWorkItemScopeView(
    ManagerWorkItemScopeKind Kind,
    int? StartPhysicalPageNumber,
    int? EndPhysicalPageNumber,
    string? Title,
    int? StartContentUnitIndex = null,
    string? StartContentUnitId = null,
    int? EndContentUnitIndex = null,
    string? EndContentUnitId = null)
{
    #region Methods Factory

    public static ManagerWorkItemScopeView Create(
        ManagerScopeContract scope)
    {
        ArgumentNullException.ThrowIfNull(
            scope);

        return scope.Kind switch
        {
            "wholeDocument" =>
                new ManagerWorkItemScopeView(
                    ManagerWorkItemScopeKind.WholeDocument,
                    StartPhysicalPageNumber:
                        null,
                    EndPhysicalPageNumber:
                        null,
                    Title:
                        null),
            "pageRange"
                when scope.StartPhysicalPageNumber is not null &&
                     scope.EndPhysicalPageNumber is not null &&
                     !string.IsNullOrWhiteSpace(
                         scope.Title) =>
                new ManagerWorkItemScopeView(
                    ManagerWorkItemScopeKind.PageRange,
                    scope.StartPhysicalPageNumber,
                    scope.EndPhysicalPageNumber,
                    scope.Title.Trim()),
            "contentUnitRange"
                when scope.StartContentUnitIndex is not null &&
                     scope.StartContentUnitIndex >=
                     0 &&
                     !string.IsNullOrWhiteSpace(
                         scope.StartContentUnitId) &&
                     scope.EndContentUnitIndex is not null &&
                     scope.EndContentUnitIndex >=
                     scope.StartContentUnitIndex &&
                     !string.IsNullOrWhiteSpace(
                         scope.EndContentUnitId) &&
                     !string.IsNullOrWhiteSpace(
                         scope.Title) =>
                new ManagerWorkItemScopeView(
                    ManagerWorkItemScopeKind.ContentUnitRange,
                    StartPhysicalPageNumber:
                        null,
                    EndPhysicalPageNumber:
                        null,
                    Title:
                        scope.Title.Trim(),
                    StartContentUnitIndex:
                        scope.StartContentUnitIndex,
                    StartContentUnitId:
                        scope.StartContentUnitId.Trim(),
                    EndContentUnitIndex:
                        scope.EndContentUnitIndex,
                    EndContentUnitId:
                        scope.EndContentUnitId.Trim()),
            _ =>
                throw new InvalidDataException(
                    "The Manager returned an unsupported processing scope.")
        };
    }

    #endregion
}

internal enum ManagerWorkItemScopeKind
{
    WholeDocument,
    PageRange,
    ContentUnitRange
}
