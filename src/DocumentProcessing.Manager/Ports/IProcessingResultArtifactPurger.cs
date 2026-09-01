using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Ports;

/// <summary>Administrative deletion port for unreferenced result bytes.</summary>
public interface IProcessingResultArtifactPurger
{
    ValueTask DeleteAsync(
        Sha256Digest digest,
        CancellationToken cancellationToken = default);
}
