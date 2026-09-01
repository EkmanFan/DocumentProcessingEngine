using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Runtime;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Durable outbound port for atomic global queue operations.
/// </summary>
/// <remarks>
/// Every operation on an existing unit lease must fence both its unit token
/// and its global runtime token against durable current ownership.
/// </remarks>
public interface IProcessingQueueStore
{
    /// <summary>
    /// Atomically claims the first pending unit for one worker.
    /// </summary>
    /// <remarks>
    /// The claim must fail when the supplied global runtime token no longer
    /// identifies the current unexpired runtime owner.
    /// </remarks>
    ValueTask<ProcessingLease?> ClaimNextAsync(
        ManagerRuntimeLease runtimeLease,
        string workerId,
        DateTimeOffset observedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews an owned lease and fences stale workers.
    /// </summary>
    ValueTask<bool> RenewLeaseAsync(
        ProcessingLease lease,
        DateTimeOffset observedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically completes an owned processing unit.
    /// </summary>
    ValueTask<bool> CompleteAsync(
        ProcessingLease lease,
        ProcessingExecutionOutcome.Success outcome,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically records the terminal failure of an owned processing unit.
    /// </summary>
    ValueTask<bool> FailAsync(
        ProcessingLease lease,
        ProcessingFailure failure,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically records a technical failure and requeues the owned unit.
    /// </summary>
    ValueTask<bool> RequeueAfterFailureAsync(
        ProcessingLease lease,
        ProcessingFailure failure,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically interrupts and requeues an owned processing unit.
    /// </summary>
    ValueTask<bool> InterruptAndRequeueAsync(
        ProcessingLease lease,
        ProcessingInterruptionReason reason,
        DateTimeOffset interruptedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues all processing units whose durable leases have expired.
    /// </summary>
    ValueTask<int> RecoverExpiredLeasesAsync(
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces the order of every pending queue unit.
    /// </summary>
    ValueTask ReorderPendingAsync(
        ReorderProcessingQueueCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically makes one shelved pending unit eligible for dispatch.
    /// </summary>
    ValueTask ReleasePendingAsync(
        ReleaseProcessingUnitCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically requeues one terminally failed unit for another attempt.
    /// </summary>
    ValueTask RetryFailedAsync(
        RetryFailedProcessingUnitCommand command,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(
            new NotSupportedException(
                "This processing-queue store does not support failed-unit retries."));

    /// <summary>Atomically removes one pending unit from the queue.</summary>
    ValueTask RemovePendingAsync(
        RemovePendingProcessingUnitCommand command,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(
            new NotSupportedException(
                "This processing-queue store does not support pending-unit removal."));

    /// <summary>Atomically removes every pending unit from the queue.</summary>
    ValueTask<int> ClearPendingAsync(
        ClearPendingProcessingQueueCommand command,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<int>(
            new NotSupportedException(
                "This processing-queue store does not support clearing pending units."));

    /// <summary>Hides one terminal unit while preserving its custody chain.</summary>
    ValueTask HideTerminalAsync(
        History.HideTerminalProcessingUnitCommand command,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(
            new NotSupportedException(
                "This processing-queue store does not support terminal-unit hiding."));

    /// <summary>
    /// Atomically removes one ready pending unit from dispatch eligibility.
    /// </summary>
    ValueTask ShelvePendingAsync(
        ShelveProcessingUnitCommand command,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(
            new NotSupportedException(
                "This processing-queue store does not support pending-unit shelving."));

    /// <summary>
    /// Atomically replaces one pending whole-document unit by ordered page ranges.
    /// </summary>
    ValueTask SplitPendingAsync(
        SplitPendingProcessingUnitCommand command,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(
            new NotSupportedException(
                "This processing-queue store does not support pending-unit splits."));
}
