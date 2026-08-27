using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound write port for the immutable processing-result registry.
/// </summary>
public interface IProcessingResultRegistryWriter
{
    /// <summary>
    /// Idempotently registers a durable processing result for its unit.
    /// </summary>
    ValueTask<ProcessingResultRegistration> RegisterAsync(
        ProcessingResultRecord result,
        CancellationToken cancellationToken = default);
}
