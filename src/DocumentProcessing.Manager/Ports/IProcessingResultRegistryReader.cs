using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound read-only port for durable processing-result registry entries.
/// </summary>
public interface IProcessingResultRegistryReader
{
    /// <summary>
    /// Reads the result registered for one processing unit, or returns null.
    /// </summary>
    ValueTask<ProcessingResultRecord?> GetByUnitAsync(
        ProcessingUnitId unitId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one result by its opaque reference, or returns null.
    /// </summary>
    ValueTask<ProcessingResultRecord?> GetByReferenceAsync(
        string resultReference,
        CancellationToken cancellationToken = default);
}
