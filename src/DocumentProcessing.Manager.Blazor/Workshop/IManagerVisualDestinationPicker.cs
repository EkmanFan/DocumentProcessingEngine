namespace DocumentProcessing.Manager.Blazor.Workshop;

/// <summary>
/// Host-provided adapter for choosing a visual destination directory.
/// </summary>
public interface IManagerVisualDestinationPicker
{
    /// <summary>
    /// Opens a native directory chooser when the host environment supports it.
    /// </summary>
    ValueTask<string?> PickAsync(
        string? currentDirectory,
        CancellationToken cancellationToken = default);
}
