namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Durable exclusive claim over one atomic processing unit.
/// </summary>
public sealed record ProcessingLease
{
    #region Properties

    /// <summary>
    /// Gets the claimed work item.
    /// </summary>
    public ProcessingWorkItem WorkItem { get; }

    /// <summary>
    /// Gets the opaque token fencing stale workers from finalization.
    /// </summary>
    public Guid Token { get; }

    /// <summary>
    /// Gets the global runtime token that authorized this unit claim.
    /// </summary>
    public Guid RuntimeLeaseToken { get; }

    /// <summary>
    /// Gets the worker identity that owns the lease.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the current lease expiration instant.
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one durable processing lease.
    /// </summary>
    public ProcessingLease(
        ProcessingWorkItem workItem,
        Guid token,
        Guid runtimeLeaseToken,
        string workerId,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(
            workItem);

        if (token ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-lease token cannot be empty.",
                nameof(token));
        }

        if (string.IsNullOrWhiteSpace(
                workerId))
        {
            throw new ArgumentException(
                "Processing worker identifier cannot be empty.",
                nameof(workerId));
        }

        if (runtimeLeaseToken ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Global runtime-lease token cannot be empty.",
                nameof(runtimeLeaseToken));
        }

        WorkItem =
            workItem;

        Token =
            token;

        RuntimeLeaseToken =
            runtimeLeaseToken;

        WorkerId =
            workerId.Trim();

        ExpiresAtUtc =
            expiresAtUtc.ToUniversalTime();
    }

    #endregion
}
