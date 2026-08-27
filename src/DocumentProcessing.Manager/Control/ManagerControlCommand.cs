namespace DocumentProcessing.Manager.Control;

/// <summary>
/// Base command for changing the Manager operating state.
/// </summary>
public abstract record ManagerControlCommand
{
    private protected ManagerControlCommand()
    {
    }
}

/// <summary>
/// Starts a stopped Manager.
/// </summary>
public sealed record StartManagerCommand
    : ManagerControlCommand;

/// <summary>
/// Pauses a running Manager after its active unit completes.
/// </summary>
public sealed record PauseManagerCommand
    : ManagerControlCommand;

/// <summary>
/// Resumes a paused Manager.
/// </summary>
public sealed record ResumeManagerCommand
    : ManagerControlCommand;

/// <summary>
/// Stops the Manager and interrupts its active unit for requeueing.
/// </summary>
public sealed record StopManagerCommand
    : ManagerControlCommand;
