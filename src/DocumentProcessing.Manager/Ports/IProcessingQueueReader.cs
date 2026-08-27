using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound read-only port for the durable processing queue.
/// </summary>
public interface IProcessingQueueReader
{
    /// <summary>
    /// Reads one consistent versioned snapshot of all processing units.
    /// </summary>
    ValueTask<ProcessingQueueSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}
