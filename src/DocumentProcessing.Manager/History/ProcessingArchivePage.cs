using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.History;

/// <summary>Represents one bounded archive-search page.</summary>
public sealed record ProcessingArchivePage
{
    #region Properties

    /// <summary>Gets the matching item count before pagination.</summary>
    public long TotalCount { get; }

    /// <summary>Gets the zero-based result offset.</summary>
    public int Offset { get; }

    /// <summary>Gets the requested maximum result count.</summary>
    public int Limit { get; }

    /// <summary>Gets the matching terminal units in requested order.</summary>
    public IReadOnlyList<ProcessingQueueItemSnapshot> Items { get; }

    #endregion

    #region ctor

    /// <summary>Creates one archive-search page.</summary>
    public ProcessingArchivePage(
        long totalCount,
        int offset,
        int limit,
        IReadOnlyList<ProcessingQueueItemSnapshot> items)
    {
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalCount));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset));
        }

        if (limit is < 1 or > ProcessingArchiveQuery.MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit));
        }

        ArgumentNullException.ThrowIfNull(
            items);

        if (items.Any(
                item =>
                    item is null ||
                    item.Status is not (
                        ProcessingUnitStatus.Succeeded or
                        ProcessingUnitStatus.Failed)))
        {
            throw new ArgumentException(
                "Archive page can contain only non-null terminal units.",
                nameof(items));
        }

        var copied =
            items.ToArray();

        if (copied.Length > limit ||
            copied.LongLength >
                totalCount)
        {
            throw new ArgumentException(
                "Archive page items exceed its declared bounds.",
                nameof(items));
        }

        TotalCount =
            totalCount;

        Offset =
            offset;

        Limit =
            limit;

        Items =
            copied;
    }

    #endregion
}
