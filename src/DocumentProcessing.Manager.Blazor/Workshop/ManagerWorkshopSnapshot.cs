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
    string DocumentTitle,
    string OriginalFileName,
    string ScopeLabel,
    int AttemptNumber,
    ManagerQueueItemStatus Status,
    long? QueuePosition,
    string? ResultReference,
    string? FailureMessage,
    DateTimeOffset UpdatedAtUtc)
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
            string.IsNullOrWhiteSpace(
                documentTitle)
                ? leafFileName
                : documentTitle,
            leafFileName,
            FormatScope(
                item.Scope),
            item.AttemptNumber,
            item.Status,
            item.QueuePosition,
            item.ResultReference,
            item.LastFailureMessage,
            item.UpdatedAtUtc.ToUniversalTime());
    }

    private static string FormatScope(
        ManagerScopeContract scope)
    {
        ArgumentNullException.ThrowIfNull(
            scope);

        return scope.Kind switch
        {
            "wholeDocument" =>
                "Document entier",
            "pageRange"
                when scope.StartPhysicalPageNumber is not null &&
                     scope.EndPhysicalPageNumber is not null &&
                     !string.IsNullOrWhiteSpace(
                         scope.Title) =>
                $"{scope.Title.Trim()} · pages {scope.StartPhysicalPageNumber}–{scope.EndPhysicalPageNumber}",
            _ =>
                throw new InvalidDataException(
                    "The Manager returned an unsupported processing scope.")
        };
    }

    #endregion
}
