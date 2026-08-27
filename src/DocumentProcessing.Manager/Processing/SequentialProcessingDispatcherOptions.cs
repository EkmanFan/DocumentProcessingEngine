namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Lease and worker configuration for the sequential dispatcher.
/// </summary>
public sealed class SequentialProcessingDispatcherOptions
{
    #region Properties

    /// <summary>
    /// Gets the stable identity of this Manager worker instance.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the duration of each durable processing lease.
    /// </summary>
    public TimeSpan LeaseDuration { get; }

    /// <summary>
    /// Gets the interval between durable lease renewals.
    /// </summary>
    public TimeSpan LeaseRenewalInterval { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates sequential-dispatcher options.
    /// </summary>
    public SequentialProcessingDispatcherOptions(
        string workerId,
        TimeSpan leaseDuration,
        TimeSpan leaseRenewalInterval)
    {
        if (string.IsNullOrWhiteSpace(
                workerId))
        {
            throw new ArgumentException(
                "Processing worker identifier cannot be empty.",
                nameof(workerId));
        }

        if (leaseDuration <=
            TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration),
                leaseDuration,
                "Lease duration must be positive.");
        }

        if (leaseRenewalInterval <=
                TimeSpan.Zero ||
            leaseRenewalInterval >=
                leaseDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseRenewalInterval),
                leaseRenewalInterval,
                "Lease renewal interval must be positive and shorter than the lease duration.");
        }

        WorkerId =
            workerId.Trim();

        LeaseDuration =
            leaseDuration;

        LeaseRenewalInterval =
            leaseRenewalInterval;
    }

    #endregion
}
