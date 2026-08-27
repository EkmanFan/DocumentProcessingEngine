using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound port for preserving exact immutable processing-result bytes.
/// </summary>
public interface IProcessingResultArtifactWriter
{
    /// <summary>
    /// Stores and hashes one complete processing-result payload idempotently.
    /// </summary>
    ValueTask<ProcessingResultArtifact> StoreAsync(
        Stream content,
        CancellationToken cancellationToken = default);
}
