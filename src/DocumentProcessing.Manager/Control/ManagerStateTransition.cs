namespace DocumentProcessing.Manager.Control;

/// <summary>
/// Deterministic result of applying a control command to a Manager state.
/// </summary>
public sealed record ManagerStateTransition
{
    #region Properties

    /// <summary>
    /// Gets the state observed before the command.
    /// </summary>
    public ManagerOperatingState PreviousState { get; }

    /// <summary>
    /// Gets the state established by the command.
    /// </summary>
    public ManagerOperatingState CurrentState { get; }

    /// <summary>
    /// Gets whether the command changed the durable state.
    /// </summary>
    public bool Changed =>
        PreviousState !=
        CurrentState;

    #endregion

    #region ctor

    internal ManagerStateTransition(
        ManagerOperatingState previousState,
        ManagerOperatingState currentState)
    {
        PreviousState =
            previousState;

        CurrentState =
            currentState;
    }

    #endregion
}
