namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Reports an optimistic-concurrency conflict on the global pending queue.
/// </summary>
public sealed class ProcessingQueueConcurrencyException
    : InvalidOperationException
{
    #region Properties

    /// <summary>
    /// Gets the queue version supplied by the caller.
    /// </summary>
    public long ExpectedVersion { get; }

    /// <summary>
    /// Gets the durable queue version observed by the adapter.
    /// </summary>
    public long ActualVersion { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a queue optimistic-concurrency exception.
    /// </summary>
    public ProcessingQueueConcurrencyException(
        long expectedVersion,
        long actualVersion)
        : base(
            $"Pending queue version conflict: expected {expectedVersion}, actual {actualVersion}.")
    {
        ExpectedVersion =
            expectedVersion;

        ActualVersion =
            actualVersion;
    }

    #endregion
}
