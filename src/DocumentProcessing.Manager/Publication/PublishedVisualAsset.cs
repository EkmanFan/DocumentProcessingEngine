using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Publication;

/// <summary>Describes one visual available from a readable result publication.</summary>
/// <param name="AssetId">Portable visual identifier from the processing result.</param>
/// <param name="MediaType">Normalized image media type.</param>
/// <param name="ByteLength">Exact visual byte length.</param>
/// <param name="Digest">Exact visual SHA-256 digest.</param>
public sealed record PublishedVisualAsset(
    string AssetId,
    string MediaType,
    long ByteLength,
    Sha256Digest Digest);

/// <summary>Owns a verified readable stream for one published visual.</summary>
/// <param name="Asset">Verified visual descriptor.</param>
/// <param name="Content">Caller-owned readable content stream.</param>
public sealed record PublishedVisualAssetContent(
    PublishedVisualAsset Asset,
    Stream Content)
    : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync() =>
        Content.DisposeAsync();
}
