using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound port for caller-owned visual bytes produced during processing.
/// </summary>
public interface IProcessingVisualAssetStore
{
    /// <summary>
    /// Verifies that an absolute visual destination exists and is writable.
    /// </summary>
    ValueTask ValidateRootAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>Opens an isolated write session for one processing unit.</summary>
    ValueTask<IProcessingVisualAssetWriteSession> BeginWriteAsync(
        string rootDirectory,
        ProcessingUnitId unitId,
        string originalFileName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stages visual bytes and publishes them after custody validation.
/// </summary>
public interface IProcessingVisualAssetWriteSession
    : IAsyncDisposable
{
    /// <summary>Opens one empty writable image destination.</summary>
    ValueTask<Stream> OpenWriteAsync(
        string mediaType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates every staged file and publishes the completed-treatment
    /// directory atomically.
    /// </summary>
    ValueTask<string> CompleteAsync(
        IReadOnlyList<ProcessingVisualAssetDescriptor> assets,
        CancellationToken cancellationToken = default);
}
