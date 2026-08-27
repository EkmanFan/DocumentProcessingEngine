using DocumentProcessing.Manager.Runtime;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Durable outbound port for exclusive ownership of the global Manager runtime.
/// </summary>
/// <remarks>
/// Implementations must atomically allow at most one unexpired lease and must
/// fence renewal and release operations with the opaque lease token.
/// </remarks>
public interface IManagerRuntimeLeaseStore
{
    /// <summary>
    /// Tries to atomically acquire the global runtime lease.
    /// </summary>
    ValueTask<ManagerRuntimeLease?> TryAcquireAsync(
        string workerId,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews the lease when its token still identifies the current owner.
    /// </summary>
    ValueTask<bool> RenewAsync(
        ManagerRuntimeLease lease,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the lease when its token still identifies the current owner.
    /// </summary>
    ValueTask<bool> ReleaseAsync(
        ManagerRuntimeLease lease,
        DateTimeOffset releasedAtUtc,
        CancellationToken cancellationToken = default);
}
