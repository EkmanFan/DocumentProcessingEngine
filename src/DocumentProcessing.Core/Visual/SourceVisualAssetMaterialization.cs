using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Visual;

/// <summary>
/// Neutral integrity/provenance metadata for one materialized source visual
/// occurrence.
///
/// Binary content is deliberately not embedded. The caller owns the destination
/// stream; this record describes exactly which source occurrence was
/// materialized and the bytes written for it.
/// </summary>
public sealed record SourceVisualAssetMaterialization
{
    public SourceVisualAssetMaterialization(
        int physicalPageNumber,
        int sourceVisualIndex,
        NormalizedRectangle declaredPageBounds,
        string profileId,
        string mediaType,
        long contentLength,
        string contentSha256)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be positive.");
        }

        if (sourceVisualIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex),
                sourceVisualIndex,
                "Source visual index must be non-negative.");
        }

        if (declaredPageBounds.Right <=
                declaredPageBounds.Left ||
            declaredPageBounds.Bottom <=
                declaredPageBounds.Top)
        {
            throw new ArgumentException(
                "Declared source visual bounds must have positive area.",
                nameof(declaredPageBounds));
        }

        if (string.IsNullOrWhiteSpace(
                profileId))
        {
            throw new ArgumentException(
                "Source visual materialization profile ID cannot be empty.",
                nameof(profileId));
        }

        if (string.IsNullOrWhiteSpace(
                mediaType) ||
            !mediaType
                .Trim()
                .StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Source visual media type must be an image media type.",
                nameof(mediaType));
        }

        if (contentLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentLength),
                contentLength,
                "Materialized visual content length must be positive.");
        }

        PhysicalPageNumber =
            physicalPageNumber;

        SourceVisualIndex =
            sourceVisualIndex;

        DeclaredPageBounds =
            declaredPageBounds;

        ProfileId =
            profileId.Trim();

        MediaType =
            mediaType
                .Trim()
                .ToLowerInvariant();

        ContentLength =
            contentLength;

        ContentSha256 =
            NormalizeSha256(
                contentSha256);
    }

    public int PhysicalPageNumber { get; }

    public int SourceVisualIndex { get; }

    public NormalizedRectangle DeclaredPageBounds { get; }

    public string ProfileId { get; }

    public string MediaType { get; }

    public long ContentLength { get; }

    public string ContentSha256 { get; }

    private static string NormalizeSha256(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "SHA-256 value cannot be empty.",
                nameof(value));
        }

        var normalized =
            value
                .Trim()
                .ToLowerInvariant();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        return normalized;
    }
}
