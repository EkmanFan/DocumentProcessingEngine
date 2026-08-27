using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound read-only port for durable immutable processing-result bytes.
/// </summary>
public interface IProcessingResultArtifactReader
{
    /// <summary>
    /// Verifies that durable result bytes still match their descriptor.
    /// </summary>
    ValueTask<bool> VerifyAsync(
        ProcessingResultArtifact artifact,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies and opens exact result bytes for sequential reading.
    /// </summary>
    ValueTask<Stream> OpenReadAsync(
        ProcessingResultArtifact artifact,
        CancellationToken cancellationToken = default);
}
