using DocumentProcessing.Manager.Blazor.ManagerApi;

namespace DocumentProcessing.Manager.Blazor.Workshop;

internal sealed record ManagerArchivePage(
    long TotalCount,
    int Offset,
    int Limit,
    IReadOnlyList<ManagerWorkItemView> Items)
{
    #region Methods Factory

    public static ManagerArchivePage Create(
        ManagerArchiveContract contract)
    {
        ArgumentNullException.ThrowIfNull(
            contract);

        if (contract.TotalCount < 0 ||
            contract.Offset < 0 ||
            contract.Limit is < 1 or > 200 ||
            contract.Items is null ||
            contract.Items.Count > contract.Limit)
        {
            throw new InvalidDataException(
                "The Manager returned an invalid archive page.");
        }

        var items =
            contract.Items
                .Select(
                    ManagerWorkItemView.Create)
                .ToArray();

        if (items.Any(
                item =>
                    item.Status is not (
                        ManagerQueueItemStatus.Succeeded or
                        ManagerQueueItemStatus.Failed)))
        {
            throw new InvalidDataException(
                "The Manager returned a non-terminal archived processing unit.");
        }

        return new ManagerArchivePage(
            contract.TotalCount,
            contract.Offset,
            contract.Limit,
            items);
    }

    #endregion
}
