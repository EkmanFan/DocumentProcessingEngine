using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Results;

/// <summary>
/// Expected immutable identity of one visual selected by DPEngine.
/// </summary>
public sealed record ProcessingVisualAssetDescriptor
{
    #region Properties

    /// <summary>Gets the result-scoped asset identifier.</summary>
    public string AssetId { get; }

    /// <summary>Gets the normalized image media type.</summary>
    public string MediaType { get; }

    /// <summary>Gets the exact expected byte length.</summary>
    public long ByteLength { get; }

    /// <summary>Gets the expected SHA-256 digest.</summary>
    public Sha256Digest Digest { get; }

    #endregion

    #region ctor

    /// <summary>Creates one expected visual-asset descriptor.</summary>
    public ProcessingVisualAssetDescriptor(
        string assetId,
        string mediaType,
        long byteLength,
        Sha256Digest digest)
    {
        if (string.IsNullOrWhiteSpace(
                assetId))
        {
            throw new ArgumentException(
                "Visual asset identifier cannot be empty.",
                nameof(assetId));
        }

        if (string.IsNullOrWhiteSpace(
                mediaType) ||
            !mediaType.Trim()
                .StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Visual asset media type must be an image media type.",
                nameof(mediaType));
        }

        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteLength));
        }

        if (string.IsNullOrWhiteSpace(
                digest.Value))
        {
            throw new ArgumentException(
                "Visual asset digest is required.",
                nameof(digest));
        }

        AssetId =
            assetId.Trim();

        MediaType =
            mediaType.Trim()
                .ToLowerInvariant();

        ByteLength =
            byteLength;

        Digest =
            digest;
    }

    #endregion
}
