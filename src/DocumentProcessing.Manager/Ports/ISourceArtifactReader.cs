using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound read-only port for retained immutable source bytes.
/// </summary>
public interface ISourceArtifactReader
{
    /// <summary>
    /// Verifies that retained bytes still match the immutable descriptor.
    /// </summary>
    ValueTask<bool> VerifyAsync(
        SourceArtifact artifact,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies and opens the exact retained bytes for sequential reading.
    /// </summary>
    ValueTask<Stream> OpenReadAsync(
        SourceArtifact artifact,
        CancellationToken cancellationToken = default);
}
