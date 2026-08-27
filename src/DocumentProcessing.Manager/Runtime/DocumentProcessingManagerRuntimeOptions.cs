namespace DocumentProcessing.Manager.Runtime;

/// <summary>
/// Configuration of the durable sequential Manager runtime.
/// </summary>
public sealed class DocumentProcessingManagerRuntimeOptions
{
    #region Properties

    /// <summary>
    /// Gets the stable identity of this Manager runtime instance.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the duration of exclusive global runtime ownership.
    /// </summary>
    public TimeSpan RuntimeLeaseDuration { get; }

    /// <summary>
    /// Gets the interval between global runtime-lease renewals.
    /// </summary>
    public TimeSpan RuntimeLeaseRenewalInterval { get; }

    /// <summary>
    /// Gets the interval used to discover remotely enqueued work or commands.
    /// </summary>
    public TimeSpan IdlePollingInterval { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates durable Manager runtime options.
    /// </summary>
    public DocumentProcessingManagerRuntimeOptions(
        string workerId,
        TimeSpan runtimeLeaseDuration,
        TimeSpan runtimeLeaseRenewalInterval,
        TimeSpan idlePollingInterval)
    {
        if (string.IsNullOrWhiteSpace(
                workerId))
        {
            throw new ArgumentException(
                "Manager runtime worker identifier cannot be empty.",
                nameof(workerId));
        }

        if (runtimeLeaseDuration <=
            TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeLeaseDuration),
                runtimeLeaseDuration,
                "Runtime lease duration must be positive.");
        }

        if (runtimeLeaseRenewalInterval <=
                TimeSpan.Zero ||
            runtimeLeaseRenewalInterval >=
                runtimeLeaseDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeLeaseRenewalInterval),
                runtimeLeaseRenewalInterval,
                "Runtime lease renewal interval must be positive and shorter than the lease duration.");
        }

        if (idlePollingInterval <=
            TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(idlePollingInterval),
                idlePollingInterval,
                "Idle polling interval must be positive.");
        }

        WorkerId =
            workerId.Trim();

        RuntimeLeaseDuration =
            runtimeLeaseDuration;

        RuntimeLeaseRenewalInterval =
            runtimeLeaseRenewalInterval;

        IdlePollingInterval =
            idlePollingInterval;
    }

    #endregion
}
