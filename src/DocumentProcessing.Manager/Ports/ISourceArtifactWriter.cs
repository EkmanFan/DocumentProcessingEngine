using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound port for preserving exact immutable source bytes.
/// </summary>
public interface ISourceArtifactWriter
{
    /// <summary>
    /// Stores and hashes the complete readable source stream idempotently.
    /// </summary>
    ValueTask<SourceArtifact> StoreAsync(
        Stream content,
        CancellationToken cancellationToken = default);
}
