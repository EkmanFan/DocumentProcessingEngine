using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Ports;

/// <summary>Administrative deletion port for one published visual directory.</summary>
public interface IProcessingVisualAssetPurger
{
    ValueTask DeletePublicationAsync(
        ProcessingUnitId unitId,
        string publicationDirectory,
        CancellationToken cancellationToken = default);
}
