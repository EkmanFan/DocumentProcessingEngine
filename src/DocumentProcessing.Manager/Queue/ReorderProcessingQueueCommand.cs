namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Replaces the global order of all pending processing units atomically.
/// </summary>
public sealed record ReorderProcessingQueueCommand
{
    #region Properties

    /// <summary>
    /// Gets the complete ordered pending-unit identities.
    /// </summary>
    public IReadOnlyList<ProcessingUnitId> OrderedPendingUnitIds { get; }

    /// <summary>
    /// Gets the expected queue version used for optimistic concurrency.
    /// </summary>
    public long ExpectedQueueVersion { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one atomic queue-reorder command.
    /// </summary>
    public ReorderProcessingQueueCommand(
        IEnumerable<ProcessingUnitId> orderedPendingUnitIds,
        long expectedQueueVersion)
    {
        ArgumentNullException.ThrowIfNull(
            orderedPendingUnitIds);

        if (expectedQueueVersion <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedQueueVersion),
                expectedQueueVersion,
                "Queue version cannot be negative.");
        }

        var snapshot =
            orderedPendingUnitIds.ToArray();

        if (snapshot.Distinct().Count() !=
            snapshot.Length)
        {
            throw new ArgumentException(
                "Queue reorder cannot contain duplicate processing-unit identifiers.",
                nameof(orderedPendingUnitIds));
        }

        if (snapshot.Any(
                unitId =>
                    unitId.Value ==
                    Guid.Empty))
        {
            throw new ArgumentException(
                "Queue reorder cannot contain an empty processing-unit identifier.",
                nameof(orderedPendingUnitIds));
        }

        OrderedPendingUnitIds =
            snapshot;

        ExpectedQueueVersion =
            expectedQueueVersion;
    }

    #endregion
}
