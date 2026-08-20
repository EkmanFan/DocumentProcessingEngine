namespace DocumentProcessing.Engine.DualRun.Dispatch;

/// <summary>
/// Bounded in-memory handoff between future producer logic and the future
/// worker supervisor.
///
/// This checkpoint intentionally has no background consumer and launches no
/// process. TryDispatch and TryTake are both non-blocking.
/// </summary>
public sealed class DocumentDualRunBoundedJobDispatcher
    : IDocumentDualRunJobDispatcher,
      IAsyncDisposable
{
    #region Variables and Constants

    private readonly object _gate =
        new();

    private readonly Queue<DocumentDualRunPreparedJob> _queue =
        new();

    private readonly int _capacity;

    private bool _stopped;

    #endregion

    #region Properties

    public int Capacity =>
        _capacity;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    public bool IsStopped
    {
        get
        {
            lock (_gate)
            {
                return _stopped;
            }
        }
    }

    #endregion

    #region ctor

    public DocumentDualRunBoundedJobDispatcher(
        int capacity)
    {
        if (capacity <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity));
        }

        _capacity =
            capacity;
    }

    #endregion

    #region Methods Producer

    public DocumentDualRunDispatchOutcome TryDispatch(
        DocumentDualRunPreparedJob job)
    {
        ArgumentNullException.ThrowIfNull(
            job);

        lock (_gate)
        {
            if (_stopped)
            {
                return DocumentDualRunDispatchOutcome
                    .Stopped;
            }

            if (_queue.Count >=
                _capacity)
            {
                return DocumentDualRunDispatchOutcome
                    .QueueFull;
            }

            _queue.Enqueue(
                job);

            return DocumentDualRunDispatchOutcome
                .Enqueued;
        }
    }

    #endregion

    #region Methods Consumer

    /// <summary>
    /// Future supervisor-side non-blocking dequeue.
    ///
    /// A successful dequeue transfers ownership from the dispatcher to the
    /// caller.
    /// </summary>
    public bool TryTake(
        out DocumentDualRunPreparedJob? job)
    {
        lock (_gate)
        {
            if (_queue.Count ==
                0)
            {
                job =
                    null;

                return false;
            }

            job =
                _queue.Dequeue();

            return true;
        }
    }

    #endregion

    #region Methods Lifecycle

    public async ValueTask DisposeAsync()
    {
        DocumentDualRunPreparedJob[] pending;

        lock (_gate)
        {
            if (_stopped &&
                _queue.Count ==
                0)
            {
                return;
            }

            _stopped =
                true;

            pending =
                _queue.ToArray();

            _queue.Clear();
        }

        foreach (var job in
                 pending)
        {
            await job
                .DisposeAsync()
                .ConfigureAwait(false);
        }
    }

    #endregion
}
