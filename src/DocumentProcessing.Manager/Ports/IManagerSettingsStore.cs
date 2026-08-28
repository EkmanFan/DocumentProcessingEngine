using DocumentProcessing.Manager.Settings;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Durable outbound port for versioned Manager settings.
/// </summary>
public interface IManagerSettingsStore
{
    /// <summary>Reads the current settings.</summary>
    ValueTask<ManagerSettingsSnapshot> GetAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically updates settings when their expected version still matches.
    /// </summary>
    ValueTask<ManagerSettingsSnapshot?> TryUpdateAsync(
        UpdateManagerSettingsCommand command,
        CancellationToken cancellationToken = default);
}
