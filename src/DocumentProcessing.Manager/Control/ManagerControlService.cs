using DocumentProcessing.Manager.Ports;

namespace DocumentProcessing.Manager.Control;

/// <summary>
/// Applies Manager commands against durable optimistic-concurrency state.
/// </summary>
public sealed class ManagerControlService
{
    #region Variables and Constants

    private readonly IManagerStateStore
        _stateStore;

    private readonly ManagerStateMachine
        _stateMachine;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the durable Manager-control use case.
    /// </summary>
    public ManagerControlService(
        IManagerStateStore stateStore,
        ManagerStateMachine? stateMachine = null)
    {
        _stateStore =
            stateStore ??
            throw new ArgumentNullException(
                nameof(stateStore));

        _stateMachine =
            stateMachine ??
            new ManagerStateMachine();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Applies one command atomically, retrying only version conflicts.
    /// </summary>
    public async ValueTask<ManagerControlResult> ExecuteAsync(
        ManagerControlCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current =
                await _stateStore
                    .GetAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            var transition =
                _stateMachine.Apply(
                    current.State,
                    command);

            var updated =
                await _stateStore
                    .TrySetAsync(
                        current.Version,
                        transition.CurrentState,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (updated is not null)
            {
                return new ManagerControlResult(
                    transition,
                    updated);
            }
        }
    }

    #endregion
}
