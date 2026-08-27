namespace DocumentProcessing.Manager.Control;

/// <summary>
/// Raised when a control command is invalid for the current Manager state.
/// </summary>
public sealed class InvalidManagerStateTransitionException
    : InvalidOperationException
{
    #region Properties

    /// <summary>
    /// Gets the state in which the command was rejected.
    /// </summary>
    public ManagerOperatingState State { get; }

    /// <summary>
    /// Gets the rejected command type.
    /// </summary>
    public Type CommandType { get; }

    #endregion

    #region ctor

    internal InvalidManagerStateTransitionException(
        ManagerOperatingState state,
        ManagerControlCommand command)
        : base(
            $"Command '{command.GetType().Name}' is invalid while the Manager is '{state}'.")
    {
        State =
            state;

        CommandType =
            command.GetType();
    }

    #endregion
}
