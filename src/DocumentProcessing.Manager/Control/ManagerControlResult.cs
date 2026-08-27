namespace DocumentProcessing.Manager.Control;

/// <summary>
/// Versioned outcome of one successfully applied Manager control command.
/// </summary>
public sealed record ManagerControlResult
{
    #region Properties

    /// <summary>
    /// Gets the deterministic semantic transition.
    /// </summary>
    public ManagerStateTransition Transition { get; }

    /// <summary>
    /// Gets the durable snapshot that linearized the command.
    /// </summary>
    public ManagerStateSnapshot Snapshot { get; }

    #endregion

    #region ctor

    internal ManagerControlResult(
        ManagerStateTransition transition,
        ManagerStateSnapshot snapshot)
    {
        Transition =
            transition ??
            throw new ArgumentNullException(
                nameof(transition));

        Snapshot =
            snapshot ??
            throw new ArgumentNullException(
                nameof(snapshot));
    }

    #endregion
}
