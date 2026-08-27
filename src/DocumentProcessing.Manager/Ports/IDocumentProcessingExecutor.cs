using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound port for executing one atomic document-processing unit.
/// </summary>
public interface IDocumentProcessingExecutor
{
    /// <summary>
    /// Executes one work item against the configured document processor.
    /// </summary>
    ValueTask<ProcessingExecutionOutcome> ExecuteAsync(
        ProcessingWorkItem workItem,
        CancellationToken cancellationToken = default);
}
