namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Consistent versioned snapshot of the durable processing queue.
/// </summary>
public sealed record ProcessingQueueSnapshot
{
    #region Properties

    /// <summary>
    /// Gets the optimistic-concurrency queue version.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets all durable processing units in consumer display order.
    /// </summary>
    public IReadOnlyList<ProcessingQueueItemSnapshot> Items { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one consistent queue snapshot.
    /// </summary>
    public ProcessingQueueSnapshot(
        long version,
        IReadOnlyList<ProcessingQueueItemSnapshot> items)
    {
        if (version <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "Queue version cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(
            items);

        if (items.Any(
                item =>
                    item is null))
        {
            throw new ArgumentException(
                "Queue snapshot cannot contain null processing units.",
                nameof(items));
        }

        var copied =
            items.ToArray();

        if (copied
                .Select(
                    item =>
                        item.WorkItem.UnitId)
                .Distinct()
                .Count() !=
            copied.Length)
        {
            throw new ArgumentException(
                "Queue snapshot cannot contain duplicate processing units.",
                nameof(items));
        }

        Version =
            version;

        Items =
            copied;
    }

    #endregion
}
