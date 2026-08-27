using System.Collections.Concurrent;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Runtime;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class SequentialProcessingDispatcherTests
{
    #region Variables and Constants

    private static readonly Guid
        RuntimeLeaseToken =
            Guid.NewGuid();

    #endregion

    #region Tests

    [Fact]
    public async Task DispatchNextAsync_NeverExecutesTwoUnitsConcurrently()
    {
        var queueStore =
            new RecordingQueueStore(
                CreateLease(),
                CreateLease());

        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var release =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var active =
            0;

        var maximumActive =
            0;

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    var current =
                        Interlocked.Increment(
                            ref active);

                    maximumActive =
                        Math.Max(
                            maximumActive,
                            current);

                    entered.TrySetResult(
                        true);

                    await release.Task
                        .WaitAsync(
                            cancellationToken);

                    Interlocked.Decrement(
                        ref active);

                    return new ProcessingExecutionOutcome.Success(
                        "result");
                });

        var dispatcher =
            CreateDispatcher(
                queueStore,
                executor);

        var first =
            dispatcher
                .DispatchNextAsync(
                    CreateRuntimeLease(),
                    ProcessingInterruptionReason.ManagerStop)
                .AsTask();

        await entered.Task;

        var second =
            dispatcher
                .DispatchNextAsync(
                    CreateRuntimeLease(),
                    ProcessingInterruptionReason.ManagerStop)
                .AsTask();

        Assert.Equal(
            1,
            Volatile.Read(
                ref maximumActive));

        release.SetResult(
            true);

        var outcomes =
            await Task.WhenAll(
                first,
                second);

        Assert.All(
            outcomes,
            outcome =>
                Assert.Equal(
                    ProcessingDispatchStatus.Succeeded,
                    outcome.Status));

        Assert.Equal(
            1,
            maximumActive);

        Assert.Equal(
            2,
            queueStore.CompletedCount);
    }

    [Fact]
    public async Task DispatchNextAsync_RequeuesInterruptedUnit()
    {
        var queueStore =
            new RecordingQueueStore(
                CreateLease());

        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    entered.SetResult(
                        true);

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "Unreachable.");
                });

        var dispatcher =
            CreateDispatcher(
                queueStore,
                executor);

        using var cancellation =
            new CancellationTokenSource();

        var dispatch =
            dispatcher
                .DispatchNextAsync(
                    CreateRuntimeLease(),
                    ProcessingInterruptionReason.ManagerStop,
                    cancellation.Token)
                .AsTask();

        await entered.Task;

        cancellation.Cancel();

        var outcome =
            await dispatch;

        Assert.Equal(
            ProcessingDispatchStatus.Interrupted,
            outcome.Status);

        Assert.Equal(
            1,
            queueStore.InterruptedCount);

        Assert.Equal(
            ProcessingInterruptionReason.ManagerStop,
            queueStore.LastInterruptionReason);

        Assert.Equal(
            0,
            queueStore.CompletedCount);

        Assert.Equal(
            0,
            queueStore.FailedCount);
    }

    [Fact]
    public async Task DispatchNextAsync_RejectsMismatchedRuntimeFence()
    {
        var queueStore =
            new RecordingQueueStore(
                CreateLease(
                    runtimeLeaseToken:
                        Guid.NewGuid()));

        var dispatcher =
            CreateDispatcher(
                queueStore,
                new DelegateExecutor(
                    (_, _) =>
                        Task.FromResult<ProcessingExecutionOutcome>(
                            new ProcessingExecutionOutcome.Success(
                                "unused"))));

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    dispatcher
                        .DispatchNextAsync(
                            CreateRuntimeLease(),
                            ProcessingInterruptionReason.ManagerStop)
                        .AsTask());

        Assert.Contains(
            "different runtime token",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DispatchNextAsync_RecordsFunctionalFailure()
    {
        var queueStore =
            new RecordingQueueStore(
                CreateLease());

        var dispatcher =
            CreateDispatcher(
                queueStore,
                new DelegateExecutor(
                    (_, _) =>
                        Task.FromResult<ProcessingExecutionOutcome>(
                            new ProcessingExecutionOutcome.Failure(
                                "unsupported",
                                "Unsupported source."))));

        var outcome =
            await dispatcher.DispatchNextAsync(
                CreateRuntimeLease(),
                ProcessingInterruptionReason.ManagerStop);

        Assert.Equal(
            ProcessingDispatchStatus.Failed,
            outcome.Status);

        Assert.Equal(
            "unsupported",
            outcome.Failure?.Code);

        Assert.Equal(
            1,
            queueStore.FailedCount);
    }

    [Fact]
    public async Task DispatchNextAsync_RequeuesTechnicalFailureWithinBound()
    {
        var queueStore =
            new RecordingQueueStore(
                CreateLease(
                    attemptNumber:
                        1));

        var dispatcher =
            CreateDispatcher(
                queueStore,
                new DelegateExecutor(
                    (_, _) =>
                        throw new IOException(
                            "Temporary provider failure.")),
                maximumAttempts:
                    3);

        var outcome =
            await dispatcher.DispatchNextAsync(
                CreateRuntimeLease(),
                ProcessingInterruptionReason.ManagerStop);

        Assert.Equal(
            ProcessingDispatchStatus.RetryScheduled,
            outcome.Status);

        Assert.Equal(
            1,
            queueStore.RequeuedAfterFailureCount);

        Assert.Equal(
            0,
            queueStore.FailedCount);
    }

    [Fact]
    public async Task DispatchNextAsync_FailsTechnicalFailureAtBound()
    {
        var queueStore =
            new RecordingQueueStore(
                CreateLease(
                    attemptNumber:
                        3));

        var dispatcher =
            CreateDispatcher(
                queueStore,
                new DelegateExecutor(
                    (_, _) =>
                        throw new IOException(
                            "Repeated provider failure.")),
                maximumAttempts:
                    3);

        var outcome =
            await dispatcher.DispatchNextAsync(
                CreateRuntimeLease(),
                ProcessingInterruptionReason.ManagerStop);

        Assert.Equal(
            ProcessingDispatchStatus.Failed,
            outcome.Status);

        Assert.Equal(
            0,
            queueStore.RequeuedAfterFailureCount);

        Assert.Equal(
            1,
            queueStore.FailedCount);
    }

    [Fact]
    public async Task DispatchNextAsync_RenewsLeaseDuringLongExecution()
    {
        var queueStore =
            new RecordingQueueStore(
                CreateLease());

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    await queueStore.RenewalObserved.Task
                        .WaitAsync(
                            cancellationToken);

                    return new ProcessingExecutionOutcome.Success(
                        "result");
                });

        var outcome =
            await CreateFastLeaseDispatcher(
                    queueStore,
                    executor)
                .DispatchNextAsync(
                    CreateRuntimeLease(),
                    ProcessingInterruptionReason.ManagerStop)
                .AsTask()
                .WaitAsync(
                    TimeSpan.FromSeconds(
                        5));

        Assert.Equal(
            ProcessingDispatchStatus.Succeeded,
            outcome.Status);

        Assert.True(
            queueStore.RenewedCount >=
            1);

        Assert.Equal(
            1,
            queueStore.CompletedCount);
    }

    [Fact]
    public async Task DispatchNextAsync_DoesNotFinalizeAfterLeaseLoss()
    {
        var queueStore =
            new RecordingQueueStore(
                CreateLease())
            {
                RenewLeaseResult =
                    false
            };

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "Unreachable.");
                });

        var outcome =
            await CreateFastLeaseDispatcher(
                    queueStore,
                    executor)
                .DispatchNextAsync(
                    CreateRuntimeLease(),
                    ProcessingInterruptionReason.ManagerStop)
                .AsTask()
                .WaitAsync(
                    TimeSpan.FromSeconds(
                        5));

        Assert.Equal(
            ProcessingDispatchStatus.LeaseLost,
            outcome.Status);

        Assert.Equal(
            0,
            queueStore.CompletedCount);

        Assert.Equal(
            0,
            queueStore.FailedCount);

        Assert.Equal(
            0,
            queueStore.InterruptedCount);
    }

    [Fact]
    public async Task RecoverExpiredLeasesAsync_UsesCurrentTime()
    {
        var queueStore =
            new RecordingQueueStore
            {
                RecoveredCount =
                    3
            };

        var now =
            new DateTimeOffset(
                2026,
                8,
                27,
                12,
                30,
                0,
                TimeSpan.Zero);

        var dispatcher =
            CreateDispatcher(
                queueStore,
                new DelegateExecutor(
                    (_, _) =>
                        Task.FromResult<ProcessingExecutionOutcome>(
                            new ProcessingExecutionOutcome.Success(
                                "unused"))),
                new FixedTimeProvider(
                    now));

        var recovered =
            await dispatcher.RecoverExpiredLeasesAsync();

        Assert.Equal(
            3,
            recovered);

        Assert.Equal(
            now,
            queueStore.LastRecoveryObservation);
    }

    #endregion

    #region Helpers

    private static SequentialProcessingDispatcher CreateDispatcher(
        IProcessingQueueStore queueStore,
        IDocumentProcessingExecutor executor,
        TimeProvider? timeProvider = null,
        int maximumAttempts =
            1) =>
        new(
            queueStore,
            executor,
            new SequentialProcessingDispatcherOptions(
                workerId:
                    "unit-test-worker",
                leaseDuration:
                    TimeSpan.FromMinutes(
                        10),
                leaseRenewalInterval:
                    TimeSpan.FromMinutes(
                        5)),
            new BoundedProcessingFailurePolicy(
                maximumAttempts),
            timeProvider);

    private static SequentialProcessingDispatcher CreateFastLeaseDispatcher(
        IProcessingQueueStore queueStore,
        IDocumentProcessingExecutor executor) =>
        new(
            queueStore,
            executor,
            new SequentialProcessingDispatcherOptions(
                workerId:
                    "unit-test-worker",
                leaseDuration:
                    TimeSpan.FromMilliseconds(
                        200),
                leaseRenewalInterval:
                    TimeSpan.FromMilliseconds(
                        10)),
            new BoundedProcessingFailurePolicy(
                maximumAttempts:
                    1));

    private static ProcessingLease CreateLease(
        int attemptNumber =
            1,
        Guid? runtimeLeaseToken =
            null)
    {
        var workItem =
            new ProcessingWorkItem(
                ProcessingUnitId.New(),
                DocumentSubmissionId.New(),
                new ProcessingUnitScope.WholeDocument(),
                attemptNumber);

        return new ProcessingLease(
            workItem,
            Guid.NewGuid(),
            runtimeLeaseToken ??
            RuntimeLeaseToken,
            "unit-test-worker",
            DateTimeOffset.UtcNow.AddMinutes(
                10));
    }

    private static ManagerRuntimeLease CreateRuntimeLease() =>
        new(
            RuntimeLeaseToken,
            "unit-test-worker",
            DateTimeOffset.UtcNow.AddMinutes(
                10));

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

    private sealed class FixedTimeProvider(
        DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            now;
    }

    private sealed class RecordingQueueStore
        : IProcessingQueueStore
    {
        private readonly ConcurrentQueue<ProcessingLease>
            _leases;

        public RecordingQueueStore(
            params ProcessingLease[] leases)
        {
            _leases =
                new ConcurrentQueue<ProcessingLease>(
                    leases);
        }

        public int CompletedCount;

        public int FailedCount;

        public int InterruptedCount;

        public int RenewedCount;

        public int RequeuedAfterFailureCount;

        public int RecoveredCount;

        public bool RenewLeaseResult =
            true;

        public TaskCompletionSource<bool> RenewalObserved
        {
            get;
        } =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public ProcessingInterruptionReason?
            LastInterruptionReason;

        public DateTimeOffset?
            LastRecoveryObservation;

        public ValueTask<ProcessingLease?> ClaimNextAsync(
            ManagerRuntimeLease runtimeLease,
            string workerId,
            DateTimeOffset leaseExpiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                _leases.TryDequeue(
                    out var lease)
                    ? lease
                    : null);
        }

        public ValueTask<bool> RenewLeaseAsync(
            ProcessingLease lease,
            DateTimeOffset leaseExpiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(
                ref RenewedCount);

            RenewalObserved.TrySetResult(
                true);

            return ValueTask.FromResult(
                RenewLeaseResult);
        }

        public ValueTask<bool> CompleteAsync(
            ProcessingLease lease,
            ProcessingExecutionOutcome.Success outcome,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(
                ref CompletedCount);

            return ValueTask.FromResult(
                true);
        }

        public ValueTask<bool> FailAsync(
            ProcessingLease lease,
            ProcessingFailure failure,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(
                ref FailedCount);

            return ValueTask.FromResult(
                true);
        }

        public ValueTask<bool> RequeueAfterFailureAsync(
            ProcessingLease lease,
            ProcessingFailure failure,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(
                ref RequeuedAfterFailureCount);

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

            LastInterruptionReason =
                reason;

            Interlocked.Increment(
                ref InterruptedCount);

            return ValueTask.FromResult(
                true);
        }

        public ValueTask<int> RecoverExpiredLeasesAsync(
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastRecoveryObservation =
                observedAtUtc;

            return ValueTask.FromResult(
                RecoveredCount);
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
