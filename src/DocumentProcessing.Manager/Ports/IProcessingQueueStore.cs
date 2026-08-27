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
}
