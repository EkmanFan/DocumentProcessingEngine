namespace DocumentProcessing.Manager.Control;

/// <summary>
/// Versioned durable snapshot of the Manager operating state.
/// </summary>
public sealed record ManagerStateSnapshot
{
    #region Properties

    /// <summary>
    /// Gets the durable operating state.
    /// </summary>
    public ManagerOperatingState State { get; }

    /// <summary>
    /// Gets the optimistic-concurrency version.
    /// </summary>
    public long Version { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one versioned Manager-state snapshot.
    /// </summary>
    public ManagerStateSnapshot(
        ManagerOperatingState state,
        long version)
    {
        if (!Enum.IsDefined(
                state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Unknown Manager operating state.");
        }

        if (version <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "Manager-state version cannot be negative.");
        }

        State =
            state;

        Version =
            version;
    }

    #endregion
}
