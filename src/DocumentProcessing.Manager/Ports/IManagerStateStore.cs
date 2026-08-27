using DocumentProcessing.Manager.Control;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Durable outbound port for the versioned Manager operating state.
/// </summary>
public interface IManagerStateStore
{
    /// <summary>
    /// Reads the current durable Manager state.
    /// </summary>
    ValueTask<ManagerStateSnapshot> GetAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically confirms or changes the state when the expected version
    /// still matches.
    /// </summary>
    /// <returns>
    /// The updated snapshot, or <see langword="null"/> when another writer
    /// changed the state first.
    /// </returns>
    ValueTask<ManagerStateSnapshot?> TrySetAsync(
        long expectedVersion,
        ManagerOperatingState state,
        CancellationToken cancellationToken = default);
}
