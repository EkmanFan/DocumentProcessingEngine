namespace DocumentProcessing.Manager.Control;

/// <summary>
/// Applies Manager control commands through state-specific behavior.
/// </summary>
public sealed class ManagerStateMachine
{
    #region Variables and Constants

    private static readonly IReadOnlyDictionary<ManagerOperatingState, IState>
        States =
            new Dictionary<ManagerOperatingState, IState>
            {
                [ManagerOperatingState.Stopped] =
                    new StoppedState(),
                [ManagerOperatingState.Running] =
                    new RunningState(),
                [ManagerOperatingState.Paused] =
                    new PausedState()
            };

    #endregion

    #region Methods

    /// <summary>
    /// Applies one command to the supplied durable operating state.
    /// </summary>
    public ManagerStateTransition Apply(
        ManagerOperatingState state,
        ManagerControlCommand command)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        if (!States.TryGetValue(
                state,
                out var behavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Unknown Manager operating state.");
        }

        return new ManagerStateTransition(
            state,
            behavior.Apply(
                command));
    }

    #endregion

    #region Internal Types

    private interface IState
    {
        ManagerOperatingState Apply(
            ManagerControlCommand command);
    }

    private sealed class StoppedState
        : IState
    {
        public ManagerOperatingState Apply(
            ManagerControlCommand command) =>
            command switch
            {
                StartManagerCommand =>
                    ManagerOperatingState.Running,
                StopManagerCommand =>
                    ManagerOperatingState.Stopped,
                _ =>
                    throw new InvalidManagerStateTransitionException(
                        ManagerOperatingState.Stopped,
                        command)
            };
    }

    private sealed class RunningState
        : IState
    {
        public ManagerOperatingState Apply(
            ManagerControlCommand command) =>
            command switch
            {
                StartManagerCommand or
                ResumeManagerCommand =>
                    ManagerOperatingState.Running,
                PauseManagerCommand =>
                    ManagerOperatingState.Paused,
                StopManagerCommand =>
                    ManagerOperatingState.Stopped,
                _ =>
                    throw new InvalidManagerStateTransitionException(
                        ManagerOperatingState.Running,
                        command)
            };
    }

    private sealed class PausedState
        : IState
    {
        public ManagerOperatingState Apply(
            ManagerControlCommand command) =>
            command switch
            {
                PauseManagerCommand =>
                    ManagerOperatingState.Paused,
                ResumeManagerCommand =>
                    ManagerOperatingState.Running,
                StopManagerCommand =>
                    ManagerOperatingState.Stopped,
                _ =>
                    throw new InvalidManagerStateTransitionException(
                        ManagerOperatingState.Paused,
                        command)
            };
    }

    #endregion
}
