using DocumentProcessing.Manager.Publication;
using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.Manager.Ports;

/// <summary>Outbound port for verified reads from a completed result publication.</summary>
public interface IProcessingVisualAssetReader
{
    /// <summary>Reads the immutable visual manifest for one result.</summary>
    ValueTask<IReadOnlyList<PublishedVisualAsset>> GetAssetsAsync(
        ProcessingResultRecord result,
        CancellationToken cancellationToken = default);

    /// <summary>Opens and verifies one visual by its portable asset identifier.</summary>
    ValueTask<PublishedVisualAssetContent?> OpenReadAsync(
        ProcessingResultRecord result,
        string assetId,
        CancellationToken cancellationToken = default);
}
