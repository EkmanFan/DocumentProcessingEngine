namespace DocumentProcessing.Manager.Persistence.Postgres;

internal static class PostgresLeaseDuration
{
    #region Methods

    public static TimeSpan Calculate(
        DateTimeOffset observedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        string parameterName)
    {
        var duration =
            leaseExpiresAtUtc.ToUniversalTime() -
            observedAtUtc.ToUniversalTime();

        if (duration <=
            TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                leaseExpiresAtUtc,
                "Lease expiration must follow the observed instant.");
        }

        return duration;
    }

    #endregion
}
