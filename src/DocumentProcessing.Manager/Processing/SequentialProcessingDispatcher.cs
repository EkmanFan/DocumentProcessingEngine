using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Runtime;

namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Claims and executes at most one unit at a time across local callers.
/// </summary>
/// <remarks>
/// Cross-process sequencing requires a global runtime lease in addition to the
/// atomic unit claims and fenced lease tokens enforced by the queue adapter.
/// </remarks>
public sealed class SequentialProcessingDispatcher
{
    #region Variables and Constants

    private readonly IProcessingQueueStore
        _queueStore;

    private readonly IDocumentProcessingExecutor
        _executor;

    private readonly SequentialProcessingDispatcherOptions
        _options;

    private readonly IProcessingFailurePolicy
        _failurePolicy;

    private readonly TimeProvider
        _timeProvider;

    private readonly SemaphoreSlim
        _dispatchGate =
            new(
                initialCount:
                    1,
                maxCount:
                    1);

    #endregion

    #region ctor

    /// <summary>
    /// Creates a lease-backed sequential processing dispatcher.
    /// </summary>
    public SequentialProcessingDispatcher(
        IProcessingQueueStore queueStore,
        IDocumentProcessingExecutor executor,
        SequentialProcessingDispatcherOptions options,
        IProcessingFailurePolicy failurePolicy,
        TimeProvider? timeProvider = null)
    {
        _queueStore =
            queueStore ??
            throw new ArgumentNullException(
                nameof(queueStore));

        _executor =
            executor ??
            throw new ArgumentNullException(
                nameof(executor));

        _options =
            options ??
            throw new ArgumentNullException(
                nameof(options));

        _failurePolicy =
            failurePolicy ??
            throw new ArgumentNullException(
                nameof(failurePolicy));

        _timeProvider =
            timeProvider ??
            TimeProvider.System;
    }

    #endregion

    #region Methods Dispatch

    /// <summary>
    /// Claims and processes the next pending unit, if one exists.
    /// </summary>
    public async ValueTask<ProcessingDispatchOutcome> DispatchNextAsync(
        ManagerRuntimeLease runtimeLease,
        ProcessingInterruptionReason interruptionReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeLease);

        if (!Enum.IsDefined(
                interruptionReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(interruptionReason),
                interruptionReason,
                "Unknown processing-interruption reason.");
        }

        return await DispatchNextAsync(
                runtimeLease,
                () =>
                    interruptionReason,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async ValueTask<ProcessingDispatchOutcome> DispatchNextAsync(
        ManagerRuntimeLease runtimeLease,
        Func<ProcessingInterruptionReason> interruptionReasonProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeLease);

        ArgumentNullException.ThrowIfNull(
            interruptionReasonProvider);

        if (!string.Equals(
                runtimeLease.WorkerId,
                _options.WorkerId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The runtime lease belongs to a different dispatcher worker.",
                nameof(runtimeLease));
        }

        await _dispatchGate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var now =
                _timeProvider.GetUtcNow();

            var lease =
                await _queueStore
                    .ClaimNextAsync(
                        runtimeLease,
                        _options.WorkerId,
                        now,
                        now +
                        _options.LeaseDuration,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (lease is null)
            {
                return new ProcessingDispatchOutcome(
                    ProcessingDispatchStatus.QueueEmpty);
            }

            if (!string.Equals(
                    lease.WorkerId,
                    _options.WorkerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The durable queue returned a lease owned by a different worker.");
            }

            if (lease.RuntimeLeaseToken !=
                runtimeLease.Token)
            {
                throw new InvalidOperationException(
                    "The durable queue returned a unit lease fenced by a different runtime token.");
            }

            return await ExecuteClaimedAsync(
                    lease,
                    interruptionReasonProvider,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _dispatchGate.Release();
        }
    }

    /// <summary>
    /// Requeues work abandoned by crashed or disconnected workers.
    /// </summary>
    public ValueTask<int> RecoverExpiredLeasesAsync(
        CancellationToken cancellationToken = default) =>
        _queueStore.RecoverExpiredLeasesAsync(
            _timeProvider.GetUtcNow(),
            cancellationToken);

    private async ValueTask<ProcessingDispatchOutcome> ExecuteClaimedAsync(
        ProcessingLease lease,
        Func<ProcessingInterruptionReason> interruptionReasonProvider,
        CancellationToken cancellationToken)
    {
        using var executionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        using var heartbeatCancellation =
            new CancellationTokenSource();

        var leaseMonitor =
            new LeaseMonitor();

        var heartbeat =
            RenewLeaseAsync(
                lease,
                leaseMonitor,
                executionCancellation,
                heartbeatCancellation.Token);

        try
        {
            var outcome =
                await _executor
                    .ExecuteAsync(
                        lease.WorkItem,
                        executionCancellation.Token)
                    .ConfigureAwait(false);

            await StopHeartbeatAsync(
                    heartbeatCancellation,
                    heartbeat)
                .ConfigureAwait(false);

            if (leaseMonitor.Lost)
            {
                return LeaseLost(
                    lease,
                    leaseMonitor);
            }

            return outcome switch
            {
                ProcessingExecutionOutcome.Success success =>
                    await CompleteAsync(
                            lease,
                            success)
                        .ConfigureAwait(false),
                ProcessingExecutionOutcome.Failure failure =>
                    await FailAsync(
                            lease,
                            ProcessingFailure.From(
                                failure))
                        .ConfigureAwait(false),
                _ =>
                    throw new InvalidOperationException(
                        $"Unsupported processing outcome '{outcome.GetType().FullName}'.")
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await StopHeartbeatAsync(
                    heartbeatCancellation,
                    heartbeat)
                .ConfigureAwait(false);

            if (leaseMonitor.Lost)
            {
                return LeaseLost(
                    lease,
                    leaseMonitor);
            }

            var interrupted =
                await _queueStore
                    .InterruptAndRequeueAsync(
                        lease,
                        GetInterruptionReason(
                            interruptionReasonProvider),
                        _timeProvider.GetUtcNow(),
                        CancellationToken.None)
                    .ConfigureAwait(false);

            return new ProcessingDispatchOutcome(
                interrupted
                    ? ProcessingDispatchStatus.Interrupted
                    : ProcessingDispatchStatus.LeaseLost,
                lease.WorkItem.UnitId);
        }
        catch (OperationCanceledException)
            when (leaseMonitor.Lost)
        {
            await StopHeartbeatAsync(
                    heartbeatCancellation,
                    heartbeat)
                .ConfigureAwait(false);

            return LeaseLost(
                lease,
                leaseMonitor);
        }
        catch (Exception exception)
        {
            await StopHeartbeatAsync(
                    heartbeatCancellation,
                    heartbeat)
                .ConfigureAwait(false);

            if (leaseMonitor.Lost)
            {
                return LeaseLost(
                    lease,
                    leaseMonitor);
            }

            var failure =
                ProcessingFailure.From(
                    exception);

            return _failurePolicy.Decide(
                       lease.WorkItem,
                       failure) ==
                   ProcessingFailureDisposition.Requeue
                ? await RequeueAfterFailureAsync(
                        lease,
                        failure)
                    .ConfigureAwait(false)
                : await FailAsync(
                        lease,
                        failure)
                    .ConfigureAwait(false);
        }
    }

    #endregion

    #region Methods Lease

    private async Task RenewLeaseAsync(
        ProcessingLease lease,
        LeaseMonitor leaseMonitor,
        CancellationTokenSource executionCancellation,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(
                        _options.LeaseRenewalInterval,
                        _timeProvider,
                        cancellationToken)
                    .ConfigureAwait(false);

                var observedAtUtc =
                    _timeProvider.GetUtcNow();

                var renewed =
                    await _queueStore
                        .RenewLeaseAsync(
                            lease,
                            observedAtUtc,
                            observedAtUtc +
                            _options.LeaseDuration,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (renewed)
                {
                    continue;
                }

                leaseMonitor.MarkLost();
                executionCancellation.Cancel();

                return;
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            leaseMonitor.MarkLost(
                ProcessingFailure.From(
                    exception));

            executionCancellation.Cancel();
        }
    }

    private static async Task StopHeartbeatAsync(
        CancellationTokenSource heartbeatCancellation,
        Task heartbeat)
    {
        heartbeatCancellation.Cancel();

        await heartbeat.ConfigureAwait(false);
    }

    private async ValueTask<ProcessingDispatchOutcome> CompleteAsync(
        ProcessingLease lease,
        ProcessingExecutionOutcome.Success success)
    {
        var completed =
            await _queueStore
                .CompleteAsync(
                    lease,
                    success,
                    _timeProvider.GetUtcNow(),
                    CancellationToken.None)
                .ConfigureAwait(false);

        return new ProcessingDispatchOutcome(
            completed
                ? ProcessingDispatchStatus.Succeeded
                : ProcessingDispatchStatus.LeaseLost,
            lease.WorkItem.UnitId);
    }

    private async ValueTask<ProcessingDispatchOutcome> FailAsync(
        ProcessingLease lease,
        ProcessingFailure failure)
    {
        var failed =
            await _queueStore
                .FailAsync(
                    lease,
                    failure,
                    _timeProvider.GetUtcNow(),
                    CancellationToken.None)
                .ConfigureAwait(false);

        return new ProcessingDispatchOutcome(
            failed
                ? ProcessingDispatchStatus.Failed
                : ProcessingDispatchStatus.LeaseLost,
            lease.WorkItem.UnitId,
            failed
                ? failure
                : null);
    }

    private async ValueTask<ProcessingDispatchOutcome>
        RequeueAfterFailureAsync(
        ProcessingLease lease,
        ProcessingFailure failure)
    {
        var requeued =
            await _queueStore
                .RequeueAfterFailureAsync(
                    lease,
                    failure,
                    _timeProvider.GetUtcNow(),
                    CancellationToken.None)
                .ConfigureAwait(false);

        return new ProcessingDispatchOutcome(
            requeued
                ? ProcessingDispatchStatus.RetryScheduled
                : ProcessingDispatchStatus.LeaseLost,
            lease.WorkItem.UnitId,
            requeued
                ? failure
                : null);
    }

    private static ProcessingDispatchOutcome LeaseLost(
        ProcessingLease lease,
        LeaseMonitor monitor) =>
        new(
            ProcessingDispatchStatus.LeaseLost,
            lease.WorkItem.UnitId,
            monitor.Failure);

    private static ProcessingInterruptionReason GetInterruptionReason(
        Func<ProcessingInterruptionReason> interruptionReasonProvider)
    {
        var reason =
            interruptionReasonProvider();

        return Enum.IsDefined(
            reason)
                ? reason
                : throw new InvalidOperationException(
                    "The interruption-reason provider returned an unknown value.");
    }

    #endregion

    #region Internal Types

    private sealed class LeaseMonitor
    {
        private int _lost;

        public bool Lost =>
            Volatile.Read(
                ref _lost) ==
            1;

        public ProcessingFailure? Failure
        {
            get;
            private set;
        }

        public void MarkLost(
            ProcessingFailure? failure = null)
        {
            Failure =
                failure;

            Volatile.Write(
                ref _lost,
                1);
        }
    }

    #endregion
}
