namespace DocumentProcessing.Manager.Runtime;

/// <summary>
/// Durable exclusive ownership of the single global processing runtime.
/// </summary>
public sealed record ManagerRuntimeLease
{
    #region Properties

    /// <summary>
    /// Gets the opaque token fencing a stale runtime owner.
    /// </summary>
    public Guid Token { get; }

    /// <summary>
    /// Gets the worker identity that owns the runtime.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the current runtime-lease expiration instant.
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one durable global runtime lease.
    /// </summary>
    public ManagerRuntimeLease(
        Guid token,
        string workerId,
        DateTimeOffset expiresAtUtc)
    {
        if (token ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Manager runtime-lease token cannot be empty.",
                nameof(token));
        }

        if (string.IsNullOrWhiteSpace(
                workerId))
        {
            throw new ArgumentException(
                "Manager runtime worker identifier cannot be empty.",
                nameof(workerId));
        }

        Token =
            token;

        WorkerId =
            workerId.Trim();

        ExpiresAtUtc =
            expiresAtUtc.ToUniversalTime();
    }

    #endregion
}
