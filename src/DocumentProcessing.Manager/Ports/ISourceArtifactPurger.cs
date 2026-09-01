using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Ports;

/// <summary>Administrative deletion port for unreferenced source bytes.</summary>
public interface ISourceArtifactPurger
{
    ValueTask DeleteAsync(
        Sha256Digest digest,
        CancellationToken cancellationToken = default);
}
