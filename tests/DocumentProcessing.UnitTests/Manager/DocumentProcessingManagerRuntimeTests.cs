using System.Collections.Concurrent;
using DocumentProcessing.Manager.Control;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Runtime;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class DocumentProcessingManagerRuntimeTests
{
    #region Tests

    [Fact]
    public async Task Pause_DrainsActiveUnitAndResumeStartsNextUnit()
    {
        var stateStore =
            new RecordingStateStore(
                ManagerOperatingState.Running);

        var queueStore =
            new RecordingQueueStore(
                CreateWorkItem(),
                CreateWorkItem());

        var firstEntered =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseFirst =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var secondEntered =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var executionNumber =
            0;

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    var current =
                        Interlocked.Increment(
                            ref executionNumber);

                    if (current ==
                        1)
                    {
                        firstEntered.TrySetResult();

                        await releaseFirst.Task
                            .WaitAsync(
                                cancellationToken);
                    }
                    else
                    {
                        secondEntered.TrySetResult();
                    }

                    return new ProcessingExecutionOutcome.Success(
                        $"result-{current}");
                });

        var runtime =
            CreateRuntime(
                "worker-one",
                stateStore,
                new RecordingRuntimeLeaseStore(),
                queueStore,
                executor);

        using var hostStopping =
            new CancellationTokenSource();

        var running =
            runtime.RunAsync(
                hostStopping.Token);

        await firstEntered.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        var paused =
            await runtime.ExecuteAsync(
                new PauseManagerCommand());

        Assert.Equal(
            ManagerOperatingState.Paused,
            paused.Snapshot.State);

        releaseFirst.TrySetResult();

        await queueStore.FirstCompletionObserved.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        var unexpectedSecond =
            await Task.WhenAny(
                secondEntered.Task,
                Task.Delay(
                    TimeSpan.FromMilliseconds(
                        150)));

        Assert.NotSame(
            secondEntered.Task,
            unexpectedSecond);

        var resumed =
            await runtime.ExecuteAsync(
                new ResumeManagerCommand());

        Assert.Equal(
            ManagerOperatingState.Running,
            resumed.Snapshot.State);

        await secondEntered.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        await queueStore.AllWorkCompleted.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        await runtime.ExecuteAsync(
            new StopManagerCommand());

        hostStopping.Cancel();

        await running.WaitAsync(
            TimeSpan.FromSeconds(
                5));

        Assert.Equal(
            2,
            queueStore.CompletedCount);

        Assert.True(
            queueStore.RecoveryObserved.Task.IsCompleted);
    }

    [Fact]
    public async Task Stop_CancelsAndRequeuesActiveUnitBeforeReturning()
    {
        var stateStore =
            new RecordingStateStore(
                ManagerOperatingState.Running);

        var queueStore =
            new RecordingQueueStore(
                CreateWorkItem());

        var entered =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    entered.TrySetResult();

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "Unreachable.");
                });

        var runtime =
            CreateRuntime(
                "worker-one",
                stateStore,
                new RecordingRuntimeLeaseStore(),
                queueStore,
                executor);

        using var hostStopping =
            new CancellationTokenSource();

        var running =
            runtime.RunAsync(
                hostStopping.Token);

        await entered.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        var stopped =
            await runtime.ExecuteAsync(
                new StopManagerCommand());

        Assert.Equal(
            ManagerOperatingState.Stopped,
            stopped.Snapshot.State);

        Assert.True(
            queueStore.InterruptionObserved.Task.IsCompleted);

        Assert.Equal(
            ProcessingInterruptionReason.ManagerStop,
            queueStore.LastInterruptionReason);

        Assert.Equal(
            1,
            queueStore.InterruptedCount);

        Assert.Equal(
            0,
            queueStore.CompletedCount);

        hostStopping.Cancel();

        await running.WaitAsync(
            TimeSpan.FromSeconds(
                5));
    }

    [Fact]
    public async Task Stop_DuringClaimLetsClaimLinearizeThenRequeuesBeforeReturning()
    {
        var stateStore =
            new RecordingStateStore(
                ManagerOperatingState.Running);

        var claimEntered =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseClaim =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var queueStore =
            new RecordingQueueStore(
                claimEntered,
                releaseClaim,
                CreateWorkItem());

        var executorCalled =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var executor =
            new DelegateExecutor(
                (_, cancellationToken) =>
                {
                    executorCalled.TrySetResult();
                    cancellationToken.ThrowIfCancellationRequested();

                    return Task.FromResult<ProcessingExecutionOutcome>(
                        new ProcessingExecutionOutcome.Success(
                            "unreachable-result"));
                });

        var runtime =
            CreateRuntime(
                "worker-one",
                stateStore,
                new RecordingRuntimeLeaseStore(),
                queueStore,
                executor);

        using var hostStopping =
            new CancellationTokenSource();

        var running =
            runtime.RunAsync(
                hostStopping.Token);

        await claimEntered.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        var stopping =
            runtime.ExecuteAsync(
                    new StopManagerCommand())
                .AsTask();

        var prematureStop =
            await Task.WhenAny(
                stopping,
                Task.Delay(
                    TimeSpan.FromMilliseconds(
                        100)));

        Assert.NotSame(
            stopping,
            prematureStop);

        releaseClaim.TrySetResult();

        var stopped =
            await stopping.WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        Assert.Equal(
            ManagerOperatingState.Stopped,
            stopped.Snapshot.State);

        await executorCalled.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        Assert.True(
            queueStore.InterruptionObserved.Task.IsCompleted);

        Assert.Equal(
            ProcessingInterruptionReason.ManagerStop,
            queueStore.LastInterruptionReason);

        Assert.False(
            running.IsCompleted);

        hostStopping.Cancel();

        await running.WaitAsync(
            TimeSpan.FromSeconds(
                5));
    }

    [Fact]
    public async Task SharedRuntimeLease_PreventsConcurrentProcessingAcrossInstances()
    {
        var stateStore =
            new RecordingStateStore(
                ManagerOperatingState.Running);

        var runtimeLeaseStore =
            new RecordingRuntimeLeaseStore();

        var queueStore =
            new RecordingQueueStore(
                CreateWorkItem(),
                CreateWorkItem());

        var firstEntered =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseExecutions =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var active =
            0;

        var maximumActive =
            0;

        var executionCount =
            0;

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    var currentActive =
                        Interlocked.Increment(
                            ref active);

                    Interlocked.Increment(
                        ref executionCount);

                    maximumActive =
                        Math.Max(
                            maximumActive,
                            currentActive);

                    firstEntered.TrySetResult();

                    try
                    {
                        await releaseExecutions.Task
                            .WaitAsync(
                                cancellationToken);
                    }
                    finally
                    {
                        Interlocked.Decrement(
                            ref active);
                    }

                    return new ProcessingExecutionOutcome.Success(
                        "result");
                });

        var firstRuntime =
            CreateRuntime(
                "worker-one",
                stateStore,
                runtimeLeaseStore,
                queueStore,
                executor);

        var secondRuntime =
            CreateRuntime(
                "worker-two",
                stateStore,
                runtimeLeaseStore,
                queueStore,
                executor);

        using var hostStopping =
            new CancellationTokenSource();

        var firstRunning =
            firstRuntime.RunAsync(
                hostStopping.Token);

        var secondRunning =
            secondRuntime.RunAsync(
                hostStopping.Token);

        await firstEntered.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                150));

        Assert.Equal(
            1,
            Volatile.Read(
                ref executionCount));

        Assert.Equal(
            1,
            Volatile.Read(
                ref maximumActive));

        Assert.Equal(
            1,
            runtimeLeaseStore.SuccessfulAcquisitionCount);

        releaseExecutions.TrySetResult();

        await queueStore.AllWorkCompleted.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        await firstRuntime.ExecuteAsync(
            new StopManagerCommand());

        hostStopping.Cancel();

        await Task.WhenAll(
                firstRunning,
                secondRunning)
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        Assert.Equal(
            2,
            executionCount);

        Assert.Equal(
            1,
            maximumActive);
    }

    [Fact]
    public async Task RuntimeLeaseLoss_CancelsAndRequeuesActiveUnit()
    {
        var stateStore =
            new RecordingStateStore(
                ManagerOperatingState.Running);

        var runtimeLeaseStore =
            new RecordingRuntimeLeaseStore(
                rejectRenewal:
                    true,
                rejectSubsequentAcquisitions:
                    true);

        var queueStore =
            new RecordingQueueStore(
                CreateWorkItem());

        var entered =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    entered.TrySetResult();

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "Unreachable.");
                });

        var runtime =
            CreateRuntime(
                "worker-one",
                stateStore,
                runtimeLeaseStore,
                queueStore,
                executor,
                fastRuntimeLease:
                    true);

        using var hostStopping =
            new CancellationTokenSource();

        var running =
            runtime.RunAsync(
                hostStopping.Token);

        await entered.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        await queueStore.InterruptionObserved.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        Assert.Equal(
            ProcessingInterruptionReason.RuntimeLeaseLost,
            queueStore.LastInterruptionReason);

        hostStopping.Cancel();

        await running.WaitAsync(
            TimeSpan.FromSeconds(
                5));
    }

    #endregion

    #region Helpers

    private static DocumentProcessingManagerRuntime CreateRuntime(
        string workerId,
        IManagerStateStore stateStore,
        IManagerRuntimeLeaseStore runtimeLeaseStore,
        IProcessingQueueStore queueStore,
        IDocumentProcessingExecutor executor,
        bool fastRuntimeLease =
            false)
    {
        var dispatcher =
            new SequentialProcessingDispatcher(
                queueStore,
                executor,
                new SequentialProcessingDispatcherOptions(
                    workerId,
                    leaseDuration:
                        TimeSpan.FromMinutes(
                            10),
                    leaseRenewalInterval:
                        TimeSpan.FromMinutes(
                            5)),
                new BoundedProcessingFailurePolicy(
                    maximumAttempts:
                        1));

        return new DocumentProcessingManagerRuntime(
            stateStore,
            runtimeLeaseStore,
            dispatcher,
            new DocumentProcessingManagerRuntimeOptions(
                workerId,
                runtimeLeaseDuration:
                    fastRuntimeLease
                        ? TimeSpan.FromMilliseconds(
                            200)
                        : TimeSpan.FromMinutes(
                            10),
                runtimeLeaseRenewalInterval:
                    fastRuntimeLease
                        ? TimeSpan.FromMilliseconds(
                            10)
                        : TimeSpan.FromMinutes(
                            5),
                idlePollingInterval:
                    TimeSpan.FromMilliseconds(
                        10)));
    }

    private static ProcessingWorkItem CreateWorkItem() =>
        new(
            ProcessingUnitId.New(),
            DocumentSubmissionId.New(),
            new ProcessingUnitScope.WholeDocument(),
            attemptNumber:
                1);

    #endregion

    #region Test Doubles

    private sealed class DelegateExecutor(
        Func<ProcessingWorkItem, CancellationToken, Task<ProcessingExecutionOutcome>>
            execute)
        : IDocumentProcessingExecutor
    {
        public ValueTask<ProcessingExecutionOutcome> ExecuteAsync(
            ProcessingWorkItem workItem,
            CancellationToken cancellationToken = default) =>
            new(
                execute(
                    workItem,
                    cancellationToken));
    }

    private sealed class RecordingStateStore(
        ManagerOperatingState initialState)
        : IManagerStateStore
    {
        private readonly object
            _sync =
                new();

        private ManagerStateSnapshot
            _snapshot =
                new(
                    initialState,
                    version:
                        0);

        public ValueTask<ManagerStateSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                return ValueTask.FromResult(
                    _snapshot);
            }
        }

        public ValueTask<ManagerStateSnapshot?> TrySetAsync(
            long expectedVersion,
            ManagerOperatingState state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                if (_snapshot.Version !=
                    expectedVersion)
                {
                    return ValueTask.FromResult<ManagerStateSnapshot?>(
                        null);
                }

                _snapshot =
                    new ManagerStateSnapshot(
                        state,
                        expectedVersion +
                        1);

                return ValueTask.FromResult<ManagerStateSnapshot?>(
                    _snapshot);
            }
        }
    }

    private sealed class RecordingRuntimeLeaseStore(
        bool rejectRenewal =
            false,
        bool rejectSubsequentAcquisitions =
            false)
        : IManagerRuntimeLeaseStore
    {
        private readonly object
            _sync =
                new();

        private ManagerRuntimeLease?
            _lease;

        private int
            _successfulAcquisitionCount;

        public int SuccessfulAcquisitionCount =>
            Volatile.Read(
                ref _successfulAcquisitionCount);

        public ValueTask<ManagerRuntimeLease?> TryAcquireAsync(
            string workerId,
            DateTimeOffset observedAtUtc,
            DateTimeOffset leaseExpiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                if (_lease is not null ||
                    rejectSubsequentAcquisitions &&
                    SuccessfulAcquisitionCount >
                    0)
                {
                    return ValueTask.FromResult<ManagerRuntimeLease?>(
                        null);
                }

                _lease =
                    new ManagerRuntimeLease(
                        Guid.NewGuid(),
                        workerId,
                        leaseExpiresAtUtc);

                Interlocked.Increment(
                    ref _successfulAcquisitionCount);

                return ValueTask.FromResult<ManagerRuntimeLease?>(
                    _lease);
            }
        }

        public ValueTask<bool> RenewAsync(
            ManagerRuntimeLease lease,
            DateTimeOffset observedAtUtc,
            DateTimeOffset leaseExpiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                return ValueTask.FromResult(
                    !rejectRenewal &&
                    _lease?.Token ==
                        lease.Token);
            }
        }

        public ValueTask<bool> ReleaseAsync(
            ManagerRuntimeLease lease,
            DateTimeOffset releasedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                if (_lease?.Token !=
                    lease.Token)
                {
                    return ValueTask.FromResult(
                        false);
                }

                _lease =
                    null;

                return ValueTask.FromResult(
                    true);
            }
        }
    }

    private sealed class RecordingQueueStore
        : IProcessingQueueStore
    {
        private readonly ConcurrentQueue<ProcessingWorkItem>
            _pending;

        private readonly ConcurrentDictionary<Guid, ProcessingWorkItem>
            _active =
                new();

        private readonly int
            _initialWorkCount;

        private readonly TaskCompletionSource?
            _claimEntered;

        private readonly TaskCompletionSource?
            _releaseClaim;

        private int
            _completedCount;

        private int
            _interruptedCount;

        public RecordingQueueStore(
            params ProcessingWorkItem[] workItems)
        {
            _pending =
                new ConcurrentQueue<ProcessingWorkItem>(
                    workItems);

            _initialWorkCount =
                workItems.Length;
        }

        public RecordingQueueStore(
            TaskCompletionSource claimEntered,
            TaskCompletionSource releaseClaim,
            params ProcessingWorkItem[] workItems)
            : this(
                workItems)
        {
            _claimEntered =
                claimEntered;

            _releaseClaim =
                releaseClaim;
        }

        public int CompletedCount =>
            Volatile.Read(
                ref _completedCount);

        public int InterruptedCount =>
            Volatile.Read(
                ref _interruptedCount);

        public ProcessingInterruptionReason?
            LastInterruptionReason;

        public TaskCompletionSource FirstCompletionObserved
        {
            get;
        } =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllWorkCompleted
        {
            get;
        } =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource InterruptionObserved
        {
            get;
        } =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource RecoveryObserved
        {
            get;
        } =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<ProcessingLease?> ClaimNextAsync(
            ManagerRuntimeLease runtimeLease,
            string workerId,
            DateTimeOffset observedAtUtc,
            DateTimeOffset leaseExpiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            _claimEntered?.TrySetResult();

            if (_releaseClaim is not null)
            {
                await _releaseClaim.Task
                    .WaitAsync(
                        cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!_pending.TryDequeue(
                    out var workItem))
            {
                return null;
            }

            var lease =
                new ProcessingLease(
                    workItem,
                    Guid.NewGuid(),
                    runtimeLease.Token,
                    workerId,
                    leaseExpiresAtUtc);

            _active.TryAdd(
                lease.Token,
                workItem);

            return lease;
        }

        public ValueTask<bool> RenewLeaseAsync(
            ProcessingLease lease,
            DateTimeOffset observedAtUtc,
            DateTimeOffset leaseExpiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                _active.ContainsKey(
                    lease.Token));
        }

        public ValueTask<bool> CompleteAsync(
            ProcessingLease lease,
            ProcessingExecutionOutcome.Success outcome,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_active.TryRemove(
                    lease.Token,
                    out _))
            {
                return ValueTask.FromResult(
                    false);
            }

            var completed =
                Interlocked.Increment(
                    ref _completedCount);

            FirstCompletionObserved.TrySetResult();

            if (completed ==
                _initialWorkCount)
            {
                AllWorkCompleted.TrySetResult();
            }

            return ValueTask.FromResult(
                true);
        }

        public ValueTask<bool> FailAsync(
            ProcessingLease lease,
            ProcessingFailure failure,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                _active.TryRemove(
                    lease.Token,
                    out _));

        public ValueTask<bool> RequeueAfterFailureAsync(
            ProcessingLease lease,
            ProcessingFailure failure,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_active.TryRemove(
                    lease.Token,
                    out var workItem))
            {
                return ValueTask.FromResult(
                    false);
            }

            _pending.Enqueue(
                workItem);

            return ValueTask.FromResult(
                true);
        }

        public ValueTask<bool> InterruptAndRequeueAsync(
            ProcessingLease lease,
            ProcessingInterruptionReason reason,
            DateTimeOffset interruptedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_active.TryRemove(
                    lease.Token,
                    out var workItem))
            {
                return ValueTask.FromResult(
                    false);
            }

            LastInterruptionReason =
                reason;

            Interlocked.Increment(
                ref _interruptedCount);

            _pending.Enqueue(
                workItem);

            InterruptionObserved.TrySetResult();

            return ValueTask.FromResult(
                true);
        }

        public ValueTask<int> RecoverExpiredLeasesAsync(
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RecoveryObserved.TrySetResult();

            return ValueTask.FromResult(
                0);
        }

        public ValueTask ReorderPendingAsync(
            ReorderProcessingQueueCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
